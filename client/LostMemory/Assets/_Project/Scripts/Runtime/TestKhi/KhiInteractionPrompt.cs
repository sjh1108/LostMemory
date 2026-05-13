using MoreMountains.TopDownEngine;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// Trigger Collider2D 영역에 플레이어가 들어오면 프롬프트(자식 GameObject)를 표시하고,
    /// 영역 안에서 지정 키를 누르면 UnityEvent 를 발동시키는 단순 인터랙션 컴포넌트.
    ///
    /// 설계 의도:
    ///   - TopDownEngine 의 ButtonActivated/InputManager 의존성 없음.
    ///   - 우리 프로젝트가 직접 키 입력(InputSystem.Keyboard) 을 감지하는 패턴과 호환.
    ///   - 다양한 영역(상점, 문, 팻말, NPC 대화 등) 에 그대로 재사용.
    ///
    /// 부착 위치:
    ///   - 같은 GameObject 에 Collider2D (Is Trigger 체크) 필수.
    ///   - promptVisuals 는 보통 자식 GameObject (예: TMP 3D + SpriteRenderer 배경).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Lost Memory/Test Khi/Khi Interaction Prompt")]
    public class KhiInteractionPrompt : MonoBehaviour
    {
        [Header("Visuals")]
        [Tooltip("영역 진입 시 활성화될 프롬프트 GameObject. 자식 또는 별도 객체. 비워두면 표시 X.")]
        [SerializeField] private GameObject promptVisuals;

        [Header("Key Text (예: \"E\")")]
        [Tooltip("표시할 키 라벨. 비워두면 아래 Activation Key 값을 자동으로 사용 (예: E).")]
        [SerializeField] private string keyText;

        [Tooltip("키 라벨을 적용할 TMP 컴포넌트. 비워두면 promptVisuals 의 자식 중 이름에 'key' 포함된 TMP 자동 검색.")]
        [SerializeField] private TMP_Text keyTextTarget;

        [Header("Description Text (예: \"상점\")")]
        [Tooltip("표시할 설명 텍스트. 비워두면 적용 안 함.")]
        [SerializeField, TextArea(1, 3)] private string descriptionText;

        [Tooltip("설명 텍스트를 적용할 TMP 컴포넌트. 비워두면 promptVisuals 의 자식 중 이름에 'desc' 포함된 TMP 자동 검색.")]
        [SerializeField] private TMP_Text descriptionTextTarget;

        [Header("Activation")]
        [Tooltip("영역 안에서 이 키를 누르면 OnActivate 발화.")]
        [SerializeField] private Key activationKey = Key.E;

        [Tooltip("키가 눌렸을 때 호출되는 콜백. Inspector 에서 상점 열기/문 열기 등 연결.")]
        public UnityEvent OnActivate;

        [Header("Player Detection")]
        [Tooltip("true 면 TopDownEngine Character.CharacterType == Player 인 객체만 인정.")]
        [SerializeField] private bool requirePlayerCharacter = true;

        [Header("Debug")]
        [SerializeField] private bool logEvents = false;

        private bool _isInside;

        private void Awake()
        {
            ApplyPromptText();
            SetVisualsActive(false);
        }

        private void OnDisable()
        {
            // 컴포넌트 비활성 시 prompt 도 같이 숨김 (씬 전환 등에서 잔상 방지).
            _isInside = false;
            SetVisualsActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsValidPlayer(other))
            {
                return;
            }

            _isInside = true;
            SetVisualsActive(true);
            Log($"Enter by {other.name}");
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsValidPlayer(other))
            {
                return;
            }

            _isInside = false;
            SetVisualsActive(false);
            Log($"Exit by {other.name}");
        }

        private void Update()
        {
            if (!_isInside)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // wasPressedThisFrame: 키가 이 프레임에 눌리는 순간 한 번만 true (꾹 눌러도 1회).
            if (keyboard[activationKey].wasPressedThisFrame)
            {
                Log($"Activated (key={activationKey})");
                OnActivate?.Invoke();
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private bool IsValidPlayer(Collider2D other)
        {
            if (!requirePlayerCharacter)
            {
                return true;
            }

            // 충돌한 collider 자체 또는 부모에서 Character 컴포넌트 탐색.
            // TestKhi 처럼 hitbox/sub-collider 가 자식인 경우도 커버.
            Character character = other.GetComponent<Character>();
            if (character == null)
            {
                character = other.GetComponentInParent<Character>();
            }

            return character != null && character.CharacterType == Character.CharacterTypes.Player;
        }

        private void SetVisualsActive(bool active)
        {
            if (promptVisuals != null)
            {
                promptVisuals.SetActive(active);
            }
        }

        private void ApplyPromptText()
        {
            // Key 텍스트: 사용자가 keyText 입력했으면 그것, 아니면 activationKey enum 자동 사용.
            string effectiveKey = !string.IsNullOrEmpty(keyText)
                ? keyText
                : activationKey.ToString();

            if (keyTextTarget == null && promptVisuals != null)
            {
                keyTextTarget = FindTmpByNameHint(promptVisuals, "key");
            }
            if (keyTextTarget != null)
            {
                keyTextTarget.text = effectiveKey;
            }

            // Description 텍스트: 입력 있을 때만 적용 (디자인 기본값 유지 옵션).
            if (descriptionTextTarget == null && promptVisuals != null)
            {
                descriptionTextTarget = FindTmpByNameHint(promptVisuals, "desc");
            }
            if (descriptionTextTarget != null && !string.IsNullOrEmpty(descriptionText))
            {
                descriptionTextTarget.text = descriptionText;
            }
        }

        /// <summary>
        /// promptVisuals 하위에서 이름에 hint 가 포함된 (대소문자 무관) TMP_Text 컴포넌트 자동 검색.
        /// 못 찾으면 첫 번째 TMP_Text 반환 (fallback).
        /// </summary>
        private static TMP_Text FindTmpByNameHint(GameObject root, string hint)
        {
            TMP_Text[] all = root.GetComponentsInChildren<TMP_Text>(true);
            if (all == null || all.Length == 0)
            {
                return null;
            }

            string lowerHint = hint.ToLowerInvariant();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.ToLowerInvariant().Contains(lowerHint))
                {
                    return all[i];
                }
            }
            return all[0];
        }

        private void Log(string msg)
        {
            if (!logEvents)
            {
                return;
            }

            Debug.Log($"[KhiInteractionPrompt:{name}] {msg}");
        }

        // Editor 편의: Inspector 에서 promptText 변경 시 Play 안 해도 즉시 반영.
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            ApplyPromptText();
        }
    }
}
