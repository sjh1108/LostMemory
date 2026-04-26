//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//
using DungeonArchitect.Frameworks.Snap;
using UnityEditor;
using UnityEngine;

namespace DungeonArchitect.Editors
{
	[CustomEditor(typeof(SnapConnection))]
	public class SnapConnectionEditor : Editor
	{
		private SerializedObject sobject;
		private SerializedProperty doorObject;
		private SerializedProperty wallObject;
		private SerializedProperty category;
		
		private SerializedProperty oneWayDoorObject;
		private SerializedProperty lockedDoors;
		
		private SerializedProperty mode2D;
		private SerializedProperty outgoingDirection2D;
		
		public void OnEnable()
		{
			sobject = new SerializedObject(target);
			
			doorObject = sobject.FindProperty("doorObject");
			wallObject = sobject.FindProperty("wallObject");
			category = sobject.FindProperty("category");
			
			oneWayDoorObject = sobject.FindProperty("oneWayDoorObject");
			lockedDoors = sobject.FindProperty("lockedDoors");
			
			mode2D = sobject.FindProperty("mode2D");
			outgoingDirection2D = sobject.FindProperty("outgoingDirection2D");
		}

		public override void OnInspectorGUI()
		{
			sobject.Update();

			GUILayout.Label("Snap Connection", InspectorStyles.HeaderStyle);
			EditorGUILayout.PropertyField(doorObject);
			EditorGUILayout.PropertyField(wallObject);
			EditorGUILayout.PropertyField(category);
			EditorGUILayout.Space();
			
			GUILayout.Label("Advanced Doors", InspectorStyles.HeaderStyle);
			EditorGUILayout.PropertyField(oneWayDoorObject);
			EditorGUILayout.PropertyField(lockedDoors);

			EditorGUILayout.Space();
			GUILayout.Label("2D Mode", InspectorStyles.HeaderStyle);
			EditorGUILayout.PropertyField(mode2D);
			if (mode2D.boolValue)
			{
				EditorGUILayout.PropertyField(outgoingDirection2D);
			}
			
			sobject.ApplyModifiedProperties();
		}
	}
}
