using System.Collections.Generic;
using LostMemory.Combat;
using LostMemory.Relics;
using LostMemory.TestKhi;
using UnityEngine;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// CL-144: 미소녀 spawner — Player 에 부착.
    ///
    /// 책임:
    /// - SetEffectApplicator (CL-140) 의 MagicalGirlSummon case 가 SetCount(N) 호출
    /// - 1~4명 미소녀를 floating 원형 배치 (반경 1.5 유닛)
    /// - PlayerRelicInventory 의 OnRelicAcquired 직접 구독 → 속성 미소녀 RelicData 추가 시
    ///   secondary tag → MagicalGirlVisual 변환 → 모든 girls 색상 갱신 (마지막 획득 정책)
    ///
    /// 본 CL 범위: 1~4. 5합체는 CL-145 영역 (BuildSet_미소녀 T5 추가 후 SetFusionMode 분기 신설).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MagicalGirlSpawner : MonoBehaviour
    {
        [Tooltip("미소녀가 따라다닐 기준 transform. 비워두면 self transform.")]
        [SerializeField] private Transform anchor;

        [Tooltip("같은 GameObject 의 PlayerStatModifierContainer. 데미지 계산용.")]
        [SerializeField] private PlayerStatModifierContainer playerStat;

        [Tooltip("같은 GameObject 의 KhiMeleeComboController. WeaponData.BaseDamage 조회.")]
        [SerializeField] private KhiMeleeComboController playerCombat;

        [Tooltip("같은 GameObject 의 PlayerRelicInventory. 속성 미소녀 시각 hook.")]
        [SerializeField] private PlayerRelicInventory inventory;

        [Tooltip("원형 배치 반경 (유닛).")]
        [SerializeField, Min(0.1f)] private float ringRadius = 1.5f;

        [Tooltip("spawn / despawn / 속성 변화 로그.")]
        [SerializeField] private bool _logSpawn = false;

        private readonly List<MagicalGirlAI> _girls = new();
        private MagicalGirlVisual _currentVisual = MagicalGirlVisual.Default;

        private void OnEnable()
        {
            // 진단 — wiring 누락 즉시 가시화
            string anchorWiring   = anchor       != null ? "OK" : "self";
            string statWiring     = playerStat   != null ? "OK" : "❌ NULL";
            string combatWiring   = playerCombat != null ? "OK" : "❌ NULL";
            string inventoryWiring = inventory   != null ? "OK" : "❌ NULL (속성 시각 hook 동작 X)";
            Debug.Log($"[MagicalGirlSpawner] OnEnable — wiring: anchor={anchorWiring}, playerStat={statWiring}, playerCombat={combatWiring}, inventory={inventoryWiring}", this);

            if (inventory != null)
                inventory.OnRelicAcquired += HandleRelicAcquired;
        }

        private void OnDisable()
        {
            if (inventory != null)
                inventory.OnRelicAcquired -= HandleRelicAcquired;
        }

        // ── public API (SetEffectApplicator 가 호출) ────────

        public void SetCount(int newCount)
        {
            newCount = Mathf.Max(0, newCount);
            while (_girls.Count < newCount) Spawn();
            while (_girls.Count > newCount) Despawn();
            RebalancePositions();
            if (_logSpawn)
                Debug.Log($"[MagicalGirl] SetCount → {newCount}, visual={_currentVisual}");
        }

        public void RegisterElementalVariant(RelicTag elementTag)
        {
            MagicalGirlVisual visual = MagicalGirlVisualPalette.FromTag(elementTag);
            _currentVisual = visual;
            foreach (MagicalGirlAI g in _girls)
            {
                if (g != null) g.SetVisual(visual);
            }
            if (_logSpawn)
                Debug.Log($"[MagicalGirl] Elemental → {elementTag} (visual={visual})");
        }

        // ── 인벤토리 hook ──────────────────────────────────

        private void HandleRelicAcquired(RelicData r)
        {
            if (r == null) return;
            // 미소녀 태그 가진 RelicData 만 처리
            bool primaryIsGirl = r.TagPrimary == RelicTag.MagicalGirl;
            bool secondaryIsGirl = r.TagSecondary == RelicTag.MagicalGirl;
            if (!primaryIsGirl && !secondaryIsGirl) return;

            // 미소녀가 아닌 쪽 태그가 속성. 양쪽 다 미소녀면 (TagPrimary == TagSecondary) Default 유지.
            RelicTag elementTag = primaryIsGirl ? r.TagSecondary : r.TagPrimary;
            if (elementTag == RelicTag.MagicalGirl || elementTag == RelicTag.None)
                return;
            RegisterElementalVariant(elementTag);
        }

        // ── 내부 ────────────────────────────────────────────

        private void Spawn()
        {
            var go = new GameObject("MagicalGirl");
            go.transform.SetParent(anchor != null ? anchor : transform, worldPositionStays: false);
            var ai = go.AddComponent<MagicalGirlAI>();
            ai.Init(playerStat, playerCombat);
            ai.SetVisual(_currentVisual);
            _girls.Add(ai);
        }

        private void Despawn()
        {
            if (_girls.Count == 0) return;
            int lastIdx = _girls.Count - 1;
            MagicalGirlAI last = _girls[lastIdx];
            _girls.RemoveAt(lastIdx);
            if (last != null) Destroy(last.gameObject);
        }

        private void RebalancePositions()
        {
            int count = _girls.Count;
            if (count == 0) return;
            for (int i = 0; i < count; i++)
            {
                if (_girls[i] == null) continue;
                float angle = (360f / count) * i * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * ringRadius;
                _girls[i].transform.localPosition = offset;
            }
        }
    }
}
