using System.Collections.Generic;
using System.IO;
using LostMemory.TestKhi;
using UnityEditor;
using UnityEngine;

namespace LostMemory.EditorTools
{
    /// <summary>
    /// `Assets/_Project/Prefabs/Weapons/` 하위 모든 prefab 일괄 검사 →
    /// <see cref="KhiArrowProjectile"/> 컴포넌트의 <c>destroyOnHit</c> SerializedField 를 true 로 강제.
    ///
    /// 배경 (Bug #33 후속):
    ///   투사체 (Bolt/Fireball/Arrow) 가 적 hit 시 damage 는 적용되지만 *destroy 안 됨* 보고.
    ///   원인: prefab inspector 의 <c>destroyOnHit</c> 가 false 로 설정되어 있어 OnTriggerEnter2D 끝의
    ///   `if (destroyOnHit) PlayImpactAndDestroy();` 가 skip → 화살이 계속 비행.
    ///
    /// 픽스: 모든 투사체 prefab 의 destroyOnHit 를 true 로 자동 설정.
    ///
    /// 사용:
    /// 1. Tools > Lost Memory > Weapons > [DRY RUN] List Projectiles Needing DestroyOnHit
    /// 2. Tools > Lost Memory > Weapons > [APPLY] Enforce DestroyOnHit On All Projectiles
    /// </summary>
    public static class EnforceProjectileDestroyOnHit
    {
        private const string WeaponsFolder = "Assets/_Project/Prefabs/Weapons";
        private const string MenuRoot = "Tools/Lost Memory/Weapons/";
        private const string DestroyOnHitFieldName = "destroyOnHit";

        [MenuItem(MenuRoot + "[DRY RUN] List Projectiles Needing DestroyOnHit")]
        public static void DryRun()
        {
            Process(applyChanges: false);
        }

        [MenuItem(MenuRoot + "[APPLY] Enforce DestroyOnHit On All Projectiles")]
        public static void Apply()
        {
            if (!EditorUtility.DisplayDialog(
                "Projectile DestroyOnHit 일괄 적용",
                $"{WeaponsFolder} 하위 모든 KhiArrowProjectile prefab 의 destroyOnHit 를 true 로 설정합니다.\n\n" +
                "이미 true 인 prefab 은 스킵.\n\n" +
                "변경된 prefab 은 자동 저장됩니다. Git 커밋 권장.",
                "진행",
                "취소"))
            {
                return;
            }

            Process(applyChanges: true);
        }

        private static void Process(bool applyChanges)
        {
            if (!AssetDatabase.IsValidFolder(WeaponsFolder))
            {
                Debug.LogError($"[EnforceProjectileDestroyOnHit] Folder not found: {WeaponsFolder}");
                return;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { WeaponsFolder });
            int updated = 0;
            int alreadyTrue = 0;
            int skippedNoComponent = 0;
            int failed = 0;
            var updatedList = new List<string>();
            var skippedList = new List<string>();

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);

                GameObject contents = null;
                try
                {
                    contents = PrefabUtility.LoadPrefabContents(path);
                    if (contents == null)
                    {
                        failed++;
                        Debug.LogWarning($"[EnforceProjectileDestroyOnHit] LoadPrefabContents returned null: {path}");
                        continue;
                    }

                    KhiArrowProjectile projectile = contents.GetComponent<KhiArrowProjectile>();
                    if (projectile == null)
                    {
                        skippedNoComponent++;
                        skippedList.Add(fileName);
                        continue;
                    }

                    // private SerializeField 접근은 SerializedObject 경유.
                    SerializedObject so = new SerializedObject(projectile);
                    SerializedProperty destroyOnHitProp = so.FindProperty(DestroyOnHitFieldName);
                    if (destroyOnHitProp == null)
                    {
                        failed++;
                        Debug.LogWarning($"[EnforceProjectileDestroyOnHit] '{DestroyOnHitFieldName}' SerializedProperty not found on '{fileName}'. KhiArrowProjectile.cs 의 field name 확인 필요.");
                        continue;
                    }

                    if (destroyOnHitProp.boolValue)
                    {
                        alreadyTrue++;
                        continue;
                    }

                    if (applyChanges)
                    {
                        destroyOnHitProp.boolValue = true;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                    }
                    updated++;
                    updatedList.Add($"{fileName} (false → true)");
                }
                catch (System.Exception ex)
                {
                    failed++;
                    Debug.LogError($"[EnforceProjectileDestroyOnHit] Failed on {path}: {ex.Message}");
                }
                finally
                {
                    if (contents != null)
                    {
                        PrefabUtility.UnloadPrefabContents(contents);
                    }
                }
            }

            if (applyChanges)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string mode = applyChanges ? "[APPLY]" : "[DRY RUN]";
            Debug.Log(
                $"{mode} Projectile DestroyOnHit 적용 결과:\n" +
                $"  • {(applyChanges ? "true 로 변경" : "변경 대상")}: {updated}\n" +
                $"  • 이미 true (skip): {alreadyTrue}\n" +
                $"  • KhiArrowProjectile 미보유 (skip): {skippedNoComponent}\n" +
                $"  • 실패: {failed}\n" +
                $"  • 총 검사 prefab: {prefabGuids.Length}");

            if (updated > 0)
            {
                Debug.Log($"{mode} 변경 대상 목록:\n  - " + string.Join("\n  - ", updatedList));
            }
            if (skippedNoComponent > 0)
            {
                Debug.Log($"{mode} KhiArrowProjectile 없는 prefab (정상 skip):\n  - " + string.Join("\n  - ", skippedList));
            }
        }
    }
}
