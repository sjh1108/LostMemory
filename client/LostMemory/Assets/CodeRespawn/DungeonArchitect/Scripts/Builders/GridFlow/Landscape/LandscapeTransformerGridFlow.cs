//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//
using System.Collections;
using System.Collections.Generic;
using DungeonArchitect.Flow.Domains.Layout;
using DungeonArchitect.Landscape;
using UnityEditor;
using UnityEngine;
using MathUtils = DungeonArchitect.Utils.MathUtils;

namespace DungeonArchitect.Builders.GridFlow.Landscape
{
    /// <summary>
    /// The type of the texture defined in the landscape paint settings.  
    /// This determines how the specified texture would be painted in the modified terrain
    /// </summary>
    public enum LandscapeTextureType
    {
        Room,
        Cliff
    }

    /// <summary>
    /// Data-structure to hold the texture settings.  This contains enough information to paint the texture 
    /// on to the terrain
    /// </summary>
    [System.Serializable]
    public class LandscapeTexture
    {
        public LandscapeTextureType textureType;
        public TerrainLayer terrainLayer;
    }

    
    public class LandscapeTransformerGridFlow : LandscapeTransformerBase
    {
        public LandscapeTexture[] textures;
        
        // The offset to apply on the terrain at the rooms and corridors. 
        // If 0, then it would touch the rooms / corridors so players can walk over it
        // Give a negative value if you want it to be below it (e.g. if you already have a ground mesh supported by pillars standing on this terrain)
        public float layoutLevelOffset = 0;

        public int smoothingDistance = 5;
        public AnimationCurve roomElevationCurve;
        public int roadBlurDistance = 6;
        public float roomBlurThreshold = 0.5f;
        
        private Vector3 chunkSize = Vector3.zero;
        private HashSet<Vector3Int> nodesToRasterize = new HashSet<Vector3Int>();
        private Vector3Int min = Vector3Int.zero;
        private Vector3Int max = Vector3Int.zero;
        private IntVector[] terrainBases;
    }
}