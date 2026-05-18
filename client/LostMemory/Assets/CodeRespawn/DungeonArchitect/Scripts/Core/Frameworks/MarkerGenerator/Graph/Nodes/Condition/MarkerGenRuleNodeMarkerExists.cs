//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using DungeonArchitect.Graphs;

namespace DungeonArchitect.MarkerGenerator.Nodes.Condition
{
    public class MarkerGenRuleNodeMarkerExists : MarkerGenRuleGraphNodeConditionBase
    {
        public string markerName = "";
        public override string Title => "Marker Exists: " + (string.IsNullOrEmpty(markerName) ? "<NONE>" : markerName);
        protected override void CreateDefaultPins()
        {
            CreateOutputPin("");
        }
    }
}