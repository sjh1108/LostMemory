using UnityEngine;

namespace LostMemory.Networking.Analytics
{
    /// <summary>
    /// 보스/적 공격 GameObject 에 부착하면 player 사망 시 cause_pattern_id / cause_enemy_id 로 보고됨.
    ///
    /// 부착 위치: 공격 controller GameObject (Bertha 의 LightAttack1Controller 등) 또는 projectile GameObject.
    /// AnalyticsDamageTracker 가 Health.Damage 의 instigator 부모 chain 에서 본 컴포넌트 검색.
    ///
    /// 둘 다 비워두면 enemy id 는 instigator 의 root GameObject 이름으로 fallback (Clone 접미사 제거).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Networking/Analytics/Analytics Pattern Tag")]
    public sealed class AnalyticsPatternTag : MonoBehaviour
    {
        [Tooltip("예: 'Bertha.LightAttack1'. 비워두면 cause_pattern_id 는 null.")]
        [SerializeField] private string patternId;

        [Tooltip("예: 'Bertha'. 비워두면 instigator root GameObject 이름 사용.")]
        [SerializeField] private string enemyId;

        public string PatternId => string.IsNullOrEmpty(patternId) ? null : patternId;
        public string EnemyId => string.IsNullOrEmpty(enemyId) ? null : enemyId;
    }
}
