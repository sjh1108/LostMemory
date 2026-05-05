using System.Collections;
using System.Collections.Generic;
using LostMemory.Combat;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// CL-145 (Phase 3): 미소녀 5합체 fusion 엔티티 — T/Y 키 분리 발동, 공유 쿨다운.
    ///
    /// MagicalGirlSpawner.EnsureFusion 가 GameObject 생성 후 AddComponent + Init 호출.
    /// Awake 에서 절차적 SpriteRenderer (분홍, 2x 크기).
    ///
    /// 발동 방식 (Phase 3 변경 — 사용자 원래 의도):
    /// - T 키: 레이저 5초 마우스 조준 지속 (오버워치 모이라 궁 풍)
    /// - Y 키: AOE 즉발 화면 전체 (강력 광역)
    /// - 공유 쿨다운 25초 — 한쪽 사용하면 둘 다 25초 막힘 (Spawner-tracked)
    /// - 전략적 선택: 적 모임 → AOE / 보스 단일 → 레이저
    /// - Fusion 진입 시 cooldown=0 (즉시 사용 가능, 5스택 도달 보상감)
    ///
    /// 카메라 흔들림 (Phase 3):
    /// - AOE (Y): 강한 흔들림 0.3초 / 0.3 강도 + 화면 플래시
    /// - 레이저 (T): 시작 약한 흔들림 + 매 1초 펄스 5번 (총 6번 = burst 동안)
    ///
    /// Character.AI 필터 (CL-144 패턴): ArcherArrow 등 발사체 제외.
    /// 무한 루프 방지: Health.Damage 직접 호출 (TargetHit 이벤트 무관).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagicalGirlFusion : MonoBehaviour
    {
        [Header("Common")]
        [SerializeField, Min(0.1f)] private float spriteSize = 0.8f;        // 일반 미소녀 0.4의 2배
        [SerializeField]            private int spriteSortingOrder = 101;

        [Header("Burst (Phase 2 — R 키)")]
        [Tooltip("Burst 지속시간 (초). R 누른 후 레이저 발화 길이.")]
        [SerializeField, Min(0.1f)] private float burstDuration = 5f;

        [Tooltip("Burst 종료 후 다음 R 까지 쿨다운 (초).")]
        [SerializeField, Min(0f)]   private float cooldown = 25f;

        [Header("Laser")]
        [SerializeField, Min(0.05f)]private float laserTickInterval = 0.1f;
        [SerializeField, Min(0f)]   private float laserDamageRatio = 0.5f;
        [SerializeField, Min(0.1f)] private float laserLength = 10f;
        [SerializeField, Min(0.1f)] private float laserWidth = 1f;

        [Header("Global AOE (Burst 시작 시 1회)")]
        [SerializeField, Min(0f)]   private float aoeDamageRatio = 3.0f;
        [SerializeField, Min(0.05f)]private float screenFlashDuration = 0.2f;

        [Header("Camera Shake")]
        [Tooltip("Burst 시작 (= AOE 폭발) 시 카메라 흔들림 지속 (초).")]
        [SerializeField, Min(0f)]   private float burstShakeDuration = 0.3f;

        [Tooltip("Burst 시작 시 카메라 흔들림 강도 (유닛).")]
        [SerializeField, Min(0f)]   private float burstShakeIntensity = 0.3f;

        [Tooltip("Burst 중 주기적 흔들림 (1초 간격) 지속 (초). 시작 흔들림보다 짧게.")]
        [SerializeField, Min(0f)]   private float periodicShakeDuration = 0.15f;

        [Tooltip("Burst 중 주기적 흔들림 강도 (유닛). 시작보다 약하게 — 두근거리는 느낌.")]
        [SerializeField, Min(0f)]   private float periodicShakeIntensity = 0.15f;

        [Tooltip("Burst 중 주기적 흔들림 간격 (초). 1초 = 매 초마다 한 번씩.")]
        [SerializeField, Min(0.1f)] private float periodicShakeInterval = 1f;

        [Header("Debug")]
        [SerializeField] private bool _logFusion = true;

        private SpriteRenderer _sr;
        private bool _burstActive;
        private float _nextReadyAt;          // Time.time 기준, 초기값 0 = 즉시 사용 가능
        private Coroutine _shakeCoroutine;

        private PlayerStatModifierContainer _stat;
        private KhiMeleeComboController _combat;
        private KhiPlayerAim _aim;
        private MagicalGirlSpawner _spawner;        // CL-145 Phase 2: cooldown 공유 source

        private static Material _laserMaterial;

        // 레이저 visual 색상 (분홍 핑크빛)
        private static readonly Color LaserColor = new Color(1f, 0.4f, 0.8f, 1f);
        private const float LaserBoltWidth = 0.4f;
        private const int LaserBoltSortingOrder = 9999;

        private void Awake()
        {
            // 절차적 sprite — 분홍, 2x 크기, 약간 투명
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: Texture2D.whiteTexture.width);
            _sr.color = new Color(1f, 0.5f, 0.8f, 0.95f);
            _sr.sortingOrder = spriteSortingOrder;
            transform.localScale = new Vector3(spriteSize, spriteSize, 1f);
        }

        public void Init(PlayerStatModifierContainer stat, KhiMeleeComboController combat, KhiPlayerAim aim, MagicalGirlSpawner spawner)
        {
            _stat = stat;
            _combat = combat;
            _aim = aim;
            _spawner = spawner;
            // Spawner 가 보관한 이전 cooldown 시점 적용 — 빌드 풀고 재진입해도 cooldown 유지.
            // 첫 fusion 진입 시 spawner._fusionCooldownEndsAt=0 → 즉시 사용 가능.
            if (_spawner != null)
                _nextReadyAt = _spawner.FusionCooldownEndsAt;
        }

        // ── Update — T (레이저) / Y (AOE) 키 listener (Phase 3) ─

        private void Update()
        {
            // Pause / reward panel 등 timeScale=0 상황에서는 입력 무시 (UI 모드).
            if (Time.timeScale == 0f) return;
            if (Keyboard.current == null) return;
            if (Keyboard.current.tKey.wasPressedThisFrame) TryFireLaserBurst();
            if (Keyboard.current.yKey.wasPressedThisFrame) TryFireAOE();
        }

        // T: 5초 레이저 burst (마우스 조준 지속) + 매 1초 펄스 흔들림
        private void TryFireLaserBurst()
        {
            if (_burstActive)
            {
                if (_logFusion) Debug.Log("[Fusion] T pressed but burst already active");
                return;
            }
            if (Time.time < _nextReadyAt)
            {
                if (_logFusion) Debug.Log($"[Fusion] T pressed but cooldown remaining {_nextReadyAt - Time.time:F1}s");
                return;
            }

            _burstActive = true;
            if (_logFusion) Debug.Log($"[Fusion] T LASER BURST START (duration={burstDuration}s, cooldown={cooldown}s after)");

            // 공유 쿨다운 예약: burst 종료 + cooldown
            if (_spawner != null)
                _spawner.NotifyBurstStarted(burstDuration, cooldown);

            // 레이저 시작 약한 흔들림 (펄스와 동급)
            TriggerCameraShake(periodicShakeDuration, periodicShakeIntensity);

            // 5초 레이저 + 매 1초 펄스 흔들림 (1/2/3/4/5초 시점)
            StartCoroutine(BurstLaserCoroutine());
            StartCoroutine(PeriodicShakeCoroutine());
        }

        // Y: AOE 즉발 화면 전체 + 강한 흔들림 + 화면 플래시
        private void TryFireAOE()
        {
            if (_burstActive)
            {
                if (_logFusion) Debug.Log("[Fusion] Y pressed but burst already active");
                return;
            }
            if (Time.time < _nextReadyAt)
            {
                if (_logFusion) Debug.Log($"[Fusion] Y pressed but cooldown remaining {_nextReadyAt - Time.time:F1}s");
                return;
            }

            if (_logFusion) Debug.Log($"[Fusion] Y AOE PULSE (cooldown={cooldown}s after)");

            // 공유 쿨다운 예약: 즉발이라 burstDuration=0
            if (_spawner != null)
                _spawner.NotifyBurstStarted(0f, cooldown);

            // 로컬 _nextReadyAt 도 즉시 갱신 — 같은 fusion 인스턴스 내 Y 연타 차단
            // (T 는 BurstLaserCoroutine 끝에서 갱신되지만 Y 는 즉발이라 여기서 직접)
            _nextReadyAt = Time.time + cooldown;

            // AOE + 강한 흔들림 (화면 플래시는 FireGlobalAOE 안에서 자동)
            FireGlobalAOE();
            TriggerCameraShake(burstShakeDuration, burstShakeIntensity);
        }

        private IEnumerator BurstLaserCoroutine()
        {
            yield return LaserCoroutine();
            _burstActive = false;
            _nextReadyAt = Time.time + cooldown;
            if (_logFusion) Debug.Log($"[Fusion] BURST END — cooldown {cooldown}s");
        }

        // Burst 중 매 periodicShakeInterval 초마다 흔들림 (시작 0초는 TryFireBurst 가 직접 처리).
        // burstDuration=5, interval=1 이면 +1, +2, +3, +4, +5 초 시점에 5번 발화 (총 6번 = 시작 포함).
        private IEnumerator PeriodicShakeCoroutine()
        {
            int totalPulses = Mathf.FloorToInt(burstDuration / periodicShakeInterval);
            for (int i = 1; i <= totalPulses && _burstActive; i++)
            {
                yield return new WaitForSeconds(periodicShakeInterval);
                if (!_burstActive) yield break;
                TriggerCameraShake(periodicShakeDuration, periodicShakeIntensity);
            }
        }

        // ── Pattern: 레이저 (burstDuration 동안 지속) ─────

        private IEnumerator LaserCoroutine()
        {
            float endsAt = Time.time + burstDuration;
            var damageBuf = new HashSet<Health>();   // 동일 틱 내 동일 적 중복 데미지 방지
            GameObject lineGO = CreateLaserLine();
            int totalHits = 0;

            while (Time.time < endsAt && this != null)
            {
                damageBuf.Clear();
                Vector2 origin = _combat != null ? (Vector2)_combat.transform.position : (Vector2)transform.position;
                Vector2 dir = ComputeLaserDirection();
                Vector2 endPoint = origin + dir * laserLength;
                Vector2 boxCenter = origin + dir * (laserLength * 0.5f);
                Vector2 boxSize = new Vector2(laserLength, laserWidth);
                float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                float damage = ComputePlayerDamage() * laserDamageRatio;
                if (damage > 0f)
                {
                    Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, boxSize, angleDeg);
                    foreach (Collider2D col in hits)
                    {
                        if (col == null) continue;
                        Health h = col.GetComponentInParent<Health>();
                        if (h == null || h.CurrentHealth <= 0f) continue;
                        Character ch = h.GetComponentInParent<Character>();
                        if (ch == null || ch.CharacterType != Character.CharacterTypes.AI) continue;
                        if (!damageBuf.Add(h)) continue;
                        h.Damage(damage, gameObject, 0f, 0f, Vector3.zero);
                        totalHits++;
                    }
                }

                UpdateLaserLine(lineGO, origin, endPoint);
                yield return new WaitForSeconds(laserTickInterval);
            }

            if (lineGO != null) Destroy(lineGO);
            if (_logFusion) Debug.Log($"[Fusion] Laser ended — {totalHits} total hits");
        }

        // ── Pattern B: 전역 AOE ────────────────────────────

        private void FireGlobalAOE()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                if (_logFusion) Debug.Log("[Fusion] GlobalAOE skipped — Camera.main null");
                return;
            }

            Vector2 viewportMin = cam.ViewportToWorldPoint(Vector2.zero);
            Vector2 viewportMax = cam.ViewportToWorldPoint(Vector2.one);
            Vector2 size = viewportMax - viewportMin;
            Vector2 center = (viewportMin + viewportMax) * 0.5f;

            float damage = ComputePlayerDamage() * aoeDamageRatio;
            int hitCount = 0;
            if (damage > 0f)
            {
                Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
                foreach (Collider2D col in hits)
                {
                    if (col == null) continue;
                    Health h = col.GetComponentInParent<Health>();
                    if (h == null || h.CurrentHealth <= 0f) continue;
                    Character ch = h.GetComponentInParent<Character>();
                    if (ch == null || ch.CharacterType != Character.CharacterTypes.AI) continue;
                    h.Damage(damage, gameObject, 0f, 0f, Vector3.zero);
                    hitCount++;
                }
            }

            StartCoroutine(ScreenFlashCoroutine(center, size));
            if (_logFusion) Debug.Log($"[Fusion] GlobalAOE → {hitCount} hits, {damage:F1} each");
        }

        private IEnumerator ScreenFlashCoroutine(Vector2 center, Vector2 size)
        {
            var go = new GameObject("FusionScreenFlash");
            go.transform.position = new Vector3(center.x, center.y, 0f);
            go.transform.localScale = new Vector3(size.x * 1.2f, size.y * 1.2f, 1f);
            // Fusion 자식으로 parent 설정 — fusion destroy 시 자동 정리. worldPositionStays=true 로 화면 중앙 위치 유지.
            // 0.2초 짧은 효과지만 burst 직후 inven clear 같은 case 에 잔존 방지.
            go.transform.SetParent(transform, worldPositionStays: true);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: Texture2D.whiteTexture.width);
            sr.color = new Color(1f, 1f, 1f, 0.5f);
            sr.sortingOrder = 32000;

            float t = 0f;
            while (t < screenFlashDuration && go != null)
            {
                t += Time.deltaTime;
                float alpha = Mathf.Lerp(0.5f, 0f, t / screenFlashDuration);
                if (sr != null) sr.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // ── 카메라 흔들림 (Phase 2) ────────────────────────

        private void TriggerCameraShake(float duration, float intensity)
        {
            Camera cam = Camera.main;
            if (cam == null || duration <= 0f || intensity <= 0f) return;
            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(ShakeCoroutine(cam, duration, intensity));
        }

        private IEnumerator ShakeCoroutine(Camera cam, float duration, float intensity)
        {
            Transform t = cam.transform;
            Vector3 originalLocal = t.localPosition;
            float elapsed = 0f;
            while (elapsed < duration && cam != null)
            {
                elapsed += Time.deltaTime;
                float damper = 1f - Mathf.Clamp01(elapsed / duration);
                Vector2 offset = Random.insideUnitCircle * intensity * damper;
                t.localPosition = originalLocal + (Vector3)offset;
                yield return null;
            }
            if (cam != null) t.localPosition = originalLocal;
            _shakeCoroutine = null;
        }

        // ── 데미지 / 방향 산출 ─────────────────────────────

        private float ComputePlayerDamage()
        {
            float atk = _combat != null && _combat.WeaponData != null ? _combat.WeaponData.BaseDamage : 0f;
            if (_stat != null) atk *= _stat.GetTotalMultiplier(StatId.AttackPower);
            return atk;
        }

        private Vector2 ComputeLaserDirection()
        {
            Vector2 dir = _aim != null ? _aim.GetAimDirection() : Vector2.zero;
            if (dir.sqrMagnitude < 0.0001f)
                dir = _combat != null ? (Vector2)_combat.transform.right : Vector2.right;
            return dir.normalized;
        }

        // ── 레이저 시각화 (CL-142 ChainBolt 패턴) ─────────

        private static Material GetLaserMaterial()
        {
            if (_laserMaterial != null) return _laserMaterial;
            Shader sh = Shader.Find("Sprites/Default")
                     ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color");
            if (sh == null)
            {
                Debug.LogError("[Fusion] Laser: shader 0개 발견. LineRenderer 보이지 않음.");
                return null;
            }
            _laserMaterial = new Material(sh) { color = LaserColor };
            return _laserMaterial;
        }

        private GameObject CreateLaserLine()
        {
            var go = new GameObject("FusionLaser");
            // Fusion 자식으로 parent 설정 → fusion destroy 시 자동 정리 (orphaned magenta line 방지).
            // useWorldSpace=true 라 LineRenderer position 은 절대좌표 사용, parent transform 무관.
            go.transform.SetParent(transform, worldPositionStays: false);
            var lr = go.AddComponent<LineRenderer>();
            Material mat = GetLaserMaterial();
            if (mat != null) lr.sharedMaterial = mat;
            lr.startColor = LaserColor;
            lr.endColor = LaserColor;
            lr.startWidth = LaserBoltWidth;
            lr.endWidth = LaserBoltWidth;
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.sortingOrder = LaserBoltSortingOrder;
            lr.alignment = LineAlignment.View;
            return go;
        }

        private static void UpdateLaserLine(GameObject go, Vector3 from, Vector3 to)
        {
            if (go == null) return;
            from.z = 0f;
            to.z = 0f;
            var lr = go.GetComponent<LineRenderer>();
            if (lr == null) return;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
        }
    }
}
