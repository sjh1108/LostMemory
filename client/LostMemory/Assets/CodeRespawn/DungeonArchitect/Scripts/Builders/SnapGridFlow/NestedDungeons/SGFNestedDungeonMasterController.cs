//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonArchitect.Builders.SnapGridFlow.NestedDungeons
{
    public class SGFNestedDungeonMasterController : DungeonEventListener
    {
        public override void OnSpawnedManagedObjects(Dungeon dungeon, GameObject[] spawnedManagedObjects, DungeonModel activeModel)
        {
            base.OnSpawnedManagedObjects(dungeon, spawnedManagedObjects, activeModel);
            
            uint masterSeed = 0;
            if (dungeon != null && dungeon.Config != null)
            {
                masterSeed = dungeon.Config.Seed;
            }

            foreach (var spawnedManagedObject in spawnedManagedObjects)
            {
                // Grab all the SGF room chunks and build their nested dungeons
                var nestedChunkControllers = spawnedManagedObject.GetComponentsInChildren<SnapGridFlowModuleNestedDungeonController>(false);
                foreach (var nestedChunkController in nestedChunkControllers)
                {
                    nestedChunkController.masterSeed = (int)masterSeed;
                    nestedChunkController.BuildChunk((int)dungeon.Config.Seed, null);
                
                    // remove the wizard script, since it's used to create the prefab and doesn't really make sense to have it here in the final dungeon
                    var setupWizard = nestedChunkController.gameObject.GetComponent<SnapGridFlowModuleNestedDungeonSetupWizard>();
                    if (setupWizard != null)
                    {
                        if (Application.isPlaying)
                        {
                            Destroy(setupWizard);
                        }
                        else
                        {
                            DestroyImmediate(setupWizard);
                        }
                    }
                }
            }
        }
    }
}