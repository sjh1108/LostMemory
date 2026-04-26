//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using System.Collections.Generic;
using UnityEngine;

namespace DungeonArchitect.Builders.Grid.Stairs.Impl
{
    public class GridStairSystemLegacy : GridStairSystemBase
    {
        public override void Generate(GridDungeonModel gridModel, GridDungeonBuilder gridBuilder, GridDungeonConfig gridConfig)
        {
            GenerateDungeonHeights(gridModel, gridConfig.Seed, true);
            
            int[] iterationWeights = {100, 50, 0, -50, -80, -130};
            for (int weightIndex = 0; weightIndex < 4; weightIndex++) {
                ConnectStairs(gridModel, gridBuilder, gridConfig.GridCellSize, iterationWeights[weightIndex]);
            }
        }
        
        class StairConnectionWeight {
	        public StairConnectionWeight(int position, int weight)  {
		        this.position = position;
		        this.weight = weight;
	        }
	        public int position;
	        public int weight;

        }

        class StairConnectionWeightComparer : IComparer<StairConnectionWeight>
        {
	        public int Compare(StairConnectionWeight x, StairConnectionWeight y)
	        {
		        if (x.weight == y.weight) return 0;
		        return (x.weight < y.weight) ? 1 : -1;
	        }
        }
        
        void ConnectStairs(GridDungeonModel gridModel, GridDungeonBuilder gridBuilder, Vector3 gridToMeshScale, int weightThreshold)
        {
            if (gridModel.Cells.Count == 0) return;
            Stack<StairEdgeInfo> stack = new Stack<StairEdgeInfo>();
			HashSet<int> visited = new HashSet<int>();
			HashSet<int> islandsVisited = new HashSet<int>();

			for (int i = 0; i < gridModel.Cells.Count; i++) {
				var startCell = gridModel.Cells[i];
				if (islandsVisited.Contains (startCell.Id)) {
					continue;
				}
				stack.Push(new StairEdgeInfo(-1, startCell.Id));
	            while (stack.Count > 0)
	            {
	                StairEdgeInfo top = stack.Pop();
	                if (top.CellIdA >= 0)
	                {
	                    int hash1 = gridBuilder.HASH(top.CellIdA, top.CellIdB);
	                    int hash2 = gridBuilder.HASH(top.CellIdB, top.CellIdA);
	                    if (visited.Contains(hash1) || visited.Contains(hash2))
	                    {
	                        // Already processed
	                        continue;
						}
						// Mark as processed
						visited.Add(hash1);
						visited.Add(hash2);

						// Mark the island as processed
						islandsVisited.Add(top.CellIdA);
						islandsVisited.Add(top.CellIdB);

	                    // Check if it is really required to place a stair here.  There might be other paths nearby to this cell
	                    bool pathExists = gridBuilder.ContainsAdjacencyPath(top.CellIdA, top.CellIdB, StairConnectionTollerance, true);
                        bool stairConnectsToDoor = gridModel.DoorManager.ContainsDoorBetweenCells(top.CellIdA, top.CellIdB);
	                    if (!pathExists || stairConnectsToDoor)
	                    {
	                        // Process the edge
	                        Cell cellA = gridModel.GetCell(top.CellIdA);
	                        Cell cellB = gridModel.GetCell(top.CellIdB);
	                        if (cellA == null || cellB == null) continue;
	                        if (cellA.Bounds.Location.y != cellB.Bounds.Location.y)
	                        {
	                            // Find the adjacent line
	                            Rectangle intersection = Rectangle.Intersect(cellA.Bounds, cellB.Bounds);
	                            if (intersection.Size.x > 0)
	                            {
	                                bool cellAAbove = (cellA.Bounds.Location.y > cellB.Bounds.Location.y);
	                                Cell stairOwner = (cellAAbove ? cellB : cellA);
	                                Cell stairConnectedTo = (!cellAAbove ? cellB : cellA);

	                                if (gridBuilder.ContainsStair(stairOwner.Id, stairConnectedTo.Id))
	                                {
	                                    // Stair already exists here. Move to the next one
	                                    continue;
	                                }

	                                bool cellOwnerOnLeft = (stairOwner.Bounds.Center().z < intersection.Location.z);
	                                int validX = intersection.Location.x;
									//int preferedLocation = MathUtils.INVALID_LOCATION;

	                                int validZ = intersection.Location.z;
	                                if (cellOwnerOnLeft) validZ--;

	                                var StairConnectionCandidates = new List<StairConnectionWeight>();
	                                for (validX = intersection.Location.x; validX < intersection.Location.x + intersection.Size.x; validX++)
	                                {
	                                    var currentPointInfo = gridModel.GetGridCellLookup(validX, validZ);
									    if (stairOwner.CellType == CellType.Room || stairConnectedTo.CellType == CellType.Room) {
										    // Make sure the stair is on a door cell
										    GridCellInfo stairCellInfo = gridModel.GetGridCellLookup(validX, validZ);
										    if (!stairCellInfo.ContainsDoor) {
											    // Stair not connected to a door. Probably trying to attach itself to a room wall. ignore
											    continue;
										    }

										    // We have a door here.  A stair case is a must, but first make sure we have a door between these two cells 
										    bool hasDoor = gridModel.DoorManager.ContainsDoorBetweenCells(stairOwner.Id, stairConnectedTo.Id);
										    if (!hasDoor) continue;

										    // Check again in more detail
										    var tz1 = validZ;
										    var tz2 = validZ - 1;
										    if (cellOwnerOnLeft) {
											    tz2 = validZ + 1;
										    }

										    hasDoor = gridModel.DoorManager.ContainsDoor(validX, tz1, validX, tz2);
										    if (hasDoor) {
											    StairConnectionCandidates.Add(new StairConnectionWeight(validX, 100));
											    break;
										    }
									    }
									    else {	// Both the cells are non-rooms (corridors)
										    int weight = 0;

										    GridCellInfo cellInfo0 = gridModel.GetGridCellLookup(validX, validZ - 1);
										    GridCellInfo cellInfo1 = gridModel.GetGridCellLookup(validX, validZ + 1);
										    weight += (cellInfo0.CellType != CellType.Unknown) ? 10 : 0;
										    weight += (cellInfo1.CellType != CellType.Unknown) ? 10 : 0;

											int adjacentOwnerZ = cellOwnerOnLeft ? (validZ - 1) : (validZ + 1);
											int adjacentConnectedToZ = !cellOwnerOnLeft ? (validZ - 1) : (validZ + 1);
										    if (currentPointInfo.ContainsDoor) {
											    // Increase the weight if we connect into a door
											    int adjacentZ = cellOwnerOnLeft ? (validZ - 1) : (validZ + 1);
											    bool ownerOnDoor = gridModel.DoorManager.ContainsDoor(validX, validZ, validX, adjacentZ);
											    if (ownerOnDoor) {
												    // Connect to this
												    weight += 100;
											    }
											    else {
												    // Add a penalty if we are creating a stair blocking a door entry/exit
												    weight -= 100;
											    }
										    }
										    else {
											    // Make sure we don't connect to a wall
												GridCellInfo adjacentOwnerCellInfo = gridModel.GetGridCellLookup(validX, adjacentOwnerZ);
											    if (adjacentOwnerCellInfo.CellType == CellType.Room) {
												    // We are connecting to a wall. Add a penalty
												    weight -= 100;
											    }
										    }

										    // Check the side of the stairs to see if we are not blocking a stair entry / exit
										    if (gridModel.ContainsStairAtLocation(validX - 1, validZ)) {
											    weight -= 60;
										    }
	                                        if (gridModel.ContainsStairAtLocation(validX + 1, validZ))
	                                        {
											    weight -= 60;
										    }

											for (int dx = -1; dx <= 1; dx++) {
												var adjacentStair = gridModel.GetStairAtLocation(validX + dx, adjacentOwnerZ);
												if (adjacentStair != null) {
													var currentRotation = Quaternion.AngleAxis(cellOwnerOnLeft ? -90 : 90, new Vector3(0, 1, 0));
													var angle = Quaternion.Angle(adjacentStair.Rotation, currentRotation);
													if (dx == 0) {
														// If we have a stair case in a perpendicular direction right near the owner, add a penalty
														var angleDelta = Mathf.Abs (Mathf.Abs(angle) - 90);
														if (angleDelta < 2) {
															weight -= 100;
														}
													} else {
														var angleDelta = Mathf.Abs (Mathf.Abs(angle) - 180);
														if (angleDelta < 2) {
															weight -= 60;
														}
													}
												}
											}
											
											// If we connect to another stair with the same angle, then increase the weight
											if (gridModel.ContainsStairAtLocation(validX, adjacentConnectedToZ)) {
												var adjacentStair = gridModel.GetStairAtLocation(validX, adjacentConnectedToZ);
												if (adjacentStair != null) {
													var currentRotation = Quaternion.AngleAxis(cellOwnerOnLeft ? -90 : 90, new Vector3(0, 1, 0));
													var angle = Quaternion.Angle(adjacentStair.Rotation, currentRotation);
													var angleDelta = Mathf.Abs(angle) % 360;
													if (angleDelta < 2) {
														weight += 50;
													}
													else {
														weight -= 50;
													}
												}
											}
											

											// check if the entry of the stair is not in a different height
											{
												var adjacentEntryCellInfo = gridModel.GetGridCellLookup(validX, adjacentOwnerZ);
												if (adjacentEntryCellInfo.CellType != CellType.Unknown) {
													var adjacentEntryCell = gridModel.GetCell(adjacentEntryCellInfo.CellId);
													if (stairOwner.Bounds.Location.y != adjacentEntryCell.Bounds.Location.y) {
														// The entry is in a different height. Check if we have a stair here
														if (!gridModel.ContainsStair(validX, adjacentOwnerZ)) {
															//Add a penalty
															weight -= 10;
														}
													}
												}
											}

										    StairConnectionCandidates.Add(new StairConnectionWeight(validX, weight));
									    }
	                                }


	                                // Create a stair if necessary
	                                if (StairConnectionCandidates.Count > 0)
	                                {
	                                    StairConnectionCandidates.Sort(new StairConnectionWeightComparer());
	                                    var candidate = StairConnectionCandidates[0];
	                                    if (candidate.weight < weightThreshold)
	                                    {
	                                        continue;
	                                    }
	                                    validX = candidate.position;

										int stairY = stairOwner.Bounds.Location.y;
										var paddingOffset = (stairOwner.Bounds.Z > stairConnectedTo.Bounds.Z) ? 1 : -1;
										// Add a corridor padding here
										//AddCorridorPadding(validX, stairY, validZ - 1);
										for (int dx = -1; dx <= 1; dx++) {
											bool requiresPadding = false;
											if (dx == 0) {
												requiresPadding = true;
											} else {
												var cellInfo = gridBuilder.GetGridCellLookup(validX + dx, validZ);
												if (cellInfo.CellType != CellType.Unknown) {
													requiresPadding = true;
												}
											}
											
											if (requiresPadding) {
												var paddingInfo = gridBuilder.GetGridCellLookup(validX + dx, validZ + paddingOffset);
												if (paddingInfo.CellType == CellType.Unknown) {
													gridBuilder.AddCorridorPadding(validX + dx, stairY, validZ + paddingOffset);
												}
											}
										}
										gridModel.BuildCellLookup();
										gridModel.BuildSpatialCellLookup();
										gridBuilder.GenerateAdjacencyLookup();
									}
	                                else
	                                {
	                                    continue;
	                                }

	                                float validY = stairOwner.Bounds.Location.y;
	                                Vector3 StairLocation = new Vector3(validX, validY, validZ);
	                                StairLocation += new Vector3(0.5f, 0, 0.5f);
	                                StairLocation = Vector3.Scale(StairLocation, gridToMeshScale);

	                                Quaternion StairRotation = Quaternion.AngleAxis(cellOwnerOnLeft ? -90 : 90, new Vector3(0, 1, 0));

	                                if (!gridModel.CellStairs.ContainsKey(stairOwner.Id))
	                                {
		                                gridModel.CellStairs.Add(stairOwner.Id, new List<StairInfo>());
	                                }
	                                StairInfo Stair = new StairInfo();
	                                Stair.OwnerCell = stairOwner.Id;
	                                Stair.ConnectedToCell = stairConnectedTo.Id;
	                                Stair.Position = StairLocation;
	                                Stair.IPosition = new IntVector(validX, (int)validY, validZ);
	                                Stair.Rotation = StairRotation;
	                                if (!gridModel.ContainsStairAtLocation(validX, validZ))
	                                {
		                                gridModel.CellStairs[stairOwner.Id].Add(Stair);
	                                }
	                            }
	                            else if (intersection.Size.z > 0)
	                            {
	                                bool cellAAbove = (cellA.Bounds.Location.y > cellB.Bounds.Location.y);

	                                Cell stairOwner = (cellAAbove ? cellB : cellA);
	                                Cell stairConnectedTo = (!cellAAbove ? cellB : cellA);

	                                if (gridBuilder.ContainsStair(stairOwner.Id, stairConnectedTo.Id))
	                                {
	                                    // Stair already exists here. Move to the next one
	                                    continue;
	                                }

	                                bool cellOwnerOnLeft = (stairOwner.Bounds.Center().x < intersection.Location.x);

	                                int validX = intersection.Location.x;
	                                if (cellOwnerOnLeft) validX--;

									int validZ = intersection.Location.z;

	                                var StairConnectionCandidates = new List<StairConnectionWeight>();
	                                for (validZ = intersection.Location.z; validZ < intersection.Location.z + intersection.Size.z; validZ++)
	                                {
	                                    var currentPointInfo = gridModel.GetGridCellLookup(validX, validZ);
									    if (stairOwner.CellType == CellType.Room || stairConnectedTo.CellType == CellType.Room) {
										    // Make sure the stair is on a door cell
										    GridCellInfo stairCellInfo = gridModel.GetGridCellLookup(validX, validZ);
										    if (!stairCellInfo.ContainsDoor) {
											    // Stair not connected to a door. Probably trying to attach itself to a room wall. ignore
											    continue;
										    }

										    // We have a door here.  A stair case is a must, but first make sure we have a door between these two cells 
										    bool hasDoor = gridModel.DoorManager.ContainsDoorBetweenCells(stairOwner.Id, stairConnectedTo.Id);
										    if (!hasDoor) continue;

										    // Check again in more detail
										    var tx1 = validX;
										    var tx2 = validX - 1;
										    if (cellOwnerOnLeft) {
											    tx2 = validX + 1;
										    }

										    hasDoor = gridModel.DoorManager.ContainsDoor(tx1, validZ, tx2, validZ);
										    if (hasDoor) {
											    StairConnectionCandidates.Add(new StairConnectionWeight(validZ, 100));
											    break;
										    }
									    }
									    else {	// Both the cells are non-rooms (corridors)
										    int weight = 0;
                                            
										    GridCellInfo cellInfo0 = gridModel.GetGridCellLookup(validX - 1, validZ);
										    GridCellInfo cellInfo1 = gridModel.GetGridCellLookup(validX + 1, validZ);
										    weight += (cellInfo0.CellType != CellType.Unknown) ? 10 : 0;
										    weight += (cellInfo1.CellType != CellType.Unknown) ? 10 : 0;
											
											int adjacentOwnerX = cellOwnerOnLeft ? (validX - 1) : (validX + 1);
											int adjacentConnectedToX = !cellOwnerOnLeft ? (validX - 1) : (validX + 1);
											if (currentPointInfo.ContainsDoor) {
											    // Increase the weight if we connect into a door
												bool ownerOnDoor = gridModel.DoorManager.ContainsDoor(validX, validZ, adjacentOwnerX, validZ);
											    if (ownerOnDoor) {
												    // Connect to this
												    weight += 100;
											    }
											    else {
												    // Add a penalty if we are creating a stair blocking a door entry/exit
												    weight -= 100;
											    }
										    }
										    else {
											    // Make sure we don't connect to a wall
											    int adjacentX = cellOwnerOnLeft ? (validX - 1) : (validX + 1);
											    GridCellInfo adjacentOwnerCellInfo = gridModel.GetGridCellLookup(adjacentX, validZ);
											    if (adjacentOwnerCellInfo.CellType == CellType.Room) {
												    // We are connecting to a wall. Add a penalty
												    weight -= 100;
											    }
										    }

										    // Check the side of the stairs to see if we are not blocking a stair entry / exit
										    if (gridModel.ContainsStairAtLocation(validX, validZ - 1)) {
											    weight -= 60;
										    }
										    if (gridModel.ContainsStairAtLocation(validX, validZ + 1)) {
											    weight -= 60;
										    }

											// If we have a stair coming out in the opposite direction, near the entry of the stair, add a penalty
											for (int dz = -1; dz <= 1; dz++) {
												var adjacentStair = gridModel.GetStairAtLocation(adjacentOwnerX, validZ + dz);
												if (adjacentStair != null) {
													var currentRotation = Quaternion.AngleAxis(cellOwnerOnLeft ? 0 : 180, new Vector3(0, 1, 0));
													var angle = Quaternion.Angle(adjacentStair.Rotation, currentRotation);
													if (dz == 0) {
														// If we have a stair case in a perpendicular direction right near the owner, add a penalty
														var angleDelta = Mathf.Abs (Mathf.Abs(angle) - 90);
														if (angleDelta < 2) {
															weight -= 100;
														}
													} else {
														var angleDelta = Mathf.Abs (Mathf.Abs(angle) - 180);
														if (angleDelta < 2) {
															weight -= 60;
														}
													}
												}
											}

											// If we connect to another stair with the same angle, the increase the weight
											if (gridModel.ContainsStairAtLocation(adjacentConnectedToX, validZ)) {
												var adjacentStair = gridModel.GetStairAtLocation(adjacentConnectedToX, validZ);
												if (adjacentStair != null) {
													var currentRotation = Quaternion.AngleAxis(cellOwnerOnLeft ? 0 : 180, new Vector3(0, 1, 0));
													var angle = Quaternion.Angle(adjacentStair.Rotation, currentRotation);
													var angleDelta = Mathf.Abs(angle) % 360;
													if (angleDelta < 2) {
														weight += 50;
													}
													else {
														weight -= 50;
													}
												}
											}


											// check if the entry of the stair is not in a different height
											{
												var adjacentEntryCellInfo = gridModel.GetGridCellLookup(adjacentOwnerX, validZ);
												if (adjacentEntryCellInfo.CellType != CellType.Unknown) {
													var adjacentEntryCell = gridModel.GetCell(adjacentEntryCellInfo.CellId);
													if (stairOwner.Bounds.Location.y != adjacentEntryCell.Bounds.Location.y) {
														// The entry is in a different height. Check if we have a stair here
														if (!gridModel.ContainsStair(adjacentOwnerX, validZ)) {
															//Add a penalty
															weight -= 10;
														}
													}
												}
											}

										    StairConnectionCandidates.Add(new StairConnectionWeight(validZ, weight));
									    }
	                                }

	                                // Connect the stairs if necessary
	                                if (StairConnectionCandidates.Count > 0)
	                                {
	                                    StairConnectionCandidates.Sort(new StairConnectionWeightComparer());
	                                    StairConnectionWeight candidate = StairConnectionCandidates[0];
	                                    if (candidate.weight < weightThreshold)
	                                    {
	                                        continue;
	                                    }
	                                    validZ = candidate.position;

										int stairY = stairOwner.Bounds.Location.y;
										var paddingOffset = (stairOwner.Bounds.X > stairConnectedTo.Bounds.X) ? 1 : -1;
										// Add a corridor padding here
										for (int dz = -1; dz <= 1; dz++) {
											bool requiresPadding = false;
											if (dz == 0) {
												requiresPadding = true;
											} else {
												var cellInfo = gridBuilder.GetGridCellLookup(validX, validZ + dz);
												if (cellInfo.CellType != CellType.Unknown) {
													requiresPadding = true;
												}
											}
											
											if (requiresPadding) {
												var paddingInfo = gridBuilder.GetGridCellLookup(validX + paddingOffset, validZ + dz);
												if (paddingInfo.CellType == CellType.Unknown) {
													gridBuilder.AddCorridorPadding(validX + paddingOffset, stairY, validZ + dz);
												}
											}
										}
										gridModel.BuildCellLookup();
										gridModel.BuildSpatialCellLookup();
										gridBuilder.GenerateAdjacencyLookup();
									}
	                                else
	                                {
	                                    continue;
	                                }

	                                float validY = stairOwner.Bounds.Location.y;
	                                Vector3 StairLocation = new Vector3(validX, validY, validZ);
	                                StairLocation += new Vector3(0.5f, 0, 0.5f);
	                                StairLocation = Vector3.Scale(StairLocation, gridToMeshScale);

	                                Quaternion StairRotation = Quaternion.AngleAxis(cellOwnerOnLeft ? 0 : 180, new Vector3(0, 1, 0));

	                                if (!gridModel.CellStairs.ContainsKey(stairOwner.Id))
	                                {
		                                gridModel.CellStairs.Add(stairOwner.Id, new List<StairInfo>());
	                                }
	                                StairInfo Stair = new StairInfo();
	                                Stair.OwnerCell = stairOwner.Id;
	                                Stair.ConnectedToCell = stairConnectedTo.Id;
	                                Stair.Position = StairLocation;
	                                Stair.IPosition = new IntVector(validX, (int)validY, validZ);
	                                Stair.Rotation = StairRotation;
	                                if (!gridModel.ContainsStairAtLocation(validX, validZ))
	                                {
		                                gridModel.CellStairs[stairOwner.Id].Add(Stair);
	                                }
	                            }
	                        }
	                    }
	                }

	                // Move to the next adjacent nodes
	                {
	                    Cell cellB = gridModel.GetCell(top.CellIdB);
	                    if (cellB == null) continue;
	                    foreach (int adjacentCell in cellB.AdjacentCells)
	                    {
	                        int hash1 = gridBuilder.HASH(cellB.Id, adjacentCell);
	                        int hash2 = gridBuilder.HASH(adjacentCell, cellB.Id);
	                        if (visited.Contains(hash1) || visited.Contains(hash2)) continue;
	                        StairEdgeInfo edge = new StairEdgeInfo(top.CellIdB, adjacentCell);
	                        stack.Push(edge);
	                    }
	                }
	            }
			}
        }
    }
}