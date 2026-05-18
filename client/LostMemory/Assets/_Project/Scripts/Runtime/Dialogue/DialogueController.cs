using System;
using System.Collections;
using System.Collections.Generic;
using LostMemory.Stage;
using UnityEngine;

namespace LostMemory.Dialogue
{
    /// <summary>
    /// Scene 단일 인스턴스. DialoguePanel.prefab 의 root 또는 자식에 부착.
    ///
    /// 책임:
    ///   1. groupId 받아서 DialogueDatabase 에서 group 조회
    ///   2. DialoguePanelView 에 한 라인씩 전달
    ///   3. 입력 (마우스 좌클릭 / Space) 받아서:
    ///      - 타이핑 중 → SkipTypewriter
    ///      - 타이핑 완료 → 다음 라인. 마지막이면 Hide + OnDialogueEnd 발화
    ///   4. IBossIntroDialoguePlayer 구현 — 보스 입장 시퀀스에서도 자동 연동
    ///
    /// 정적 접근:
    ///   - DialogueController.Instance (Scene 의 첫 인스턴스 자동 캐시. DontDestroyOnLoad X)
    ///   - Scene 별로 새 인스턴스. 사용자 결정사항.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Dialogue/Dialogue Controller")]
    public sealed class DialogueController : MonoBehaviour, IBossIntroDialoguePlayer
    {
        [Header("Refs")]
        [SerializeField] private DialoguePanelView panelView;
        [SerializeField] private DialogueDatabase database;
        [SerializeField] private PortraitCatalog portraitCatalog;
        [SerializeField] private DialogueSkin defaultSkin;

        [Header("Input")]
        [Tooltip("타이프라이터 진행/완료/넘기기 등에 사용되는 advance 키 (Space 등)")]
        [SerializeField] private KeyCode advanceKey = KeyCode.Space;
        [Tooltip("마우스 좌클릭으로도 advance 허용")]
        [SerializeField] private bool useMouseLeftClick = true;

        [Header("Debug")]
        [SerializeField] private bool debugLogging;

        // === Static Scene-local instance ===
        private static DialogueController _instance;
        public static DialogueController Instance
        {
            get
            {
                if (_instance == null)
                {
                    // FindFirstObjectByType: Unity 2022.3+ 권장. 비활성도 inspector 에서 enabled=false 가 아니면 찾힘.
                    _instance = FindFirstObjectByType<DialogueController>(FindObjectsInactive.Include);
                }
                return _instance;
            }
        }

        // === Runtime state ===
        private DialogueGroup _currentGroup;
        private int _currentLineIndex;
        private bool _isPlaying;
        private Action _onCurrentGroupFinished;
        private Queue<PendingGroup> _pendingQueue;

        private readonly struct PendingGroup
        {
            public readonly string GroupId;
            public readonly Action OnFinished;
            public PendingGroup(string groupId, Action onFinished) { GroupId = groupId; OnFinished = onFinished; }
        }

        public bool IsPlaying => _isPlaying;
        public string CurrentGroupId => _currentGroup?.GroupId;

        /// <summary>
        /// (groupId) — 한 그룹 종료 시 발화.
        /// </summary>
        public event Action<string> OnDialogueEnd;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[DialogueController] Scene 에 이미 인스턴스가 존재 — 이 인스턴스는 무시됨. ({name})", this);
                return;
            }
            _instance = this;

            if (panelView == null) panelView = GetComponentInChildren<DialoguePanelView>(includeInactive: true);
            if (defaultSkin != null && panelView != null) panelView.ApplySkin(defaultSkin);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (!_isPlaying) return;

            bool advancePressed = Input.GetKeyDown(advanceKey) || (useMouseLeftClick && Input.GetMouseButtonDown(0));
            if (!advancePressed) return;

            if (panelView != null && panelView.IsTyping)
            {
                panelView.SkipTypewriter();
            }
            else
            {
                AdvanceToNextLine();
            }
        }

        // === Public API ===

        /// <summary>
        /// groupId 로 대화 시작. 이미 진행 중이면 대기열에 추가.
        /// </summary>
        public void Show(string groupId, Action onGroupFinished = null)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                Debug.LogWarning("[DialogueController] Show 호출에 빈 groupId.", this);
                onGroupFinished?.Invoke();
                return;
            }
            if (_isPlaying)
            {
                EnqueueGroup(groupId, onGroupFinished);
                return;
            }
            StartGroup(groupId, onGroupFinished);
        }

        /// <summary>
        /// 여러 그룹을 순차 재생 (보스 인트로 cue 배열 같은 경우).
        /// 마지막 그룹까지 끝나면 onAllFinished 발화.
        /// </summary>
        public void ShowSequence(IList<string> groupIds, Action onAllFinished = null)
        {
            if (groupIds == null || groupIds.Count == 0)
            {
                onAllFinished?.Invoke();
                return;
            }

            // 첫 그룹만 즉시(또는 대기열 끝에) 시작, 나머지는 대기열 push.
            // 마지막 그룹의 onFinished 에 onAllFinished 부착.
            for (int i = 0; i < groupIds.Count; i++)
            {
                bool isLast = (i == groupIds.Count - 1);
                Show(groupIds[i], isLast ? onAllFinished : null);
            }
        }

        public void Hide()
        {
            CleanupCurrent();
            if (panelView != null) panelView.HidePanel();
        }

        public void ApplySkin(DialogueSkin skin)
        {
            if (panelView != null) panelView.ApplySkin(skin);
        }

        // === IBossIntroDialoguePlayer ===

        public void Play(
            BossIntroSequenceData sequenceData,
            BossRoomTransitionCompletedContext context,
            Action onCompleted)
        {
            if (sequenceData == null || !sequenceData.HasDialogueCueIds)
            {
                if (debugLogging) Debug.Log("[DialogueController] BossIntro: cue 없음. 즉시 완료.", this);
                onCompleted?.Invoke();
                return;
            }

            ShowSequence(sequenceData.DialogueCueIds, onCompleted);
        }

        // === Internal ===

        private void EnqueueGroup(string groupId, Action onFinished)
        {
            _pendingQueue ??= new Queue<PendingGroup>();
            _pendingQueue.Enqueue(new PendingGroup(groupId, onFinished));
            if (debugLogging) Debug.Log($"[DialogueController] '{groupId}' 대기열에 추가 (depth={_pendingQueue.Count}).", this);
        }

        private void StartGroup(string groupId, Action onFinished)
        {
            if (database == null)
            {
                Debug.LogError("[DialogueController] database 미할당.", this);
                onFinished?.Invoke();
                return;
            }
            if (!database.TryGetGroup(groupId, out DialogueGroup group))
            {
                Debug.LogError($"[DialogueController] groupId '{groupId}' 를 DB 에서 찾을 수 없음.", this);
                onFinished?.Invoke();
                return;
            }
            if (group.Count == 0)
            {
                Debug.LogWarning($"[DialogueController] groupId '{groupId}' 라인 0개.", this);
                onFinished?.Invoke();
                return;
            }

            _currentGroup = group;
            _currentLineIndex = 0;
            _isPlaying = true;
            _onCurrentGroupFinished = onFinished;

            if (debugLogging) Debug.Log($"[DialogueController] Show '{groupId}' ({group.Count} lines).", this);
            DisplayCurrentLine();
        }

        private void DisplayCurrentLine()
        {
            if (_currentGroup == null) return;
            DialogueLine line = _currentGroup[_currentLineIndex];
            Sprite portrait = portraitCatalog != null ? portraitCatalog.Get(line.PortraitId) : null;

            if (panelView == null)
            {
                Debug.LogError("[DialogueController] panelView 미할당.", this);
                return;
            }
            panelView.ShowLine(line, portrait);
        }

        private void AdvanceToNextLine()
        {
            if (_currentGroup == null) return;
            _currentLineIndex++;
            if (_currentLineIndex >= _currentGroup.Count)
            {
                FinishCurrentGroup();
                return;
            }
            DisplayCurrentLine();
        }

        private void FinishCurrentGroup()
        {
            string finishedId = _currentGroup?.GroupId;
            Action finishedCallback = _onCurrentGroupFinished;

            CleanupCurrent();

            if (debugLogging) Debug.Log($"[DialogueController] '{finishedId}' 종료.", this);
            OnDialogueEnd?.Invoke(finishedId);
            finishedCallback?.Invoke();

            // 대기열에 다음이 있으면 이어서 재생.
            if (_pendingQueue != null && _pendingQueue.Count > 0)
            {
                PendingGroup next = _pendingQueue.Dequeue();
                StartGroup(next.GroupId, next.OnFinished);
            }
            else
            {
                if (panelView != null) panelView.HidePanel();
            }
        }

        private void CleanupCurrent()
        {
            _currentGroup = null;
            _currentLineIndex = 0;
            _isPlaying = false;
            _onCurrentGroupFinished = null;
        }
    }
}
