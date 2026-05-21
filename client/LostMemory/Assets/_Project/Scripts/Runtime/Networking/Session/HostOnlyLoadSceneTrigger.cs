using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// Test_MultiLobby (또는 다른 멀티 씬) 의 진입 포탈 트리거.
    /// A-4: 4인 멀티 게이트 — 연결된 플레이어가 *모두* trigger 안에 들어와야 호스트의 interactKey 입력으로 LoadScene 발화.
    /// 게스트는 NGO scene sync 로 자동 따라옴.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Lost Memory/Networking/Host Only Load Scene Trigger")]
    public sealed class HostOnlyLoadSceneTrigger : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField, Tooltip("NGO LoadScene 대상 씬 이름. Build Settings 에 enabled 등록 필수.")]
        private string targetSceneName = "Mob_Sync_Test";

        [SerializeField, Tooltip("LoadScene 모드.")]
        private UnityEngine.SceneManagement.LoadSceneMode loadSceneMode = UnityEngine.SceneManagement.LoadSceneMode.Single;

        [Header("Interaction")]
        [SerializeField, Tooltip("플레이어 식별 Tag.")]
        private string playerTag = "Player";

        [SerializeField, Tooltip("진입 키.")]
        private Key interactKey = Key.E;

        [Header("Multiplayer Gate")]
        [SerializeField, Tooltip("true: NGO 활성 시 연결된 플레이어가 *모두* trigger 안에 있어야 LoadScene 가능.")]
        private bool requireAllPlayersInside = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLog = false;

        // 4인 환경: NGO PlayerObject 가 모든 클라에서 sync 되어 trigger 이벤트 일관. root 단위로 set 관리.
        private readonly HashSet<Transform> _playersInside = new HashSet<Transform>();

        /// <summary>Q-3: UI "X/Y 대기 중" 표시 구독용. 인자: (currentInside, required).</summary>
        public event Action<int, int> PortalReadinessChanged;

        public int PlayersInsideCount => _playersInside.Count;
        public int RequiredPlayerCount => GetRequiredCount();
        public bool IsReadyToLoad => _playersInside.Count >= GetRequiredCount();

        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null && !col.isTrigger)
            {
                Debug.LogWarning($"[HostOnlyLoadSceneTrigger] Collider2D 가 isTrigger=false. 자동 전환.", this);
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            Transform root = other.transform.root;
            if (_playersInside.Add(root))
            {
                if (verboseLog) Debug.Log($"[HostOnlyLoadSceneTrigger] Player entered ({root.name}). count={_playersInside.Count}/{GetRequiredCount()}.", this);
                PortalReadinessChanged?.Invoke(_playersInside.Count, GetRequiredCount());
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            Transform root = other.transform.root;
            if (_playersInside.Remove(root))
            {
                if (verboseLog) Debug.Log($"[HostOnlyLoadSceneTrigger] Player exited ({root.name}). count={_playersInside.Count}/{GetRequiredCount()}.", this);
                PortalReadinessChanged?.Invoke(_playersInside.Count, GetRequiredCount());
            }
        }

        private void Update()
        {
            if (_playersInside.Count == 0) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!keyboard[interactKey].wasPressedThisFrame) return;

            TryLoadScene();
        }

        private int GetRequiredCount()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return 1;
            return nm.ConnectedClientsIds.Count;
        }

        private void TryLoadScene()
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogWarning($"[HostOnlyLoadSceneTrigger] targetSceneName 비어있음.", this);
                return;
            }

            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening)
            {
                Debug.LogWarning($"[HostOnlyLoadSceneTrigger] NetworkManager 비활성.", this);
                return;
            }

            if (!nm.IsServer)
            {
                if (verboseLog) Debug.Log($"[HostOnlyLoadSceneTrigger] 게스트 입력 무시.", this);
                return;
            }

            if (requireAllPlayersInside)
            {
                int required = GetRequiredCount();
                if (_playersInside.Count < required)
                {
                    Debug.Log($"[HostOnlyLoadSceneTrigger] 대기 — {_playersInside.Count}/{required} 명. LoadScene 보류.", this);
                    return;
                }
            }

            if (verboseLog) Debug.Log($"[HostOnlyLoadSceneTrigger] 호스트 LoadScene: {targetSceneName}", this);
            nm.SceneManager.LoadScene(targetSceneName, loadSceneMode);
        }
    }
}
