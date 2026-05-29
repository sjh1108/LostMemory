using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LostMemory.UI.Status
{
    /// <summary>
    /// 플레이어 상태창 패널 — 합산 스탯 섹션 + 보유 OnHit 효과 섹션을 표시.
    /// Presenter 가 만든 ViewModel 을 Render(vm) 으로 받아 row 풀에 바인딩.
    /// row prefab 은 PlayerStatusRowView 부착된 prefab 1종을 두 섹션에서 재사용.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Player Status Panel View")]
    public sealed class PlayerStatusPanelView : MonoBehaviour
    {
        [Header("Row Prefab")]
        [Tooltip("동적 Instantiate 할 1행 prefab. PlayerStatusRowView 부착.")]
        [SerializeField] private PlayerStatusRowView _rowPrefab;

        [Header("Section Containers")]
        [Tooltip("합산 스탯 행이 들어갈 부모 (VerticalLayoutGroup 부착).")]
        [SerializeField] private Transform _statsParent;

        [Tooltip("보유 OnHit 효과 행이 들어갈 부모 (VerticalLayoutGroup 부착).")]
        [SerializeField] private Transform _onHitsParent;

        [Header("Section Empty Hint (옵션)")]
        [Tooltip("스탯 섹션이 비어있을 때 표시할 안내 TMP. 비활성/활성으로만 토글. null 이어도 동작.")]
        [SerializeField] private TMP_Text _statsEmptyHint;

        [Tooltip("OnHit 섹션이 비어있을 때 표시할 안내 TMP. null 이어도 동작.")]
        [SerializeField] private TMP_Text _onHitsEmptyHint;

        private readonly List<PlayerStatusRowView> _statRows = new();
        private readonly List<PlayerStatusRowView> _onHitRows = new();

        /// <summary>ViewModel 로 패널 전체 갱신. presenter 가 호출.</summary>
        public void Render(StatusPanelViewModel vm)
        {
            if (vm == null)
            {
                HideAllRows();
                ToggleEmptyHints(true, true);
                return;
            }

            BindStatRows(vm.Stats, _statRows, _statsParent);
            BindEffectRows(vm.OnHits, _onHitRows, _onHitsParent);

            ToggleEmptyHints(vm.Stats.Count == 0, vm.OnHits.Count == 0);
        }

        private void BindStatRows(IReadOnlyList<StatusPanelViewModel.StatRow> rows,
                                  List<PlayerStatusRowView> pool, Transform parent)
        {
            EnsureRowPool(pool, parent, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                pool[i].gameObject.SetActive(true);
                pool[i].Bind(r.Name, r.TotalDisplay, r.Description);
            }
            for (int i = rows.Count; i < pool.Count; i++)
            {
                pool[i].gameObject.SetActive(false);
            }
        }

        private void BindEffectRows(IReadOnlyList<StatusPanelViewModel.EffectRow> rows,
                                    List<PlayerStatusRowView> pool, Transform parent)
        {
            EnsureRowPool(pool, parent, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                pool[i].gameObject.SetActive(true);
                pool[i].Bind(r.Name, "", r.Description);
            }
            for (int i = rows.Count; i < pool.Count; i++)
            {
                pool[i].gameObject.SetActive(false);
            }
        }

        private void EnsureRowPool(List<PlayerStatusRowView> pool, Transform parent, int needed)
        {
            if (_rowPrefab == null || parent == null) return;
            while (pool.Count < needed)
            {
                PlayerStatusRowView row = Instantiate(_rowPrefab, parent);
                pool.Add(row);
            }
        }

        private void ToggleEmptyHints(bool statsEmpty, bool onHitsEmpty)
        {
            if (_statsEmptyHint != null) _statsEmptyHint.gameObject.SetActive(statsEmpty);
            if (_onHitsEmptyHint != null) _onHitsEmptyHint.gameObject.SetActive(onHitsEmpty);
        }

        private void HideAllRows()
        {
            foreach (var r in _statRows) if (r != null) r.gameObject.SetActive(false);
            foreach (var r in _onHitRows) if (r != null) r.gameObject.SetActive(false);
        }
    }
}
