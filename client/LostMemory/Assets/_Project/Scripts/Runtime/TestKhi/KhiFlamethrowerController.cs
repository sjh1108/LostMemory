using System;
using LostMemory.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 화염방사기 모드 입력·마나·발사 상태 컨트롤러. 활/스태프 컨트롤러와 같은 위치(캐릭터 root)에 부착.
    /// - 좌클릭(hold): Primary 분사 — 기본 데미지/범위/마나.
    /// - 우클릭(hold): Secondary 분사 — 강화된 데미지/범위/마나, 별도 VFX.
    /// 우선순위 정책: 우클릭 > 좌클릭. 우클릭 눌리면 좌클릭 분사 즉시 중단, 우 떼면 좌가 여전히
    /// 눌려있을 시 자동으로 Primary 재개. 마나가 0 이 되면 분사 자동 중단. 회복은 PlayerMana.autoRecover.
    /// WeaponModeController 가 모드 토글 시 enabled = false 로 입력 무시.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [AddComponentMenu("Lost Memory/Test Khi/Khi Flamethrower Controller")]
    public class KhiFlamethrowerController : MonoBehaviour
    {
        [Header("Refs (Awake 시 자동 resolve)")]
        [SerializeField] private KhiFlameZone flameZone;
        [Tooltip("플레이어 마나. 비어있으면 부모 체인에서 자동 검색. null 이면 마나 무제한으로 동작.")]
        [SerializeField] private PlayerMana playerMana;

        [Header("Primary Stream (Left Click Hold)")]
        [Tooltip("Primary 분사 ParticleSystem. 좌클릭 hold 동안 Play, release 시 Stop.")]
        [SerializeField] private ParticleSystem primaryFlameParticles;
        [Tooltip("초당 직접 데미지(콘 안 적 1명 기준).")]
        [SerializeField, Min(0f)] private float primaryDamagePerSec = 15f;
        [Tooltip("데미지 틱 간격(초). 0.1 = 1초 10회.")]
        [SerializeField, Min(0.02f)] private float primaryTickInterval = 0.1f;
        [Tooltip("매 틱마다 적에게 갱신하는 화상 초당 데미지.")]
        [SerializeField, Min(0f)] private float primaryBurnDamagePerSec = 3f;
        [Tooltip("매 틱마다 갱신하는 화상 지속시간(초).")]
        [SerializeField, Min(0.1f)] private float primaryBurnDurationOnHit = 2f;
        [Tooltip("초당 마나 소비량(Primary 분사 중).")]
        [SerializeField, Min(0f)] private float primaryManaPerSec = 8f;

        [Header("Secondary Stream (Right Click Hold) — 강화")]
        [Tooltip("Secondary 분사 ParticleSystem. 사용자가 별도로 만든 VFX 를 드래그.")]
        [SerializeField] private ParticleSystem secondaryFlameParticles;
        [Tooltip("초당 직접 데미지(Secondary).")]
        [SerializeField, Min(0f)] private float secondaryDamagePerSec = 35f;
        [Tooltip("데미지 틱 간격(Secondary).")]
        [SerializeField, Min(0.02f)] private float secondaryTickInterval = 0.08f;
        [Tooltip("Secondary 화상 초당 데미지.")]
        [SerializeField, Min(0f)] private float secondaryBurnDamagePerSec = 8f;
        [Tooltip("Secondary 화상 지속시간(초).")]
        [SerializeField, Min(0.1f)] private float secondaryBurnDurationOnHit = 3f;
        [Tooltip("초당 마나 소비량(Secondary 분사 중). Primary 보다 크게 설정 권장.")]
        [SerializeField, Min(0f)] private float secondaryManaPerSec = 20f;

        [Header("Debug")]
        [SerializeField] private bool logShotsToConsole = false;

        private float _manaAccumulator;
        private FlameMode? _activeStreamMode;
        // Phase E: 비-owner 측 자체 분사 차단.
        private NetworkObject _cachedNetObj;
        private bool _netObjResolved;

        public bool IsStreaming => _activeStreamMode.HasValue;
        public FlameMode? ActiveStreamMode => _activeStreamMode;

        public event Action<FlameMode> StreamStarted;
        public event Action<FlameMode> StreamStopped;

        private void Awake()
        {
            if (flameZone == null) flameZone = GetComponent<KhiFlameZone>();
            if (playerMana == null) playerMana = GetComponentInParent<PlayerMana>();
            if (flameZone != null)
            {
                flameZone.Initialize(
                    primaryDamagePerSec, primaryTickInterval, primaryBurnDamagePerSec, primaryBurnDurationOnHit,
                    secondaryDamagePerSec, secondaryTickInterval, secondaryBurnDamagePerSec, secondaryBurnDurationOnHit,
                    gameObject);
                flameZone.SetStreaming(false);
            }
            StopParticles(FlameMode.Primary);
            StopParticles(FlameMode.Secondary);
        }

        private void OnDisable()
        {
            if (_activeStreamMode.HasValue) StopStreaming();
        }

        private void Update()
        {
            // Phase E: 비-owner clone 은 자체 분사 금지. owner 만 입력 처리.
            if (IsRemoteClone())
            {
                if (_activeStreamMode.HasValue) StopStreaming();
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            bool rightPressed = mouse.rightButton.isPressed;
            bool leftPressed = mouse.leftButton.isPressed;

            // 우선순위: 우클릭 > 좌클릭. 마나 없으면 발동 안 함.
            FlameMode? wanted = null;
            if (rightPressed && HasManaForStream()) wanted = FlameMode.Secondary;
            else if (leftPressed && HasManaForStream()) wanted = FlameMode.Primary;

            // 상태 전이 처리
            if (wanted != _activeStreamMode)
            {
                if (_activeStreamMode.HasValue) StopStreaming();
                if (wanted.HasValue) StartStreaming(wanted.Value);
            }

            // 마나 소비 (활성 모드에 따라 다른 비율)
            if (_activeStreamMode.HasValue && !DrainStreamMana())
            {
                StopStreaming();
            }
        }

        /// <summary>Phase E: 비-owner clone 여부 (cached).</summary>
        private bool IsRemoteClone()
        {
            if (!_netObjResolved)
            {
                _cachedNetObj = GetComponentInParent<NetworkObject>();
                _netObjResolved = true;
            }
            return _cachedNetObj != null && _cachedNetObj.IsSpawned && !_cachedNetObj.IsOwner;
        }

        private bool HasManaForStream()
        {
            return playerMana == null || playerMana.CurrentMana > 0;
        }

        /// <summary>분사 중 매 프레임 마나 소비. false 반환 시 분사 중단해야 함.</summary>
        private bool DrainStreamMana()
        {
            if (playerMana == null) return true;

            float drainPerSec = GetManaPerSec(_activeStreamMode!.Value);
            if (drainPerSec <= 0f) return true;

            _manaAccumulator += drainPerSec * Time.deltaTime;
            int whole = Mathf.FloorToInt(_manaAccumulator);
            if (whole <= 0) return true;

            _manaAccumulator -= whole;
            if (!playerMana.Consume(whole))
            {
                // 잔량 부족 — 남은 마나 모두 긁어 소비 후 중단.
                int remaining = playerMana.CurrentMana;
                if (remaining > 0) playerMana.Consume(remaining);
                _manaAccumulator = 0f;
                return false;
            }
            return true;
        }

        private float GetManaPerSec(FlameMode mode)
        {
            return mode == FlameMode.Secondary ? secondaryManaPerSec : primaryManaPerSec;
        }

        private void StartStreaming(FlameMode mode)
        {
            _activeStreamMode = mode;
            _manaAccumulator = 0f;
            if (flameZone != null) flameZone.SetStreaming(true, mode);
            PlayParticles(mode);
            StreamStarted?.Invoke(mode);
            if (logShotsToConsole) Debug.Log($"[KhiFlame] stream START {mode}");
        }

        private void StopStreaming()
        {
            if (!_activeStreamMode.HasValue) return;
            FlameMode mode = _activeStreamMode.Value;
            _activeStreamMode = null;
            _manaAccumulator = 0f;
            if (flameZone != null) flameZone.SetStreaming(false, mode);
            StopParticles(mode);
            StreamStopped?.Invoke(mode);
            if (logShotsToConsole) Debug.Log($"[KhiFlame] stream STOP {mode}");
        }

        private void PlayParticles(FlameMode mode)
        {
            ParticleSystem ps = mode == FlameMode.Secondary ? secondaryFlameParticles : primaryFlameParticles;
            if (ps == null) return;
            ps.Play(true);
        }

        private void StopParticles(FlameMode mode)
        {
            ParticleSystem ps = mode == FlameMode.Secondary ? secondaryFlameParticles : primaryFlameParticles;
            if (ps == null) return;
            if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
