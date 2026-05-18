//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//
using DungeonArchitect.Graphs;
using UnityEngine;

namespace DungeonArchitect.Flow.Domains.Tilemap.Tooling
{
    public class FlowTilemapToolGraph : Graph
    {
        public override void OnEnable()
        {
            base.OnEnable();

            hideFlags = HideFlags.HideInHierarchy;
        }

    }
}
