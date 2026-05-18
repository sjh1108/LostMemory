using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// CL-204: 5종 미소녀 visual 별 sprite + 공격 패턴 + 파라미터 데이터 driven 카탈로그.
    ///
    /// 사용 방식: MagicalGirlAI / MagicalGirlSpawner 가 [SerializeField] catalog 로 참조 → visual 키로 entry 조회.
    /// 데이터 변경(밸런스 조정)은 SO Inspector 에서, 코드 변경 0.
    /// (나) 마이그레이션 시 entry 키만 RelicEffectType 으로 swap 가능.
    /// </summary>
    [CreateAssetMenu(menuName = "Lost Memory/Magical Girl/Attack Catalog", fileName = "MagicalGirlAttackCatalog")]
    public sealed class MagicalGirlAttackCatalog : ScriptableObject
    {
        public enum AttackKind
        {
            Projectile,         // 1·3·5 — 직선 발사체, OnTriggerEnter 데미지 1회
            AOEAtTarget,        // 2 — spawn 시점 가장 가까운 적 위치에 1회 snap 후 정지, 지속 tick 데미지 (적이 빠져나가면 회피 가능)
            AOEStationary,      // 4 — 미소녀 전방 정지 spawn, 지속 tick 데미지 + 끌어당김
        }

        [Serializable]
        public struct Entry
        {
            [Tooltip("이 entry 가 적용될 visual 키.")]
            public MagicalGirlVisual visual;

            [Tooltip("미소녀 본체 sprite (girl.png 분할).")]
            public Sprite sprite;

            [Tooltip("발사체 또는 AOE 루트 prefab. 본체에 MagicalGirlProjectile 또는 MagicalGirlAOE 부착 필요.")]
            public GameObject vfxPrefab;

            [Tooltip("공격 유형.")]
            public AttackKind kind;

            [Tooltip("playerAtk * damageRatio 형태. CL-144 기본 0.30.")]
            [Min(0f)] public float damageRatio;

            [Tooltip("공격 간격 (초). CL-144 기본 1.5.")]
            [Min(0.1f)] public float attackInterval;

            [Tooltip("Projectile 속도 (유닛/초). AttackKind=Projectile 일 때만 사용.")]
            [Min(0f)] public float projectileSpeed;

            [Tooltip("Projectile 수명 (초). AttackKind=Projectile 일 때만 사용.")]
            [Min(0f)] public float projectileLifetime;

            [Tooltip("AOE 반경 (유닛). AttackKind=AOE* 일 때만 사용.")]
            [Min(0f)] public float aoeRadius;

            [Tooltip("AOE 지속 시간 (초). AttackKind=AOE* 일 때만 사용.")]
            [Min(0f)] public float aoeDuration;

            [Tooltip("AOE 데미지 tick 주기 (초). AttackKind=AOE* 일 때만 사용.")]
            [Min(0.05f)] public float aoeTickInterval;

            [Tooltip("Blackhole 끌어당김 속도 (유닛/초). AttackKind=AOEStationary 전용. 0 이면 끌어당김 없음.")]
            [Min(0f)] public float pullSpeed;

            [Tooltip("적에게 적용할 Slow status magnitude (0~1). 0 = 적용 안 함. CL-202 EnemyStatusEffect 가 처리 (sprite 푸르게 + 이속 감소).")]
            [Range(0f, 1f)] public float slowMagnitude;

            [Tooltip("Slow status 지속 시간 (초). slowMagnitude > 0 일 때만 의미 있음. tickInterval 보다 약간 길게 두면 매 tick 갱신됨.")]
            [Min(0f)] public float slowDuration;

            [Tooltip("Projectile 적중/만료 시 spawn 할 1회 폭발 VFX. null 이면 spawn 안 함. 임팩트 강조용 + 향후 burn DoT 통합 hook. AttackKind=Projectile 에서만 사용.")]
            public GameObject hitVfxPrefab;
        }

        [SerializeField] private List<Entry> _entries = new();

        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>visual 매칭 entry 조회. 없으면 has=false, default(Entry) 반환.</summary>
        public bool TryGet(MagicalGirlVisual visual, out Entry entry)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].visual == visual)
                {
                    entry = _entries[i];
                    return true;
                }
            }
            entry = default;
            return false;
        }
    }
}
