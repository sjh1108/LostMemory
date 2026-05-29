using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LostMemory.Networking.Common;
using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// CL-139: 빌드 매니저 — 보유 RelicData 의 듀얼 태그를 카운트하고 16세트 각각의
    /// 활성 티어를 계산한다. 효과 적용은 본 매니저의 책임이 아니며, CL-140
    /// (EffectApplicator) 가 <see cref="OnSetTierChanged"/> 이벤트를 구독해 처리.
    ///
    /// 동작:
    /// - <see cref="PlayerRelicInventory"/> 의 OnRelicAcquired/Removed/Cleared 구독
    /// - 변화 발생 시 dirty flag set → LateUpdate 1회 재계산 (한 프레임 N번 변화 흡수)
    /// - 각 BuildSetData 의 RequiredCount 임계치 기준 *최고* 티어만 활성
    /// - <see cref="HostAuthority"/> 게이트 (싱글/호스트만 카운트)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Relics/Build Manager")]
    public sealed class BuildManager : MonoBehaviour
    {
        [Tooltip("같은 GameObject 의 PlayerRelicInventory.")]
        [SerializeField] private PlayerRelicInventory inventory;

        [Tooltip("16개 BuildSetData SO 모두 할당. 누락 시 Awake 경고.")]
        [SerializeField] private BuildSetData[] _setDatabase;

        [Tooltip("티어 변경 로그 활성화 — 디버그용.")]
        [SerializeField] private bool _logTierChanges = false;

        // Phase B-1 패턴 재활용 (RelicEffectRegistry 와 동일).
        // 싱글 실행 시 NetworkManager 비활성 → true, 멀티 실행 시 호스트만 true.
        // F-2: HostAuthority 로 일원화. 싱글 → true, 멀티 호스트 → true, 멀티 게스트 → false.

        private readonly Dictionary<RelicTag, int> _tagCounts = new();
        private readonly Dictionary<RelicTag, int> _activeTiers = new();      // -1 = 미발동
        private readonly Dictionary<RelicTag, BuildSetData> _setByTag = new();
        private bool _isDirty;

        /// <summary>
        /// 세트 활성 티어가 변경됐을 때 발화. EffectApplicator(CL-140) 의 핵심 인터페이스.
        /// 인자: (변경된 세트 태그, 이전 티어, 새 티어). 티어는 0+ 인덱스이며 -1 은 미발동.
        /// </summary>
        public event Action<RelicTag, int, int> OnSetTierChanged;

        /// <summary>각 세트 태그별 현재 듀얼 태그 카운트.</summary>
        public IReadOnlyDictionary<RelicTag, int> AllCounts      => _tagCounts;

        /// <summary>각 세트 태그별 현재 활성 티어 인덱스. -1 = 미발동.</summary>
        public IReadOnlyDictionary<RelicTag, int> AllActiveTiers => _activeTiers;

        /// <summary>특정 태그의 현재 카운트. 미등록 태그면 0.</summary>
        public int GetTagCount(RelicTag tag) =>
            _tagCounts.TryGetValue(tag, out int c) ? c : 0;

        /// <summary>특정 세트의 현재 활성 티어 인덱스. -1 = 미발동.</summary>
        public int GetActiveTier(RelicTag tag) =>
            _activeTiers.TryGetValue(tag, out int t) ? t : -1;

        /// <summary>현재 활성 티어의 SetTier 데이터. 미발동 또는 미등록 시 null.</summary>
        public SetTier? GetActiveTierData(RelicTag tag)
        {
            int idx = GetActiveTier(tag);
            if (idx < 0) return null;
            if (!_setByTag.TryGetValue(tag, out BuildSetData set) || set == null) return null;
            if (idx >= set.Tiers.Count) return null;
            return set.Tiers[idx];
        }

        /// <summary>
        /// CL-140: 특정 태그의 BuildSetData 자체. 미등록 시 null.
        /// SetEffectApplicator 가 oldTier 효과 제거 시 set.Tiers[oldTier] 의 EffectType 등 메타 데이터 조회용.
        /// </summary>
        public BuildSetData GetSetForTag(RelicTag tag) =>
            _setByTag.TryGetValue(tag, out BuildSetData set) ? set : null;

        /// <summary>
        /// Inspector 에서 등록된 BuildSetData 배열 (등록 순서 유지). null/중복은 호출 측에서 거르기.
        /// SetEffectPanelView 가 안정 정렬의 3차 키로 사용.
        /// </summary>
        public IReadOnlyList<BuildSetData> RegisteredSetsInOrder => _setDatabase;

        private void Awake()
        {
            if (_setDatabase == null || _setDatabase.Length == 0)
            {
                Debug.LogWarning("[BuildManager] _setDatabase 비어 있음. 16개 BuildSetData 할당 필요.");
                return;
            }

            int registered = 0;
            foreach (BuildSetData set in _setDatabase)
            {
                if (set == null) continue;
                if (_setByTag.ContainsKey(set.SetTag))
                {
                    Debug.LogWarning($"[BuildManager] 중복 SetTag: {set.SetTag} (asset={set.name}). 첫 번째만 사용.");
                    continue;
                }
                _setByTag[set.SetTag]   = set;
                _activeTiers[set.SetTag] = -1;
                _tagCounts[set.SetTag]   = 0;
                registered++;
            }

            if (registered < 16)
                Debug.LogWarning($"[BuildManager] BuildSetData {registered}/16 등록됨. 나머지는 카운트 무시.");
        }

        private void OnEnable()
        {
            if (inventory == null)
            {
                Debug.LogError($"[BuildManager] inventory 필드가 null. Inspector에서 PlayerRelicInventory 드래그 필요. (host={gameObject.name})", this);
                return;
            }
            if (_logTierChanges) Debug.Log($"[BuildManager] OnEnable — inventory 구독 시작. host={gameObject.name}, inventoryHost={inventory.gameObject.name}, ownedCount={inventory.OwnedRelics.Count}", this);
            inventory.OnRelicAcquired += HandleInventoryChanged;
            inventory.OnRelicRemoved  += HandleInventoryChanged;
            inventory.OnCleared       += HandleInventoryCleared;
            // 컴포넌트 활성화 시점에 이미 인벤토리에 유물이 있을 수 있음 → 1회 강제 재계산
            _isDirty = true;
        }

        private void OnDisable()
        {
            if (inventory == null) return;
            inventory.OnRelicAcquired -= HandleInventoryChanged;
            inventory.OnRelicRemoved  -= HandleInventoryChanged;
            inventory.OnCleared       -= HandleInventoryCleared;
        }

        private void HandleInventoryChanged(RelicData _)
        {
            if (!HostAuthority.IsHost) return;
            _isDirty = true;
        }

        private void HandleInventoryCleared()
        {
            if (!HostAuthority.IsHost) return;
            _isDirty = true;
        }

        private void LateUpdate()
        {
            if (!_isDirty) return;
            _isDirty = false;
            RecalculateAllTiers();
        }

        private void RecalculateAllTiers()
        {
            if (_setByTag.Count == 0) return;

            // 1. 카운트 리셋 (등록된 태그만)
            foreach (RelicTag tag in _setByTag.Keys.ToList())
                _tagCounts[tag] = 0;

            // 2. 듀얼 태그 합산. TagPrimary == TagSecondary 면 자연스럽게 +2.
            if (inventory != null)
            {
                foreach (RelicData relic in inventory.OwnedRelics)
                {
                    if (relic == null) continue;
                    if (relic.TagPrimary != RelicTag.None && _tagCounts.ContainsKey(relic.TagPrimary))
                        _tagCounts[relic.TagPrimary]++;
                    if (relic.TagSecondary != RelicTag.None && _tagCounts.ContainsKey(relic.TagSecondary))
                        _tagCounts[relic.TagSecondary]++;
                }
            }

            // 3. 각 세트의 활성 티어 재계산 + 변화시 이벤트 발화
            foreach (KeyValuePair<RelicTag, BuildSetData> kv in _setByTag)
            {
                RelicTag tag = kv.Key;
                BuildSetData set = kv.Value;
                int newTier = ComputeActiveTier(set, _tagCounts[tag]);
                int oldTier = _activeTiers[tag];
                if (newTier == oldTier) continue;

                _activeTiers[tag] = newTier;
                if (_logTierChanges)
                    Debug.Log($"[BuildManager] {tag}: tier {oldTier} → {newTier} (count={_tagCounts[tag]})");
                OnSetTierChanged?.Invoke(tag, oldTier, newTier);
            }
        }

        /// <summary>도달한 가장 높은 티어 인덱스. 어느 임계치도 못 넘으면 -1.</summary>
        private static int ComputeActiveTier(BuildSetData set, int count)
        {
            if (set == null || set.Tiers == null) return -1;
            int idx = -1;
            for (int i = 0; i < set.Tiers.Count; i++)
            {
                if (count >= set.Tiers[i].RequiredCount)
                    idx = i;
            }
            return idx;
        }

        [ContextMenu("Debug — Print all counts/tiers")]
        private void DebugPrintAll()
        {
            var sb = new StringBuilder("[BuildManager] 현재 상태\n");
            foreach (KeyValuePair<RelicTag, BuildSetData> kv in _setByTag)
            {
                RelicTag tag = kv.Key;
                int count = _tagCounts[tag];
                int tier  = _activeTiers[tag];
                sb.AppendLine($"  {tag,-12} count={count,-3} tier={tier}");
            }
            Debug.Log(sb.ToString());
        }
    }
}
