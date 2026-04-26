//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using System;
using DungeonArchitect.Flow.Impl.SnapGridFlow;
using UnityEditor;
using UnityEngine;

namespace DungeonArchitect.Editors
{
    [CustomEditor(typeof(SnapGridFlowModuleBounds))]
    public class SnapGridFlowModuleBoundsEditor : Editor
    {
        private SerializedObject sobject;
        private SerializedProperty mode;
        private SerializedProperty chunkSize;
        private SerializedProperty chunkSize2D;
        private SerializedProperty doorOffsetY;
        private SerializedProperty boundsColor;
        private SerializedProperty doorColor;
        private SerializedProperty doorDrawSize;
        
        public void OnEnable()
        {
            sobject = new SerializedObject(target);
			
            mode = sobject.FindProperty("mode");
            chunkSize = sobject.FindProperty("chunkSize");
            chunkSize2D = sobject.FindProperty("chunkSize2D");
            doorOffsetY = sobject.FindProperty("doorOffsetY");
            boundsColor = sobject.FindProperty("boundsColor");
            doorColor = sobject.FindProperty("doorColor");
            doorDrawSize = sobject.FindProperty("doorDrawSize");
        }

        public override void OnInspectorGUI()
        {
            sobject.Update();

            GUILayout.Label("Module Bounds", InspectorStyles.HeaderStyle);
            EditorGUILayout.PropertyField(mode);

            if (mode.enumValueIndex == 0)   // 3D
            {
                EditorGUILayout.PropertyField(chunkSize);
                EditorGUILayout.PropertyField(doorOffsetY);
            }
            else
            {
                EditorGUILayout.PropertyField(chunkSize2D);
            }
            
            EditorGUILayout.PropertyField(boundsColor);
            EditorGUILayout.PropertyField(doorColor);
            EditorGUILayout.PropertyField(doorDrawSize);
            
            sobject.ApplyModifiedProperties();
        }
    }
}