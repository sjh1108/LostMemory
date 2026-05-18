using LostMemory.Data;
using LostMemory.TestKhi;
using Unity.Netcode;
using UnityEngine;

namespace LostMemory.Networking.Player
{
    /// <summary>
    /// 공격 시각 효과 (KhiAttackVisualPresenter / KhiSlashAnimator / KhiWeaponPresenter) 가 의존하는
    /// KhiMeleeComboController 의 AttackStarted / AttackActiveStarted / AttackActiveEnded 이벤트를
    /// owner 측에서 ClientRpc 로 broadcast → non-owner 측이 같은 핸들러를 직접 호출해 시각 효과를 재현.
    ///
    /// 배경 (Bug #19):
    ///   PlayerMovementSync 는 non-owner 측에서 KhiMeleeComboController 를 disable → 위 3개 이벤트
    ///   발화 자체가 막힘. NetworkAnimator 만으로는 Animator state 만 sync 되고 KhiSlashAnimator /
    ///   KhiWeaponPresenter / KhiAttackVisualPresenter 의 sprite/swing/vfx 코루틴 기반 시각 효과는
    ///   비동기됨. 본 컴포넌트가 그 격차를 메움.
    ///
    /// 의존 (클라팀 합의 완료 — private → public 변경):
    ///   - KhiAttackVisualPresenter.HandleAttackStarted(KhiAttackRequest, AttackStepData)
    ///   - KhiSlashAnimator.HandleAttackActiveStarted(KhiAttackRequest, AttackStepData)
    ///   - KhiWeaponPresenter.HandleAttackActiveStarted(KhiAttackRequest, AttackStepData)
    ///   - KhiWeaponPresenter.HandleAttackActiveEnded(KhiAttackRequest, AttackStepData)
    ///
    /// 부착 위치: 플레이어 prefab 의 root (NetworkObject 와 같은 GameObject).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Networking/Attack Broadcast")]
    public sealed class AttackBroadcast : NetworkBehaviour
    {
        [Header("Refs (OnNetworkSpawn 자동 해석)")]
        [SerializeField] private KhiMeleeComboController comboController;
        [SerializeField] private KhiAttackVisualPresenter attackVisualPresenter;
        [SerializeField] private KhiSlashAnimator slashAnimator;
        [SerializeField] private KhiWeaponPresenter weaponPresenter;
        [SerializeField] private WeaponData weaponData;

        private enum AttackEvent : byte
        {
            Started = 0,
            ActiveStarted = 1,
            ActiveEnded = 2,
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ResolveRefs();

            if (IsOwner && comboController != null)
            {
                comboController.AttackStarted += OnAttackStartedOwner;
                comboController.AttackActiveStarted += OnAttackActiveStartedOwner;
                comboController.AttackActiveEnded += OnAttackActiveEndedOwner;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && comboController != null)
            {
                comboController.AttackStarted -= OnAttackStartedOwner;
                comboController.AttackActiveStarted -= OnAttackActiveStartedOwner;
                comboController.AttackActiveEnded -= OnAttackActiveEndedOwner;
            }
            base.OnNetworkDespawn();
        }

        private void ResolveRefs()
        {
            if (comboController == null) comboController = GetComponent<KhiMeleeComboController>();
            if (attackVisualPresenter == null) attackVisualPresenter = GetComponent<KhiAttackVisualPresenter>();
            if (slashAnimator == null) slashAnimator = GetComponent<KhiSlashAnimator>();
            if (weaponPresenter == null) weaponPresenter = GetComponentInChildren<KhiWeaponPresenter>(true);
            if (weaponData == null && comboController != null) weaponData = comboController.WeaponData;
        }

        private void OnAttackStartedOwner(KhiAttackRequest req, AttackStepData step)
        {
            RelayAttackEventServerRpc(AttackEvent.Started, req.ComboStep, req.AimDirection, req.AimAngleDegrees, req.Origin, req.SequenceId);
        }

        private void OnAttackActiveStartedOwner(KhiAttackRequest req, AttackStepData step)
        {
            RelayAttackEventServerRpc(AttackEvent.ActiveStarted, req.ComboStep, req.AimDirection, req.AimAngleDegrees, req.Origin, req.SequenceId);
        }

        private void OnAttackActiveEndedOwner(KhiAttackRequest req, AttackStepData step)
        {
            RelayAttackEventServerRpc(AttackEvent.ActiveEnded, req.ComboStep, req.AimDirection, req.AimAngleDegrees, req.Origin, req.SequenceId);
        }

        /// <summary>
        /// owner → server 중계. ClientRpc 는 server 만 발화 가능하므로 게스트 owner 가 직접 broadcast 못 함 → 본 ServerRpc 거쳐 server 가 broadcast.
        /// </summary>
        [ServerRpc]
        private void RelayAttackEventServerRpc(
            AttackEvent eventType,
            int comboStep,
            Vector2 aimDirection,
            float aimAngleDegrees,
            Vector3 origin,
            int sequenceId)
        {
            BroadcastAttackEventClientRpc(eventType, comboStep, aimDirection, aimAngleDegrees, origin, sequenceId);
        }

        [ClientRpc]
        private void BroadcastAttackEventClientRpc(
            AttackEvent eventType,
            int comboStep,
            Vector2 aimDirection,
            float aimAngleDegrees,
            Vector3 origin,
            int sequenceId)
        {
            // owner 는 이미 로컬 이벤트 핸들러를 통해 시각 효과 처리. 중복 방지.
            if (IsOwner) return;

            AttackStepData step = ResolveStep(comboStep);
            if (step == null) return;

            KhiAttackRequest req = new KhiAttackRequest
            {
                SequenceId = sequenceId,
                ComboStep = comboStep,
                AimDirection = aimDirection,
                AimAngleDegrees = aimAngleDegrees,
                Origin = origin,
                StartedAt = Time.time,
                Attacker = gameObject,
            };

            switch (eventType)
            {
                case AttackEvent.Started:
                    if (attackVisualPresenter != null)
                    {
                        attackVisualPresenter.HandleAttackStarted(req, step);
                    }
                    break;
                case AttackEvent.ActiveStarted:
                    if (slashAnimator != null)
                    {
                        slashAnimator.HandleAttackActiveStarted(req, step);
                    }
                    if (weaponPresenter != null)
                    {
                        weaponPresenter.HandleAttackActiveStarted(req, step);
                    }
                    break;
                case AttackEvent.ActiveEnded:
                    if (weaponPresenter != null)
                    {
                        weaponPresenter.HandleAttackActiveEnded(req, step);
                    }
                    break;
            }
        }

        private AttackStepData ResolveStep(int comboStep)
        {
            if (weaponData == null || weaponData.Steps == null || weaponData.Steps.Length == 0)
            {
                return null;
            }
            int index = Mathf.Clamp(comboStep - 1, 0, weaponData.Steps.Length - 1);
            return weaponData.Steps[index];
        }
    }
}
