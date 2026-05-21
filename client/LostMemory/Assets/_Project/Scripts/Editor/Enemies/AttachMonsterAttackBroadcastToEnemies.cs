using System.Collections.Generic;
using System.IO;
using LostMemory.Combat.Telegraph;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace LostMemory.EditorTools
{
    /// <summary>
    /// `Assets/_Project/Prefabs/Enemies/` 하위 모든 prefab 일괄 검사 →
    /// NetworkObject 있는 prefab root 에 <see cref="MonsterAttackBroadcast"/> 가 없으면 자동 부착.
    ///
    /// 배경:
    ///   NGO 는 prefab 기반 spawn → NetworkBehaviour 런타임 AddComponent 는 게스트와 schema mismatch.
    ///   따라서 *모든 enemy prefab 에 사전 부착* 만이 정공. 새 enemy 추가 시 본 menu 한 번 실행하면
    ///   자동으로 모든 enemy 에 broadcast 컴포넌트 부착 → 게스트 측 telegraph 시각 sync 동작.
    ///
    /// 사용:
    /// 1. Tools > Lost Memory > Enemies > [DRY RUN] List Enemies Needing MonsterAttackBroadcast
    ///    → 처리 대상 prefab 목록 콘솔 출력만 (변경 없음).
    /// 2. 콘솔 확인 후 OK 면:
    /// 3. Tools > Lost Memory > Enemies > [APPLY] Attach MonsterAttackBroadcast To All Enemies
    ///    → 확인 대화상자 후 실제 prefab 수정 + 저장.
    ///
    /// 식별 규칙:
    ///   - `Assets/_Project/Prefabs/Enemies/` 의 .prefab 파일 (재귀 포함 — Boss/ 하위도 검색)
    ///   - root 에 NetworkObject 가 있어야 부착 가능 (없으면 skip + 경고)
    ///   - 이미 MonsterAttackBroadcast 부착되어 있으면 skip
    ///
    /// 제외 (이름 패턴):
    ///   - 발사체 / area attack 인스턴스 prefab — 본체 몬스터가 아닌 *공격용 인스턴스* 는 부착 불필요.
    ///   - 예: SkeletonMageProjectile, SkeletonMageAreaAttack 등 ("Projectile", "AreaAttack", "Bullet", "Missile" 키워드)
    ///   - 단, 부착되어도 무해 (broadcast 호출 안 하면 동작 안 함) — 그저 *몬스터 본체만* 명확히 하려는 의도.
    ///
    /// 안전성:
    ///   - PrefabUtility.LoadPrefabContents / SaveAsPrefabAsset 사용 → Unity 표준 prefab 수정.
    ///   - 처리 중 실패한 prefab 만 스킵, 전체 중단 X.
    ///   - Git 으로 단일 커밋 백업 후 실행 권장.
    /// </summary>
    public static class AttachMonsterAttackBroadcastToEnemies
    {
        private const string EnemiesFolder = "Assets/_Project/Prefabs/Enemies";
        private const string MenuRoot = "Tools/Lost Memory/Enemies/";

        // 본체가 아닌 *공격 인스턴스* prefab 제외 (이름 키워드 매칭). 부착되어도 무해하나 깔끔하게 분리.
        private static readonly string[] ExcludeNameKeywords =
        {
            "Projectile",
            "AreaAttack",
            "Bullet",
            "Missile",
            "Spell",
        };

        [MenuItem(MenuRoot + "[DRY RUN] List Enemies Needing MonsterAttackBroadcast")]
        public static void DryRun()
        {
            Process(applyChanges: false);
        }

        [MenuItem(MenuRoot + "[APPLY] Attach MonsterAttackBroadcast To All Enemies")]
        public static void Apply()
        {
            if (!EditorUtility.DisplayDialog(
                "MonsterAttackBroadcast 일괄 부착",
                $"{EnemiesFolder} 하위 모든 enemy prefab 에 MonsterAttackBroadcast 를 추가합니다.\n\n" +
                "이미 부착된 prefab + 발사체/AOE 인스턴스 prefab 은 스킵됩니다.\n\n" +
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
            if (!AssetDatabase.IsValidFolder(EnemiesFolder))
            {
                Debug.LogError($"[AttachMonsterAttackBroadcast] Folder not found: {EnemiesFolder}");
                return;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemiesFolder });
            int attached = 0;
            int alreadyAttached = 0;
            int skippedByName = 0;
            int skippedNoNetworkObject = 0;
            int failed = 0;
            var attachedList = new List<string>();
            var noNetObjList = new List<string>();
            var skippedNameList = new List<string>();

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);

                // 제외 키워드 매칭
                if (ContainsExcludedKeyword(fileName))
                {
                    skippedByName++;
                    skippedNameList.Add(fileName);
                    continue;
                }

                GameObject contents = null;
                try
                {
                    contents = PrefabUtility.LoadPrefabContents(path);
                    if (contents == null)
                    {
                        failed++;
                        Debug.LogWarning($"[AttachMonsterAttackBroadcast] LoadPrefabContents returned null: {path}");
                        continue;
                    }

                    // NetworkObject 존재 검증 — NetworkBehaviour 동작 전제 조건.
                    NetworkObject netObj = contents.GetComponent<NetworkObject>();
                    if (netObj == null)
                    {
                        skippedNoNetworkObject++;
                        noNetObjList.Add($"{fileName} ({path})");
                        continue;
                    }

                    // 이미 부착되어 있나
                    MonsterAttackBroadcast existing = contents.GetComponent<MonsterAttackBroadcast>();
                    if (existing != null)
                    {
                        alreadyAttached++;
                        continue;
                    }

                    // 부착 + 저장
                    if (applyChanges)
                    {
                        contents.AddComponent<MonsterAttackBroadcast>();
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                    }
                    attached++;
                    attachedList.Add($"{fileName} ({path})");
                }
                catch (System.Exception ex)
                {
                    failed++;
                    Debug.LogError($"[AttachMonsterAttackBroadcast] Failed on {path}: {ex.Message}");
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

            // 결과 보고
            string mode = applyChanges ? "[APPLY]" : "[DRY RUN]";
            Debug.Log(
                $"{mode} MonsterAttackBroadcast 부착 결과:\n" +
                $"  • {(applyChanges ? "부착됨" : "부착 대상")}: {attached}\n" +
                $"  • 이미 부착됨 (skip): {alreadyAttached}\n" +
                $"  • 이름 제외 (Projectile/AreaAttack 등 skip): {skippedByName}\n" +
                $"  • NetworkObject 누락 (skip + 수동 작업 필요): {skippedNoNetworkObject}\n" +
                $"  • 실패: {failed}\n" +
                $"  • 총 검사 prefab: {prefabGuids.Length}");

            if (attached > 0)
            {
                Debug.Log($"{mode} 부착 대상 목록:\n  - " + string.Join("\n  - ", attachedList));
            }
            if (skippedNoNetworkObject > 0)
            {
                Debug.LogWarning(
                    $"{mode} NetworkObject 없어 skip 된 prefab — 멀티 sync 가 필요하면 수동으로 NetworkObject 추가 후 본 menu 재실행:\n  - " +
                    string.Join("\n  - ", noNetObjList));
            }
            if (skippedByName > 0)
            {
                Debug.Log(
                    $"{mode} 이름 제외 (발사체/AOE 인스턴스 — 본체 몬스터 아님) skip:\n  - " +
                    string.Join("\n  - ", skippedNameList));
            }
        }

        private static bool ContainsExcludedKeyword(string fileName)
        {
            for (int i = 0; i < ExcludeNameKeywords.Length; i++)
            {
                if (fileName.IndexOf(ExcludeNameKeywords[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
