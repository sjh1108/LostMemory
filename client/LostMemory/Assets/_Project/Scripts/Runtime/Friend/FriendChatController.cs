using LostMemory.Networking.Common;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Friend
{
    /// <summary>
    /// 소꿉친구 채팅 패널의 오케스트레이터.
    ///
    /// 책임:
    ///   1. Open()  — 채팅 패널 활성화 + 플레이어 이동/전투 봉쇄
    ///   2. Close() — 채팅 패널 비활성화 + 플레이어 조작 복원
    ///   3. ESC 키 닫기
    ///
    /// [초심자 설명]
    ///   ShopController 와 동일한 "오케스트레이터" 역할입니다.
    ///   NPC 에서 F 를 누르면 이 컴포넌트의 Toggle() 이 호출되고,
    ///   채팅창을 열면서 플레이어가 이동/공격/대쉬/패링을 못 하게 막습니다.
    ///   채팅창을 닫으면 모든 조작이 복원됩니다.
    ///
    /// Inspector 연결 필수:
    ///   panel              — FriendChatPanelView 가 붙은 GameObject
    ///   playerMovement     — 씬의 CharacterMovement
    ///   playerAim          — 씬의 KhiPlayerAim
    ///   playerWeaponPresenter — 씬의 KhiWeaponPresenter
    ///   playerMeleeCombo   — 씬의 KhiMeleeComboController
    ///   playerDash         — 씬의 KhiDashController
    ///   playerParry        — 씬의 KhiParryController
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Friend/Friend Chat Controller")]
    public sealed class FriendChatController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private FriendChatPanelView panel;

        [Header("Player Controls (씬의 플레이어 컴포넌트를 Inspector 에서 드래그)")]
        [SerializeField] private CharacterMovement playerMovement;
        [SerializeField] private KhiPlayerAim playerAim;
        [SerializeField] private KhiWeaponPresenter playerWeaponPresenter;
        [SerializeField] private KhiMeleeComboController playerMeleeCombo;
        [SerializeField] private KhiDashController playerDash;
        [SerializeField] private KhiParryController playerParry;

        [Header("Debug")]
        [SerializeField] private bool logFlow = false;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            // 멀티에서 host 만 채팅 가능. guest 는 컨트롤러 자체를 비활성화해
            // OnEnable/Update 가 호출되지 않게 한다.
            // HostAuthority.IsHost — NetworkManager 가 없거나 비활성(=싱글 실행) 이면 true.
            if (!HostAuthority.IsHost)
            {
                if (logFlow) Debug.Log("[FriendChatController] guest — 비활성화.");
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            if (panel != null) panel.OnCloseRequested += Close;
        }

        private void OnDisable()
        {
            if (panel != null) panel.OnCloseRequested -= Close;
            // panic restore — 패널 떠있는 채 disable 시 조작 봉쇄 잠금 방지.
            if (IsOpen)
            {
                RestorePlayerControls();
                IsOpen = false;
            }
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                if (logFlow) Debug.Log("[FriendChatController] ESC → Close.");
                Close();
            }
        }

        public void Open()
        {
            if (IsOpen) return;
            if (panel == null)
            {
                Debug.LogError("[FriendChatController] panel 이 null. Inspector 에서 연결하세요.", this);
                return;
            }

            // 멀티: 인스펙터에 플레이어 컴포넌트가 비어 있으면 런타임에 host 의 PlayerObject 에서 찾는다.
            // 싱글/Town.unity: 인스펙터로 이미 채워져 있으면 skip — 기존 동작 보존.
            ResolvePlayerComponentsIfNeeded();

            IsOpen = true;
            panel.gameObject.SetActive(true);
            panel.OnOpen();
            SuppressPlayerControls();

            if (logFlow) Debug.Log("[FriendChatController] Opened.");
        }

        /// <summary>
        /// 인스펙터로 와이어된 플레이어 컴포넌트가 없으면 NGO LocalClient.PlayerObject 에서 찾아 채운다.
        /// 멀티 환경에서 플레이어가 런타임에 스폰되는 경우를 위한 lazy 바인딩.
        /// 한 번 채워지면 이후 호출에서는 skip.
        /// </summary>
        private void ResolvePlayerComponentsIfNeeded()
        {
            // 이미 인스펙터로 채워졌으면 skip (싱글/Town 회귀 안전).
            if (playerMovement != null) return;

            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening)
            {
                // 싱글 실행인데 인스펙터도 비어 있으면 봉쇄 동작이 안 되지만 패널 자체는 열림.
                if (logFlow) Debug.LogWarning("[FriendChatController] 싱글 실행 + 인스펙터 플레이어 ref 비어있음 — 플레이어 봉쇄 skip.");
                return;
            }

            NetworkClient local = nm.LocalClient;
            Transform root = local?.PlayerObject?.transform;
            if (root == null)
            {
                Debug.LogWarning("[FriendChatController] LocalClient.PlayerObject 미스폰 — 플레이어 컴포넌트 바인딩 실패.");
                return;
            }

            playerMovement       = root.GetComponentInChildren<CharacterMovement>(true);
            playerAim            = root.GetComponentInChildren<KhiPlayerAim>(true);
            playerWeaponPresenter = root.GetComponentInChildren<KhiWeaponPresenter>(true);
            playerMeleeCombo     = root.GetComponentInChildren<KhiMeleeComboController>(true);
            playerDash           = root.GetComponentInChildren<KhiDashController>(true);
            playerParry          = root.GetComponentInChildren<KhiParryController>(true);

            if (logFlow)
            {
                Debug.Log($"[FriendChatController] 런타임 바인딩 — move={playerMovement != null} " +
                          $"aim={playerAim != null} weapon={playerWeaponPresenter != null} " +
                          $"melee={playerMeleeCombo != null} dash={playerDash != null} parry={playerParry != null}");
            }
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            panel.gameObject.SetActive(false);
            RestorePlayerControls();

            if (logFlow) Debug.Log("[FriendChatController] Closed.");
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        // ShopController.SuppressPlayerControls 와 동일 패턴
        private void SuppressPlayerControls()
        {
            if (playerMovement != null)       playerMovement.MovementForbidden = true;
            if (playerAim != null)            playerAim.enabled = false;
            if (playerWeaponPresenter != null) playerWeaponPresenter.enabled = false;
            if (playerMeleeCombo != null)     playerMeleeCombo.ExternalBlock = true;
            if (playerDash != null)           playerDash.PermitAbility(false);
            if (playerParry != null)          playerParry.ExternalBlock = true;
        }

        private void RestorePlayerControls()
        {
            if (playerMovement != null)       playerMovement.MovementForbidden = false;
            if (playerAim != null)            playerAim.enabled = true;
            if (playerWeaponPresenter != null) playerWeaponPresenter.enabled = true;
            if (playerMeleeCombo != null)     playerMeleeCombo.ExternalBlock = false;
            if (playerDash != null)           playerDash.PermitAbility(true);
            if (playerParry != null)          playerParry.ExternalBlock = false;
        }
    }
}
