using LostMemory.Networking.Common;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Networking.Player
{
    /// <summary>
    /// 플레이어 prefab 의 네트워크 동기화 진입점. Phase B-2.
    ///
    /// 정책:
    ///   - 위치 동기화: <see cref="NetworkTransform.OnIsServerAuthoritative"/> 를 false 로 override 해
    ///     *owner 권위* 동작. 자기 캐릭터의 위치는 자기 클라가 결정하고 다른 측에 broadcast.
    ///   - 비-owner 캐릭터: TDE <see cref="Character.CharacterType"/> 를 AI 로 전환해
    ///     <c>InputManager</c> 가 입력을 보내지 않도록 한다. 결과적으로 자체 이동 0, 위치는
    ///     NetworkTransform 가 owner 로부터 받은 값으로만 갱신.
    ///   - owner 캐릭터: <see cref="LocalPlayerResolver.Register"/> 호출해 UI/카메라가
    ///     "내 플레이어" 를 식별 가능하게 한다.
    ///
    /// 부착 위치: 플레이어 prefab 의 *루트* (NetworkObject 와 같은 GameObject).
    /// 권장 컴포넌트 순서: NetworkObject → (TDE Character 외) → PlayerMovementSync.
    ///
    /// 후속 Phase B 에서 추가될 컴포넌트와의 관계:
    ///   - B-3 KhiPlayerStateNetSync: 행동 상태 broadcast. 본 컴포넌트와 *별도* GameObject 부착 가능.
    ///   - B-4 MeleeHitboxNetRouter: hitbox 자식 오브젝트에 부착. 본 컴포넌트와 무관.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Networking/Player Movement Sync")]
    public sealed class PlayerMovementSync : NetworkTransform
    {
        [Header("Optional Refs (Awake 자동 해석)")]
        [SerializeField] private Character character;
        [SerializeField] private KhiPlayerStateAggregator stateAggregator;

        [Header("Behavior")]
        [SerializeField, Tooltip("비-owner 캐릭터의 Character.CharacterType 을 AI 로 전환할지. " +
            "false 로 두면 InputManager 의 입력이 양쪽 캐릭터 모두에 적용되어 동작 이상 발생 가능.")]
        private bool convertNonOwnerToAi = true;

        [SerializeField, Tooltip("비-owner 캐릭터의 마우스/InputAction 직접 의존 컴포넌트(KhiMeleeCombo/Parry/Dash/FinisherLunge/WeaponPresenter)를 비활성화할지. " +
            "Phase B-2 임시 처리. Phase B-3 에서 owner 가 행동 상태를 broadcast 하면 비-owner 측 시각 효과 reproduce 로 보강 예정.")]
        private bool disableInputComponentsOnNonOwner = true;

        // NetworkTransform default = server authoritative. 우리는 owner 권위.
        protected override bool OnIsServerAuthoritative() => false;

        protected override void Awake()
        {
            base.Awake();
            ResolveRefs();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ResolveRefs();

            if (IsOwner)
            {
                AlignToSpawnPoint();

                // 씬 전환 시 PlayerObject 는 DontDestroyOnLoad 라 살아남고 position 유지 →
                // 새 씬에 진입할 때마다 active scene 의 spawn point 로 재정렬.
                SceneManager.activeSceneChanged += HandleActiveSceneChanged;

                if (stateAggregator != null)
                {
                    LocalPlayerResolver.Register(stateAggregator);
                }
                NetLog.Info("Player",
                    $"Local player spawned. ClientId={NetworkManager.LocalClientId} " +
                    $"OwnerClientId={OwnerClientId}", this);
            }
            else
            {
                if (convertNonOwnerToAi && character != null)
                {
                    character.CharacterType = Character.CharacterTypes.AI;
                }
                if (disableInputComponentsOnNonOwner)
                {
                    DisableInputComponentsForNonOwner();
                }
                NetLog.Info("Player",
                    $"Remote player spawned. OwnerClientId={OwnerClientId} " +
                    $"LocalClientId={NetworkManager.LocalClientId}", this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
                if (stateAggregator != null)
                {
                    LocalPlayerResolver.Unregister(stateAggregator);
                }
            }
            base.OnNetworkDespawn();
        }

        private void HandleActiveSceneChanged(Scene previous, Scene current)
        {
            if (!IsOwner) return;
            AlignToSpawnPoint();
        }

        private void ResolveRefs()
        {
            if (character == null) character = GetComponent<Character>();
            if (stateAggregator == null) stateAggregator = GetComponent<KhiPlayerStateAggregator>();
        }

        /// <summary>
        /// owner 측 spawn 직후 Tag "Respawn" 인 GameObject 위치로 align.
        /// NetworkTransform 이 owner-authoritative 라 다음 frame 에 다른 클라에도 자동 broadcast.
        /// Tag "Respawn" 없으면 align skip (Town_solo / Test_MultiLobby 처럼 spawn 위치 신경 안 쓰는 씬).
        /// </summary>
        private void AlignToSpawnPoint()
        {
            GameObject spawnPoint = GameObject.FindGameObjectWithTag("Respawn");
            if (spawnPoint == null) return;
            transform.position = spawnPoint.transform.position;
        }

        private void DisableInputComponentsForNonOwner()
        {
            DisableIfPresent<KhiMeleeComboController>();
            DisableIfPresent<KhiParryController>();
            DisableIfPresent<KhiDashController>();
            DisableIfPresent<KhiFinisherLunge>();
            // KhiWeaponPresenter 는 비활성하지 않음 — Update 에서 KhiPlayerAim.GetAimDirection()
            // (NetworkVariable sync 값) 받아 무기 회전 적용. non-owner 측에서도 무기 위치/방향이
            // owner 의 마우스 방향을 따라가도록 한다 (Bug #22 의 일부).
        }

        private void DisableIfPresent<T>() where T : MonoBehaviour
        {
            T component = GetComponent<T>();
            if (component != null) component.enabled = false;
        }

        private void DisableInChildrenIfPresent<T>() where T : MonoBehaviour
        {
            T[] components = GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                components[i].enabled = false;
            }
        }
    }
}
