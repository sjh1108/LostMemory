using System;
using LostMemory.Town;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Stage
{
    /// <summary>
    /// 던전 첫 진입 시 자동으로 튜토리얼 패널을 띄우는 컨트롤러.
    /// TownTutorialController 와 동일한 단순 패턴 — Page 배열을 사용자가 Next/Prev 클릭으로 순회.
    /// 이벤트 기반 단계별 진행은 제거 (시간 압박 + 동기화 복잡도 회피).
    /// "다시 보지 않기" 체크 시 PlayerPrefs 에 기록.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Dungeon Tutorial Controller")]
    public class DungeonTutorialController : MonoBehaviour
    {
        // enum 들은 인스펙터 호환성을 위해 유지 (씬 파일에 직렬화된 이전 데이터 보존). 로직에서는 사용 안 함.
        public enum Step { Controls, DoorsLocked, Combat, Reward, Inventory, NextRoom, Portal }
        public enum MarkerMode { None, StaticWorld, DynamicNearestEnemy, DynamicNearestExit, DynamicPortal }

        [Serializable]
        public struct Page
        {
            [Tooltip("로직 미사용 — 인스펙터 호환용. Pages 배열 순서대로 진행.")]
            public Step Step;
            public string Title;
            [TextArea(2, 5)] public string Body;
            [Tooltip("로직 미사용 — 인스펙터 호환용. StaticPosition 이 (0,0,0) 이 아니면 마커 표시.")]
            public MarkerMode MarkerMode;
            [Tooltip("월드 마커 위치. (0,0,0) 이면 마커 숨김.")]
            public Vector3 StaticPosition;
            [Tooltip("로직 미사용 — 인스펙터 호환용.")]
            public bool AutoAdvance;
            [Tooltip("로직 미사용 — 인스펙터 호환용.")]
            public float AutoAdvanceSeconds;
        }

        [Header("Pages (Inspector 에서 직접 입력)")]
        [SerializeField] private Page[] _pages;

        [Header("Refs")]
        [SerializeField] private TutorialPanelView _panel;
        [Tooltip("월드에 표시할 화살표/마커 prefab. null 이면 마커 생략.")]
        [SerializeField] private GameObject _worldMarkerPrefab;
        [Tooltip("마커가 따라다닐 플레이어 Transform. 비어 있으면 Tag 'Player' 자동 탐색.")]
        [SerializeField] private Transform _playerTransform;
        [Tooltip("패널 닫힘 상태에서 우상단에 떠 있는 작은 '튜토리얼' 버튼. 클릭 시 패널을 다시 연다. null 허용.")]
        [SerializeField] private Button _minimizedButton;

        // 인스펙터 호환 위해 남기되 로직에서는 사용하지 않음.
        [SerializeField, HideInInspector] private DungeonRunBootstrap _dungeonBootstrap;
        [SerializeField, HideInInspector] private bool _pauseDuringControlsStep;
        [SerializeField, HideInInspector] private bool _logFlow;

        [Header("Persistence")]
        [Tooltip("이미 봤음을 기록할 PlayerPrefs 키.")]
        [SerializeField] private string _seenKey = "DungeonTutorialSeen";

        [Header("Debug")]
        [Tooltip("true 면 PlayerPrefs 무시하고 항상 표시 (에디터 테스트용).")]
        [SerializeField] private bool _alwaysShow = false;

        private GameObject _markerInstance;
        private TutorialWorldMarker _marker;
        private int _currentIndex;

        private void Awake()
        {
            // root 가 아니어도 시도 — Unity 가 경고만 내고 무시. root 이면 적용.
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start()
        {
            if (_panel == null)
            {
                Debug.LogError("[DungeonTutorial] panel 이 null. wiring 확인.", this);
                return;
            }

            if (_pages == null || _pages.Length == 0)
            {
                Debug.LogWarning("[DungeonTutorial] _pages 비어 있음. 패널 표시 생략.", this);
                _panel.Hide();
                SetMinimizedActive(false);
                return;
            }

            _panel.OnNextClicked  += HandleNext;
            _panel.OnPrevClicked  += HandlePrev;
            _panel.OnCloseClicked += HandleClose;
            _panel.SetNextButtonVisible(true);
            _panel.SetPrevButtonVisible(true);

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
            if (_panel.DontShowAgain)
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

            if (_marker != null)
            {
                if (page.StaticPosition != Vector3.zero)
                    _marker.SetTarget(page.StaticPosition);
                else
                    _marker.ClearTarget();
            }
        }

        private void EnsureMarker()
        {
            if (_worldMarkerPrefab == null) return;
            if (_markerInstance != null) return;

            if (_playerTransform == null)
            {
                GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null) _playerTransform = playerGo.transform;
            }

            if (_playerTransform == null)
            {
                Debug.LogWarning("[DungeonTutorial] playerTransform 을 찾지 못함. 마커가 동작하지 않음.", this);
                return;
            }

            _markerInstance = Instantiate(_worldMarkerPrefab);
            _markerInstance.name = "DungeonTutorialMarker";
            if (_markerInstance.TryGetComponent(out TutorialWorldMarker marker))
            {
                _marker = marker;
                _marker.SetPlayer(_playerTransform);
            }
        }

        private void DestroyMarker()
        {
            if (_markerInstance == null) return;
            Destroy(_markerInstance);
            _markerInstance = null;
            _marker = null;
        }

        [ContextMenu("Debug — Reset Dungeon Tutorial")]
        private void DebugResetSeen()
        {
            PlayerPrefs.DeleteKey(_seenKey);
            PlayerPrefs.Save();
            Debug.Log($"[DungeonTutorial] PlayerPrefs key '{_seenKey}' 삭제. 다음 던전 진입부터 재표시.", this);
        }
    }
}
