using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Networking.Analytics
{
    /// <summary>
    /// 모든 데미지 이벤트 (MMDamageTakenEvent) 를 listen 해서 *플레이어* 가 받은 마지막 일격의
    /// 원인 (enemy_id, pattern_id) 을 정적 캐시. AnalyticsClient 가 player_died 발화 시 읽어감.
    ///
    /// AnalyticsClient.EnsureExists() 가 함께 보장. MMEventManager 는 OnEnable/OnDisable 로 wiring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnalyticsDamageTracker : MonoBehaviour, MMEventListener<MMDamageTakenEvent>
    {
        /// <summary>마지막으로 *플레이어* 가 받은 데미지의 적 식별자. null 가능.</summary>
        public static string LastEnemyId { get; private set; }
        /// <summary>마지막으로 *플레이어* 가 받은 데미지의 공격 패턴 식별자. null 가능.</summary>
        public static string LastPatternId { get; private set; }

        private void OnEnable()
        {
            this.MMEventStartListening<MMDamageTakenEvent>();
        }

        private void OnDisable()
        {
            this.MMEventStopListening<MMDamageTakenEvent>();
        }

        public void OnMMEvent(MMDamageTakenEvent eventType)
        {
            // 플레이어만 — KhiDownController 가 있는 GameObject 가 피해자인지로 판정.
            if (eventType.AffectedHealth == null) return;
            var down = eventType.AffectedHealth.GetComponentInParent<TestKhi.KhiDownController>();
            if (down == null) return;

            GameObject instigator = eventType.Instigator;
            if (instigator == null)
            {
                LastEnemyId = null;
                LastPatternId = null;
                return;
            }

            // pattern_id: instigator 부모 chain 에서 AnalyticsPatternTag 검색. 없으면 null.
            AnalyticsPatternTag tag = instigator.GetComponentInParent<AnalyticsPatternTag>();
            LastPatternId = tag != null ? tag.PatternId : null;

            // enemy_id: tag 의 enemyId 우선, 없으면 instigator root GameObject 이름.
            if (tag != null && !string.IsNullOrEmpty(tag.EnemyId))
            {
                LastEnemyId = tag.EnemyId;
            }
            else
            {
                string rootName = instigator.transform.root != null ? instigator.transform.root.name : instigator.name;
                LastEnemyId = StripClone(rootName);
            }
        }

        private static string StripClone(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            const string suffix = "(Clone)";
            return name.EndsWith(suffix) ? name.Substring(0, name.Length - suffix.Length).Trim() : name;
        }
    }
}
