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
        private const string DungeonScenePath = "Assets/_Project/Scenes/Dungeon/Dungeon.unity";
        private const string DungeonSceneName = "Dungeon";
        private const string PlayerId = "Player1";
        private const string ActionMapName = "TestKhi";
        private const string TownRootName = "TownRoot";
        private const string TownMapName = "TownMap_Temp";

        private static readonly Vector3 TownSpawnPosition = new Vector3(0f, -5f, 0f);
        private static readonly Vector3 TownReturnSpawnPosition = new Vector3(-2f, -1f, 0f);
        private static readonly Vector3 DungeonPortalPosition = new Vector3(0f, 5f, 0f);

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

            GameObject root = new GameObject(TownRootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            CreateCamera(scene, root.transform);
            CreateTempTownMap(scene, root.transform);
            CreateSpawnPoint(scene, root.transform);
            CreatePlayer(scene, root.transform);
            CreateInputManager(scene, root.transform);
            CreateDungeonPortal(scene, root.transform);
            EnsureCl208TownLayoutStructure(scene, root.transform);

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

        [MenuItem("Lost Memory/Scenes/Setup CL-208 Town Layout Structure")]
        public static void SetupCl208TownLayoutStructure()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureSceneFolder();

            Scene scene = File.Exists(TownScenePath)
                ? EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject root = EnsureRootObject(scene, TownRootName);
            EnsureCl208TownLayoutStructure(scene, root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TownScenePath);
            AssetDatabase.Refresh();

            Selection.activeGameObject = root;
            EditorUtility.DisplayDialog(
                "Setup CL-208 Town Layout Structure",
                "CL-208 town layout structure was applied to:\n" + TownScenePath,
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
            spawnPoint.transform.position = TownSpawnPosition;
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
            player.transform.position = TownSpawnPosition;
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
            portal.transform.position = DungeonPortalPosition;

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
            GameObject gridObject = new GameObject(TownMapName);
            SceneManager.MoveGameObjectToScene(gridObject, scene);
            gridObject.transform.SetParent(parent);

            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            Tilemap floor = CreateTilemapLayer(gridObject.transform, "Floor", "Ground", 0, "Ground");
            Tilemap path = CreateTilemapLayer(gridObject.transform, "Path", "Ground", 1, "Ground");
            CreateTilemapLayer(gridObject.transform, "Decoration", "Ground", 5, "Ground");
            CreateTilemapLayer(gridObject.transform, "PropsBack", "Foreground", 5, "Obstacles");
            CreateTilemapLayer(gridObject.transform, "BuildingVisuals", "Foreground", 10, "Obstacles");
            Tilemap walls = CreateTilemapLayer(gridObject.transform, "Walls", "Foreground", 20, "Obstacles");
            CreateTilemapLayer(gridObject.transform, "BuildingForeground", "Above", 0, "Obstacles");
            CreateTilemapLayer(gridObject.transform, "PropsForeground", "Above", 5, "Obstacles");
            CreateTilemapLayer(gridObject.transform, "Markers", "Above", 50, "Ground");

            Tile floorTile = CreateTile("Town Floor Tile", new Color(0.16f, 0.19f, 0.18f));
            Tile accentTile = CreateTile("Town Accent Floor Tile", new Color(0.19f, 0.23f, 0.21f));
            Tile pathTile = CreateTile("Town Path Tile", new Color(0.27f, 0.23f, 0.18f));
            Tile plazaTile = CreateTile("Town Plaza Tile", new Color(0.23f, 0.25f, 0.23f));
            Tile wallTile = CreateTile("Town Wall Tile", new Color(0.28f, 0.32f, 0.35f));

            const int halfWidth = 10;
            const int halfHeight = 7;

            for (int x = -halfWidth; x <= halfWidth; x++)
            {
                for (int y = -halfHeight; y <= halfHeight; y++)
                {
                    Vector3Int tilePosition = new Vector3Int(x, y, 0);
                    bool border = x == -halfWidth || x == halfWidth || y == -halfHeight || y == halfHeight;

                    floor.SetTile(tilePosition, ((x + y) & 1) == 0 ? floorTile : accentTile);

                    bool verticalPath = Mathf.Abs(x) <= 1 && y >= -5 && y <= 5;
                    bool horizontalPath = Mathf.Abs(y) <= 1 && x >= -7 && x <= 7;
                    bool plaza = Mathf.Abs(x) <= 2 && Mathf.Abs(y) <= 2;
                    if (verticalPath || horizontalPath || plaza)
                    {
                        path.SetTile(tilePosition, plaza ? plazaTile : pathTile);
                    }

                    if (border)
                    {
                        walls.SetTile(tilePosition, wallTile);
                    }
                }
            }

            walls.gameObject.AddComponent<TilemapCollider2D>();
        }

        private static Tilemap CreateTilemapLayer(Transform parent, string objectName, string sortingLayerName, int sortingOrder, string layerName)
        {
            GameObject layer = new GameObject(objectName);
            layer.transform.SetParent(parent);
            ResetLocalTransform(layer.transform);

            int unityLayer = LayerMask.NameToLayer(layerName);
            if (unityLayer >= 0)
            {
                layer.layer = unityLayer;
            }

            Tilemap tilemap = layer.AddComponent<Tilemap>();
            TilemapRenderer renderer = layer.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = sortingLayerName;
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

        private static void EnsureCl208TownLayoutStructure(Scene scene, Transform root)
        {
            EnsureTownMapLayers(scene, root);
            EnsureSceneChild(scene, root, "TownProps_Back");
            EnsureSceneChild(scene, root, "TownProps_Foreground");
            EnsureSceneChild(scene, root, "TownVFX");

            GameObject townSpawnPoint = EnsureSceneChild(scene, root, "TownSpawnPoint");
            townSpawnPoint.transform.position = TownSpawnPosition;

            GameObject returnSpawnPoint = EnsureSceneChild(scene, root, "TownReturnSpawnPoint");
            returnSpawnPoint.transform.position = TownReturnSpawnPosition;

            GameObject portal = FindDirectChild(root, "Portal_ToDungeon")?.gameObject;
            if (portal == null)
            {
                CreateDungeonPortal(scene, root);
            }
            else
            {
                portal.transform.position = DungeonPortalPosition;
            }

            EnsurePlaceholder(scene, root, "MemoryHouse_Placeholder", new Vector3(-6f, 2f, 0f), new Color(0.65f, 0.5f, 0.85f, 0.9f));
            EnsurePlaceholder(scene, root, "PlayerHouse_Placeholder", new Vector3(-6f, -3f, 0f), new Color(0.35f, 0.75f, 0.95f, 0.9f));
            EnsurePlaceholder(scene, root, "NpcHouse_Placeholder", new Vector3(5f, -2f, 0f), new Color(0.9f, 0.72f, 0.35f, 0.9f));
            EnsurePlaceholder(scene, root, "NoticeBoard_Placeholder", new Vector3(3.5f, -4f, 0f), new Color(0.75f, 0.55f, 0.3f, 0.9f));
            EnsurePlaceholder(scene, root, "Portal_ToMultiDungeon_Placeholder", new Vector3(6f, 3f, 0f), new Color(0.2f, 0.85f, 0.65f, 0.9f));

            Transform player = FindDirectChild(root, "Player");
            if (player != null)
            {
                player.position = TownSpawnPosition;
            }
        }

        private static void EnsureTownMapLayers(Scene scene, Transform root)
        {
            GameObject mapObject = EnsureSceneChild(scene, root, TownMapName);
            Grid grid = mapObject.GetComponent<Grid>();
            if (grid == null)
            {
                grid = mapObject.AddComponent<Grid>();
            }
            grid.cellSize = Vector3.one;
            ResetLocalTransform(mapObject.transform);

            EnsureTilemapLayer(scene, mapObject.transform, "Floor", "Ground", 0, "Ground", false);
            EnsureTilemapLayer(scene, mapObject.transform, "Path", "Ground", 1, "Ground", false);
            EnsureTilemapLayer(scene, mapObject.transform, "Decoration", "Ground", 5, "Ground", false);
            EnsureTilemapLayer(scene, mapObject.transform, "PropsBack", "Foreground", 5, "Obstacles", false);
            EnsureTilemapLayer(scene, mapObject.transform, "BuildingVisuals", "Foreground", 10, "Obstacles", false);
            EnsureTilemapLayer(scene, mapObject.transform, "Walls", "Foreground", 20, "Obstacles", true);
            EnsureTilemapLayer(scene, mapObject.transform, "BuildingForeground", "Above", 0, "Obstacles", false);
            EnsureTilemapLayer(scene, mapObject.transform, "PropsForeground", "Above", 5, "Obstacles", false);
            EnsureTilemapLayer(scene, mapObject.transform, "Markers", "Above", 50, "Ground", false);
        }

        private static Tilemap EnsureTilemapLayer(Scene scene, Transform parent, string objectName, string sortingLayerName, int sortingOrder, string layerName, bool blocksMovement)
        {
            GameObject layer = EnsureSceneChild(scene, parent, objectName);
            ResetLocalTransform(layer.transform);

            int unityLayer = LayerMask.NameToLayer(layerName);
            if (unityLayer >= 0)
            {
                layer.layer = unityLayer;
            }

            Tilemap tilemap = layer.GetComponent<Tilemap>();
            if (tilemap == null)
            {
                tilemap = layer.AddComponent<Tilemap>();
            }

            TilemapRenderer renderer = layer.GetComponent<TilemapRenderer>();
            if (renderer == null)
            {
                renderer = layer.AddComponent<TilemapRenderer>();
            }
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;

            ConfigureTilemapCollision(layer, blocksMovement);
            return tilemap;
        }

        private static void ConfigureTilemapCollision(GameObject layer, bool blocksMovement)
        {
            TilemapCollider2D[] colliders = layer.GetComponents<TilemapCollider2D>();
            if (blocksMovement)
            {
                if (colliders.Length == 0)
                {
                    layer.AddComponent<TilemapCollider2D>();
                }
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Object.DestroyImmediate(colliders[i]);
            }
        }

        private static void EnsurePlaceholder(Scene scene, Transform root, string objectName, Vector3 position, Color markerColor)
        {
            GameObject placeholder = EnsureSceneChild(scene, root, objectName);
            placeholder.transform.position = position;

            Transform visual = FindDirectChild(placeholder.transform, "PlaceholderVisual");
            GameObject visualObject;
            if (visual == null)
            {
                visualObject = new GameObject("PlaceholderVisual");
                SceneManager.MoveGameObjectToScene(visualObject, scene);
                visualObject.transform.SetParent(placeholder.transform);
                visualObject.transform.localPosition = Vector3.zero;
                visualObject.transform.localRotation = Quaternion.identity;
                visualObject.transform.localScale = Vector3.one;
            }
            else
            {
                visualObject = visual.gameObject;
            }

            SpriteRenderer renderer = visualObject.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = visualObject.AddComponent<SpriteRenderer>();
            }
            renderer.sprite = CreateSprite(objectName + " Marker", Color.white);
            renderer.color = markerColor;
            renderer.sortingLayerName = "Above";
            renderer.sortingOrder = 45;
        }

        private static GameObject EnsureRootObject(Scene scene, string objectName)
        {
            GameObject root = FindRootGameObject(scene, objectName);
            if (root != null)
            {
                return root;
            }

            root = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        private static GameObject EnsureSceneChild(Scene scene, Transform parent, string objectName)
        {
            Transform existing = FindDirectChild(parent, objectName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(child, scene);
            child.transform.SetParent(parent);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        private static void ResetLocalTransform(Transform target)
        {
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static GameObject FindRootGameObject(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == objectName)
                {
                    return roots[i];
                }
            }

            return null;
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
