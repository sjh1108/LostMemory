//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//
namespace DungeonArchitect.Builders.Grid
{
    /// <summary>
    /// Platform volumes add a platform in the scene encompassing the volume
    /// </summary>
    public class PlatformVolume : Volume
    {
        public CellType cellType = CellType.Corridor;
        public bool lockHeight = false;
    }
}
