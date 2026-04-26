//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonArchitect.Builders.Grid.Stairs.Impl.V2;
using DungeonArchitect.Utils;

namespace DungeonArchitect.Builders.Grid.Stairs.Impl
{
    namespace V2 {
        public class FIslandBoundaryEdge {
            public int OwningCellId = -1;
            public int RemoteCellId = -1;
        };
    
        public class FIslandBoundary
        {
            public List<FIslandBoundaryEdge> Edges = new List<FIslandBoundaryEdge>();
        };

        public class FIsland
        {
            public readonly List<int> CellIds = new List<int>();
            public readonly Dictionary<int, FIslandBoundary> Boundaries = new Dictionary<int, FIslandBoundary>(); // Boundaries to other islands [Remote island id . boundary info]
            public readonly HashSet<Vector2Int> RasterizedTileCoords = new HashSet<Vector2Int>();
            public int Y = 0;
            public bool HeightClamped = false;
        };

        public class FIslandLookup {
            public Dictionary<int, int> CellToIslandMap = new Dictionary<int, int>();
        }
        
            
    }
    public class GridStairSystemV2 : GridStairSystemBase
    {
        /// <summary>
        /// Having stairs inside rooms would mis-align the room walls. Check this flag if you don't want it
        /// </summary>
        public bool avoidStairsInsideRooms = true;

        public bool avoidSingleCellStairDeadEnds = true;
        
        public override void Generate(GridDungeonModel model, GridDungeonBuilder builder, GridDungeonConfig config)
        {
            if (model == null || builder == null || config == null)
            {
                return;
            }

            GenerateDungeonHeights(model, config.Seed, false);
    
            ConnectStairs(model, builder, config.GridCellSize);
        }
        
        class FGridDungeonStairCandidate {
            public StairInfo StairInfo = new StairInfo();
            public Vector3Int TileCoord = new Vector3Int();
            public Vector3Int RemoteTileCoord = new Vector3Int();
            public Action FuncRegisterDisallowedStairStates = () => { };
            public List<Vector2Int> CoordsToOccupy = new List<Vector2Int>();
        };

        protected virtual void ConnectStairs(GridDungeonModel GridModel, GridDungeonBuilder GridBuilder, Vector3 GridToMeshScale)
        {
             if (GridModel == null || GridBuilder == null || GridModel.Cells.Count == 0) return;

            const int MAX_ITERATIONS = 100;
            int numIterations = 0;
            bool bContinueIteration = true;
            while (bContinueIteration && numIterations++ < MAX_ITERATIONS) {
                // Generate the islands
                List<FIsland> islands = new List<FIsland>();
                FIslandLookup islandLookup = null;
                GenerateIslands(GridModel, islands, ref islandLookup);
                
                GridModel.CellStairs.Clear();

                List<FIslandBoundaryEdge> edgesToFixOnSameHeight = new List<FIslandBoundaryEdge>();
            
                Func<Vector2Int, Vector2Int, int> funcGetStairOrientation = (tileCoord, remoteCoord) =>
                {
                    // 0: Up       (0, 1)
                    // 1: Right    (1, 0)
                    // 2: Down     (0,-1)
                    // 3: Left     (-1,0)
                    var dir = remoteCoord - tileCoord;
                    if (dir.x == 0 && dir.y == 1) return 0; // Up
                    if (dir.x == 1 && dir.y == 0) return 1; // Right
                    if (dir.x == 0 && dir.y == -1) return 2; // Down
                    if (dir.x == -1 && dir.y == 0) return 3; // Left
                    Debug.Assert(false);    // Should not come here
                    return 0;
                };
                
                
                //HashSet<Vector2Int> ForceOccupiedCell = new HashSet<Vector2Int>();
                Dictionary<Vector2Int, HashSet<int>> disallowedCellStairOrientations = new Dictionary<Vector2Int, HashSet<int>>();
                Func<Vector2Int, int, bool> isStairOrientationAllowed = (tileCoord, orientation) =>
                {
                    if (disallowedCellStairOrientations.TryGetValue(tileCoord, out HashSet<int> disallowedOrientations))
                    {
                        return !disallowedOrientations.Contains(orientation);
                    }

                    return true;
                };
                
                Action<Vector2Int, int> registerDisallowedOrientation = (tileCoord, orientation) =>
                {
                    if (!disallowedCellStairOrientations.TryGetValue(tileCoord, out HashSet<int> orientations))
                    {
                        orientations = new HashSet<int>();
                        disallowedCellStairOrientations[tileCoord] = orientations;
                    }
                    orientations.Add(orientation);
                };

                Action<Vector2Int> registerDisallowedOrientationOnAllSides = (tileCoord) =>
                {
                    registerDisallowedOrientation(tileCoord, 0);
                    registerDisallowedOrientation(tileCoord, 1);
                    registerDisallowedOrientation(tileCoord, 2);
                    registerDisallowedOrientation(tileCoord, 3);
                };
                
                Action<Vector2Int, int> registerDisallowedOrientationOnPerpendicularSides = (tileCoord, orientation) =>
                {
                    registerDisallowedOrientation(tileCoord, (orientation + 1) % 4);
                    registerDisallowedOrientation(tileCoord, (orientation + 3) % 4);
                };

                foreach (FIsland island in islands) {
                    foreach (var entry in island.Boundaries) {
                        int adjacentIslandIdx = entry.Key;
                        FIsland adjacentIsland = islands[adjacentIslandIdx];
                        if (island.Y < adjacentIsland.Y) {
                            FIslandBoundary boundary = entry.Value;

                            foreach (FIslandBoundaryEdge boundaryEdge in boundary.Edges) {
                                Cell owningCell = GridModel.GetCell(boundaryEdge.OwningCellId);
                                Cell remoteCell = GridModel.GetCell(boundaryEdge.RemoteCellId);
                                if (owningCell == null || remoteCell == null) {
                                    continue;
                                }
                            
                                List<FGridDungeonStairCandidate> boundaryStairCandidates = new List<FGridDungeonStairCandidate>();
                                List<FIslandBoundaryEdge> invalidStairEdges = new List<FIslandBoundaryEdge>();

                                Action<Rectangle, float, IAxisDomain, IAxisDomain> ProcessIntersection = (Intersection, InRotationOffset, PrimaryAxis, SecondaryAxis) =>
                                {
                                    Func<int, int, Vector2Int> MakeCoord = (PrimaryValue, SecondaryValue) =>
                                    {
                                        Vector2Int Coord = new Vector2Int();
                                        PrimaryAxis.Set(ref Coord, PrimaryValue);
                                        SecondaryAxis.Set(ref Coord, SecondaryValue);
                                        return Coord;
                                    };

                                    Func<int, int, int, Vector3Int> MakeCoord3 = (PrimaryValue, SecondaryValue, Y) =>
                                    {
                                        Vector3Int Coord = new Vector3Int();
                                        PrimaryAxis.Set(ref Coord, PrimaryValue);
                                        SecondaryAxis.Set(ref Coord, SecondaryValue);
                                        Coord.y = Y;
                                        return Coord;
                                    };

                                    bool bConnectsToRoom = (owningCell.CellType == CellType.Room || remoteCell.CellType == CellType.Room);
                                    for (int D = 0; D < PrimaryAxis.Get(Intersection.Size); D++)
                                    {
                                        float RotationOffset = InRotationOffset;
                                        bool bRemoteCellAfter = SecondaryAxis.Get(remoteCell.Bounds.Location) > SecondaryAxis.Get(owningCell.Bounds.Location);

                                        Vector3Int TileCoord = owningCell.Bounds.Location;
                                        PrimaryAxis.Set(ref TileCoord, PrimaryAxis.Get(Intersection.Location) + D);

                                        if (bRemoteCellAfter)
                                        {
                                            TileCoord += MakeCoord3(0, SecondaryAxis.Get(owningCell.Bounds.Size) - 1, 0);
                                        }

                                        Vector3Int RemoteTileCoord = TileCoord;
                                        RemoteTileCoord += bRemoteCellAfter ? MakeCoord3(0, 1, 0) : MakeCoord3(0, -1, 0);

                                        if (bRemoteCellAfter)   
                                        {
                                            RotationOffset += Mathf.PI;
                                        }

                                        bool bStairValid = true;
                                        Action FuncRegisterDisallowedStairStates = () => { };
                                        {
                                            if (Mathf.Abs(owningCell.Bounds.Location.y - remoteCell.Bounds.Location.y) > MaxAllowedStairHeight)
                                            {
                                                bStairValid = false;
                                            }
                                            else if (bConnectsToRoom)
                                            {
                                                if (avoidStairsInsideRooms)
                                                {
                                                    if (owningCell.CellType == CellType.Room)
                                                    {
                                                        bStairValid = false;
                                                    }
                                                }

                                                if (bStairValid)
                                                {
                                                    bool hasDoor = GridModel.DoorManager.ContainsDoor(TileCoord.x, TileCoord.z, RemoteTileCoord.x, RemoteTileCoord.z);
                                                    bStairValid = hasDoor;
                                                }
                                            }

                                            // Discard single cell corridor islands
                                            if (avoidSingleCellStairDeadEnds && remoteCell.CellType != CellType.Room)
                                            {
                                                if (islandLookup.CellToIslandMap.TryGetValue(remoteCell.Id, out int IslandIdx))
                                                {
                                                    if (IslandIdx >= 0 && IslandIdx < islands.Count)
                                                    {
                                                        var RemoteIsland = islands[IslandIdx];
                                                        if (RemoteIsland.RasterizedTileCoords.Count == 1)
                                                        {
                                                            // We don't want to go up to a single cell dead end
                                                            bStairValid = false;
                                                        }
                                                    }
                                                }
                                            }

                                            if (bStairValid)
                                            {
                                                /*
                                                 *  Top floor
                                                 *  _ R _
                                                 *  A S A
                                                 *  B E B
                                                 *
                                                 *  S = Staircase,
                                                 *  A, B, E = tiles near the stair-case
                                                 *  R = Top cell of the stair-case
                                                 *  if A exists, B should exist
                                                 *  E should always exist, either in this island, or on another island in the same height
                                                 */
                                                Vector2Int TileCoord2 = new Vector2Int(TileCoord.x, TileCoord.z);
                                                Vector2Int RemoteTileCoord2 = new Vector2Int(RemoteTileCoord.x, RemoteTileCoord.z);
                                                Vector2Int CoordA0 = TileCoord2 + MakeCoord(-1, 0);
                                                Vector2Int CoordA1 = TileCoord2 + MakeCoord(1, 0);

                                                int DSec = bRemoteCellAfter ? -1 : 1;
                                                Vector2Int CoordB0 = TileCoord2 + MakeCoord(-1, DSec);
                                                Vector2Int CoordB1 = TileCoord2 + MakeCoord(1, DSec);
                                                Vector2Int CoordE = TileCoord2 + MakeCoord(0, DSec);

                                                bool A0 = island.RasterizedTileCoords.Contains(CoordA0);
                                                bool A1 = island.RasterizedTileCoords.Contains(CoordA1);
                                                bool B0 = island.RasterizedTileCoords.Contains(CoordB0);
                                                bool B1 = island.RasterizedTileCoords.Contains(CoordB1);
                                                bool E = island.RasterizedTileCoords.Contains(CoordE);

                                                int stairOrientation = funcGetStairOrientation(TileCoord2, RemoteTileCoord2);
                                                FuncRegisterDisallowedStairStates = () =>
                                                {
                                                    registerDisallowedOrientationOnAllSides(TileCoord2);
                                                    registerDisallowedOrientationOnPerpendicularSides(RemoteTileCoord2, stairOrientation);
                                                    if (E) registerDisallowedOrientationOnPerpendicularSides(CoordE, stairOrientation);
                                                    if (A0) registerDisallowedOrientationOnPerpendicularSides(CoordA0, stairOrientation);
                                                    if (A1) registerDisallowedOrientationOnPerpendicularSides(CoordA1, stairOrientation);
                                                    if (B0) registerDisallowedOrientationOnAllSides(CoordB0);
                                                    if (B1) registerDisallowedOrientationOnAllSides(CoordB1);
                                                };
                                                
                                                if (!isStairOrientationAllowed(TileCoord2, stairOrientation)
                                                    || !isStairOrientationAllowed(RemoteTileCoord2, stairOrientation)
                                                    || !isStairOrientationAllowed(CoordE, stairOrientation))
                                                {
                                                    bStairValid = false;
                                                }

                                                if (!E)
                                                {
                                                    bStairValid = false;
                                                }

                                                if (A0)
                                                {
                                                    if (!B0)
                                                    {
                                                        bStairValid = false;
                                                    }
                                                }
                                                if (A1)
                                                {
                                                    if (!B1)
                                                    {
                                                        bStairValid = false;
                                                    }
                                                }

                                                if (GridModel.DoorManager.ContainsDoor(CoordA0, TileCoord2))
                                                {
                                                    bStairValid = false;
                                                }
                                                if (GridModel.DoorManager.ContainsDoor(CoordA1, TileCoord2))
                                                {
                                                    bStairValid = false;
                                                }
                                            }
                                        }

                                        if (bStairValid)
                                        {
                                            var candidate = new FGridDungeonStairCandidate();
                                            var stair = candidate.StairInfo;
                                            stair.OwnerCell = boundaryEdge.OwningCellId;
                                            stair.ConnectedToCell = boundaryEdge.RemoteCellId;
                                            stair.Position = Vector3.Scale(new Vector3(TileCoord.x + 0.5f, TileCoord.y, TileCoord.z + 0.5f), GridToMeshScale);
                                            stair.Rotation = Quaternion.AngleAxis(RotationOffset * Mathf.Rad2Deg, Vector3.up);
                                            stair.IPosition = TileCoord;

                                            candidate.TileCoord = TileCoord;
                                            candidate.RemoteTileCoord = RemoteTileCoord;
                                            candidate.FuncRegisterDisallowedStairStates = FuncRegisterDisallowedStairStates;

                                            boundaryStairCandidates.Add(candidate);
                                        }
                                        else
                                        {
                                            invalidStairEdges.Add(boundaryEdge);
                                        }
                                    }
                                };
                                
                            
                                // Find the intersecting line
                                Rectangle intersection = Rectangle.Intersect(owningCell.Bounds, remoteCell.Bounds);
                                if (intersection.Size.x > 0) {
                                    ProcessIntersection(intersection, Mathf.PI * 0.5f, new FAxisDomainX(), new FAxisDomainZ());
                                }
                                else if (intersection.Size.z > 0) {
                                    ProcessIntersection(intersection, Mathf.PI, new FAxisDomainZ(), new FAxisDomainX());
                                }

                                if (boundaryStairCandidates.Count > 0)
                                {
                                    foreach (var Candidate in boundaryStairCandidates)
                                    {
                                        bool bPathExists = GridBuilder.ContainsAdjacencyPath(Candidate.StairInfo.OwnerCell, Candidate.StairInfo.ConnectedToCell, StairConnectionTollerance, true);
                                        if (!bPathExists)
                                        {
                                            var BestStair = Candidate.StairInfo;

                                            // C# Dictionary approach instead of FindOrAdd
                                            if (!GridModel.CellStairs.ContainsKey(BestStair.OwnerCell))
                                            {
                                                GridModel.CellStairs[BestStair.OwnerCell] = new List<StairInfo>();
                                            }
                                            GridModel.CellStairs[BestStair.OwnerCell].Add(BestStair);
                                            Candidate.FuncRegisterDisallowedStairStates();
                                        }
                                    }
                                }
                                else
                                {
                                    Debug.Assert(invalidStairEdges.Count > 0);
                                    edgesToFixOnSameHeight.AddRange(invalidStairEdges);   // TODO: Adding just one will increase the iterations, try adding half of them randomly
                                }
                            }
                        }
                    }
                }

                bContinueIteration = false;
                // Check if we need to fix the heights of certain cells
                HashSet<Cell> ProcessedCoordCells = new HashSet<Cell>();
                
                Action<Cell, int> SetCellY = (cell, newY) => {
                    Rectangle bounds = cell.Bounds;
                    IntVector location = bounds.Location;
                    location.y = newY;
                    bounds.Location = location;
                    cell.Bounds = bounds;
                };
                
                bool raiseAllNonClampedCells = false;
                foreach (var BoundaryEdge in edgesToFixOnSameHeight)
                {
                    bool bPathExists = GridBuilder.ContainsAdjacencyPath(BoundaryEdge.OwningCellId, BoundaryEdge.RemoteCellId, StairConnectionTollerance, true);
                    if (!bPathExists)
                    {
                        Cell CellA = GridModel.GetCell(BoundaryEdge.OwningCellId);
                        Cell CellB = GridModel.GetCell(BoundaryEdge.RemoteCellId);
                        Debug.Assert(CellA != null && CellB != null);
                        {
                            Cell CellHigher = CellA.Bounds.Location.y > CellB.Bounds.Location.y ? CellA : CellB;
                            Cell CellLower = CellA.Bounds.Location.y < CellB.Bounds.Location.y ? CellA : CellB;
                        
                            if (!ProcessedCoordCells.Contains(CellHigher))
                            {
                                if (!CellHigher.HeightClamped)
                                {
                                    SetCellY(CellHigher, CellHigher.Bounds.Location.y - 1);
                                    ProcessedCoordCells.Add(CellHigher);
                                }
                                else
                                {
                                    raiseAllNonClampedCells = true;
                                }
                                bContinueIteration = true;
                            }
                        }
                    }
                }

                if (raiseAllNonClampedCells)
                {
                    foreach (var cell in GridModel.Cells)
                    {
                        if (!cell.HeightClamped)
                        {
                            SetCellY(cell, cell.Bounds.Location.y + 1);
                        }
                    }
                }
            }
        } 
        
        protected void VisitIslandCellRecursive(Cell InCell, FIsland OutIsland, GridDungeonModel GridModel, HashSet<int> VisitedCells) {
            if(VisitedCells.Contains(InCell.Id) || InCell.CellType == CellType.Unknown) {
                return;
            }
            Debug.Assert(InCell.Bounds.Location.y == OutIsland.Y);
                    
            VisitedCells.Add(InCell.Id);
            OutIsland.CellIds.Add(InCell.Id);
            {
                Vector2Int BaseLocation = new Vector2Int(InCell.Bounds.Location.x, InCell.Bounds.Location.z);
                for (int x = 0; x < InCell.Bounds.Size.x; x++) {
                    for (int z = 0; z < InCell.Bounds.Size.z; z++) {
                        OutIsland.RasterizedTileCoords.Add(BaseLocation + new Vector2Int(x, z));
                    }
                }
            }

            if (InCell.HeightClamped) {
                // The cell's height is clamped, We don't want to add other cells here
                OutIsland.HeightClamped = true;
                return;
            }
            
            if (InCell.CellType == CellType.Room) {
                // Rooms are single islands
                return;
            }
                    
            foreach (int AdjacentCellId in InCell.AdjacentCells) {
                if (!VisitedCells.Contains(AdjacentCellId))
                {
                    Cell AdjacentCell = GridModel.GetCell(AdjacentCellId);
                    if (AdjacentCell != null) {
                        // Cells on an island are on the same height.  Also, rooms are isolated islands
                        bool bBelongsToIsland = (AdjacentCell.Bounds.Location.y == OutIsland.Y)
                            && AdjacentCell.CellType != CellType.Room;
                                
                        if (bBelongsToIsland) {
                            VisitIslandCellRecursive(AdjacentCell, OutIsland, GridModel, VisitedCells);
                        }
                    }
                }
            }
        }
        
        protected void GenerateIslands(GridDungeonModel GridModel, List<FIsland> OutIslands, ref FIslandLookup OutLookups) {
            {
                HashSet<int> VisitedCells = new HashSet<int>();
                foreach (Cell Cell in GridModel.Cells) {
                    if (VisitedCells.Contains(Cell.Id) || Cell.CellType == CellType.Unknown) {
                        continue;
                    }

                    FIsland Island = new FIsland();
                    OutIslands.Add(Island);
                    Island.Y = Cell.Bounds.Location.y;
                    VisitIslandCellRecursive(Cell, Island, GridModel, VisitedCells);
                }
            }

            OutIslands.Sort((a, b) =>
            {
                if (a.Y == b.Y) return 0;
                return a.Y > b.Y ? 1 : -1;
            });
            
            // Data structure for fast cell to island mapping
            OutLookups = new FIslandLookup();
            Dictionary<int, int> CellToIslandMap = OutLookups.CellToIslandMap;
            for (int IslandIdx = 0; IslandIdx < OutIslands.Count; IslandIdx++) {
                FIsland Island = OutIslands[IslandIdx];
                foreach (int CellId in Island.CellIds) {
                    CellToIslandMap[CellId] = IslandIdx;
                }
            }
            
            foreach (Cell Cell in GridModel.Cells)
            {
                if (CellToIslandMap.TryGetValue(Cell.Id, out int IslandIdx))
                {
                    foreach (int AdjacentCell in Cell.AdjacentCells)
                    {
                        if (CellToIslandMap.TryGetValue(AdjacentCell, out int AdjacentIslandIdx))
                        {
                            if (IslandIdx == AdjacentIslandIdx)
                            {
                                continue;
                            }

                            if (!OutIslands[IslandIdx].Boundaries.TryGetValue(AdjacentIslandIdx, out FIslandBoundary Boundary))
                            {
                                Boundary = new FIslandBoundary();
                                OutIslands[IslandIdx].Boundaries[AdjacentIslandIdx] = Boundary;
                            }

                            FIslandBoundaryEdge BoundaryEdge = new FIslandBoundaryEdge
                            {
                                OwningCellId = Cell.Id,
                                RemoteCellId = AdjacentCell
                            };
                            Boundary.Edges.Add(BoundaryEdge);
                        }
                    }
                }
            }
        }
    }
}