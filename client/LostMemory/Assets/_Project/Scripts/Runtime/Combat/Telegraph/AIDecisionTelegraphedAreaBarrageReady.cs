using MoreMountains.Tools;
using UnityEngine;

namespace LostMemory.Combat.Telegraph
{
    [AddComponentMenu("Lost Memory/Combat/Telegraph/AI Decision Telegraphed Area Barrage Ready")]
    public sealed class AIDecisionTelegraphedAreaBarrageReady : AIDecision
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
            return barrageAction != null && barrageAction.CanStartFromCurrentTarget();
        }

        private void RefreshReferences()
        {
            barrageAction ??= GetComponent<AIActionTelegraphedAreaBarrage>();
        }
    }
}
