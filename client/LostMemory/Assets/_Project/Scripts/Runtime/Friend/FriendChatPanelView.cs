using System;
using System.Collections;
using System.Collections.Generic;
using LostMemory.Networking.Llm;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Friend
{
    /// <summary>
    /// 소꿉친구 채팅 패널 뷰. 게임 채팅 로그(롤/MMO) 스타일.
    ///
    /// 한 줄 포맷:
    ///   <color=#xxx>발신자이름</color>: 메시지
    ///
    /// [초심자 설명]
    ///   플레이어가 InputField 에 메시지를 입력하고 전송하면:
    ///     1. 플레이어 라인 즉시 추가
    ///     2. LLM API 에 대화 기록 전송
    ///     3. NPC 라인 추가 (스트리밍이면 글자가 실시간으로 채워짐)
    ///
    ///   대화 히스토리(_history)를 누적해 이전 맥락을 기억하는 대화가 가능합니다.
    ///   멀티플레이 채팅으로 확장 시에도 chatLinePrefab 과 AddLine() 패턴을 그대로 재사용 가능.
    ///
    /// Inspector 연결 필수:
    ///   scrollRect       — 채팅창 ScrollRect
    ///   contentParent    — 라인들이 생성될 부모 (VerticalLayoutGroup + ContentSizeFitter 부착)
    ///   inputField       — 플레이어 입력창 (TMP_InputField)
    ///   sendButton       — 전송 버튼
    ///   closeButton      — 닫기 버튼 (null 허용 — ESC 로도 닫힘)
    ///   typingIndicator  — "..." 타이핑 중 표시 오브젝트 (null 허용)
    ///   chatLinePrefab   — 채팅 한 줄 프리팹 (ChatMessageView 부착, TMP_Text Rich Text ON)
    ///   characterContext — 소꿉친구 캐릭터 설정 ScriptableObject
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Friend/Friend Chat Panel View")]
    public sealed class FriendChatPanelView : MonoBehaviour
    {
        [Header("UI Refs")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform contentParent;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button closeButton;
        [Tooltip("전송 후 LLM 응답을 기다리는 동안 표시할 '...' 오브젝트. null 허용.")]
        [SerializeField] private GameObject typingIndicator;

        [Header("Prefab")]
        [Tooltip("채팅 한 줄 프리팹. TMP_Text 하나만 가진 단순한 라인 (Rich Text ON 필수).")]
        [SerializeField] private ChatMessageView chatLinePrefab;

        [Header("Character")]
        [SerializeField] private FriendCharacterContext characterContext;

        [Header("Senders")]
        [SerializeField] private string playerName = "나";
        [SerializeField] private Color  playerColor = new Color(0.50f, 0.82f, 1.00f); // 하늘색
        [SerializeField] private Color  npcColor    = new Color(1.00f, 0.82f, 0.29f); // 따뜻한 노랑
        [SerializeField] private Color  systemColor = new Color(0.70f, 0.70f, 0.70f); // 회색 (오류/시스템 메시지)

        [Header("Settings")]
        [Tooltip("히스토리에서 시스템 프롬프트 외 유지할 최대 메시지 수. 초과 시 오래된 것부터 삭제.")]
        [SerializeField] private int maxHistoryMessages = 20;
        [Tooltip("true: 타이핑 효과(스트리밍), false: 응답 완료 후 한 번에 표시(빠른 테스트용)")]
        [SerializeField] private bool useStreaming = true;

        /// <summary>FriendChatController 가 구독. 닫기 버튼 또는 외부 요청 시 발생.</summary>
        public event Action OnCloseRequested;

        // ── 내부 상태 ────────────────────────────────────────────────────

        // LLM 에 보내는 대화 기록. 앞쪽 system 메시지(들)는 절대 삭제 안 함.
        private readonly List<LlmApiClient.ChatMessage> _history = new();
        private bool _isWaitingForResponse;
        private ChatMessageView _currentNpcLine; // 스트리밍 중인 NPC 라인

        // ── 라이프사이클 ────────────────────────────────────────────────

        private void Awake()
        {
            if (sendButton  != null) sendButton.onClick.AddListener(OnSendClicked);
            if (closeButton != null) closeButton.onClick.AddListener(() => OnCloseRequested?.Invoke());
            if (typingIndicator != null) typingIndicator.SetActive(false);

            // 시스템 프롬프트를 히스토리 첫 번째로 추가.
            // 채팅창을 닫았다 열어도 히스토리는 유지되어 문맥 있는 대화가 계속됨.
            if (characterContext != null && _history.Count == 0)
                _history.Add(new LlmApiClient.ChatMessage("system", characterContext.BuildSystemPrompt()));
        }

        private void Update()
        {
            // Enter(Return) 키로도 전송. InputField 가 focus 상태이고 대기 중이 아닐 때만.
            if (inputField != null && inputField.isFocused
                && Input.GetKeyDown(KeyCode.Return)
                && !_isWaitingForResponse)
            {
                OnSendClicked();
            }
        }

        /// <summary>FriendChatController.Open() 이 패널 활성화 직후 호출.</summary>
        public void OnOpen()
        {
            ScrollToBottom();
            if (inputField != null) inputField.ActivateInputField();
        }

        /// <summary>
        /// 게임 컨텍스트(현재 층, HP 등)를 히스토리에 주입.
        /// FriendChatController.Open() 직전에 호출하면 LLM 이 게임 상황을 참고할 수 있음.
        /// </summary>
        public void InjectGameContext(string contextText)
        {
            // index 1 이 이미 context system 메시지면 교체, 없으면 삽입.
            if (_history.Count > 1 && _history[1].role == "system")
                _history[1] = new LlmApiClient.ChatMessage("system", contextText);
            else
                _history.Insert(1, new LlmApiClient.ChatMessage("system", contextText));
        }

        // ── 전송 처리 ────────────────────────────────────────────────────

        private void OnSendClicked()
        {
            if (_isWaitingForResponse) return;
            if (inputField == null) return;

            string userText = inputField.text.Trim();
            if (string.IsNullOrEmpty(userText)) return;

            inputField.text = string.Empty;
            inputField.ActivateInputField();

            // 1. 플레이어 라인 즉시 추가
            AddLine(playerName, playerColor, userText);

            // 2. 히스토리에 추가 + 토큰 제한 관리
            _history.Add(new LlmApiClient.ChatMessage("user", userText));
            TrimHistory();

            // 3. LLM 호출
            StartCoroutine(SendToLlmCoroutine());
        }

        private IEnumerator SendToLlmCoroutine()
        {
            _isWaitingForResponse = true;
            SetInputEnabled(false);
            if (typingIndicator != null) typingIndicator.SetActive(true);

            string npcName = characterContext != null ? characterContext.NpcName : "루아나";

            if (useStreaming)
            {
                // 빈 NPC 라인을 먼저 만들고 글자가 도착할 때마다 실시간으로 채움.
                // 이름 prefix 를 미리 박아두면 AppendText 가 자연스럽게 이름 뒤로 글자를 붙임.
                _currentNpcLine = SpawnLineWithPrefix(npcName, npcColor);

                yield return StartCoroutine(LlmApiClient.ChatStreamCoroutine(
                    _history,
                    onChunk: chunk =>
                    {
                        _currentNpcLine?.AppendText(chunk);
                        ScrollToBottom();
                    },
                    onComplete: fullText =>
                    {
                        _history.Add(new LlmApiClient.ChatMessage("assistant", fullText));
                        TrimHistory();
                        FinishResponse();
                    },
                    onError: _ =>
                    {
                        // 스트리밍 중 끊긴 라인은 시스템 색의 안내 메시지로 교체.
                        if (_currentNpcLine != null)
                        {
                            string colorHex = ColorUtility.ToHtmlStringRGB(systemColor);
                            _currentNpcLine.SetText(
                                $"<color=#{colorHex}>(지금 연결이 잠깐 끊겼어. 잠시 후에 다시 말걸어줘)</color>");
                        }
                        FinishResponse();
                    }
                ));
            }
            else
            {
                // 비스트리밍: Task 를 코루틴에서 폴링 (while (!task.IsCompleted) yield return null)
                var task = LlmApiClient.ChatAsync(_history);
                while (!task.IsCompleted) yield return null;

                string result = task.IsCompletedSuccessfully ? task.Result : null;

                if (!string.IsNullOrEmpty(result))
                {
                    AddLine(npcName, npcColor, result);
                    _history.Add(new LlmApiClient.ChatMessage("assistant", result));
                    TrimHistory();
                }
                else
                {
                    AddSystemLine("(지금 연결이 잠깐 끊겼어. 잠시 후에 다시 말걸어줘)");
                }

                FinishResponse();
            }
        }

        private void FinishResponse()
        {
            _isWaitingForResponse = false;
            _currentNpcLine       = null;
            SetInputEnabled(true);
            if (typingIndicator != null) typingIndicator.SetActive(false);
            ScrollToBottom();
        }

        // ── 라인 생성 ────────────────────────────────────────────────────

        /// <summary>
        /// 채팅 로그에 한 줄 추가. "발신자: 본문" 형식.
        /// 멀티플레이로 확장할 때도 이 메서드 하나만 호출하면 됨.
        /// </summary>
        private ChatMessageView AddLine(string senderName, Color color, string text)
        {
            var line = SpawnLineWithPrefix(senderName, color);
            if (line != null) line.AppendText(text);
            return line;
        }

        /// <summary>
        /// "발신자: " prefix 만 박힌 빈 라인을 생성. 스트리밍 모드의 시작점으로 사용.
        /// </summary>
        private ChatMessageView SpawnLineWithPrefix(string senderName, Color color)
        {
            if (chatLinePrefab == null)
            {
                Debug.LogError(
                    "[FriendChatPanelView] chatLinePrefab 이 null. Inspector 에서 연결하세요.", this);
                return null;
            }

            var line = Instantiate(chatLinePrefab, contentParent);
            string colorHex = ColorUtility.ToHtmlStringRGB(color);
            line.SetText($"<color=#{colorHex}>{senderName}</color>: ");
            ScrollToBottom();
            return line;
        }

        /// <summary>오류/안내 메시지 같은 시스템 라인. 발신자 prefix 없이 회색으로 표시.</summary>
        private void AddSystemLine(string text)
        {
            if (chatLinePrefab == null) return;
            var line = Instantiate(chatLinePrefab, contentParent);
            string colorHex = ColorUtility.ToHtmlStringRGB(systemColor);
            line.SetText($"<color=#{colorHex}>{text}</color>");
            ScrollToBottom();
        }

        // ── 유틸 ─────────────────────────────────────────────────────────

        private void ScrollToBottom()
        {
            if (scrollRect == null) return;
            StartCoroutine(ScrollToBottomNextFrame());
        }

        // VerticalLayoutGroup 의 레이아웃 재계산이 1프레임 뒤에 완료되므로 딜레이 필요
        private IEnumerator ScrollToBottomNextFrame()
        {
            yield return null;
            if (scrollRect != null)
                scrollRect.normalizedPosition = Vector2.zero; // (0,0) = 맨 아래
        }

        private void SetInputEnabled(bool enabled)
        {
            if (sendButton  != null) sendButton.interactable  = enabled;
            if (inputField  != null) inputField.interactable  = enabled;
        }

        /// <summary>
        /// 히스토리가 너무 길어지면 오래된 메시지 삭제 (앞쪽 system 메시지들은 유지).
        /// LLM 의 컨텍스트 토큰 초과를 방지하는 슬라이딩 윈도우.
        /// </summary>
        private void TrimHistory()
        {
            // 앞쪽 연속된 system 메시지 개수 = 시스템 프롬프트 + (선택) 게임 컨텍스트
            int systemCount = 0;
            for (int i = 0; i < _history.Count; i++)
            {
                if (_history[i].role == "system") systemCount++;
                else break;
            }

            while (_history.Count > systemCount + maxHistoryMessages)
                _history.RemoveAt(systemCount); // 가장 오래된 유저/어시스턴트 메시지 삭제
        }
    }
}
