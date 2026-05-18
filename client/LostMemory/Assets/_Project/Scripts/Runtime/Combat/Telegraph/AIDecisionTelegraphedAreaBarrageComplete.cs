using MoreMountains.Tools;
using UnityEngine;

namespace LostMemory.Combat.Telegraph
{
    [AddComponentMenu("Lost Memory/Combat/Telegraph/AI Decision Telegraphed Area Barrage Complete")]
    public sealed class AIDecisionTelegraphedAreaBarrageComplete : AIDecision
    {
        [SerializeField] private AIActionTelegraphedAreaBarrage barrageAction;

        private void Reset()
        {
            RefreshReferences();
        }

        protected override void Awake()
        {
            base.Awake();
            RefreshReferences();
        }

        public override void Initialization()
        {
            base.Initialization();
            RefreshReferences();
        }

        public override bool Decide()
        {
            RefreshReferences();
            return barrageAction != null && barrageAction.HasCompletedBarrage;
        }

        private void RefreshReferences()
        {
            barrageAction ??= GetComponent<AIActionTelegraphedAreaBarrage>();
        }
    }
}
