using LostMemory.MagicalGirl;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// 미소녀 5인 합체 궁극기(T 키) 사용 가능 상태를 HUD 아이콘으로 표시.
    /// PlayerHUD 대쉬 키 옆에 추가하는 T 키 아이콘 GameObject 의 활성/비활성 토글.
    ///
    /// 폴링 패턴 — MagicalGirlSpawner.IsUltimateReady 를 매 프레임 조회.
    /// 상태 변화 시에만 SetActive + (선택) 사운드 발화. 매 프레임 SetActive 호출 회피.
    ///
    /// 부착 위치: PlayerHUD GameObject 또는 별도 매니저. _iconRoot 에 표시/숨김 대상 T 아이콘 GameObject 드래그.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Ultimate Ready Hud Presenter")]
    public sealed class UltimateReadyHudPresenter : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("MagicalGirlSpawner. 비워두면 FindAnyObjectByType 으로 자동 탐색.")]
        [SerializeField] private MagicalGirlSpawner spawner;

        [Tooltip("T 키 아이콘 GameObject. ready=true 면 활성, false 면 비활성.")]
        [SerializeField] private GameObject iconRoot;

        [Header("Audio (선택)")]
        [Tooltip("Ready 상태로 진입한 순간 1회 재생할 효과음. null 이면 사운드 생략.")]
        [SerializeField] private AudioClip readyChimeClip;
        [SerializeField, Range(0f, 1f)] private float readyChimeVolume = 0.8f;

        [Header("Debug")]
        [SerializeField] private bool logTransitions = false;

        private bool _lastReady;
        private AudioSource _audioSource;

        private void Awake()
        {
            if (iconRoot != null) iconRoot.SetActive(false);
            // 사운드 재생용 — 인스펙터 미연결 시 자체 AudioSource 추가
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null && readyChimeClip != null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
            }
        }

        private void Start()
        {
            ResolveSpawnerIfNeeded();
        }

        private void Update()
        {
            ResolveSpawnerIfNeeded();
            if (spawner == null) return;

            bool nowReady = spawner.IsUltimateReady;
            if (nowReady == _lastReady) return;

            _lastReady = nowReady;
            if (iconRoot != null) iconRoot.SetActive(nowReady);

            // ready=true 로 전환한 순간 (false→true) 만 사운드. ready 해제 시는 무음.
            if (nowReady && readyChimeClip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(readyChimeClip, readyChimeVolume);
            }

            if (logTransitions)
                Debug.Log($"[UltimateReadyHud] Ready = {nowReady}", this);
        }

        private void ResolveSpawnerIfNeeded()
        {
            if (spawner != null) return;
            spawner = FindAnyObjectByType<MagicalGirlSpawner>();
        }
    }
}
