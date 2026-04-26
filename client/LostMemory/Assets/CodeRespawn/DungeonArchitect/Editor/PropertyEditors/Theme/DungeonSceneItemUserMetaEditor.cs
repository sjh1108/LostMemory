//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using UnityEditor;
using UnityEngine;

namespace DungeonArchitect.Editors.Theme
{
    [CustomEditor(typeof(DungeonSceneItemUserMeta))]
    public class DungeonSceneItemUserMetaEditor : Editor
    {
        SerializedObject sobject;
        SerializedProperty overrideNavigation;
        SerializedProperty affectsNavigation;
        
        public void OnEnable()
        {
            sobject = new SerializedObject(target);
            overrideNavigation = sobject.FindProperty("overrideNavigation");
            affectsNavigation = sobject.FindProperty("affectsNavigation");
        }

        public override void OnInspectorGUI()
        {
            sobject.Update();

            EditorGUILayout.PropertyField(overrideNavigation);
            if (overrideNavigation.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(affectsNavigation);
                EditorGUI.indentLevel--;
            }
            
            sobject.ApplyModifiedProperties();
        }
    }
}