using LostMemory.Networking.Monster;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace LostMemory.EditorTools.Enemies
{
    /// <summary>
    /// 선택된 prefab(들) root 에 NetworkObject + NetworkTransform + MonsterHealthSync + MonsterNetSync + NetworkAnimator 일괄 부착.
    ///
    /// 사용:
    ///   1. Project 창에서 Enemy prefab 들 다중 선택
    ///   2. 메뉴: Lost Memory > Sync > Add Monster Sync To Selected Prefabs
    ///   3. 콘솔 + 다이얼로그에 처리 결과 표시
    ///
    /// 이미 부착된 컴포넌트는 skip (idempotent).
    /// NetworkObject 의 GlobalObjectIdHash 는 Unity 가 자동 stamp.
    /// NetworkTransform 은 server-authoritative (default) — 호스트만 위치 변경, 게스트는 sync 받음.
    /// NetworkAnimator 는 Animator 컴포넌트가 있는 GameObject (root 또는 자식 자동 검색) 에 부착.
    /// </summary>
    public static class AddMonsterSyncMenu
    {
        private const string MenuPath = "Lost Memory/Sync/Add Monster Sync To Selected Prefabs";

        [MenuItem(MenuPath)]
        private static void AddMonsterSync()
        {
            int processed = 0;
            int skipped = 0;
            int errors = 0;

            foreach (Object obj in Selection.objects)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".prefab"))
                {
                    skipped++;
                    continue;
                }

                GameObject prefab = PrefabUtility.LoadPrefabContents(assetPath);
                if (prefab == null)
                {
                    Debug.LogError($"[AddMonsterSync] Failed to load: {assetPath}");
                    errors++;
                    continue;
                }

                bool dirty = false;

                // 1. NetworkObject (root 에 부착)
                if (prefab.GetComponent<NetworkObject>() == null)
                {
                    prefab.AddComponent<NetworkObject>();
                    dirty = true;
                }

                // 2. NetworkTransform — server-authoritative 위치/회전 sync.
                //    MonsterNetSync 주석에 명시: "위치/회전: NetworkTransform 가 sync. 본 컴포넌트는 그 옆에 부착."
                if (prefab.GetComponent<NetworkTransform>() == null)
                {
                    prefab.AddComponent<NetworkTransform>();
                    dirty = true;
                }

                // 3. MonsterHealthSync
                if (prefab.GetComponent<MonsterHealthSync>() == null)
                {
                    prefab.AddComponent<MonsterHealthSync>();
                    dirty = true;
                }

                // 4. MonsterNetSync
                if (prefab.GetComponent<MonsterNetSync>() == null)
                {
                    prefab.AddComponent<MonsterNetSync>();
                    dirty = true;
                }

                // 5. NetworkAnimator — Animator 컴포넌트가 있는 GameObject 에 부착 (애니메이션 동기화).
                //    Animator 가 root 가 아닌 자식에 있을 수 있어 GetComponentInChildren 사용.
                Animator animator = prefab.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    GameObject animatorHost = animator.gameObject;
                    if (animatorHost.GetComponent<NetworkAnimator>() == null)
                    {
                        NetworkAnimator netAnim = animatorHost.AddComponent<NetworkAnimator>();
                        // NetworkAnimator 의 Animator field 자동 set (Awake 에서 처리되지만 명시).
                        SerializedObject so = new SerializedObject(netAnim);
                        SerializedProperty animProp = so.FindProperty("m_Animator");
                        if (animProp != null)
                        {
                            animProp.objectReferenceValue = animator;
                            so.ApplyModifiedProperties();
                        }
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefab, assetPath);
                    Debug.Log($"[AddMonsterSync] Updated: {assetPath}");
                    processed++;
                }
                else
                {
                    Debug.Log($"[AddMonsterSync] Already complete: {assetPath}");
                    skipped++;
                }

                PrefabUtility.UnloadPrefabContents(prefab);

                // NetworkObject 의 GlobalObjectIdHash 는 prefab import 시점에 stamp 됨.
                // SaveAsPrefabAsset 만으론 hash 가 stamp 안 되는 경우가 있어
                // ForceUpdate 로 명시적 re-import 트리거 (NGO 의 OnPostprocessAllAssets 콜백 발화).
                // 이걸 안 하면 DefaultNetworkPrefabs.asset 에 등록돼도 hash=0 으로 NGO 가 "invalid" 라며 제거 →
                // enemy spawn / sync 모두 작동 안 함.
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            // 모든 prefab 처리 후 한 번 더 SaveAssets / Refresh — DefaultNetworkPrefabs.asset 의 hash 재계산도 트리거.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Add Monster Sync",
                $"처리됨: {processed}\nSkip (이미 있음/대상 아님): {skipped}\n에러: {errors}\n\n" +
                $"부착 컴포넌트:\n- NetworkObject (root)\n- NetworkTransform (root, server-authoritative)\n- MonsterHealthSync (root)\n- MonsterNetSync (root)\n- NetworkAnimator (Animator 가 있는 GameObject)",
                "OK");
        }

        [MenuItem(MenuPath, true)]
        private static bool AddMonsterSyncValidate()
        {
            return Selection.objects != null && Selection.objects.Length > 0;
        }
    }
}
