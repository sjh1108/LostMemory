//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using UnityEngine;

namespace DungeonArchitect.Common
{
    public class DungeonBillboardGizmo : MonoBehaviour
    {
        public string icon = "CodeRespawn/DungeonArchitect/default_gizmo_icon.png";
        public float scale = 1.0f;
        public Color tint = Color.white;
        void OnDrawGizmos()
        {
            Gizmos.DrawIcon(transform.position, icon, true, tint);
        }
    }
}