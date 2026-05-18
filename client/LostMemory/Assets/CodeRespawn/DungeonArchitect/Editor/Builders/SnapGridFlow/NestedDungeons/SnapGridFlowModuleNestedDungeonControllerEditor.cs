//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using DungeonArchitect.Builders.SnapGridFlow.NestedDungeons;
using UnityEditor;
using UnityEngine;

namespace DungeonArchitect.Editors.SGF.NestedDungeons
{
    [CustomEditor(typeof(SnapGridFlowModuleNestedDungeonController))]
    public class SnapGridFlowModuleNestedDungeonControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        
            SnapGridFlowModuleNestedDungeonController room = (SnapGridFlowModuleNestedDungeonController)target;
        
            if(GUILayout.Button("Randomize Chunk"))
            {
                room.RandomizeRoom();
            }
            
            if(GUILayout.Button("Build Chunk"))
            {
                room.RebuildRoom();
            }
        
            if(GUILayout.Button("Destroy Chunk"))
            {
                room.DestroyRoom();
            }
        }
    }
    
}