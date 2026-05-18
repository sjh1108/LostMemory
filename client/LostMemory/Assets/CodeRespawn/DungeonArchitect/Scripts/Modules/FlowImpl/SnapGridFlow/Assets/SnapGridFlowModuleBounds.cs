//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//
using UnityEngine;

namespace DungeonArchitect.Flow.Impl.SnapGridFlow
{
    public enum SnapGridFlowModuleBoundsMode
    {
        Mode3D,
        Mode2D
    }
    
    [System.Serializable]
    public class SnapGridFlowModuleBounds : ScriptableObject
    {
        [Tooltip("Is this for 3D or 2D tilemaps. The visual guides will change depending on the mode")]
        public SnapGridFlowModuleBoundsMode mode = SnapGridFlowModuleBoundsMode.Mode3D;
        
        [Tooltip("The world size of a module chunk (1x1x1).  A module can span multiple chunks (e.g 2x2x1)")]
        public Vector3 chunkSize = new Vector3(40, 20, 40);

        [Tooltip("The world size of a module chunk (1x1) in unity units.  A module can span multiple chunks (e.g 2x2x1)")]
        public Vector2 chunkSize2D = new Vector2(16, 12);
        
        [Tooltip("How high do you want the door to be from the lower bounds. This will create a door visual indicator on the bounds actor, aiding your while designing your modules.  This is used for preview only")]
        public float doorOffsetY = 5;

        [Tooltip("The color of the bounds wireframe. Use this bounds as a reference while designing your module prefabs.  This is used for preview only")]
        public Color boundsColor = Color.red;

        [Tooltip("The color of the Door Info. Use this align the doors in your module prefabs.  This is used for preview only")]
        public Color doorColor = Color.blue;

        [Tooltip("Specifies how big the blue door marker visuals will be.  This is used for preview only")]
        public float doorDrawSize = 4;
    }
}