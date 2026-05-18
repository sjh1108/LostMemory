//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using DungeonArchitect.Graphs;
using UnityEngine;

namespace DungeonArchitect.MarkerGenerator.Pins
{
    public class MarkerGenRuleGraphPinExec : MarkerGenRuleGraphPin
    {

        public override Color GetPinColor()
        {
            if (ClickState == GraphPinMouseState.Hover)
            {
                return new Color(0.5f, 1.0f, 1.0f);
            }
            else if (ClickState == GraphPinMouseState.Clicked)
            {
                return new Color(1.0f, 1.0f, 1.0f);
            }
            
            return Color.white;
        }
    }
}