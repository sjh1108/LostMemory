using System.Threading.Tasks;
using LostMemory.Combat;
using LostMemory.Data;
using LostMemory.Memory;
using LostMemory.Networking.Player;
using LostMemory.Networking.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Talents
{
    /// <summary>
    /// 재능 분배 패널 전체를 담당하는 뷰 컴포넌트.
    /// TalentPanel 프리팹 루트 GameObject에 부착한다.
    /// </summary>
    public class TalentPanelView : MonoBehaviour
    {
        [SerializeField] private TalentData[] _talentDatas;
        [SerializeField] private TextMeshProUGUI _remainingPointsText;
        [SerializeField] private TalentRowView[] _rows;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private bool _autoOpenOnStart = true;
        [SerializeField] private int _debugTotalPoints = 10;
        [Tooltip("false로 설정하면 ESC 키 처리를 외부(TownTopRightHUDView 등)에 위임합니다.")]
        [SerializeField] private bool _handleEscKey = true;

        private TalentModel _model;

        public bool IsOpen => gameObject.activeSelf;

        private async void Start()
        {
            var saved = TalentSaveService.Load();
            int effectiveTotal = _debugTotalPoints + MemoryMetaService.Load().BonusTalentPoints;
            _model = new TalentModel(_talentDatas, effectiveTotal, saved);

            for (int i = 0; i < _rows.Length; i++)
            {
                var row = _rows[i];
                row.Init(_talentDatas[i].DisplayName, _talentDatas[i].MaxLevel);

                var type = row.TalentType;
                row.PlusButton.onClick.AddListener(() => { _model.TryAdd(type); Refresh(); });
                row.MinusButton.onClick.AddListener(() => { _model.TryRemove(type); Refresh(); });
            }

            _resetButton.onClick.AddListener(() => { _model.Reset(); Refresh(); });
            _saveButton.onClick.AddListener(OnSave);
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);

            if (_autoOpenOnStart)
                gameObject.SetActive(true);
            else
                gameObject.SetActive(false);

            Refresh();

            // 백엔드 동기화 — 실패 시 위 PlayerPrefs 모델 유지 (fallback 안전)
            await TryPullFromServerAsync();
        }

        private void Update()
        {
            if (_handleEscKey && Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        // ── 외부 공개 API ────────────────────────────────────

        /// <summary>NPC 상호작용 시 패널을 연다. 저장된 투자값을 다시 로드해 최신 상태로 표시.</summary>
        public async void Open()
        {
            var saved = TalentSaveService.Load();
            int effectiveTotal = _debugTotalPoints + MemoryMetaService.Load().BonusTalentPoints;
            _model = new TalentModel(_talentDatas, effectiveTotal, saved);
            Refresh();
            gameObject.SetActive(true);

            // 백엔드 동기화 — 실패 시 위 PlayerPrefs 모델 유지
            await TryPullFromServerAsync();
        }

        /// <summary>패널을 열거나 닫는다.</summary>
        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        /// <summary>패널을 닫는다. 미저장 변경사항은 버려진다.</summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void Refresh()
        {
            _remainingPointsText.text = $"남은 포인트: {_model.RemainingPoints}";
            foreach (var row in _rows)
            {
                var type = row.TalentType;
                row.Refresh(_model.GetInvested(type), _model.CanAdd(type), _model.CanRemove(type));
            }
        }

        private async void OnSave()
        {
            // 1. 로컬 (PlayerPrefs) — 즉시 안전
            TalentSaveService.Save(_model);
            ApplyStatsImmediately();

            // 2. 백엔드 동기화 — 실패해도 로컬은 이미 저장됨
            if (SessionApiClient.IsLoggedIn)
            {
                bool ok = await TalentSyncService.PushAsync(_model);
                if (!ok)
                {
                    Debug.LogWarning("[TalentPanelView] 백엔드 저장 실패 — 로컬만 저장됨. 다시 장착 시 재시도됨.", this);
                }
            }
        }

        /// <summary>
        /// 백엔드의 4 slot 값으로 모델을 갱신. 실패 시 기존 PlayerPrefs 모델 유지.
        /// 비로그인 상태면 skip.
        /// </summary>
        private async Task TryPullFromServerAsync()
        {
            if (!SessionApiClient.IsLoggedIn) return;
            if (_talentDatas == null || _talentDatas.Length == 0) return;

            int effectiveTotal = _debugTotalPoints + MemoryMetaService.Load().BonusTalentPoints;
            var pulled = await TalentSyncService.PullAsync(_talentDatas, effectiveTotal);
            if (pulled != null)
            {
                _model = pulled;
                Refresh();
                // 로컬도 동기화 — 다음 진입 시 같은 값으로 시작
                TalentSaveService.Save(_model);
            }
        }

        /// <summary>
        /// 저장 직후 PlayerStatModifierContainer 를 직접 갱신해 마을 HUD 도 즉시 반영.
        /// 던전 진입 시 TalentStartupApplier 가 같은 Source 로 RemoveBySource + 재등록하므로 중복 누적 없음.
        /// </summary>
        private void ApplyStatsImmediately()
        {
            // 멀티 fix: FindAnyObjectByType 는 호스트/게스트 race (씬에 양쪽 player 존재 시 잘못된 container 잡힘).
            // LocalPlayer 측 container 명시 조회 — TalentStartupApplier 와 동일 패턴으로 일관성.
            // 솔로/Editor 단일 씬은 LocalPlayerResolver 가 fallback 으로 첫 Character 잡음.
            PlayerStatModifierContainer container =
                LocalPlayerResolver.GetComponentOnLocalPlayer<PlayerStatModifierContainer>()
                ?? FindAnyObjectByType<PlayerStatModifierContainer>(FindObjectsInactive.Include);
            if (container == null)
            {
                Debug.LogWarning("[TalentPanelView] PlayerStatModifierContainer 가 씬에 없음 — 즉시 적용 불가.", this);
                return;
            }

            RunStartStats stats = TalentCalculator.Calculate(_model);

            // 이전에 같은 Source 로 등록한 stat 제거 → 누적 방지
            container.RemoveBySource(TalentStartupApplier.Source);

            if (stats.CriticalRate != 0f)
                container.AddPermanent(StatId.Critical, stats.CriticalRate, TalentStartupApplier.Source);
            if (stats.AttackSpeed != 0f)
                container.AddPermanent(StatId.AttackSpeed, stats.AttackSpeed, TalentStartupApplier.Source);
            if (stats.Defense != 0f)
                container.AddPermanent(StatId.Defense, stats.Defense, TalentStartupApplier.Source);
            if (stats.MaxHealth != 0f)
                container.AddPermanent(StatId.MaxHealth, stats.MaxHealth, TalentStartupApplier.Source);
            if (stats.MoveSpeed != 0f)
                container.AddPermanent(StatId.MoveSpeed, stats.MoveSpeed, TalentStartupApplier.Source);

            Debug.Log($"[TalentPanelView] 즉시 적용 — Critical={stats.CriticalRate:+0.0%;-0.0%;0%} " +
                      $"AttackSpeed={stats.AttackSpeed:+0.0%;-0.0%;0%} Defense={stats.Defense:F2} " +
                      $"MaxHealth={stats.MaxHealth:+0.0%;-0.0%;0%} MoveSpeed={stats.MoveSpeed:+0.0%;-0.0%;0%}", this);
        }
    }
}
