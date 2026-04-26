//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using DungeonArchitect.Graphs;
using UnityEngine;

namespace DungeonArchitect.MarkerGenerator.Pins
{
    public abstract class MarkerGenRuleGraphPin : GraphPin
    {
        public string text = "";
        
        public virtual Color GetPinColor()
        {
            return Color.white;
        }

    }
}