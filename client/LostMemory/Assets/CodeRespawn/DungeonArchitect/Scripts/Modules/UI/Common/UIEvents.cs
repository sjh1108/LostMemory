//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//
using UnityEngine;

namespace DungeonArchitect.UI.Widgets
{
    public class WidgetClickEvent
    {
        public Event e;
        public UISystem uiSystem;
        public object userdata;
    }
    public delegate void OnWidgetClicked(WidgetClickEvent clickEvent);

}
