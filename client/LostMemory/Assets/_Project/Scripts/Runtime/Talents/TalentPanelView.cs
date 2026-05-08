using LostMemory.Data;
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

        private TalentModel _model;

        public bool IsOpen => gameObject.activeSelf;

        private void Start()
        {
            var saved = TalentSaveService.Load();
            _model = new TalentModel(_talentDatas, _debugTotalPoints, saved);

            for (int i = 0; i < _rows.Length; i++)
            {
                var row = _rows[i];
                row.Init(_talentDatas[i].DisplayName);

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
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        // ── 외부 공개 API ────────────────────────────────────

        /// <summary>NPC 상호작용 시 패널을 연다. 저장된 투자값을 다시 로드해 최신 상태로 표시.</summary>
        public void Open()
        {
            var saved = TalentSaveService.Load();
            _model = new TalentModel(_talentDatas, _debugTotalPoints, saved);
            Refresh();
            gameObject.SetActive(true);
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

        private void OnSave()
        {
            TalentSaveService.Save(_model);
        }
    }
}
