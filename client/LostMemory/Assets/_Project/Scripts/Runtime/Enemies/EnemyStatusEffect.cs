using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    /// <summary>
    /// CL-142: 적의 Slow / Freeze status 관리. CL-143 에서 Burn 도트 추가 예정.
    ///
    /// OnHitEffectRegistry 가 첫 hit 시점에 자동 부착 (GetOrAdd) → 적 prefab 수정 불필요.
    ///
    /// TDE CharacterMovement.MovementSpeedMultiplier 를 조작해 이속 변화/정지 적용.
    /// 빙결 시 multiplier=0 으로 강제 정지.
    ///
    /// 빙결 시각화: 적 위치에 파란 투명 SpriteRenderer 절차적 생성 (placeholder).
    /// 정식 VFX 는 후속 ticket 에서 교체.
    ///
    /// 중첩 정책 (사용자 결정):
    /// - Slow: 더 강한 magnitude 만 유지, 시간은 max 유지
    /// - Freeze: 새 시간으로 갱신
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyStatusEffect : MonoBehaviour
    {
        // 빙결 visual 설정 (placeholder)
        private static readonly Color FreezeVisualColor = new Color(0.4f, 0.7f, 1.0f, 0.55f);
        private const int FreezeVisualSortingOrder = 9999;

        private CharacterMovement _movement;
        private float _baseSpeedMultiplier = 1f;
        private bool _baseCaptured;

        private float _slowMagnitude;
        private float _slowExpiresAt;
        private float _freezeExpiresAt;

        private GameObject _freezeVisual;
        private bool _freezeVisualActive;

        private void Awake()
        {
            _movement = GetComponentInParent<CharacterMovement>();
            if (_movement != null)
            {
                _baseSpeedMultiplier = _movement.MovementSpeedMultiplier;
                _baseCaptured = true;
            }
        }

        private void OnDestroy()
        {
            if (_freezeVisual != null)
            {
                Destroy(_freezeVisual);
            }
        }

        // ── 등록 API (OnHitEffectRegistry 호출) ─────────────

        public void ApplySlow(float magnitude, float duration)
        {
            // 더 강한 슬로우 유지 (또는 기존 만료 시 갱신)
            if (magnitude > _slowMagnitude || Time.time >= _slowExpiresAt)
            {
                _slowMagnitude = magnitude;
            }
            _slowExpiresAt = Mathf.Max(_slowExpiresAt, Time.time + duration);
            ApplyMovementMultiplier();
        }

        public void ApplyFreeze(float durationSeconds)
        {
            // 갱신 정책: 항상 새 시간으로 (사용자 결정)
            _freezeExpiresAt = Time.time + durationSeconds;
            ApplyMovementMultiplier();
        }

        // ── 만료 처리 ───────────────────────────────────────

        private void Update()
        {
            bool changed = false;
            if (_slowMagnitude > 0f && Time.time >= _slowExpiresAt)
            {
                _slowMagnitude = 0f;
                changed = true;
            }
            if (_freezeExpiresAt > 0f && Time.time >= _freezeExpiresAt)
            {
                _freezeExpiresAt = 0f;
                changed = true;
            }
            if (changed) ApplyMovementMultiplier();
        }

        // ── Movement multiplier + Visual 조정 ───────────────

        private void ApplyMovementMultiplier()
        {
            if (_movement == null)
            {
                // Awake 시점에 부모에 CharacterMovement 가 없었을 수 있음 (자동 부착 케이스)
                _movement = GetComponentInParent<CharacterMovement>();
                if (_movement == null) return;
                if (!_baseCaptured)
                {
                    _baseSpeedMultiplier = _movement.MovementSpeedMultiplier;
                    _baseCaptured = true;
                }
            }

            bool isFrozen = Time.time < _freezeExpiresAt;
            float slowFactor = (_slowMagnitude > 0f && Time.time < _slowExpiresAt)
                ? Mathf.Clamp(1f - _slowMagnitude, 0f, 1f)
                : 1f;
            float effectiveMul = isFrozen ? 0f : (_baseSpeedMultiplier * slowFactor);
            _movement.MovementSpeedMultiplier = effectiveMul;

            SetFreezeVisualActive(isFrozen);
        }

        // ── 빙결 시각화 (placeholder) ───────────────────────

        private void SetFreezeVisualActive(bool active)
        {
            if (active == _freezeVisualActive && _freezeVisual != null) return;

            if (active)
            {
                EnsureFreezeVisual();
                if (_freezeVisual != null) _freezeVisual.SetActive(true);
            }
            else if (_freezeVisual != null)
            {
                _freezeVisual.SetActive(false);
            }
            _freezeVisualActive = active;
        }

        private void EnsureFreezeVisual()
        {
            if (_freezeVisual != null) return;

            _freezeVisual = new GameObject("FreezeVisual");
            _freezeVisual.transform.SetParent(transform, worldPositionStays: false);
            _freezeVisual.transform.localPosition = Vector3.zero;

            SpriteRenderer sr = _freezeVisual.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: Texture2D.whiteTexture.width);
            sr.color = FreezeVisualColor;
            sr.sortingOrder = FreezeVisualSortingOrder;

            // 적 sprite 크기 추정 → 같은 크기로 scale (없으면 기본 1×1.5)
            SpriteRenderer enemySr = GetComponentInParent<SpriteRenderer>();
            Vector3 scale = new Vector3(1f, 1.5f, 1f);
            if (enemySr != null && enemySr.bounds.size.sqrMagnitude > 0f)
            {
                Vector2 size = enemySr.bounds.size;
                scale = new Vector3(size.x, size.y, 1f);
            }
            _freezeVisual.transform.localScale = scale;

            Debug.Log($"[EnemyStatusEffect] FreezeVisual 생성 — host={gameObject.name}, scale={scale}, color={FreezeVisualColor}, sortingOrder={FreezeVisualSortingOrder}, sprite={(sr.sprite != null ? "OK" : "NULL")}, material={(sr.sharedMaterial != null ? sr.sharedMaterial.shader.name : "NULL")}");
        }
    }
}
