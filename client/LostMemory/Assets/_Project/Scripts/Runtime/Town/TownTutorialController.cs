using System;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Town
{
    /// <summary>
    /// 마을 첫 진입 시 자동으로 튜토리얼 패널을 띄우고, 페이지 전환에 맞춰 월드 마커를 이동시키는 컨트롤러.
    /// 이미 본 적 있으면(PlayerPrefs) 자체적으로 비활성화.
    /// 패널 닫을 때 "다시 보지 않기" 체크되어 있으면 PlayerPrefs 에 기록.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Town/Town Tutorial Controller")]
    public class TownTutorialController : MonoBehaviour
    {
        [Serializable]
        public struct Page
        {
            public string Title;
            [TextArea(2, 5)] public string Body;
            public Vector3 WorldMarkerPosition;
        }

        [Header("Pages (Inspector 에서 직접 입력)")]
        [SerializeField] private Page[] _pages;

        [Header("Refs")]
        [SerializeField] private TutorialPanelView _panel;
        [Tooltip("월드에 표시할 화살표/마커 prefab. 플레이어 머리 위에서 목적지를 가리킴.")]
        [SerializeField] private GameObject _worldMarkerPrefab;
        [Tooltip("마커가 따라다닐 플레이어 Transform. 비어 있으면 Tag 'Player' 로 자동 탐색.")]
        [SerializeField] private Transform _playerTransform;
        [Tooltip("패널 닫힘 상태에서 우상단에 떠 있는 작은 '튜토리얼' 버튼. 클릭 시 패널을 다시 연다.")]
        [SerializeField] private Button _minimizedButton;

        [Header("Persistence")]
        [Tooltip("이미 봤음을 기록할 PlayerPrefs 키. 다른 마을/리셋 시 키 분리 가능.")]
        [SerializeField] private string _seenKey = "TownTutorialSeen";

        [Header("Debug")]
        [Tooltip("true 면 PlayerPrefs 무시하고 항상 표시 (에디터 테스트용).")]
        [SerializeField] private bool _alwaysShow = false;
        [Tooltip("[TownTutorialController] HandleClose / PlayerPrefs 키 삭제 등 진단 로그. 평소 OFF.")]
        [SerializeField] private bool verboseLog = false;

        private GameObject _markerInstance;
        private int _currentIndex;

        private void Start()
        {
            if (_panel == null)
            {
                Debug.LogError("[TownTutorialController] panel 이 null. wiring 확인.", this);
                return;
            }

            if (_pages == null || _pages.Length == 0)
            {
                Debug.LogWarning("[TownTutorialController] _pages 비어 있음. 패널 표시 생략.", this);
                _panel.Hide();
                SetMinimizedActive(false);
                return;
            }

            _panel.OnNextClicked  += HandleNext;
            _panel.OnPrevClicked  += HandlePrev;
            _panel.OnCloseClicked += HandleClose;

            if (_minimizedButton != null)
                _minimizedButton.onClick.AddListener(HandleReopen);

            bool firstTime = _alwaysShow || PlayerPrefs.GetInt(_seenKey, 0) == 0;
            if (firstTime)
            {
                OpenPanel();
            }
            else
            {
                _panel.Hide();
                SetMinimizedActive(true);
            }
        }

        private void OnDestroy()
        {
            if (_panel != null)
            {
                _panel.OnNextClicked  -= HandleNext;
                _panel.OnPrevClicked  -= HandlePrev;
                _panel.OnCloseClicked -= HandleClose;
            }
            if (_minimizedButton != null)
                _minimizedButton.onClick.RemoveListener(HandleReopen);
        }

        private void OpenPanel()
        {
            _currentIndex = 0;
            _panel.Show();
            SetMinimizedActive(false);
            EnsureMarker();
            ApplyPage();
        }

        private void HandleReopen()
        {
            OpenPanel();
        }

        private void SetMinimizedActive(bool active)
        {
            if (_minimizedButton == null) return;
            _minimizedButton.gameObject.SetActive(active);
        }

        private void HandleNext()
        {
            if (_currentIndex >= _pages.Length - 1)
            {
                // 마지막 페이지에서 Next = 완료
                HandleClose();
                return;
            }
            _currentIndex++;
            ApplyPage();
        }

        private void HandlePrev()
        {
            if (_currentIndex <= 0) return;
            _currentIndex--;
            ApplyPage();
        }

        private void HandleClose()
        {
            bool dontShow = _panel.DontShowAgain;
            if (verboseLog) Debug.Log($"[TownTutorialController] HandleClose dontShowAgain={dontShow} key='{_seenKey}'", this);
            if (dontShow)
            {
                PlayerPrefs.SetInt(_seenKey, 1);
                PlayerPrefs.Save();
            }

            _panel.Hide();
            DestroyMarker();
            // "다시 보지 않기" 가 체크돼도 작은 버튼은 항상 표시 — 사용자가 원할 때 다시 볼 수 있게.
            SetMinimizedActive(true);
        }

        private void ApplyPage()
        {
            Page page = _pages[_currentIndex];
            _panel.BindPage(_currentIndex, _pages.Length, page.Title, page.Body);

            if (_markerInstance != null && _markerInstance.TryGetComponent(out TutorialWorldMarker marker))
            {
                marker.SetTarget(page.WorldMarkerPosition);
            }
        }

        private void EnsureMarker()
        {
            if (_worldMarkerPrefab == null) return;
            if (_markerInstance != null) return;

            // 플레이어 Transform 자동 탐색 (Inspector 미할당 시 Tag 로 fallback)
            if (_playerTransform == null)
            {
                GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null) _playerTransform = playerGo.transform;
            }

            if (_playerTransform == null)
            {
                Debug.LogWarning("[TownTutorialController] playerTransform 을 찾지 못함. 마커가 동작하지 않음.", this);
                return;
            }

            _markerInstance = Instantiate(_worldMarkerPrefab);
            _markerInstance.name = "TutorialWorldMarker (Spawned)";
            if (_markerInstance.TryGetComponent(out TutorialWorldMarker marker))
                marker.SetPlayer(_playerTransform);
        }

        private void DestroyMarker()
        {
            if (_markerInstance == null) return;
            Destroy(_markerInstance);
            _markerInstance = null;
        }

        [ContextMenu("Debug — Reset Seen Flag")]
        private void DebugResetSeen()
        {
            PlayerPrefs.DeleteKey(_seenKey);
            PlayerPrefs.Save();
            Debug.Log($"[TownTutorialController] PlayerPrefs key '{_seenKey}' 삭제. 다음 Play 부터 재표시.", this);
        }
    }
}
