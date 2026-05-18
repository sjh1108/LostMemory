//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using DungeonArchitect.Graphs;
using DungeonArchitect.MarkerGenerator.Nodes.Actions;
using DungeonArchitect.MarkerGenerator.Nodes.Condition;
using UnityEngine;

namespace DungeonArchitect.MarkerGenerator
{
    public class MarkerGenRuleGraph : Graph
    {
        [SerializeField]
        public MarkerGenRuleNodeResult resultNode;
        
        [SerializeField]
        public MarkerGenRuleNodeOnPass passNode;
        
        public override void OnEnable()
        {
            base.OnEnable();

            hideFlags = HideFlags.HideInHierarchy;
            
            
        }
    }
}