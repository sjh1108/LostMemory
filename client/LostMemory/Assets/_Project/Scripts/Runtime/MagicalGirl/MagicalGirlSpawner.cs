using System.Collections.Generic;
using LostMemory.Combat;
using LostMemory.Relics;
using LostMemory.TestKhi;
using UnityEngine;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// CL-144 / CL-145: 미소녀 spawner — Player 에 부착.
    ///
    /// 책임:
    /// - SetEffectApplicator (CL-140) 의 MagicalGirlSummon / MagicalGirlFusion case 가 SetCount(N) 호출
    /// - 1~4명 미소녀를 floating 원형 배치 (반경 1.5 유닛)
    /// - 5명 도달 시 일반 미소녀 모두 despawn + Fusion 엔티티 1개 spawn (CL-145)
    /// - PlayerRelicInventory 의 OnRelicAcquired 직접 구독 → 속성 미소녀 RelicData 추가 시
    ///   secondary tag → MagicalGirlVisual 변환 → 모든 girls 색상 갱신 (마지막 획득 정책)
    ///
    /// SetCount(N) 단일 진입점:
    /// - N>=5: fusion 모드 (일반 미소녀 모두 despawn + EnsureFusion)
    /// - N<5 : 일반 모드 (EndFusion + N명 spawn/despawn + 원형 재배치)
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

        [Tooltip("같은 GameObject 의 KhiPlayerAim. CL-145 fusion 레이저 마우스 조준 hook (없으면 player.right fallback).")]
        [SerializeField] private KhiPlayerAim playerAim;

        [Tooltip("원형 배치 반경 (유닛).")]
        [SerializeField, Min(0.1f)] private float ringRadius = 1.5f;

        [Tooltip("spawn / despawn / 속성 변화 / fusion 진입·종료 로그.")]
        [SerializeField] private bool _logSpawn = false;

        private readonly List<MagicalGirlAI> _girls = new();
        private MagicalGirlVisual _currentVisual = MagicalGirlVisual.Default;
        private GameObject _fusionInstance;        // CL-145 fusion 단일 인스턴스

        // CL-145 Phase 2: fusion 인스턴스 간 공유 cooldown.
        // Burst 시작 시 NotifyBurstStarted 가 (burst 종료 + 25s) 시점 예약.
        // 빌드 풀어서 fusion 해제되어도 spawner 는 살아있으므로 cooldown 유지 → 익스플로잇 방지.
        private float _fusionCooldownEndsAt = 0f;

        public float FusionCooldownEndsAt => _fusionCooldownEndsAt;

        private void OnEnable()
        {
            // 진단 — wiring 누락 즉시 가시화
            string anchorWiring   = anchor       != null ? "OK" : "self";
            string statWiring     = playerStat   != null ? "OK" : "❌ NULL";
            string combatWiring   = playerCombat != null ? "OK" : "❌ NULL";
            string inventoryWiring = inventory   != null ? "OK" : "❌ NULL (속성 시각 hook 동작 X)";
            string aimWiring      = playerAim    != null ? "OK" : "⚠ NULL (fusion 레이저 fallback=right)";
            Debug.Log($"[MagicalGirlSpawner] OnEnable — wiring: anchor={anchorWiring}, playerStat={statWiring}, playerCombat={combatWiring}, inventory={inventoryWiring}, playerAim={aimWiring}", this);

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

            // CL-145: N>=5 → fusion 모드. 일반 미소녀 모두 despawn + Fusion 1개 spawn.
            if (newCount >= 5)
            {
                while (_girls.Count > 0) Despawn();
                EnsureFusion();
                if (_logSpawn)
                    Debug.Log($"[MagicalGirl] Fusion ON (count={newCount})");
                return;
            }

            // N<5 → 일반 모드. Fusion 종료 + N명 spawn/despawn + 원형 재배치.
            EndFusion();
            while (_girls.Count < newCount) Spawn();
            while (_girls.Count > newCount) Despawn();
            RebalancePositions();
            if (_logSpawn)
                Debug.Log($"[MagicalGirl] SetCount → {newCount}, visual={_currentVisual}");
        }

        /// <summary>
        /// CL-145 Phase 2: Fusion 이 burst 발화 시 호출. Cooldown 시점을 spawner level 로 예약.
        /// 빌드 풀어서 fusion 해제되어도 cooldown 유지 → 새 fusion 인스턴스가 같은 cooldown 받음.
        /// </summary>
        public void NotifyBurstStarted(float burstDuration, float cooldown)
        {
            _fusionCooldownEndsAt = Time.time + burstDuration + cooldown;
            if (_logSpawn)
                Debug.Log($"[MagicalGirl] Fusion cooldown 예약 — ends at {_fusionCooldownEndsAt:F1} (now+{burstDuration + cooldown:F1}s)");
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

        // ── CL-145 fusion 헬퍼 ──────────────────────────────

        private void EnsureFusion()
        {
            if (_fusionInstance != null) return;
            _fusionInstance = new GameObject("MagicalGirlFusion");
            _fusionInstance.transform.SetParent(anchor != null ? anchor : transform, worldPositionStays: false);
            _fusionInstance.transform.localPosition = new Vector3(0f, 1.0f, 0f);  // 머리 위
            var fusion = _fusionInstance.AddComponent<MagicalGirlFusion>();
            fusion.Init(playerStat, playerCombat, playerAim, this);  // spawner 전달 → cooldown 동기화
        }

        private void EndFusion()
        {
            if (_fusionInstance == null) return;
            Destroy(_fusionInstance);
            _fusionInstance = null;
            if (_logSpawn)
                Debug.Log("[MagicalGirl] Fusion OFF");
        }
    }
}
