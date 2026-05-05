using System.Collections;
using LostMemory.Combat;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// CL-144: 개별 미소녀 자동 공격 컴포넌트.
    ///
    /// MagicalGirlSpawner 가 GameObject 생성 후 AddComponent 로 부착 → Init 호출.
    /// Awake 에서 SpriteRenderer 절차적 생성 (CL-142 FreezeVisual / CL-143 BurnVisual 패턴 미러).
    /// Update 에서 attackInterval 마다 가장 가까운 적(Character.AI) 검색 → Damage.
    ///
    /// 무적: Health 컴포넌트 없음. 적 공격에 영향 X.
    /// 충돌: Collider 없음. 적과 물리 충돌 X.
    ///
    /// Character.AI 필터: CL-143 발견 이슈 (ArcherArrow 등 발사체에 OnHit 적용) 자체 해결.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagicalGirlAI : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float attackInterval = 1.5f;
        [SerializeField, Min(0.1f)] private float attackRange = 5f;
        [SerializeField, Min(0f)]   private float damageRatio = 0.30f;
        [SerializeField, Min(0.1f)] private float spriteSize = 0.4f;
        [SerializeField]            private int   spriteSortingOrder = 100;

        private SpriteRenderer _sr;
        private float _nextAttackAt;
        private PlayerStatModifierContainer _playerStat;
        private KhiMeleeComboController _playerCombat;

        // 검색 임시 버퍼 (heap alloc 방지)
        private static readonly Collider2D[] _searchBuf = new Collider2D[16];

        private void Awake()
        {
            // 절차적 sprite 생성 — 흰색 텍스처를 색상으로 틴트
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: Texture2D.whiteTexture.width);
            _sr.color = MagicalGirlVisualPalette.Get(MagicalGirlVisual.Default);
            _sr.sortingOrder = spriteSortingOrder;
            transform.localScale = new Vector3(spriteSize, spriteSize, 1f);
        }

        public void Init(PlayerStatModifierContainer stat, KhiMeleeComboController combat)
        {
            _playerStat = stat;
            _playerCombat = combat;
        }

        public void SetVisual(MagicalGirlVisual visual)
        {
            if (_sr == null) return;
            _sr.color = MagicalGirlVisualPalette.Get(visual);
        }

        private void Update()
        {
            if (Time.time < _nextAttackAt) return;
            Health target = FindClosestEnemy();
            if (target == null) return;
            Attack(target);
            _nextAttackAt = Time.time + attackInterval;
        }

        private Health FindClosestEnemy()
        {
            // CL-146: Range multiplier 적용 — 미소녀 사거리 확장
            float rangeMul = _playerStat != null ? _playerStat.GetTotalMultiplier(StatId.Range) : 1f;
            float effectiveRange = attackRange * rangeMul;
            int hits = Physics2D.OverlapCircleNonAlloc(transform.position, effectiveRange, _searchBuf);
            Health closest = null;
            float minDistSq = float.MaxValue;
            for (int i = 0; i < hits; i++)
            {
                Collider2D col = _searchBuf[i];
                if (col == null) continue;
                Health h = col.GetComponentInParent<Health>();
                if (h == null || h.CurrentHealth <= 0f) continue;
                // CL-143 follow-up: Character.AI 만 — 발사체(ArcherArrow 등) / Player 제외
                Character ch = h.GetComponentInParent<Character>();
                if (ch == null || ch.CharacterType != Character.CharacterTypes.AI) continue;

                float dSq = ((Vector2)(h.transform.position - transform.position)).sqrMagnitude;
                if (dSq < minDistSq)
                {
                    minDistSq = dSq;
                    closest = h;
                }
            }
            return closest;
        }

        private void Attack(Health target)
        {
            float playerAtk = _playerCombat != null && _playerCombat.WeaponData != null
                ? _playerCombat.WeaponData.BaseDamage
                : 0f;
            if (_playerStat != null)
                playerAtk *= _playerStat.GetTotalMultiplier(StatId.AttackPower);
            float damage = playerAtk * damageRatio;
            if (damage <= 0f) return;

            target.Damage(damage, gameObject, 0f, 0f, Vector3.zero);
            StartCoroutine(FlashCoroutine());
        }

        private IEnumerator FlashCoroutine()
        {
            if (_sr == null) yield break;
            Color original = _sr.color;
            _sr.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            if (_sr != null) _sr.color = original;
        }
    }
}
