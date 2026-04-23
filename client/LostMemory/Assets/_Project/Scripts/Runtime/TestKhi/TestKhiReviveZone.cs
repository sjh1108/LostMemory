using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-014 MVP 테스트용 부활 영역.
    /// Down 상태 플레이어가 trigger 영역 안에 있을 때 E 입력으로 부활을 트리거한다.
    /// holdDuration == 0 이면 탭 한 번으로 즉시 부활, > 0 이면 해당 시간만큼 누르고 있어야 완료.
    /// 솔로 씬에선 "혼자서 영역에 들어와 E 누름" 경로가 되므로 실제 아군 부활 시뮬은 불완전하다.
    /// 후속 CL에서 아군 플레이어 기반으로 교체될 예정.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Test Khi Revive Zone")]
    [RequireComponent(typeof(Collider2D))]
    public class TestKhiReviveZone : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Min(0f)] private float holdDuration = 0f;
        [SerializeField] private bool resetProgressOnRelease = true;

        [Header("Debug")]
        [SerializeField] private bool logEventsToConsole = false;

        public event Action<float> ReviveProgressChanged;
        public event Action<KhiDownController> ReviveTriggered;

        private KhiDownController _occupant;
        private float _progress;
        private bool _wasHolding;

        public float CurrentProgress => _progress;
        public bool HasDownOccupant => _occupant != null && _occupant.IsDown;

        private void OnDisable()
        {
            ResetProgress();
            _occupant = null;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_occupant != null)
            {
                return;
            }

            KhiDownController controller = other.GetComponentInParent<KhiDownController>();
            if (controller != null)
            {
                _occupant = controller;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (_occupant == null)
            {
                return;
            }

            KhiDownController controller = other.GetComponentInParent<KhiDownController>();
            if (controller == _occupant)
            {
                _occupant = null;
                ResetProgress();
            }
        }

        private void Update()
        {
            if (_occupant == null || !_occupant.IsDown)
            {
                if (_progress > 0f)
                {
                    ResetProgress();
                }
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            bool isHolding = kb.eKey.isPressed;
            bool wasPressed = kb.eKey.wasPressedThisFrame;

            if (holdDuration <= 0f)
            {
                if (wasPressed)
                {
                    TriggerReviveNow(1f);
                }
                return;
            }

            if (isHolding)
            {
                _progress = Mathf.Min(holdDuration, _progress + Time.deltaTime);
                ReviveProgressChanged?.Invoke(Mathf.Clamp01(_progress / holdDuration));

                if (_progress >= holdDuration)
                {
                    TriggerReviveNow(1f);
                    return;
                }
            }
            else if (_wasHolding && resetProgressOnRelease)
            {
                ResetProgress();
            }

            _wasHolding = isHolding;
        }

        private void TriggerReviveNow(float finalProgress)
        {
            if (_occupant == null)
            {
                return;
            }

            KhiDownController target = _occupant;

            ReviveProgressChanged?.Invoke(Mathf.Clamp01(finalProgress));
            bool started = target.TryBeginRevive(gameObject);

            if (started && holdDuration > 0f)
            {
                // hold 모드에서 TryBeginRevive는 tap 완료 상태로만 CompleteRevive를 호출하므로
                // hold 완료 시엔 직접 ForceRevive로 확정한다.
                target.ForceRevive();
            }

            ReviveTriggered?.Invoke(target);

            if (logEventsToConsole)
            {
                Debug.Log($"[TestKhiReviveZone] Revive triggered on {target.name} (progress={finalProgress:F2})");
            }

            ResetProgress();
        }

        private void ResetProgress()
        {
            if (_progress == 0f && !_wasHolding)
            {
                return;
            }

            _progress = 0f;
            _wasHolding = false;
            ReviveProgressChanged?.Invoke(0f);
        }
    }
}
