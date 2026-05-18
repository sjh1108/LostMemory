//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using UnityEngine;

namespace DungeonArchitect.MarkerGenerator.Nodes.Actions.Info
{
    public class MarkerGenRuleActionInfoAddMarker : MarkerGenRuleActionInfo
    {
        public string markerName = "";
        public string[] copyRotationFromMarkers;
        public string[] copyHeightFromMarkers;
    }
}