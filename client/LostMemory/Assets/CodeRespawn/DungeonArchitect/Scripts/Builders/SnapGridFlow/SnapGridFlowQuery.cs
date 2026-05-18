//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using System;
using System.Collections.Generic;
using DungeonArchitect.Flow.Domains.Layout;
using DungeonArchitect.Flow.Impl.SnapGridFlow;
using DungeonArchitect.Flow.Items;
using DungeonArchitect.Utils;
using UnityEngine;

namespace DungeonArchitect.Builders.SnapGridFlow
{
    [System.Serializable]
    public struct SGFQueryModuleInfo
    {
        [SerializeField] 
        public DungeonUID ModuleInstanceId;

        [SerializeField] 
        public Bounds bounds;
    }

    public class SnapGridFlowQuery : DungeonQuery
    {

        [HideInInspector]
        public SGFQueryModuleInfo[] modules;

        private SnapGridFlowModel sgfModel;
        private SnapGridFlowConfig sgfConfig;
        private FlowLayoutGraphQuery graphQuery;

        public override void OnPostLayoutBuild()
        {
            Initialize();
        }

        public override void OnPostDungeonBuild()
        {
            Initialize();
        }

        void Initialize()
        {
            sgfModel = GetComponent<SnapGridFlowModel>();
            if (sgfModel == null)
            {
                return;
            }

            if (sgfConfig == null)
            {
                sgfConfig = GetComponent<SnapGridFlowConfig>();
            }
            
            graphQuery = new FlowLayoutGraphQuery(sgfModel.layoutGraph);

            RefreshModuleList();
        }

        void RefreshModuleList()
        {
            var moduleInfoList = new List<SGFQueryModuleInfo>();
            foreach (var node in sgfModel.snapModules)
            {
                var info = new SGFQueryModuleInfo();
                info.ModuleInstanceId = node.ModuleInstanceId;
                Vector3 chunkSize;
                Vector3Int numChunks;
                if (node.SpawnedModule != null)
                {
                    var module = node.SpawnedModule;
                    var moduleBounds = module.moduleBounds;
                    chunkSize = moduleBounds.chunkSize;
                    numChunks = module.numChunks;
                }
                else
                {
                    if (sgfConfig != null && sgfConfig.moduleDatabase != null && sgfConfig.moduleDatabase.ModuleBoundsAsset != null)
                    {
                        chunkSize = sgfConfig.moduleDatabase.ModuleBoundsAsset.chunkSize;
                        numChunks = node.ModuleDBItem != null ? node.ModuleDBItem.NumChunks : Vector3Int.zero;
                    }
                    else
                    {
                        chunkSize = Vector3.zero;
                        numChunks = Vector3Int.zero;
                    }
                }

                {
                    var boxSize = Vector3.Scale(chunkSize, MathUtils.ToVector3(numChunks));
                    var extent = boxSize * 0.5f;
                    var center = extent;
                    var localBounds = new Bounds(center, boxSize);
                    var localToWorld = node.WorldTransform;
                    info.bounds = MathUtils.TransformBounds(localToWorld, localBounds);
                }
                moduleInfoList.Add(info);
            }

            modules = moduleInfoList.ToArray();
        }
        public override void Release()
        {
            modules = Array.Empty<SGFQueryModuleInfo>();
            sgfModel = null;
        }

        public bool IsValid()
        {
            return modules != null && modules.Length > 0;
        }
        
        SnapGridFlowModel GetModel()
        {
            if (sgfModel == null)
            {
                sgfModel = GetComponent<SnapGridFlowModel>();
            }

            return sgfModel;
        }
        
        public SgfModuleNode GetRoomNodeAtLocation(Vector3 position)
        {
            var instanceId = DungeonUID.Empty;
            foreach (var info in modules)
            {
                var bounds = info.bounds;
                if (bounds.Contains(position))
                {
                    instanceId = info.ModuleInstanceId;
                    break;
                }
            }

            if (instanceId == DungeonUID.Empty)
            {
                return null;
            }

            var model = GetModel();
            if (model == null || model.snapModules == null)
            {
                return null;
            }

            foreach (var node in model.snapModules)
            {
                if (node.ModuleInstanceId == instanceId)
                {
                    return node;
                }
            }

            return null;
        }

        /// <summary>
        /// Find the distance from the start room to the specified room.   
        /// </summary>
        /// <param name="layoutNode">The layout node to test. Use GetRoomNodeAtLocation to get the sgf module node and grab the layout node from there</param>
        /// <param name="followOneWayDoors">While searching, should we follow the one way door rule or go ignore the constraint and allow the search to go through it in the opposite direction</param>
        /// <param name="distance">The resulting distance from the start room to the specified room</param>
        /// <returns></returns>
        public bool GetDistanceFromStartRoom(FlowLayoutGraphNode layoutNode, bool followOneWayDoors, out int distance)
        {
            if (graphQuery == null || graphQuery.Graph == null)
            {
                distance = 0;
                return false;
            }

            var startNode = graphQuery.Graph.GetNodeWithItem(FlowGraphItemType.Entrance);
            if (startNode == null)
            {
                distance = 0;
                return false;
            }

            return graphQuery.GetDistanceBetweenNodes( startNode, layoutNode, followOneWayDoors, out distance);
        }
        /// <summary>
        /// Find the distance from the specified room to the goal room.   
        /// </summary>
        /// <param name="layoutNode">The layout node to test. Use GetRoomNodeAtLocation to get the sgf module node and grab the layout node from there</param>
        /// <param name="followOneWayDoors">While searching, should we follow the one way door rule or go ignore the constraint and allow the search to go through it in the opposite direction</param>
        /// <param name="distance">The resulting distance from the specified room to the goal room</param>
        /// <returns></returns>

        public bool GetDistanceToGoalRoom(FlowLayoutGraphNode layoutNode, bool followOneWayDoors, out int distance)
        {
            if (graphQuery == null || graphQuery.Graph == null)
            {
                distance = 0;
                return false;
            }

            var goalNode = graphQuery.Graph.GetNodeWithItem(FlowGraphItemType.Exit);
            if (goalNode == null)
            {
                distance = 0;
                return false;
            }

            return graphQuery.GetDistanceBetweenNodes(layoutNode, goalNode, followOneWayDoors, out distance);
        }
        
        public SgfModuleDoor[] GetDoorsInRoomNode(Vector3 position)
        {
            var roomNode = GetRoomNodeAtLocation(position);
            if (roomNode == null || roomNode.SpawnedModule == null)
            {
                return null;
            }

            return roomNode.Doors;
        }
        
        public GameObject GetRoomGameObject(Vector3 position)
        {
            var roomNode = GetRoomNodeAtLocation(position);
            if (roomNode == null || roomNode.SpawnedModule == null)
            {
                return null;
            }

            return roomNode.SpawnedModule.gameObject;
        }

        public int GetDistanceToPathStart(FlowLayoutGraphNode layoutNode)
        {
            return _GetDistanceToPathRecursive(layoutNode, 
                (uid) => graphQuery.GetIncomingNodes(uid),
                0, new HashSet<DungeonUID>());
        }
        
        public int GetDistanceToPathEnd(FlowLayoutGraphNode layoutNode)
        {
            return _GetDistanceToPathRecursive(layoutNode, 
                (uid) => graphQuery.GetOutgoingNodes(uid),
                0, new HashSet<DungeonUID>());
        }
        
        private int _GetDistanceToPathRecursive(FlowLayoutGraphNode layoutNode, Func<DungeonUID, DungeonUID[]> fnGetRemoteNodes, int currentLength, HashSet<DungeonUID> visited)
        {
            if (visited.Contains(layoutNode.nodeId))
            {
                return currentLength;
            }
            visited.Add(layoutNode.nodeId);
            
            var incomingNodes = fnGetRemoteNodes(layoutNode.nodeId);
            int maxLength = currentLength;
            foreach (var incomingNodeId in incomingNodes)
            {
                var incomingNode = graphQuery.GetNode(incomingNodeId);
                if (incomingNode.pathName == layoutNode.pathName)
                {
                    int branchLength = _GetDistanceToPathRecursive(incomingNode, fnGetRemoteNodes, currentLength + 1, visited);
                    maxLength = Mathf.Max(maxLength, branchLength);
                }
            }

            return maxLength;
        }
        
    }
}