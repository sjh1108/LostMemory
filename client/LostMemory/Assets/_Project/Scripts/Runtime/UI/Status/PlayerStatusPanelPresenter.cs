using System;
using System.Collections.Generic;
using LostMemory.Combat;
using LostMemory.Networking.Player;
using LostMemory.Relics;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.UI.Status
{
    /// <summary>
    /// 플레이어 상태창 Presenter — LocalPlayer 측의 합산 스탯 + OnHit 효과를 모아 ViewModel 로 변환,
    /// View 에 전달. PlayerStatModifierContainer.ModifiersChanged 와 PlayerRelicInventory 이벤트를 구독해
    /// 변경 시 자동 갱신. NetworkBehaviour 불필요 — owner 클라이언트의 로컬 컴포넌트만 본다.
    ///
    /// 같은 GameObject 의 PlayerStatusPanelView 를 inspector 에 wire 하거나 GetComponent 로 자동 해결.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Player Status Panel Presenter")]
    public sealed class PlayerStatusPanelPresenter : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("같은 GameObject 의 PlayerStatusPanelView. 미할당 시 GetComponent 로 자동 해결.")]
        [SerializeField] private PlayerStatusPanelView _view;

        [Header("Debug")]
        [SerializeField] private bool _logRefresh = false;

        private PlayerStatModifierContainer _statContainer;
        private OnHitEffectRegistry _onHitRegistry;
        private PlayerRelicInventory _relicInventory;

        // CollectActive 결과 재사용 버퍼 (heap alloc 회피).
        private readonly List<PlayerStatModifierContainer.ActiveStatSnapshot> _statBuf = new(32);
        private readonly List<OnHitEffectRegistry.ActiveOnHitSnapshot> _onHitBuf = new(8);

        // ViewModel 도 재사용. View 가 Render 마다 새 인스턴스를 요구하지 않음.
        private readonly StatusPanelViewModel _vm = new();

        // StatId 별 합산 임시 dict — 매 빌드 시 Clear 후 채움.
        private readonly Dictionary<StatId, float> _statSums = new(16);

        private void Awake()
        {
            if (_view == null) _view = GetComponent<PlayerStatusPanelView>();
        }

        private void OnEnable()
        {
            ResolveRefs();
            SubscribeAll();
            LocalPlayerResolver.LocalPlayerReady += HandleLocalPlayerReady;
            Refresh();
        }

        private void OnDisable()
        {
            LocalPlayerResolver.LocalPlayerReady -= HandleLocalPlayerReady;
            UnsubscribeAll();
        }

        private void HandleLocalPlayerReady(KhiPlayerStateAggregator _)
        {
            // LocalPlayer 가 새로 등록되면 컴포넌트 다시 해결.
            UnsubscribeAll();
            _statContainer = null;
            _onHitRegistry = null;
            _relicInventory = null;
            ResolveRefs();
            SubscribeAll();
            Refresh();
        }

        private void ResolveRefs()
        {
            if (_statContainer == null)
                _statContainer = LocalPlayerResolver.GetComponentOnLocalPlayer<PlayerStatModifierContainer>();
            if (_onHitRegistry == null)
                _onHitRegistry = LocalPlayerResolver.GetComponentOnLocalPlayer<OnHitEffectRegistry>();
            if (_relicInventory == null)
                _relicInventory = LocalPlayerResolver.GetComponentOnLocalPlayer<PlayerRelicInventory>();
        }

        private void SubscribeAll()
        {
            if (_statContainer != null)
                _statContainer.ModifiersChanged += HandleModifiersChanged;
            if (_relicInventory != null)
            {
                _relicInventory.OnRelicAcquired += HandleRelicChanged;
                _relicInventory.OnRelicRemoved  += HandleRelicChanged;
                _relicInventory.OnCleared       += HandleCleared;
            }
        }

        private void UnsubscribeAll()
        {
            if (_statContainer != null)
                _statContainer.ModifiersChanged -= HandleModifiersChanged;
            if (_relicInventory != null)
            {
                _relicInventory.OnRelicAcquired -= HandleRelicChanged;
                _relicInventory.OnRelicRemoved  -= HandleRelicChanged;
                _relicInventory.OnCleared       -= HandleCleared;
            }
        }

        private void HandleModifiersChanged(StatId _) => Refresh();
        private void HandleRelicChanged(RelicData _)  => Refresh();
        private void HandleCleared()                   => Refresh();

        /// <summary>ViewModel 을 다시 만들고 View 에 push.</summary>
        public void Refresh()
        {
            if (_view == null) return;

            ResolveRefs();
            BuildViewModel(_vm);
            _view.Render(_vm);

            if (_logRefresh)
                Debug.Log($"[PlayerStatusPanelPresenter] Refresh — Stats={_vm.Stats.Count} OnHits={_vm.OnHits.Count}");
        }

        // 메이플식 "기본 stat 항상 표시" 목록. 이 순서대로 패널 위쪽에 고정 노출.
        // StatBaseProvider 에서 HasBase=true 인 stat (HP/MoveSpeed) 는 "Total (Base+Bonus)" 로,
        // 나머지는 "+N%" 로 표기. modifier=0 이어도 표시.
        private static readonly StatId[] s_alwaysVisible =
        {
            StatId.MaxHealth,
            StatId.AttackPower,
            StatId.MoveSpeed,
            StatId.Defense,
            StatId.Critical,
            StatId.CriticalDamage,
        };

        private Character ResolveLocalCharacter()
        {
            return LocalPlayerResolver.LocalCharacter;
        }

        private void BuildViewModel(StatusPanelViewModel vm)
        {
            vm.Clear();

            // ── 섹션 1: 합산 스탯 ─────────────────────────────
            Character localChar = ResolveLocalCharacter();

            // 1-A: always-visible core stat. modifier=0 이어도 항상 1 row.
            // _statSums 는 1-B 단계에서 사용 (always-visible 항목 중복 제거 위해 미리 mark).
            HashSet<StatId> renderedInCore = new();
            foreach (StatId stat in s_alwaysVisible)
            {
                var sv = StatBaseProvider.Resolve(stat, localChar, _statContainer);
                string display = sv.HasBase
                    ? StatIdLabels.FormatBaseAndBonus(stat, sv.Base, sv.Bonus, sv.Total)
                    : StatIdLabels.FormatBonusOrZero(stat, sv.Bonus);
                string name = StatIdLabels.LabelFor(stat);
                string desc = StatIdLabels.DescriptionFor(stat);
                vm.Stats.Add(new StatusPanelViewModel.StatRow(name, display, desc));
                renderedInCore.Add(stat);
                // MaxHealth row 가 MaxHealthFlat 도 흡수 — 같은 값(체력)이라 중복 row 방지.
                if (stat == StatId.MaxHealth) renderedInCore.Add(StatId.MaxHealthFlat);
            }

            // 1-B: 그 외 modifier 가 등록된 stat 만 추가. (always-visible 에 이미 포함된 건 skip)
            if (_statContainer != null)
            {
                _statContainer.CollectActive(_statBuf);
                _statSums.Clear();
                foreach (var s in _statBuf)
                {
                    if (renderedInCore.Contains(s.Stat)) continue;
                    // 조건부 modifier 는 매 호출마다 predicate 평가 필요하지만 CollectActive 는 무평가로 보냄.
                    // 표시 목적상 보유 효과로 같이 노출.
                    if (!_statSums.ContainsKey(s.Stat)) _statSums[s.Stat] = 0f;
                    _statSums[s.Stat] += s.Magnitude;
                }
                foreach (var kv in _statSums)
                {
                    string display = StatIdLabels.FormatTotal(kv.Key, kv.Value);
                    if (string.IsNullOrEmpty(display)) continue;
                    string name = StatIdLabels.LabelFor(kv.Key);
                    string desc = StatIdLabels.DescriptionFor(kv.Key);
                    vm.Stats.Add(new StatusPanelViewModel.StatRow(name, display, desc));
                }
            }

            // ── 섹션 2: 보유 OnHit 효과 ─────────────────────────
            if (_onHitRegistry != null)
            {
                _onHitRegistry.CollectActive(_onHitBuf);
                // 같은 RelicEffectType 이 여러 source 로 등록될 수 있어 type 별 1행으로 합쳐 표시.
                // (Magnitude 합산은 OnHit 의미상 비표시 — 어떤 효과를 가지고 있는지만 알려주면 충분.)
                HashSet<RelicEffectType> seen = new();
                foreach (var e in _onHitBuf)
                {
                    if (!StatIdLabels.IsOnHitEffect(e.Type)) continue;
                    if (!seen.Add(e.Type)) continue;
                    string name = RelicEffectLabels.LabelFor(e.Type);
                    string desc = StatIdLabels.OnHitDescriptionFor(e.Type);
                    vm.OnHits.Add(new StatusPanelViewModel.EffectRow(name, desc));
                }
            }
        }
    }
}
