#if UNITY_EDITOR
using System.Collections.Generic;
using LostMemory.Data;
using UnityEditor;
using UnityEngine;

namespace LostMemory.EditorTools
{
    /// <summary>
    /// CL-090 Step 6 — Auto-revert 안전망.
    ///
    /// 문제: Unity 의 ScriptableObject 는 Play 모드 변경이 자동으로 영구 저장됨.
    ///        라이브 튠 중 사고 (실수로 슬라이더 건드림 등) 가 영구 변경으로 직결.
    ///
    /// 해결: Play 시작 시 모든 WeaponData 자산을 in-memory snapshot.
    ///        Play 종료 시 사용자가 명시적으로 "Save Current Values" 누른 자산 외에는 자동 복원.
    ///        Save 시점이 새 baseline 으로 갱신됨 (Save 후 추가 변경도 정지 시 복원됨).
    ///
    /// 패턴: Unreal Engine 의 PIE (Play In Editor) 자산 격리 워크플로우와 유사.
    /// </summary>
    [InitializeOnLoad]
    public static class WeaponDataPlayModeIsolator
    {
        // Snapshot: assetInstanceID → JSON serialized state at last commit point (Play 시작 또는 마지막 Save 시점).
        private static readonly Dictionary<int, string> _snapshots = new Dictionary<int, string>();

        // Diagnostic: log 출력 여부. 진단 시 true 로 변경. 평소 false (Console 노이즈 방지).
        private const bool VerboseLogs = false;

        static WeaponDataPlayModeIsolator()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // Runtime → Editor 직접 참조 회피용 이벤트 구독.
            // WeaponData 의 "Save Current Values" ContextMenu 가 발화하는 정적 이벤트.
            WeaponDataEvents.OnAssetSaved -= OnAssetSavedFromContextMenu;
            WeaponDataEvents.OnAssetSaved += OnAssetSavedFromContextMenu;
        }

        /// <summary>
        /// Domain reload 가 ExitingEditMode 직후 발생하는 Unity 동작 대응.
        /// playModeStateChanged 의 ExitingEditMode 가 옛 도메인에서 발화 후 unload 되어 누락되는 케이스를
        /// [InitializeOnEnterPlayMode] 가 새 도메인에서 호출되며 backstop.
        /// Play 진입 시 무조건 한 번 호출됨 — Snapshot 안전 보장.
        /// </summary>
        [InitializeOnEnterPlayMode]
        private static void OnEnterPlayMode(EnterPlayModeOptions options)
        {
            SnapshotAllWeaponData();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    SnapshotAllWeaponData();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    RestoreUncommitted();
                    _snapshots.Clear();
                    break;
            }
        }

        /// <summary>
        /// WeaponData 의 "Save Current Values" ContextMenu 클릭 시 호출됨 (이벤트 콜백).
        /// 본 자산의 snapshot 을 현재 값으로 갱신 → Play 종료 시 이 시점 값 보존됨.
        /// </summary>
        private static void OnAssetSavedFromContextMenu(WeaponData asset)
        {
            MarkCommitted(asset);
        }

        /// <summary>
        /// Play 시작 직전: 프로젝트의 모든 WeaponData 자산을 snapshot.
        /// </summary>
        private static void SnapshotAllWeaponData()
        {
            _snapshots.Clear();
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(WeaponData));
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                WeaponData asset = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                if (asset != null)
                {
                    _snapshots[asset.GetInstanceID()] = EditorJsonUtility.ToJson(asset);
                }
            }
            if (VerboseLogs)
            {
                Debug.Log($"[WeaponDataPlayModeIsolator] Snapshotted {_snapshots.Count} WeaponData assets at Play start.");
            }
        }

        /// <summary>
        /// Play 종료 직후: snapshot 으로부터 자산 값을 복원.
        /// "Save Current Values" 클릭한 자산은 snapshot 이 그 시점 값으로 갱신돼 있어, 그 시점으로 복원됨 = 사용자가 의도한 값 보존.
        /// 그 외 자산은 Play 시작 시 snapshot 그대로 = Play 중 변경 폐기.
        /// </summary>
        private static void RestoreUncommitted()
        {
            int restoredCount = 0;
            foreach (var pair in _snapshots)
            {
                Object obj = EditorUtility.InstanceIDToObject(pair.Key);
                if (obj is WeaponData asset && asset != null)
                {
                    EditorJsonUtility.FromJsonOverwrite(pair.Value, asset);
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                    restoredCount++;
                }
            }
            if (VerboseLogs && restoredCount > 0)
            {
                Debug.Log($"[WeaponDataPlayModeIsolator] Restored {restoredCount} WeaponData assets to last commit baseline.");
            }
        }

        /// <summary>
        /// "Save Current Values" ContextMenu 가 호출.
        /// 현재 자산 값을 새 snapshot 으로 저장 → 다음 Play 종료 시 이 시점으로 복원됨 (이후 변경은 폐기).
        /// </summary>
        public static void MarkCommitted(WeaponData asset)
        {
            if (asset == null)
            {
                return;
            }
            int id = asset.GetInstanceID();
            _snapshots[id] = EditorJsonUtility.ToJson(asset);
            if (VerboseLogs)
            {
                Debug.Log($"[WeaponDataPlayModeIsolator] Committed snapshot updated for: {asset.name}");
            }
        }
    }
}
#endif
