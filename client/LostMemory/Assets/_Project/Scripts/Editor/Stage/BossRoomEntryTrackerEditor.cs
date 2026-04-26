using LostMemory.Stage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LostMemory.Editor
{
    [CustomEditor(typeof(BossRoomEntryTracker))]
    public sealed class BossRoomEntryTrackerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            bool changed = EditorGUI.EndChangeCheck();

            serializedObject.ApplyModifiedProperties();

            BossRoomEntryTracker tracker = (BossRoomEntryTracker)target;
            if (changed)
            {
                tracker.RefreshCondition(false);
                MarkDirty(tracker);
            }

            EditorGUILayout.Space();
            DrawSummary(tracker.CurrentCondition);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Condition"))
                {
                    tracker.RefreshCondition(false);
                    MarkDirty(tracker);
                }

                if (GUILayout.Button("Reset Progress"))
                {
                    tracker.ResetProgress();
                    MarkDirty(tracker);
                }
            }
        }

        private static void MarkDirty(BossRoomEntryTracker tracker)
        {
            if (tracker == null)
            {
                return;
            }

            EditorUtility.SetDirty(tracker);

            if (!Application.isPlaying && tracker.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(tracker.gameObject.scene);
            }
        }

        private static void DrawSummary(BossRoomEntryConditionResult result)
        {
            EditorGUILayout.LabelField("Boss Entry Summary", EditorStyles.boldLabel);

            MessageType messageType;
            string message;

            if (!result.HasBossRoom)
            {
                messageType = MessageType.Warning;
                message = "No boss room was found in the configured room sequence.";
            }
            else if (!result.HasConfiguredRequirements)
            {
                messageType = MessageType.Warning;
                message = "No required rooms were configured before the boss room.";
            }
            else if (result.CanEnterBossRoom)
            {
                messageType = MessageType.Info;
                message = "Boss room entry is currently unlocked.";
            }
            else
            {
                messageType = MessageType.Warning;
                message = $"Boss room is locked. {result.RemainingRequiredRoomCount} required room(s) remain.";
            }

            EditorGUILayout.HelpBox(message, messageType);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Can Enter Boss Room", result.CanEnterBossRoom);
                EditorGUILayout.IntField("Required Room Count", result.RequiredRoomCount);
                EditorGUILayout.IntField("Completed Required Rooms", result.CompletedRequiredRoomCount);
                EditorGUILayout.IntField("Remaining Required Rooms", result.RemainingRequiredRoomCount);
                EditorGUILayout.IntField("First Incomplete Index", result.FirstIncompleteRoomSequenceIndex);
                EditorGUILayout.TextField("First Incomplete Room Id", result.FirstIncompleteRoomId);
            }
        }
    }
}
