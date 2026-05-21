using System.Collections.Generic;
using LostMemory.Combat;
using LostMemory.Data;
using LostMemory.Networking.Player;
using MoreMountains.TopDownEngine;
using UnityEngine;
// LostMemory.Networking.Player 는 이미 import — PlayerHealthSync 동일 namespace.

namespace LostMemory.TestKhi
{
    public class KhiMeleeHitbox : MonoBehaviour
    {
        private const string ProjectileLayerName = "Projectile";

        [SerializeField] private LayerMask targetLayers = ~0;
        [SerializeField] private bool ignoreProjectileTargets = true;
        [SerializeField] private int maximumHitsPerSample = 32;
        [SerializeField] private float targetInvincibilityDuration = 0f;
        [SerializeField] private float targetFlickerDuration = 0f;
        [Tooltip("CL-146: 같은 GameObject (또는 부모) 의 PlayerStatModifierContainer. 비워두면 GetComponentInParent. Range multiplier 조회용.")]
        [SerializeField] private PlayerStatModifierContainer statContainer;
        [Tooltip("멀티 환경 server-authoritative damage relay. 비워두면 GetComponentInParent. 솔로면 null 허용.")]
        [SerializeField] private PlayerDamageRelay damageRelay;
        [Tooltip("Editor Scene 뷰에서 hitbox 윤곽 Gizmo 표시 (선택된 GameObject 만). 인게임 화면엔 영향 없음.")]
        [SerializeField] private bool drawDebugGizmos = false;
        [SerializeField] private Color debugGizmoColor = new Color(1f, 0.2f, 0.1f, 0.25f);
        [Tooltip("인게임 화면에 hitbox 박스 SpriteRenderer 로 표시 (디버깅 용). default OFF — 시각 검증 시만 ON.")]
        [SerializeField] private bool showRuntimePreview = false;
        [SerializeField] private Color runtimePreviewColor = new Color(1f, 0f, 0f, 0.28f);
        [SerializeField] private int runtimePreviewSortingOrder = 1000;

        private Collider2D[] _overlapResults;
        private bool _hasDebugHitbox;
        private Vector2 _debugCenter;
        private Vector2 _debugSize;
        private float _debugAngleDeg;
        private SpriteRenderer _runtimePreviewRenderer;
        private Sprite _runtimePreviewSprite;
        private int _projectileLayer = -1;

        private void Awake()
        {
            _projectileLayer = LayerMask.NameToLayer(ProjectileLayerName);
            EnsureOverlapBuffer();
            EnsureRuntimePreview();
            if (statContainer == null)
                statContainer = GetComponentInParent<PlayerStatModifierContainer>();
            if (damageRelay == null)
                damageRelay = GetComponentInParent<PlayerDamageRelay>();
        }

        public int Sample(KhiAttackRequest request, AttackStepData step, Vector2 globalPostRotationOffset, float damage, HashSet<Health> alreadyHit, List<Health> hitsThisSample)
        {
            EnsureOverlapBuffer();
            hitsThisSample?.Clear();

            float aimAngleDeg = request.AimAngleDegrees;
            // baseline offset(Right 기준)을 현재 aim 각도로 회전시켜 실제 center 계산.
            Vector2 rotatedOffset = (Vector2)(Quaternion.Euler(0f, 0f, aimAngleDeg) * (Vector3)step.hitboxOffset);
            // CL: globalPostRotationOffset 은 회전 후 더함 → 좌/우 어느 방향이든 항상 같은 양만큼 시프트.
            Vector2 center = (Vector2)request.Origin + rotatedOffset + globalPostRotationOffset;
            // CL-146: Range multiplier 적용 — 평타 hitbox 크기 확장. 상한 200%.
            float rangeMul = statContainer != null ? statContainer.GetCappedMultiplier(StatId.Range, 1f) : 1f;
            Vector2 finalSize = step.hitboxSize * rangeMul;
            _debugCenter = center;
            _debugSize = finalSize;
            _debugAngleDeg = aimAngleDeg;
            _hasDebugHitbox = true;
            ShowRuntimePreview(center, finalSize, aimAngleDeg);

            int hitCount = Physics2D.OverlapBoxNonAlloc(center, finalSize, aimAngleDeg, _overlapResults, targetLayers);
            int appliedHits = 0;

            // Phase D 진단: guest 공격 sync 추적 — hit count 0 이면 guest hitbox 가 host enemy 와 collision 안 잡힘 (씬 차이 의심).
            // damageRelay 의 verboseLog 와 짝지어 분석.
            if (hitCount > 0)
            {
                Debug.Log($"[KhiMeleeHitbox] Sample combo={request.ComboStep} seq={request.SequenceId} → {hitCount} colliders found (center={center} size={finalSize})", this);
            }

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = _overlapResults[i];
                if (hitCollider == null)
                {
                    continue;
                }

                if (ignoreProjectileTargets && IsProjectileTarget(hitCollider))
                {
                    continue;
                }

                Health health = hitCollider.GetComponentInParent<Health>();
                if (health == null || alreadyHit.Contains(health) || IsOwnedByAttacker(health, request.Attacker))
                {
                    continue;
                }
                // Phase E: PvP 미상정 — 다른 player 도 friendly. PlayerHealthSync 컴포넌트 기준 일괄 skip.
                if (health.GetComponentInParent<PlayerHealthSync>() != null)
                {
                    continue;
                }

                // 게스트가 ServerRpc 로 호스트에 위임할 경로면 자체 CanTakeDamageThisFrame 가드 우회.
                // 비-server 측 target Health 는 MonsterHealthSync/PlayerHealthSync 가 DamageDisabled() 호출했기 때문에
                // CanTakeDamageThisFrame=false 가 되어 RelayDamage 도달 전에 차단되어 버린다.
                // 실제 데미지 판정은 호스트 측 Health 가 자체 가드로 처리.
                bool willRelayToServer = damageRelay != null && damageRelay.IsSpawned && !damageRelay.IsServer;
                if (!willRelayToServer && !health.CanTakeDamageThisFrame())
                {
                    continue;
                }

                alreadyHit.Add(health);
                if (damageRelay != null)
                {
                    damageRelay.RelayDamage(health, damage, request.Attacker, targetFlickerDuration, targetInvincibilityDuration, request.AimDirection);
                }
                else
                {
                    health.Damage(damage, request.Attacker, targetFlickerDuration, targetInvincibilityDuration, request.AimDirection);
                }
                hitsThisSample?.Add(health);
                appliedHits++;
            }

            return appliedHits;
        }

        public void HideRuntimePreview()
        {
            if (_runtimePreviewRenderer != null)
            {
                _runtimePreviewRenderer.enabled = false;
            }
        }

        private void EnsureOverlapBuffer()
        {
            if (_overlapResults != null && _overlapResults.Length == maximumHitsPerSample)
            {
                return;
            }

            _overlapResults = new Collider2D[Mathf.Max(1, maximumHitsPerSample)];
        }

        private void EnsureRuntimePreview()
        {
            if (!showRuntimePreview || _runtimePreviewRenderer != null)
            {
                return;
            }

            GameObject previewObject = new GameObject("Khi_MeleeHitboxPreview");
            previewObject.transform.SetParent(transform, false);
            _runtimePreviewRenderer = previewObject.AddComponent<SpriteRenderer>();
            _runtimePreviewRenderer.sprite = GetRuntimePreviewSprite();
            _runtimePreviewRenderer.color = runtimePreviewColor;
            CopySortingLayerFromOwner(_runtimePreviewRenderer);
            _runtimePreviewRenderer.sortingOrder = runtimePreviewSortingOrder;
            _runtimePreviewRenderer.enabled = false;
        }

        private Sprite GetRuntimePreviewSprite()
        {
            if (_runtimePreviewSprite != null)
            {
                return _runtimePreviewSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Khi_MeleeHitboxPreviewTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            _runtimePreviewSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _runtimePreviewSprite.name = "Khi_MeleeHitboxPreviewSprite";
            return _runtimePreviewSprite;
        }

        private void ShowRuntimePreview(Vector2 center, Vector2 size, float angleDeg)
        {
            if (!showRuntimePreview)
            {
                return;
            }

            EnsureRuntimePreview();
            if (_runtimePreviewRenderer == null)
            {
                return;
            }

            _runtimePreviewRenderer.transform.SetPositionAndRotation(
                new Vector3(center.x, center.y, transform.position.z),
                Quaternion.Euler(0f, 0f, angleDeg));
            _runtimePreviewRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);
            _runtimePreviewRenderer.color = runtimePreviewColor;
            CopySortingLayerFromOwner(_runtimePreviewRenderer);
            _runtimePreviewRenderer.sortingOrder = runtimePreviewSortingOrder;
            _runtimePreviewRenderer.enabled = true;
        }

        private void CopySortingLayerFromOwner(SpriteRenderer targetRenderer)
        {
            SpriteRenderer ownerRenderer = GetComponentInChildren<SpriteRenderer>();
            if (ownerRenderer == null || ownerRenderer == targetRenderer)
            {
                return;
            }

            targetRenderer.sortingLayerID = ownerRenderer.sortingLayerID;
        }

        private static bool IsOwnedByAttacker(Health health, GameObject attacker)
        {
            if (attacker == null)
            {
                return false;
            }

            return health.gameObject == attacker || health.transform.IsChildOf(attacker.transform);
        }

        private bool IsProjectileTarget(Collider2D hitCollider)
        {
            if (_projectileLayer >= 0 && hitCollider.gameObject.layer == _projectileLayer)
            {
                return true;
            }

            return hitCollider.GetComponentInParent<Projectile>() != null;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos || !_hasDebugHitbox)
            {
                return;
            }

            Matrix4x4 prevMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(_debugCenter, Quaternion.Euler(0f, 0f, _debugAngleDeg), Vector3.one);
            Gizmos.color = debugGizmoColor;
            Gizmos.DrawCube(Vector3.zero, _debugSize);
            Gizmos.color = new Color(debugGizmoColor.r, debugGizmoColor.g, debugGizmoColor.b, 0.9f);
            Gizmos.DrawWireCube(Vector3.zero, _debugSize);
            Gizmos.matrix = prevMatrix;
        }
    }
}
