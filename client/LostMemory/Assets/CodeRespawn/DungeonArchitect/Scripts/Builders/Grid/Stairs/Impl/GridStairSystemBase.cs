//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using System;
using System.Collections.Generic;
using DungeonArchitect.Utils;
using UnityEngine;

namespace DungeonArchitect.Builders.Grid.Stairs.Impl
{
    public abstract class GridStairSystemBase : MonoBehaviour, IGridStairSystemInterface
    {
        /// <summary>
        /// The number of logical floor units the dungeon height can vary. This determines how high the dungeon's height
        /// can vary (e.g. max 2 floors high).   Set this value depending on the stair meshes you designer has created. 
        /// For e.g.,  if there are two stair meshes, one 200 units high (1 floor) and another 400 units high (2 floors), this value should be set to 2
        /// If you do not want any stairs / height variations, set this value to 0
        /// </summary>
        public int MaxAllowedStairHeight = 1;
        
        /// <summary>
        /// Tweak this value to increase / reduce the height variations (and stairs) in your dungeon.
        /// A value close to 0 reduces the height variation and increases as you approach 1. 
        /// Increasing this value to a higher level might create dungeons with no place for
        /// proper stair placement since there is too much height variation.   
        /// A value of 0.2 to 0.4 seems good
        /// </summary>
        public float HeightVariationProbability = 0.2f;
            
        /// <summary>
        /// The generator would add stairs to make different areas of the dungeon accessible.
        /// However, we do not want too many stairs. For e.g., before adding a stair in a 
        /// particular elevated area, the generator would check if this area is already 
        /// accessible from a nearby stair. If so, it would not add it.   
        /// This tolerance parameter determines how far to look for an existing path
        /// before we can add a stair.   Play with this parameter if you see too
        /// many stairs close to each other, or too few
        /// </summary>
        public int StairConnectionTollerance = 6;
            
        public abstract void Generate(GridDungeonModel model, GridDungeonBuilder builder, GridDungeonConfig config);


        protected virtual void GenerateDungeonHeights(GridDungeonModel gridModel, uint seed, bool bFixHeights)
        {
            // build the adjacency graph in memory
            if (gridModel.Cells.Count == 0) return;
            Dictionary<int, CellHeightNode> CellHeightNodes = new Dictionary<int, CellHeightNode>();

            var srandom = new PMRandom(seed);
            HashSet<int> visited = new HashSet<int>();
            Action<Cell[]> ProcessCells = (startCells) =>
            {
                Queue<CellHeightFrameInfo> queue = new Queue<CellHeightFrameInfo>();
                foreach (var startCell in startCells)
                {
                    if (visited.Contains(startCell.Id))
                    {
                        continue;
                    }
                    queue.Enqueue(new CellHeightFrameInfo(startCell.Id, startCell.Bounds.Location.y));
                }
                
                while (queue.Count > 0)
                {
                    CellHeightFrameInfo top = queue.Dequeue();
                    if (visited.Contains(top.CellId)) continue;
                    visited.Add(top.CellId);

                    Cell cell = gridModel.GetCell(top.CellId);
                    if (cell == null) continue;

                    bool applyHeightVariation = (cell.Bounds.Size.x > 1 && cell.Bounds.Size.z > 1);
                    applyHeightVariation &= (cell.CellType != CellType.Room);
                    applyHeightVariation &= (cell.CellType != CellType.CorridorPadding);
                    applyHeightVariation &= !cell.UserDefined;
                    applyHeightVariation &= !cell.HeightClamped;

                    if (applyHeightVariation)
                    {
                        float rand = srandom.GetNextUniformFloat();
                        if (rand < HeightVariationProbability / 2.0f)
                        {
                            top.CurrentHeight--;
                        }
                        else if (rand < HeightVariationProbability)
                        {
                            top.CurrentHeight++;
                        }
                    }

                    if (cell.UserDefined || cell.HeightClamped)
                    {
                        top.CurrentHeight = cell.Bounds.Location.y;
                    }

                    CellHeightNode node = new CellHeightNode();
                    node.CellId = cell.Id;
                    node.Height = top.CurrentHeight;
                    node.MarkForIncrease = false;
                    node.MarkForDecrease = false;
                    CellHeightNodes.Add(node.CellId, node);

                    // Add the child nodes
                    foreach (int childId in cell.AdjacentCells)
                    {
                        if (visited.Contains(childId)) continue;
                        queue.Enqueue(new CellHeightFrameInfo(childId, top.CurrentHeight));
                    }
                }
            };

            List<Cell> clampedCells = new List<Cell>();
            foreach (var startCell in gridModel.Cells)
            {
                if (startCell.HeightClamped)
                {
                    clampedCells.Add(startCell);
                }
            }

            ProcessCells(clampedCells.ToArray());

            foreach (var startCell in gridModel.Cells)
            {
                ProcessCells(new []{ startCell });
            }
            
            if (bFixHeights)
            {
                // Fix the dungeon heights
                const int FIX_MAX_TRIES = 50;	// TODO: Move to config
                int fixIterations = 0;
                while (fixIterations < FIX_MAX_TRIES && FixDungeonCellHeights(gridModel, CellHeightNodes))
                {
                    fixIterations++;
                }
            }
            
            // Assign the calculated heights
            foreach (Cell cell in gridModel.Cells)
            {
                if (CellHeightNodes.ContainsKey(cell.Id))
                {
                    CellHeightNode node = CellHeightNodes[cell.Id];
                    var bounds = cell.Bounds;
                    var location = cell.Bounds.Location;
                    location.y = node.Height;
                    bounds.Location = location;
                    cell.Bounds = bounds;
                }
            }
        }

        /** Iteratively fixes the dungeon adjacent cell heights if they are too high up. Returns true if more iterative fixes are required */
        protected virtual bool FixDungeonCellHeights(GridDungeonModel gridModel, Dictionary<int, CellHeightNode> cellHeightNodes,
            HashSet<KeyValuePair<int, int>> clampedAdjacentNodes = null)
        {
            bool bContinueIteration = false;
            if (gridModel.Cells.Count == 0) {
                return false;   // Do not continue iteration
            }

            HashSet<int> visited = new HashSet<int>();
            Stack<int> stack = new Stack<int>();
            Cell rootCell = gridModel.Cells[0];
            stack.Push(rootCell.Id);
            while (stack.Count > 0)
            {
                int cellId = stack.Pop();
                if (visited.Contains(cellId)) continue;
                visited.Add(cellId);

                Cell cell = gridModel.GetCell(cellId);
                if (cell == null) continue;

                if (!cellHeightNodes.ContainsKey(cellId)) continue;
                CellHeightNode heightNode = cellHeightNodes[cellId];

                heightNode.MarkForIncrease = false;
                heightNode.MarkForDecrease = false;

                // Check if the adjacent cells have unreachable heights
                if (!cell.HeightClamped)
                {
                    foreach (int childId in cell.AdjacentCells)
                    {
                        Cell childCell = gridModel.GetCell(childId);
                        if (childCell == null || !cellHeightNodes.ContainsKey(childId)) continue;
                        CellHeightNode childHeightNode = cellHeightNodes[childId];
                        int heightDifference = Mathf.Abs(childHeightNode.Height - heightNode.Height);
                        
                        int maxAllowedHeight = MaxAllowedStairHeight;
                        if (clampedAdjacentNodes != null && clampedAdjacentNodes.Contains(new KeyValuePair<int, int>(cell.Id, childId))) {
                            maxAllowedHeight = 0;
                        }
            
                        bool notReachable = (heightDifference > maxAllowedHeight);
                        if (notReachable)
                        {
                            if (heightNode.Height > childHeightNode.Height)
                            {
                                heightNode.MarkForDecrease = true;
                            }
                            else
                            {
                                heightNode.MarkForIncrease = true;
                            }

                            break;
                        }
                    }
                }

                // Add the child nodes
                foreach (int childId in cell.AdjacentCells)
                {
                    if (visited.Contains(childId)) continue;
                    stack.Push(childId);
                }
            }

            bool bHeightChanged = false;
            foreach (int cellId in cellHeightNodes.Keys)
            {
                CellHeightNode heightNode = cellHeightNodes[cellId];
                if (heightNode.MarkForDecrease)
                {
                    heightNode.Height--;
                    bHeightChanged = true;
                }
                else if (heightNode.MarkForIncrease)
                {
                    heightNode.Height++;
                    bHeightChanged = true;
                }
            }

            // Iterate this function again if the height was changed in this step
            bContinueIteration = bHeightChanged;
            return bContinueIteration;
        }
    }
}