using System.Collections.Generic;
using LostMemory.Relics;
using UnityEngine;

namespace LostMemory.Shop
{
    /// <summary>
    /// 인벤토리 패널 옆에 띄우는 세트효과 표시 패널.
    /// BuildManager 의 활성 티어/카운트를 N세트 행으로 시각화한다.
    /// row 는 _rowPrefab 을 _rowParent 아래로 동적 Instantiate (BuildManager 등록 세트 수만큼).
    ///
    /// 정렬:
    ///   1차) 활성 티어 desc (-1 = 가장 아래)
    ///   2차) 카운트 desc
    ///   3차) BuildManager.RegisteredSetsInOrder 등록 순 asc (안정성)
    ///
    /// 갱신 트리거:
    ///   - Init() 1회 (인벤토리 Open 시점)
    ///   - BuildManager.OnSetTierChanged 구독 — 티어 변경 시 즉시 재정렬·재표시
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Shop/Set Effect Panel View")]
    public class SetEffectPanelView : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("동적으로 Instantiate 할 1행 prefab. SetEffectRowView 를 루트에 가진 prefab.")]
        [SerializeField] private SetEffectRowView _rowPrefab;
        [Tooltip("Instantiate 한 row 의 부모. 보통 ScrollRect 의 Content (VerticalLayoutGroup 부착).")]
        [SerializeField] private Transform _rowParent;

        private BuildManager _buildManager;
        private readonly List<SetEffectRowView> _rows = new List<SetEffectRowView>(16);

        private readonly List<Entry> _entries = new List<Entry>(16);

        private struct Entry
        {
            public BuildSetData Set;
            public int Count;
            public int Tier;
            public int RegIdx;
        }

        /// <summary>
        /// BuildManager 와 연결하고 즉시 1회 갱신한다. Inventory 패널 열릴 때 호출.
        /// 첫 호출에 row 풀을 생성하고, 두 번째 호출부터는 기존 구독을 정리하고 새 BuildManager 로 갈아끼운다.
        /// </summary>
        public void Init(BuildManager buildManager)
        {
            if (_buildManager != null && _buildManager != buildManager)
            {
                _buildManager.OnSetTierChanged -= HandleTierChanged;
            }

            _buildManager = buildManager;

            if (_buildManager == null)
            {
                Debug.LogError("[SetEffectPanelView] buildManager 가 null. wiring 확인.", this);
                return;
            }

            _buildManager.OnSetTierChanged -= HandleTierChanged;
            _buildManager.OnSetTierChanged += HandleTierChanged;

            EnsureRows(_buildManager.RegisteredSetsInOrder?.Count ?? 0);
            Refresh();
        }

        private void OnDisable()
        {
            if (_buildManager != null)
                _buildManager.OnSetTierChanged -= HandleTierChanged;
        }

        private void HandleTierChanged(RelicTag _, int __, int ___)
        {
            // 티어가 바뀐 세트만 재바인딩하면 더 효율적이지만, 정렬 순서가 함께 바뀔 수 있어 전체 Refresh.
            Refresh();
        }

        /// <summary>현재 BuildManager 상태로 row 들을 다시 정렬·바인딩한다.</summary>
        public void Refresh()
        {
            if (_buildManager == null) return;

            BuildEntries();
            SortEntries();
            ApplyToRows();
        }

        /// <summary>
        /// 필요한 만큼 row 를 lazy instantiate. 한 번 만들면 풀로 재사용.
        /// targetCount > 현재 풀 크기 → 부족분만 Instantiate.
        /// targetCount < 현재 풀 크기 → 남는 row 는 Refresh 단계에서 SetActive(false).
        /// </summary>
        private void EnsureRows(int targetCount)
        {
            if (_rowPrefab == null || _rowParent == null)
            {
                Debug.LogError("[SetEffectPanelView] _rowPrefab 또는 _rowParent 가 null. wiring 확인.", this);
                return;
            }

            for (int i = _rows.Count; i < targetCount; i++)
            {
                SetEffectRowView row = Instantiate(_rowPrefab, _rowParent);
                row.gameObject.name = $"SetEffectRow ({i})";
                _rows.Add(row);
            }
        }

        private void BuildEntries()
        {
            _entries.Clear();
            var registered = _buildManager.RegisteredSetsInOrder;
            if (registered == null) return;

            for (int i = 0; i < registered.Count; i++)
            {
                BuildSetData set = registered[i];
                if (set == null) continue;
                RelicTag tag = set.SetTag;
                _entries.Add(new Entry
                {
                    Set    = set,
                    Count  = _buildManager.GetTagCount(tag),
                    Tier   = _buildManager.GetActiveTier(tag),
                    RegIdx = i,
                });
            }
        }

        private void SortEntries()
        {
            _entries.Sort(static (a, b) =>
            {
                int t = b.Tier.CompareTo(a.Tier);
                if (t != 0) return t;
                int c = b.Count.CompareTo(a.Count);
                if (c != 0) return c;
                return a.RegIdx.CompareTo(b.RegIdx);
            });
        }

        private void ApplyToRows()
        {
            // 등록 세트 수 변동에 대비해 다시 ensure (보통 첫 Init 후엔 no-op).
            EnsureRows(_entries.Count);

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry e = _entries[i];
                SetEffectRowView row = _rows[i];
                if (row == null) continue;
                // sibling order 도 정렬에 맞춰 재배치 (Vertical Layout Group 이 위에서부터 그림).
                row.transform.SetSiblingIndex(i);
                row.Bind(e.Set, e.Count, e.Tier);
            }
            // 남는 row 는 비활성화.
            for (int i = _entries.Count; i < _rows.Count; i++)
            {
                if (_rows[i] != null) _rows[i].gameObject.SetActive(false);
            }
        }
    }
}
