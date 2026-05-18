using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat.Telegraph
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Telegraph/Tracking Telegraphed Melee Attack Controller")]
    public sealed class TrackingTelegraphedMeleeAttackController : MonoBehaviour
    {
        [SerializeField] private TelegraphedAreaAttackController attackController;
        [SerializeField] private AIBrain brain;
        [SerializeField] private bool trackTargetX = true;
        [SerializeField] private bool trackTargetY = true;
        [SerializeField] private bool trackAttackDirection = true;
        [SerializeField, Min(0f)] private float trackingDuration = 0.45f;

        private bool _wasInTelegraphState;
        private float _elapsed;

        private void Reset()
        {
            RefreshReferences();
        }

        private void Awake()
        {
            RefreshReferences();
        }

        private void OnValidate()
        {
            trackingDuration = Mathf.Max(0f, trackingDuration);
            RefreshReferences();
        }

        private void Update()
        {
            if (attackController == null)
            {
                return;
            }

            bool isInTelegraphState = IsInTelegraphState();
            if (!isInTelegraphState)
            {
                _wasInTelegraphState = false;
                _elapsed = 0f;
                return;
            }

            if (!_wasInTelegraphState)
            {
                _wasInTelegraphState = true;
                _elapsed = 0f;
            }

            if (_elapsed <= trackingDuration)
            {
                attackController.TrackLockedAttackToTarget(
                    trackTargetX,
                    trackTargetY,
                    trackAttackDirection);
            }

            _elapsed += Time.deltaTime;
        }

        public void RefreshReferences()
        {
            attackController ??= GetComponent<TelegraphedAreaAttackController>();
            brain ??= GetComponent<AIBrain>();
        }

        private bool IsInTelegraphState()
        {
            return brain != null
                && brain.CurrentState != null
                && brain.CurrentState.StateName == attackController.TelegraphStateName;
        }
    }
}
