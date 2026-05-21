using System;
using System.Collections.Generic;
using System.Linq;
using LostMemory.Player;
using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// 런(Run) 중 플레이어가 보유한 유물 목록을 관리한다.
    /// RunManager 또는 Player GameObject에 부착한다.
    ///
    /// DefaultExecutionOrder(-100): PlayerRunState 스냅샷 복원이 같은 GameObject 의 다른
    /// 컴포넌트(MagicalGirlSpawner / RelicEffectRegistry / BuildManager 등) OnEnable 보다
    /// *먼저* 끝나야 OwnedRelics 가 빈 상태로 OnEnable replay 가 헛돌지 않는다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerRelicInventory : MonoBehaviour
    {
        [Header("CL-146 Inventory Capacity")]
        [Tooltip("기본 인벤토리 최대 슬롯 (4×4 그리드).")]
        [SerializeField, Min(1)] private int _baseMaxSlots = 16;

        [Header("CL-151 Grid (자동 배치)")]
        [Tooltip("그리드 가로 셀 수.")]
        [SerializeField, Min(1)] private int _maxCols = 4;

        [Tooltip("그리드 세로 셀 수.")]
        [SerializeField, Min(1)] private int _maxRows = 4;

        [Header("Consumables")]
        [SerializeField] private PlayerConsumableInventory _consumableInventory;

        private readonly List<RelicData> _ownedRelics = new();

        // CL-146: 행운 3스택 (LuckSlotExpand) 효과로 추가되는 보너스 슬롯.
        // SetEffectApplicator 가 AddSlots(1) 호출, 비활성화 시 AddSlots(-1).
        private int _bonusSlots = 0;

        // CL-151: 2D 셀 그리드 + Placement 리스트 (1D _ownedRelics 와 병행, 같은 항목 동기 유지).
        private InventoryGrid _grid;
        private readonly List<RelicPlacement> _placements = new();

        /// <summary>현재 보유한 유물 목록 (읽기 전용)</summary>
        public IReadOnlyList<RelicData> OwnedRelics => _ownedRelics;

        /// <summary>CL-146: 현재 최대 슬롯 (기본 + 행운 보너스). CL-148 인벤토리 UI 가 동적 셀 표시용으로 조회.</summary>
        public int MaxSlots => _baseMaxSlots + _bonusSlots;

        /// <summary>CL-151: 그리드 가로 셀 수.</summary>
        public int MaxCols => _maxCols;

        /// <summary>CL-151: 그리드 세로 셀 수.</summary>
        public int MaxRows => _maxRows;

        /// <summary>CL-151: 현재 배치된 유물들 (좌표 포함, 읽기 전용). UI 가 다중 칸 시각화용으로 순회.</summary>
        public IReadOnlyList<RelicPlacement> Placements => _placements;

        /// <summary>CL-146: 슬롯 변경 시 발화 (인자 = 새 MaxSlots). UI sync 용.</summary>
        public event Action<int> MaxSlotsChanged;

        /// <summary>유물이 새로 획득되었을 때 발화. RelicEffectRegistry 등이 구독.</summary>
        public event Action<RelicData> OnRelicAcquired;

        /// <summary>CL-139: 유물이 인벤토리에서 제거됐을 때 발화. BuildManager 가 카운트 갱신용으로 구독.</summary>
        public event Action<RelicData> OnRelicRemoved;

        /// <summary>CL-109: Run 종료 시 인벤토리 비워질 때 발화. RelicEffectRegistry 가 modifier/shield 일괄 정리.</summary>
        public event Action OnCleared;

        /// <summary>CL-151: 배치 변경 시 발화 (TryAdd / Remove / Sort / Clear). UI 갱신 hook.</summary>
        public event Action OnPlacementChanged;

        /// <summary>
        /// CL-152: TryAdd 가 사이즈 초과 또는 그리드 공간 부족으로 실패 시 발화.
        /// ToastNotifierBridge 가 구독해 사용자 알림. 소모품/중복은 정상 흐름이라 발화 X.
        /// 인자: (relic, reason) — reason 예: "사이즈 초과", "공간 부족".
        /// </summary>
        public event Action<RelicData, string> OnTryAddRejected;

        private void Awake()
        {
            _grid = new InventoryGrid(_maxCols, _maxRows);
            ResolveConsumableInventory();

            // 씬 전환 직전 StageRouteManager 가 캡처한 스냅샷이 있으면 데이터 복구.
            // 이벤트 발화는 OnPlacementChanged 한 번만 — OnRelicAcquired 는 발화하지 않음
            // (RelicEffectRegistry 는 OnEnable replay 경로로 effect 재등록).
            PlayerRunState runState = PlayerRunState.Instance;
            if (runState != null && runState.HasSnapshot)
            {
                LoadFrom(runState.Snapshot);
                _restoredFromSnapshot = true;
            }
        }

        // Awake 시점 LoadFrom 으로 채워진 데이터에 대해 UI(InventoryPanelView 등)는 OnPlacementChanged
        // 를 통해 갱신된다. 그러나 OnPlacementChanged 구독은 다른 컴포넌트들의 OnEnable 단계에서
        // 일어나므로 Awake 안에서 발화하면 일부 구독자가 누락된다. Start 단계에서 1회 발화 → 모두 구독 완료 후 갱신.
        private bool _restoredFromSnapshot;

        private void Start()
        {
            if (!_restoredFromSnapshot) return;
            OnPlacementChanged?.Invoke();
        }

        /// <summary>
        /// 씬 전환을 대비해 현재 인벤토리 상태(배치 좌표 + 보너스 슬롯)를 스냅샷으로 반환.
        /// 호출 측: StageRouteManager.TryLoadRouteNode 의 씬 로드 직전 (PlayerRunState.Capture 로 보관).
        /// </summary>
        public void CaptureSnapshotInto(ref PlayerSnapshot snapshot)
        {
            snapshot.Placements = new List<RelicPlacementSnapshot>(_placements.Count);
            for (int i = 0; i < _placements.Count; i++)
            {
                RelicPlacement p = _placements[i];
                if (p.Relic == null) continue;
                snapshot.Placements.Add(new RelicPlacementSnapshot
                {
                    Data = p.Relic,
                    X = p.X,
                    Y = p.Y,
                });
            }
            snapshot.BonusSlots = _bonusSlots;
        }

        /// <summary>
        /// 스냅샷으로부터 인벤토리 데이터를 복구.
        /// 이벤트 발화 정책: OnRelicAcquired 는 호출하지 않음 (effect 중복 등록 방지).
        /// RelicEffectRegistry 는 OnEnable 단계에서 OwnedRelics 를 순회해 자체 replay.
        /// BuildManager 는 OnEnable 에서 _isDirty=true 로 LateUpdate 재계산하므로 추가 신호 불필요.
        /// </summary>
        public void LoadFrom(PlayerSnapshot snapshot)
        {
            if (_grid == null) _grid = new InventoryGrid(_maxCols, _maxRows);
            _grid.Reset();
            _placements.Clear();
            _ownedRelics.Clear();
            _bonusSlots = Mathf.Max(0, snapshot.BonusSlots);

            if (snapshot.Placements != null)
            {
                for (int i = 0; i < snapshot.Placements.Count; i++)
                {
                    RelicPlacementSnapshot s = snapshot.Placements[i];
                    if (s.Data == null) continue;

                    int w = s.Data.Width;
                    int h = s.Data.Height;
                    if (w <= 0 || h <= 0) continue;
                    if (s.X < 0 || s.Y < 0 || s.X + w > _maxCols || s.Y + h > _maxRows)
                    {
                        Debug.LogWarning($"[PlayerRelicInventory] LoadFrom: {s.Data.name} 좌표 ({s.X},{s.Y}) 그리드 범위 초과 — 스킵");
                        continue;
                    }

                    _grid.Mark(s.X, s.Y, w, h, true);
                    _placements.Add(new RelicPlacement
                    {
                        Relic = s.Data,
                        Origin = new Vector2Int(s.X, s.Y),
                    });
                    _ownedRelics.Add(s.Data);
                }
            }
        }

        /// <summary>
        /// CL-146: 행운 3스택 효과 — 보너스 슬롯 가감. AddSlots(1) 활성화, AddSlots(-1) 비활성화.
        /// _bonusSlots 가 음수가 되지 않도록 clamp.
        /// </summary>
        public void AddSlots(int amount)
        {
            int newBonus = Mathf.Max(0, _bonusSlots + amount);
            if (newBonus == _bonusSlots) return;
            _bonusSlots = newBonus;
            Debug.Log($"[PlayerRelicInventory] MaxSlots {MaxSlots} ({(amount >= 0 ? "+" : "")}{amount} slots, base={_baseMaxSlots}, bonus={_bonusSlots})");
            MaxSlotsChanged?.Invoke(MaxSlots);
        }

        private PlayerConsumableInventory ResolveConsumableInventory()
        {
            if (_consumableInventory != null)
            {
                return _consumableInventory;
            }

            _consumableInventory = GetComponent<PlayerConsumableInventory>();
            if (_consumableInventory != null)
            {
                return _consumableInventory;
            }

            _consumableInventory = GetComponentInChildren<PlayerConsumableInventory>(true);
            if (_consumableInventory != null)
            {
                return _consumableInventory;
            }

            _consumableInventory = GetComponentInParent<PlayerConsumableInventory>();
            if (_consumableInventory != null)
            {
                return _consumableInventory;
            }

            _consumableInventory = gameObject.AddComponent<PlayerConsumableInventory>();
            return _consumableInventory;
        }

        /// <summary>
        /// 유물을 인벤토리에 추가한다.
        /// 소모품은 PlayerConsumableInventory 로 라우팅하고, 일반 유물은 그리드 인벤토리에 배치한다.
        /// </summary>
        /// <returns>실제로 추가됐으면 true</returns>
        public bool TryAdd(RelicData relic)
        {
            if (relic == null)
            {
                Debug.LogWarning("[PlayerRelicInventory] relic is null");
                return false;
            }

            if (relic.IsConsumable)
            {
                // 소모품은 단축키바(PlayerConsumableInventory)로 라우팅 — 유물 인벤토리 미등록
                PlayerConsumableInventory consumableInventory = ResolveConsumableInventory();
                if (consumableInventory == null)
                {
                    Debug.LogWarning($"[PlayerRelicInventory] 소모품 인벤토리 없음 — '{relic.DisplayName}' 추가 실패", this);
                    return false;
                }

                bool added = consumableInventory.TryAdd(relic);
                Debug.Log($"[PlayerRelicInventory] 소모품은 단축키바로 라우팅: {relic.DisplayName}, added={added}");
                return added;
            }

            // CL-151: 사이즈 초과 검증 (CL-150 const → _maxCols/_maxRows 정식 필드 교체)
            if (relic.Width > _maxCols || relic.Height > _maxRows)
            {
                Debug.LogWarning($"[CL-151] {relic.DisplayName} 사이즈 ({relic.Width}×{relic.Height}) 가 인벤토리 ({_maxCols}×{_maxRows}) 초과 — TryAdd reject");
                OnTryAddRejected?.Invoke(relic, "사이즈 초과");
                return false;
            }

            // 일반 유물: 중복 체크
            if (Has(relic))
            {
                Debug.LogWarning($"[PlayerRelicInventory] 이미 보유 중: {relic.DisplayName}");
                return false;
            }

            // CL-151: 자동 배치 (Top-left first fit)
            if (_grid == null) _grid = new InventoryGrid(_maxCols, _maxRows);
            if (!_grid.TryPlace(relic, out var origin))
            {
                Debug.LogWarning($"[CL-151] {relic.DisplayName} 배치 공간 부족 ({_maxCols}×{_maxRows} 그리드 가득 참) — TryAdd reject");
                OnTryAddRejected?.Invoke(relic, "공간 부족");
                return false;
            }
            _placements.Add(new RelicPlacement { Relic = relic, Origin = origin });

            int emptyIdx = _ownedRelics.IndexOf(null);
            if (emptyIdx >= 0) _ownedRelics[emptyIdx] = relic;
            else               _ownedRelics.Add(relic);
            OnRelicAcquired?.Invoke(relic);
            OnPlacementChanged?.Invoke();
            Debug.Log($"[PlayerRelicInventory] 유물 획득: {relic.DisplayName} @ ({origin.x},{origin.y}) {relic.Width}×{relic.Height}");
            return true;
        }

        /// <summary>해당 유물을 보유 중인지 확인한다.</summary>
        public bool Has(RelicData relic) =>
            _ownedRelics.Any(r => r != null && r.name == relic.name);

        /// <summary>
        /// RewardPool.DrawThree()에 넘길 보유 유물 이름 목록을 반환한다.
        /// </summary>
        public IEnumerable<string> GetOwnedNames() =>
            _ownedRelics.Where(r => r != null).Select(r => r.name);

        /// <summary>유물을 인벤토리에서 1개 제거한다.</summary>
        /// <returns>실제로 제거됐으면 true</returns>
        public bool Remove(RelicData relic)
        {
            if (relic == null) return false;
            int idx = _ownedRelics.FindIndex(r => r != null && r.name == relic.name);
            if (idx < 0) return false;
            _ownedRelics[idx] = null;   // 슬롯 위치 유지 — 제거 대신 null로 교체

            // CL-151: 점유 해제 + placements 제거
            int pIdx = _placements.FindIndex(p => p.Relic != null && p.Relic.name == relic.name);
            if (pIdx >= 0)
            {
                var p = _placements[pIdx];
                _grid?.Mark(p.X, p.Y, p.W, p.H, false);
                _placements.RemoveAt(pIdx);
                OnPlacementChanged?.Invoke();
            }

            OnRelicRemoved?.Invoke(relic);
            Debug.Log($"[PlayerRelicInventory] 유물 버림: {relic.DisplayName}");
            return true;
        }

        /// <summary>
        /// 두 인덱스의 유물 위치를 교환한다. 리스트가 짧으면 null로 채워 확장한다.
        /// CL-151: swap 후 자동 Sort(RarityThenSize) 호출 — 2D 그리드 일관성 유지.
        /// </summary>
        public void Swap(int indexA, int indexB)
        {
            if (indexA < 0 || indexB < 0) return;
            if (indexA == indexB) return;

            // 목적지 인덱스까지 null로 채워 리스트를 확장
            int needed = Mathf.Max(indexA, indexB) + 1;
            while (_ownedRelics.Count < needed)
                _ownedRelics.Add(null);

            (_ownedRelics[indexA], _ownedRelics[indexB]) = (_ownedRelics[indexB], _ownedRelics[indexA]);

            // CL-151: 1D swap 만으로는 2D placements 정합성 깨짐 → 자동 재배치
            Sort(InventorySortMode.RarityThenSize);
        }

        /// <summary>런 종료 시 인벤토리를 초기화한다. CL-109: OnCleared 이벤트 발화로 Registry 가 modifier/shield 정리.</summary>
        public void Clear()
        {
            _ownedRelics.Clear();
            ResolveConsumableInventory()?.Clear();

            // CL-151: 그리드 점유 + placements 리셋
            _grid?.Reset();
            _placements.Clear();
            OnPlacementChanged?.Invoke();

            OnCleared?.Invoke();
        }

        /// <summary>
        /// CL-151: 정리 (정렬 + 재배치). _ownedRelics 를 mode 기준 정렬 후 그리드 리셋 + 재배치.
        /// 주의: <see cref="OnRelicAcquired"/> / <see cref="OnRelicRemoved"/> 발화 X — effect 재계산 회피.
        /// <see cref="OnPlacementChanged"/> 만 발화하여 UI 만 갱신.
        /// </summary>
        public void Sort(InventorySortMode mode)
        {
            if (_grid == null) _grid = new InventoryGrid(_maxCols, _maxRows);

            IEnumerable<RelicData> sorted = mode switch
            {
                InventorySortMode.RarityDesc =>
                    _ownedRelics.Where(r => r != null).OrderByDescending(r => (int)r.Rarity),
                InventorySortMode.SizeDesc =>
                    _ownedRelics.Where(r => r != null).OrderByDescending(r => r.OccupiedCells),
                InventorySortMode.RarityThenSize =>
                    _ownedRelics.Where(r => r != null)
                        .OrderByDescending(r => (int)r.Rarity)
                        .ThenByDescending(r => r.OccupiedCells),
                _ => _ownedRelics.Where(r => r != null),
            };
            var newOrder = sorted.ToList();

            // 직접 _ownedRelics.Clear() — Remove 메서드 사용 X (OnRelicRemoved 발화 회피)
            _grid.Reset();
            _placements.Clear();
            _ownedRelics.Clear();

            foreach (var relic in newOrder)
            {
                if (_grid.TryPlace(relic, out var origin))
                {
                    _ownedRelics.Add(relic);
                    _placements.Add(new RelicPlacement { Relic = relic, Origin = origin });
                }
                else
                {
                    // 정렬 후 배치 실패 = 사이즈 합 초과 (이론적 불가능 — TryAdd 시점 가드)
                    Debug.LogError($"[CL-151] 정리 후 {relic.name} 배치 실패 — 데이터 손실 방지를 위해 1D 만 추가");
                    _ownedRelics.Add(relic);
                }
            }

            OnPlacementChanged?.Invoke();
            Debug.Log($"[CL-151] Sort({mode}) — {_ownedRelics.Count} 아이템 재배치 완료");
        }

        // ── CL-107 검증용 디버그 진입점 ─────────────────────
        // CL-110 의 RewardPanel 자동 흐름이 완성되기 전까지 Inspector 에서 수동 TryAdd 로 효과 검증.
        [Header("Debug (CL-107 검증용)")]
        [Tooltip("Inspector 우상단 ︙ → 'Debug — Add all assigned relics' 클릭 시 모두 TryAdd. 비워두면 무동작.")]
        [SerializeField] private RelicData[] _debugRelicsToAdd;

        [ContextMenu("Debug — Add all assigned relics")]
        private void DebugAddAllAssignedRelics()
        {
            if (_debugRelicsToAdd == null) return;
            foreach (RelicData r in _debugRelicsToAdd)
            {
                if (r != null) TryAdd(r);
            }
        }

        // CL-139 검증용: Run 종료(=Clear) 흐름을 수동 트리거. BuildManager / EffectApplicator(CL-140)
        // 의 OnCleared 구독 경로 확인용. RunManager 까지 가지 않고도 인벤토리 비우기 + OnCleared 발화.
        [ContextMenu("Debug — Clear inventory")]
        private void DebugClear() => Clear();

        // CL-151 검증용: Sort 직접 호출 (UI 정리 버튼 미완성 상태에서 데이터 검증)
        [ContextMenu("Debug — Sort (RarityThenSize)")]
        private void DebugSortRarityThenSize() => Sort(InventorySortMode.RarityThenSize);

        [ContextMenu("Debug — Sort (RarityDesc)")]
        private void DebugSortRarityDesc() => Sort(InventorySortMode.RarityDesc);

        [ContextMenu("Debug — Sort (SizeDesc)")]
        private void DebugSortSizeDesc() => Sort(InventorySortMode.SizeDesc);

        // CL-151 검증용: 현재 그리드 점유를 ASCII 로 콘솔 출력 (UI 미완성 시 시각 대체)
        [ContextMenu("Debug — Print Inventory Grid")]
        private void DebugPrintGrid()
        {
            if (_grid == null) { Debug.LogWarning("[CL-151] Grid not initialized"); return; }
            char[,] map = new char[_maxCols, _maxRows];
            for (int y = 0; y < _maxRows; y++)
                for (int x = 0; x < _maxCols; x++)
                    map[x, y] = '.';
            foreach (var p in _placements)
            {
                if (p.Relic == null) continue;
                char c = p.Relic.DisplayName.Length > 0 ? p.Relic.DisplayName[0] : '?';
                for (int dy = 0; dy < p.H; dy++)
                    for (int dx = 0; dx < p.W; dx++)
                        if (p.X + dx < _maxCols && p.Y + dy < _maxRows)
                            map[p.X + dx, p.Y + dy] = c;
            }
            var sb = new System.Text.StringBuilder();
            for (int y = 0; y < _maxRows; y++)
            {
                for (int x = 0; x < _maxCols; x++) sb.Append(map[x, y]);
                sb.AppendLine();
            }
            Debug.Log($"[CL-151] Grid ({_maxCols}×{_maxRows}, {_placements.Count} placements):\n{sb}");
        }
    }
}
