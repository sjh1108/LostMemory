using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
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
        [SerializeField] private bool logFlow = true;

        public bool IsOpen { get; private set; }

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

            IsOpen = true;
            panel.gameObject.SetActive(true);
            panel.OnOpen();
            SuppressPlayerControls();

            if (logFlow) Debug.Log("[FriendChatController] Opened.");
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
