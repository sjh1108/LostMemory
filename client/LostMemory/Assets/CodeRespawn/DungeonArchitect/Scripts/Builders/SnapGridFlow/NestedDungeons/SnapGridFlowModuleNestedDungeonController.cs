//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using System.Collections;
using System.Collections.Generic;
using DungeonArchitect;
using DungeonArchitect.Flow.Impl.SnapGridFlow.Components;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonArchitect.Builders.SnapGridFlow.NestedDungeons
{
    [ExecuteInEditMode]
    public class SnapGridFlowModuleNestedDungeonController : MonoBehaviour
    {
        public Dungeon moduleDungeon;
        public int localSeed = 0;
        
        [HideInInspector]
        public int masterSeed = 0;
        
        private IDungeonSceneObjectInstantiator cachedObjectInstantiator = null;
        
        public void BuildChunk(int masterSeed, IDungeonSceneObjectInstantiator objectInstantiator)
        {
            if (objectInstantiator == null)
            {
                objectInstantiator = new RuntimeDungeonSceneObjectInstantiator();
            }

            AcquireContainedVolumes();
            
            cachedObjectInstantiator = objectInstantiator;
            if (moduleDungeon != null && moduleDungeon.Config != null)
            {
                moduleDungeon.Config.Seed = (uint)masterSeed;
                moduleDungeon.Build(objectInstantiator);
            }
        }

        /// <summary>
        /// Assign all the volumes that are inside the module bounds, to the nested dungeon
        /// </summary>
        private void AcquireContainedVolumes()
        {
            var sgfModule = GetComponent<SnapGridFlowModule>();
            if (sgfModule != null)
            {
                var localSize = Vector3.Scale(sgfModule.numChunks, sgfModule.moduleBounds.chunkSize);
                var localBounds = new Bounds(localSize / 2.0f, localSize);
                Volume[] volumes = GameObject.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                
                foreach (var volume in volumes)
                {
                    var volumeLocalPos = transform.InverseTransformPoint(volume.transform.position);
                    if (localBounds.Contains(volumeLocalPos))
                    {
                        // This volume's center lies within the bounds.   Set the owner to the nested dungeon
                        volume.dungeon = moduleDungeon;
                    }
                }
            }
        }
        
        private int GenerateDeterministicSeed()
        {
            var pos = transform.position;
            unchecked // Allow integer overflow
            {
                int hash = 23;
                hash = hash * 31 + Mathf.RoundToInt(pos.x);
                hash = hash * 31 + Mathf.RoundToInt(pos.y);
                hash = hash * 31 + Mathf.RoundToInt(pos.z);
                hash = hash * 31 + Mathf.RoundToInt(transform.rotation.eulerAngles.z * 100);
                hash = hash * 31 + localSeed;
                hash = hash * 31 + masterSeed;
                return hash;
            }
        }
        public void RebuildRoom()
        {
            var dungeonSeed = GenerateDeterministicSeed();
            BuildChunk(dungeonSeed, cachedObjectInstantiator);
        }
        
        public void RandomizeRoom()
        {
            localSeed = Random.Range(0, int.MaxValue);
            RebuildRoom();
        }
        
        public void DestroyRoom()
        {
            if (moduleDungeon != null)
            {
                moduleDungeon.DestroyDungeon();
            }
        }
    }
}