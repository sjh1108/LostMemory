using System;
using LostMemory.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// 스태프 모드의 입력·스킬 발동 컨트롤러.
    /// - 좌클릭(hold): 마법탄 자동 연사 — 약한 데미지, 마나 무료, boltInterval 마다 한 발.
    /// - 우클릭(짧게): 파이어볼 — 마나 fireballManaCost 소비, 관통 강타.
    /// - 우클릭(길게 chargeThreshold 이상): 메테오 — 마나 meteorManaCost 소비, 마우스 worldPos 광역.
    /// 우클릭은 release 시점에 누른 시간으로 분기 (MOBA 스타일). 미만 = 파이어볼, 이상 = 메테오.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [AddComponentMenu("Lost Memory/Test Khi/Khi Staff Controller")]
    public class KhiStaffController : MonoBehaviour
    {
        [Header("Refs (Awake 시 자동 resolve)")]
        [SerializeField] private PlayerMana playerMana;
        [Tooltip("투사체 발사 위치 (마법탄·파이어볼 공용). 비어있으면 transform 사용.")]
        [SerializeField] private Transform projectileSpawnPoint;
        [Tooltip("마우스 worldPos 변환용. 비어있으면 Camera.main.")]
        [SerializeField] private Camera aimCamera;

        [Header("Magic Bolt (Left Click Hold — 평타 자동 연사)")]
        [Tooltip("마법탄 prefab. KhiArrow.prefab(단발) 재사용 추천.")]
        [SerializeField] private KhiArrowProjectile boltPrefab;
        [SerializeField, Min(1f)] private float boltSpeed = 16f;
        [SerializeField, Min(0f)] private float boltDamage = 8f;
        [Tooltip("뿅뿅뿅 — 마법탄 사이 최소 간격(초).")]
        [SerializeField, Min(0.02f)] private float boltInterval = 0.15f;
        [SerializeField, Min(0)] private int boltManaCost = 0;
        [Tooltip("마법탄 유도 — 초당 회전 각도. 0 = 직선, 60~90 = 약한 유도, 180+ = 강한 유도.")]
        [SerializeField, Min(0f)] private float boltHomingTurnRate = 90f;
        [Tooltip("유도 감지 반경. 이 안의 가장 가까운 적 추적.")]
        [SerializeField, Min(0f)] private float boltHomingRadius = 8f;

        [Header("Fireball (Right Click Tap — 짧게 누름)")]
        [Tooltip("파이어볼 prefab. KhiFireball.prefab(관통) 사용.")]
        [SerializeField] private KhiArrowProjectile fireballPrefab;
        [SerializeField, Min(1f)] private float fireballSpeed = 14f;
        [SerializeField, Min(0f)] private float fireballDamage = 25f;
        [SerializeField, Min(0)] private int fireballManaCost = 50;
        [Tooltip("연속 입력 방지용 짧은 쿨.")]
        [SerializeField, Min(0.05f)] private float fireballCooldown = 0.3f;

        [Header("Meteor (Right Click Hold — 차지)")]
        [SerializeField] private KhiMeteor meteorPrefab;
        [SerializeField, Min(0f)] private float meteorDamage = 80f;
        [SerializeField, Min(0.5f)] private float meteorRadius = 2f;
        [SerializeField, Min(0)] private int meteorManaCost = 100;
        [SerializeField, Min(0.05f)] private float meteorCooldown = 0.5f;

        [Header("Charge")]
        [Tooltip("우클릭 누른 시간이 이 값 이상이면 메테오, 미만이면 파이어볼. 추후 시각 신호로 표시 예정.")]
        [SerializeField, Min(0.1f)] private float chargeThreshold = 0.5f;

        [Header("Charge UI (Optional)")]
        [Tooltip("자식으로 자동 Instantiate 될 차징바 prefab. 비워두면 UI 없음.")]
        [SerializeField] private GameObject chargeBarPrefab;
        [Tooltip("차징바의 localPosition (캐릭터 root 기준). 머리 위 = (0, 0.8, 0) 권장.")]
        [SerializeField] private Vector3 chargeBarLocalOffset = new Vector3(0f, 0.8f, 0f);

        [Header("Debug")]
        [SerializeField] private bool logSkillsToConsole = false;

        private float _nextBoltAt;
        private float _nextFireballAt;
        private float _nextMeteorAt;
        private float _rightPressStartTime;
        private bool _isHoldingRight;

        /// <summary>우클릭 차징 진행도 0~1. UI 차징바가 구독.</summary>
        public float ChargeProgress01 => _isHoldingRight && chargeThreshold > 0f
            ? Mathf.Clamp01((Time.time - _rightPressStartTime) / chargeThreshold)
            : 0f;
        /// <summary>지금 우클릭 차징 중인가.</summary>
        public bool IsCharging => _isHoldingRight;
        /// <summary>차징 임계값 도달했는가 (release 시 메테오 발동).</summary>
        public bool IsChargeReady => ChargeProgress01 >= 1f;
        private int _sequenceId;

        public event Action<KhiAttackRequest> BoltFired;
        public event Action<KhiAttackRequest> FireballFired;
        public event Action<KhiAttackRequest> MeteorFired;

        // 기존 호환 — 활성 모드 토글 등에서 구독 중인 외부 코드가 있을 수 있음.
        public event Action<KhiAttackRequest> Skill1Fired;
        public event Action<KhiAttackRequest> Skill2Fired;

        private void Awake()
        {
            playerMana ??= GetComponentInParent<PlayerMana>();
            if (projectileSpawnPoint == null) projectileSpawnPoint = transform;

            if (chargeBarPrefab != null)
            {
                GameObject bar = Instantiate(chargeBarPrefab, transform);
                bar.transform.localPosition = chargeBarLocalOffset;
            }
        }

        private void OnDisable()
        {
            // 모드 전환 시 차지 상태 reset.
            _isHoldingRight = false;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // 좌클릭 hold → 자동 연사.
            if (mouse.leftButton.isPressed && Time.time >= _nextBoltAt)
            {
                TryCastBolt();
            }

            // 우클릭 down → 차지 시작.
            if (mouse.rightButton.wasPressedThisFrame)
            {
                _rightPressStartTime = Time.time;
                _isHoldingRight = true;
            }

            // 우클릭 release → 차지 시간 평가.
            if (mouse.rightButton.wasReleasedThisFrame && _isHoldingRight)
            {
                _isHoldingRight = false;
                float heldFor = Time.time - _rightPressStartTime;
                if (heldFor >= chargeThreshold)
                {
                    if (Time.time >= _nextMeteorAt) TryCastMeteor();
                }
                else
                {
                    if (Time.time >= _nextFireballAt) TryCastFireball();
                }
            }
        }

        private void TryCastBolt()
        {
            if (boltPrefab == null) return;
            if (boltManaCost > 0 && playerMana != null && !playerMana.Consume(boltManaCost))
            {
                _nextBoltAt = Time.time + boltInterval;
                return;
            }

            Vector3 spawnPos = projectileSpawnPoint.position;
            Vector2 aimDir = ComputeAimFromOrigin(spawnPos);

            KhiArrowProjectile bolt = Instantiate(boltPrefab, spawnPos, Quaternion.identity);
            bolt.Launch(aimDir, boltSpeed, boltDamage, gameObject);
            if (boltHomingTurnRate > 0f && boltHomingRadius > 0f)
            {
                bolt.SetHoming(boltHomingTurnRate, boltHomingRadius);
            }

            _nextBoltAt = Time.time + boltInterval;

            KhiAttackRequest request = MakeRequest(aimDir, spawnPos);
            BoltFired?.Invoke(request);
            if (logSkillsToConsole) Debug.Log($"[KhiStaff] Bolt seq={request.SequenceId} dmg={boltDamage}");
        }

        private void TryCastFireball()
        {
            if (fireballPrefab == null) return;
            if (playerMana != null && !playerMana.Consume(fireballManaCost)) return;

            Vector3 spawnPos = projectileSpawnPoint.position;
            Vector2 aimDir = ComputeAimFromOrigin(spawnPos);

            KhiArrowProjectile fireball = Instantiate(fireballPrefab, spawnPos, Quaternion.identity);
            fireball.Launch(aimDir, fireballSpeed, fireballDamage, gameObject);

            _nextFireballAt = Time.time + fireballCooldown;

            KhiAttackRequest request = MakeRequest(aimDir, spawnPos);
            FireballFired?.Invoke(request);
            Skill1Fired?.Invoke(request);
            if (logSkillsToConsole) Debug.Log($"[KhiStaff] Fireball seq={request.SequenceId} dmg={fireballDamage}");
        }

        private void TryCastMeteor()
        {
            if (meteorPrefab == null) return;
            if (playerMana != null && !playerMana.Consume(meteorManaCost)) return;

            Vector3 mouseWorld = GetMouseWorld();
            mouseWorld.z = 0f;
            KhiMeteor meteor = Instantiate(meteorPrefab, mouseWorld, Quaternion.identity);
            meteor.Detonate(meteorDamage, meteorRadius, gameObject);

            _nextMeteorAt = Time.time + meteorCooldown;

            KhiAttackRequest request = MakeRequest(Vector2.down, mouseWorld);
            MeteorFired?.Invoke(request);
            Skill2Fired?.Invoke(request);
            if (logSkillsToConsole) Debug.Log($"[KhiStaff] Meteor seq={request.SequenceId} pos={mouseWorld}");
        }

        private KhiAttackRequest MakeRequest(Vector2 dir, Vector3 origin)
        {
            return new KhiAttackRequest
            {
                SequenceId = ++_sequenceId,
                ComboStep = 1,
                AimDirection = dir,
                AimAngleDegrees = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg,
                Origin = origin,
                StartedAt = Time.time,
                Attacker = gameObject
            };
        }

        private Vector2 ComputeAimFromOrigin(Vector3 origin)
        {
            Vector3 worldPos = GetMouseWorld();
            Vector2 dir = (Vector2)(worldPos - origin);
            return dir.sqrMagnitude > Mathf.Epsilon ? dir.normalized : Vector2.right;
        }

        private Vector3 GetMouseWorld()
        {
            Camera cam = aimCamera != null ? aimCamera : Camera.main;
            Mouse mouse = Mouse.current;
            if (cam == null || mouse == null) return transform.position;
            Vector2 screenPos = mouse.position.ReadValue();
            return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        }
    }
}
