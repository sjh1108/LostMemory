//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using DungeonArchitect.Graphs;

namespace DungeonArchitect.MarkerGenerator.Nodes.Condition
{
    public class MarkerGenRuleNodeNot : MarkerGenRuleGraphNodeConditionBase
    {
        public override string Title => "NOT";

        protected override void CreateDefaultPins()
        {
            CreateInputPin("");
            CreateOutputPin("");
        }
    }
}