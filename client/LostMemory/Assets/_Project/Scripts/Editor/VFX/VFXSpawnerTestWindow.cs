using LostMemory.VFX;
using UnityEditor;
using UnityEngine;

namespace LostMemory.VFX.EditorTools
{
    /// <summary>
    /// CL-200: VFXSpawner API 동작 검증용 EditorWindow.
    /// Tools > LostMemory > VFX > Spawner Test 메뉴로 접근. Play 모드 전용.
    /// CL-201~205 작업자가 새 VFX 프리팹 검증할 때 재사용.
    /// </summary>
    public sealed class VFXSpawnerTestWindow : EditorWindow
    {
        private GameObject _prefab;
        private Transform _attachTarget;
        private Vector3 _localOffset = Vector3.zero;
        private float _autoDestroySeconds = 1f;
        private GameObject _lastSpawned;

        [MenuItem("Tools/LostMemory/VFX/Spawner Test")]
        private static void Open()
        {
            GetWindow<VFXSpawnerTestWindow>("VFX Spawner Test");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("VFXSpawner Smoke Test", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Play 모드에서 동작합니다. 프리팹을 드롭하고 버튼을 누르세요.\n" +
                "_Project/Prefabs/VFX/VFXDummy_Test.prefab 권장.",
                MessageType.Info);

            EditorGUILayout.Space();
            _prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", _prefab, typeof(GameObject), false);
            _attachTarget = (Transform)EditorGUILayout.ObjectField("Attach Target", _attachTarget, typeof(Transform), true);
            _localOffset = EditorGUILayout.Vector3Field("Local Offset (Attached)", _localOffset);
            _autoDestroySeconds = EditorGUILayout.FloatField("Auto Destroy (s)", _autoDestroySeconds);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Spawn (world origin)"))
                    DoSpawn();
                if (GUILayout.Button("Spawn Attached"))
                    DoSpawnAttached();
                if (GUILayout.Button("Despawn (last spawned)"))
                    DoDespawn();
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Play 모드에서만 버튼이 활성화됩니다.", MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last Spawned", _lastSpawned == null ? "(none)" : _lastSpawned.name);
        }

        private void DoSpawn()
        {
            Vector3 origin = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.pivot
                : Vector3.zero;
            _lastSpawned = VFXSpawner.Spawn(_prefab, origin, Quaternion.identity, _autoDestroySeconds);
            Debug.Log($"[VFXSpawnerTest] Spawn → {_lastSpawned} at {origin}, autoDestroy={_autoDestroySeconds}s");
        }

        private void DoSpawnAttached()
        {
            _lastSpawned = VFXSpawner.SpawnAttached(_prefab, _attachTarget, _localOffset, _autoDestroySeconds);
            Debug.Log($"[VFXSpawnerTest] SpawnAttached → {_lastSpawned} on {_attachTarget}, offset={_localOffset}");
        }

        private void DoDespawn()
        {
            if (_lastSpawned == null)
            {
                Debug.LogWarning("[VFXSpawnerTest] Despawn 호출 — 마지막 스폰 인스턴스 없음.");
                return;
            }
            string n = _lastSpawned.name;
            VFXSpawner.Despawn(_lastSpawned);
            _lastSpawned = null;
            Debug.Log($"[VFXSpawnerTest] Despawn → {n}");
        }
    }
}
