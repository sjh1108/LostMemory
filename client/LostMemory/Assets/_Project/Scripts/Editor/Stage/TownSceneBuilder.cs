using System.Collections.Generic;
using System.IO;
using LostMemory.SceneFlow;
using LostMemory.TestKhi;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace LostMemory.Editor
{
    public static class TownSceneBuilder
    {
        private const string TownScenePath = "Assets/_Project/Scenes/Town/Town.unity";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string DungeonScenePath = "Assets/Scenes/Test/BossClear.unity";
        private const string DungeonSceneName = "BossClear";
        private const string PlayerId = "Player1";
        private const string ActionMapName = "TestKhi";

        [MenuItem("Lost Memory/Scenes/Create Minimal Town Scene")]
        public static void CreateMinimalTownScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (File.Exists(TownScenePath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Create Minimal Town Scene",
                    "Town.unity already exists. Overwrite it?",
                    "Overwrite",
                    "Cancel");

                if (!overwrite)
                {
                    return;
                }
            }

            EnsureSceneFolder();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject root = new GameObject("TownRoot");
            SceneManager.MoveGameObjectToScene(root, scene);

            CreateCamera(scene, root.transform);
            CreateTempTownMap(scene, root.transform);
            CreateSpawnPoint(scene, root.transform);
            CreatePlayer(scene, root.transform);
            CreateInputManager(scene, root.transform);
            CreateDungeonPortal(scene, root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TownScenePath);
            AssetDatabase.Refresh();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(TownScenePath);
            EditorUtility.DisplayDialog(
                "Create Minimal Town Scene",
                "Minimal Town scene created at:\n" + TownScenePath,
                "OK");
        }

        [MenuItem("Lost Memory/Scenes/Add Town Test Scenes To Build Settings")]
        public static void AddTownTestScenesToBuildSettings()
        {
            AddBuildSettingScenes(TownScenePath, DungeonScenePath);
            EditorUtility.DisplayDialog(
                "Build Settings",
                "Town test scenes were added to Build Settings if they were missing.",
                "OK");
        }

        private static void EnsureSceneFolder()
        {
            EnsureFolder("Assets/_Project", "Scenes");
            EnsureFolder("Assets/_Project/Scenes", "Town");
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string path = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static void CreateCamera(Scene scene, Transform parent)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.09f, 0.11f, 1f);

            cameraObject.AddComponent<AudioListener>();
        }

        private static void CreateSpawnPoint(Scene scene, Transform parent)
        {
            GameObject spawnPoint = new GameObject("TownSpawnPoint");
            SceneManager.MoveGameObjectToScene(spawnPoint, scene);
            spawnPoint.transform.SetParent(parent);
            spawnPoint.transform.position = Vector3.zero;
        }

        private static void CreatePlayer(Scene scene, Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject player;

            if (prefab != null)
            {
                player = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            }
            else
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                SceneManager.MoveGameObjectToScene(player, scene);
            }

            if (player == null)
            {
                Debug.LogWarning("[TownSceneBuilder] Could not create player object.");
                return;
            }

            player.name = "Player";
            player.transform.SetParent(parent);
            player.transform.position = Vector3.zero;
            player.transform.rotation = Quaternion.identity;
            player.transform.localScale = Vector3.one;
        }

        private static void CreateInputManager(Scene scene, Transform parent)
        {
            GameObject inputObject = new GameObject("Town Input Manager");
            SceneManager.MoveGameObjectToScene(inputObject, scene);
            inputObject.transform.SetParent(parent);

            TestKhiInputManager inputManager = inputObject.AddComponent<TestKhiInputManager>();
            inputManager.PlayerID = PlayerId;
            inputManager.InputForcedMode = MoreMountains.TopDownEngine.InputManager.InputForcedModes.Desktop;
            inputManager.AutoMobileDetection = false;

            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            inputManager.Configure(actions, ActionMapName);
        }

        private static void CreateDungeonPortal(Scene scene, Transform parent)
        {
            GameObject portal = new GameObject("Portal_ToDungeon");
            SceneManager.MoveGameObjectToScene(portal, scene);
            portal.transform.SetParent(parent);
            portal.transform.position = new Vector3(3f, 0f, 0f);

            CircleCollider2D trigger = portal.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.65f;

            GameObject visual = new GameObject("PortalVisual");
            visual.transform.SetParent(portal.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(1.35f, 1.35f, 1f);

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateSprite("TownPortalSprite", Color.white);
            renderer.color = new Color(0.1f, 0.75f, 1f, 0.8f);
            renderer.sortingOrder = 20;

            SceneLoadPortalController portalController = portal.AddComponent<SceneLoadPortalController>();
            portalController.Configure(DungeonSceneName, true, KeyCode.E, PlayerId);
        }

        private static void CreateTempTownMap(Scene scene, Transform parent)
        {
            GameObject gridObject = new GameObject("TownMap_Temp");
            SceneManager.MoveGameObjectToScene(gridObject, scene);
            gridObject.transform.SetParent(parent);

            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            Tilemap floor = CreateTilemapLayer(gridObject.transform, "Floor", 0, "Ground");
            Tilemap walls = CreateTilemapLayer(gridObject.transform, "Walls", 10, "Obstacles");

            Tile floorTile = CreateTile("Town Floor Tile", new Color(0.16f, 0.19f, 0.18f));
            Tile accentTile = CreateTile("Town Accent Floor Tile", new Color(0.19f, 0.23f, 0.21f));
            Tile wallTile = CreateTile("Town Wall Tile", new Color(0.28f, 0.32f, 0.35f));

            const int halfWidth = 6;
            const int halfHeight = 4;

            for (int x = -halfWidth; x <= halfWidth; x++)
            {
                for (int y = -halfHeight; y <= halfHeight; y++)
                {
                    Vector3Int tilePosition = new Vector3Int(x, y, 0);
                    bool border = x == -halfWidth || x == halfWidth || y == -halfHeight || y == halfHeight;

                    floor.SetTile(tilePosition, ((x + y) & 1) == 0 ? floorTile : accentTile);

                    if (border)
                    {
                        walls.SetTile(tilePosition, wallTile);
                    }
                }
            }

            floor.gameObject.AddComponent<TilemapCollider2D>();
            walls.gameObject.AddComponent<TilemapCollider2D>();
        }

        private static Tilemap CreateTilemapLayer(Transform parent, string objectName, int sortingOrder, string layerName)
        {
            GameObject layer = new GameObject(objectName);
            layer.transform.SetParent(parent);

            int unityLayer = LayerMask.NameToLayer(layerName);
            if (unityLayer >= 0)
            {
                layer.layer = unityLayer;
            }

            Tilemap tilemap = layer.AddComponent<Tilemap>();
            TilemapRenderer renderer = layer.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        private static Tile CreateTile(string tileName, Color color)
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = tileName;
            tile.sprite = CreateSprite(tileName + " Sprite", color);
            tile.color = Color.white;
            return tile;
        }

        private static Sprite CreateSprite(string spriteName, Color color)
        {
            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                name = spriteName + " Texture"
            };

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = spriteName;
            return sprite;
        }

        private static void AddBuildSettingScenes(params string[] paths)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path) || ContainsScene(scenes, path))
                {
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static bool ContainsScene(List<EditorBuildSettingsScene> scenes, string path)
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == path)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
