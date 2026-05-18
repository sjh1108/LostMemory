#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-015 디버그용 OnGUI 상태 오버레이.
    /// KhiPlayerStateAggregator를 씬에서 자동 탐색해 현재 상태, 서브스테이트 타이머,
    /// raw 값, 최근 전이 history를 표시한다. F3으로 토글.
    /// 릴리스 빌드에서는 클래스 전체가 컴파일 제외된다.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Player State Debug Overlay")]
    [DefaultExecutionOrder(300)]
    public class KhiPlayerStateDebugOverlay : MonoBehaviour
    {
        [SerializeField] private KhiPlayerStateAggregator aggregator;
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;
        [SerializeField] private bool visibleOnStart = true;
        [SerializeField, Min(1)] private int historyCapacity = 10;
        [SerializeField] private Vector2 panelOrigin = new Vector2(10f, 10f);
        [SerializeField] private Vector2 panelSize = new Vector2(260f, 240f);
        [SerializeField] private int fontSize = 12;

        private readonly Queue<TransitionEntry> _history = new Queue<TransitionEntry>();
        private bool _visible;
        private GUIStyle _labelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _boxStyle;
        private bool _stylesInitialized;
        private KhiPlayerStateAggregator _subscribedTo;

        private struct TransitionEntry
        {
            public KhiPlayerState Previous;
            public KhiPlayerState Next;
            public float Time;
        }

        private void Awake()
        {
            _visible = visibleOnStart;
        }

        private void OnEnable()
        {
            EnsureAggregator();
        }

        private void OnDisable()
        {
            UnsubscribeFromAggregator();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            HandleToggle();

            if (aggregator == null || !aggregator.isActiveAndEnabled)
            {
                aggregator = null;
                UnsubscribeFromAggregator();
                EnsureAggregator();
            }
        }

        private void HandleToggle()
        {
            bool pressed = Input.GetKeyDown(toggleKey);
            if (!pressed && Keyboard.current != null)
            {
                pressed = Keyboard.current.f3Key.wasPressedThisFrame;
            }

            if (pressed)
            {
                _visible = !_visible;
            }
        }

        private void EnsureAggregator()
        {
            if (aggregator == null)
            {
                aggregator = FindFirstObjectByType<KhiPlayerStateAggregator>(FindObjectsInactive.Include);
            }

            if (aggregator != null && _subscribedTo != aggregator)
            {
                UnsubscribeFromAggregator();
                aggregator.StateChanged += HandleStateChanged;
                _subscribedTo = aggregator;
            }
        }

        private void UnsubscribeFromAggregator()
        {
            if (_subscribedTo != null)
            {
                _subscribedTo.StateChanged -= HandleStateChanged;
                _subscribedTo = null;
            }
        }

        private void HandleStateChanged(KhiPlayerState previous, KhiPlayerState next)
        {
            _history.Enqueue(new TransitionEntry
            {
                Previous = previous,
                Next = next,
                Time = Time.time
            });

            while (_history.Count > historyCapacity)
            {
                _history.Dequeue();
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !_visible)
            {
                return;
            }

            InitializeStyles();

            Rect rect = new Rect(panelOrigin.x, panelOrigin.y, panelSize.x, panelSize.y);
            GUI.Box(rect, GUIContent.none, _boxStyle);

            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f));
            DrawBody();
            GUILayout.EndArea();
        }

        private void DrawBody()
        {
            GUILayout.Label($"[KhiPlayerState]   {toggleKey} to toggle", _titleStyle);

            if (aggregator == null)
            {
                GUILayout.Label("No player (aggregator missing)", _labelStyle);
                return;
            }

            KhiPlayerState current = aggregator.CurrentState;
            string currentLine = current switch
            {
                KhiPlayerState.Parry => $"Parry ({aggregator.ParryRemaining:F2}s)",
                KhiPlayerState.Hurt => $"Hurt ({aggregator.HurtRemaining:F2}s)",
                KhiPlayerState.Down => $"Down ({aggregator.DownRemaining:F2}s)",
                _ => current.ToString()
            };

            GUILayout.Label($"State : {currentLine}", _labelStyle);
            GUILayout.Label($"Prev  : {aggregator.PreviousState}", _labelStyle);
            GUILayout.Label($"t_in  : {aggregator.TimeInCurrentState:F3}s", _labelStyle);
            GUILayout.Space(4f);

            GUILayout.Label("Raw:", _labelStyle);
            GUILayout.Label($"  Parry   : {aggregator.RawParryState} (rem {aggregator.ParryRemaining:F2})", _labelStyle);
            GUILayout.Label($"  HitStun : {aggregator.RawHitStunState}", _labelStyle);
            GUILayout.Label($"  Down    : {aggregator.RawDownState}", _labelStyle);
            GUILayout.Label($"  Melee   : atk={aggregator.RawIsAttacking} rec={aggregator.RawIsInAttackRecovery}", _labelStyle);
            GUILayout.Label($"  Dash    : {aggregator.RawIsDashing}", _labelStyle);
            GUILayout.Label($"  Cond    : {aggregator.RawConditionState}   Move: {aggregator.RawMovementState}", _labelStyle);
            GUILayout.Space(4f);

            GUILayout.Label("History:", _labelStyle);
            StringBuilder sb = new StringBuilder();
            foreach (TransitionEntry entry in _history)
            {
                sb.AppendLine($"  {entry.Previous} -> {entry.Next} @ {entry.Time:F3}");
            }
            if (sb.Length == 0)
            {
                GUILayout.Label("  (no transitions yet)", _labelStyle);
            }
            else
            {
                GUILayout.Label(sb.ToString().TrimEnd(), _labelStyle);
            }
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized)
            {
                return;
            }

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = Texture2D.grayTexture }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                normal = { textColor = Color.white },
                richText = false,
                wordWrap = false
            };

            _titleStyle = new GUIStyle(_labelStyle)
            {
                fontStyle = FontStyle.Bold
            };

            _stylesInitialized = true;
        }
    }
}
#endif
