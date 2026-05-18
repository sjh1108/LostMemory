using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace LostMemory.TestKhi
{
    [ExecuteAlways]
    public class TestKhiSceneBootstrap : MonoBehaviour
    {
        private const string TilemapGridName = "TestKhi Tilemap Grid";
        private const string CharacterObjectName = "TestKhi_MinimalCharacter2D";
        private const string InputManagerObjectName = "TestKhi Input Manager";
        private const string InteractableObjectName = "TestKhi Interaction Test";
        private const string DoorObjectName = "TestKhi Door Block";
        private const string DamageDummyPrefix = "TestKhi Damage Dummy";
        private const string DamageTrapObjectName = "TestKhi Damage Trap";
        private const string ReviveZoneObjectName = "TestKhi Revive Zone";

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string inputActionsAssetPath = "Assets/InputSystem_Actions.inputactions";
        [SerializeField] private string actionMapName = "TestKhi";
        [SerializeField] private bool createTilemapOnAwake = true;
        [SerializeField] private bool createPersistentTilemapInEditMode = true;
        [SerializeField] private bool createPersistentInputManagerInEditMode = true;
        [SerializeField] private bool createPersistentCharacterInEditMode = true;
        [SerializeField] private bool createPersistentInteractableInEditMode = true;
        [SerializeField] private bool createInteractableOnAwake = true;
        [SerializeField] private bool createDamageDummiesOnAwake = true;
        [SerializeField] private bool createDamageTrapOnAwake = true;
        [SerializeField] private string characterPrefabPath = "Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab";
        [SerializeField] private int mapWidth = 18;
        [SerializeField] private int mapHeight = 10;
        [SerializeField] private Vector2 playerStart = Vector2.zero;
        [SerializeField] private Vector2 interactablePosition = new Vector2(2f, 0f);
        [SerializeField] private Vector2 damageTrapPosition = new Vector2(0f, -2.5f);
        [Header("Revive Zone (MVP debug)")]
        [SerializeField] private bool createReviveZoneOnAwake = false;
        [SerializeField] private Vector2 reviveZonePosition = new Vector2(2.5f, -2.5f);
        [SerializeField] private Vector2 reviveZoneSize = new Vector2(1.5f, 1.5f);
        [SerializeField, Min(0f)] private float reviveZoneHoldDuration = 0f;

        [Header("Debug Respawn (Defeated 상태 플레이어 재활성)")]
        [SerializeField] private bool enableDebugRespawnKey = true;
        [SerializeField] private KeyCode debugRespawnKey = KeyCode.P;

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                QueueEnsureEditModeSceneObjects();
                return;
            }

            EnsureCamera();
            EnsureHitStopController();

            if (createTilemapOnAwake)
            {
                CreateTilemap();
            }

            EnsureSingleInteractableObject();

            if (createInteractableOnAwake && FindObjectInScene(InteractableObjectName) == null)
            {
                CreateInteractableObject();
            }

            if (createDamageDummiesOnAwake)
            {
                EnsureDamageDummies();
            }

            if (createDamageTrapOnAwake)
            {
                EnsureDamageTrap();
            }

            if (createReviveZoneOnAwake)
            {
                EnsureReviveZone();
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (enableDebugRespawnKey && Input.GetKeyDown(debugRespawnKey))
            {
                TryDebugRespawnAnyDefeatedPlayer();
            }
        }

        private void TryDebugRespawnAnyDefeatedPlayer()
        {
            KhiDownController[] all = FindObjectsByType<KhiDownController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                KhiDownController ctrl = all[i];
                if (ctrl != null && ctrl.IsDefeated)
                {
                    ctrl.DebugRespawn();
                }
            }
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                return;
            }

            QueueEnsureEditModeSceneObjects();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            QueueEnsureEditModeSceneObjects();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.delayCall -= EnsureEditModeSceneObjects;
                EditorApplication.update -= EditorUpdateEnsureSceneObjects;
            }
        }
#endif

        private void QueueEnsureEditModeSceneObjects()
        {
#if UNITY_EDITOR
            EditorApplication.delayCall -= EnsureEditModeSceneObjects;
            EditorApplication.delayCall += EnsureEditModeSceneObjects;
            EditorApplication.update -= EditorUpdateEnsureSceneObjects;
            EditorApplication.update += EditorUpdateEnsureSceneObjects;
#endif
        }

#if UNITY_EDITOR
        private void EditorUpdateEnsureSceneObjects()
        {
            if (Application.isPlaying || this == null)
            {
                EditorApplication.update -= EditorUpdateEnsureSceneObjects;
                return;
            }

            if (!IsSceneReady())
            {
                return;
            }

            EnsureEditModeSceneObjects();
            EditorApplication.update -= EditorUpdateEnsureSceneObjects;
        }
#endif

        [ContextMenu("Create TestKhi Scene Objects")]
        private void EnsureEditModeSceneObjects()
        {
            if (Application.isPlaying || this == null)
            {
                return;
            }

            if (!IsSceneReady())
            {
                return;
            }

            bool changed = false;
            changed |= EnsureSingleInteractableObject();

            if (createPersistentTilemapInEditMode && FindTilemapGridInScene() == null)
            {
                CreateTilemap();
                changed = true;
            }

            if (createPersistentInputManagerInEditMode && FindObjectInScene(InputManagerObjectName) == null)
            {
                changed |= CreateInputManagerObject();
            }

            if (createPersistentCharacterInEditMode && FindObjectInScene(CharacterObjectName) == null)
            {
                changed |= CreateCharacterPrefabInstance();
            }

            if (createPersistentInteractableInEditMode && FindObjectInScene(InteractableObjectName) == null)
            {
                changed |= CreateInteractableObject();
            }

            if (createDamageTrapOnAwake)
            {
                changed |= EnsureDamageTrap();
            }

            if (createReviveZoneOnAwake)
            {
                changed |= EnsureReviveZone();
            }

#if UNITY_EDITOR
            if (changed && gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        private bool CreateCharacterPrefabInstance()
        {
#if UNITY_EDITOR
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(characterPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Could not find character prefab at '{characterPrefabPath}'.");
                return false;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, gameObject.scene) as GameObject;
            if (instance == null)
            {
                return false;
            }

            instance.name = CharacterObjectName;
            instance.transform.position = playerStart;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return true;
#else
            return false;
#endif
        }

        private bool CreateInputManagerObject()
        {
            GameObject inputManagerObject = new GameObject(InputManagerObjectName);
            SceneManager.MoveGameObjectToScene(inputManagerObject, gameObject.scene);

            TestKhiInputManager inputManager = inputManagerObject.AddComponent<TestKhiInputManager>();
            inputManager.PlayerID = "Player1";
            inputManager.InputForcedMode = MoreMountains.TopDownEngine.InputManager.InputForcedModes.Desktop;
            inputManager.AutoMobileDetection = false;
            inputManager.Configure(ResolveInputActions(), actionMapName);
            return true;
        }

        private bool CreateInteractableObject()
        {
            GameObject interactableObject = new GameObject(InteractableObjectName);
            SceneManager.MoveGameObjectToScene(interactableObject, gameObject.scene);
            interactableObject.transform.position = interactablePosition;
            interactableObject.transform.rotation = Quaternion.identity;
            interactableObject.transform.localScale = Vector3.one;

            SpriteRenderer spriteRenderer = interactableObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreateSprite(InteractableObjectName + "Sprite", Color.white);
            spriteRenderer.color = new Color(0.2f, 0.55f, 0.95f, 0.85f);
            spriteRenderer.sortingOrder = 4;

            BoxCollider2D collider = interactableObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.5f, 1.5f);

            TestKhiInteractionZone interactionZone = interactableObject.AddComponent<TestKhiInteractionZone>();
            interactionZone.ConfigureForTest();
            ConfigureInteractableObject(interactableObject);
            return true;
        }

        private void EnsureDamageDummies()
        {
            CreateDamageDummyIfMissing(DamageDummyPrefix + " Right", new Vector2(2.5f, 0f));
            CreateDamageDummyIfMissing(DamageDummyPrefix + " Up", new Vector2(0f, 2.5f));
            CreateDamageDummyIfMissing(DamageDummyPrefix + " Left", new Vector2(-2.5f, 0f));
            CreateDamageDummyIfMissing(DamageDummyPrefix + " Down", new Vector2(0f, -2.5f));
        }

        private void CreateDamageDummyIfMissing(string objectName, Vector2 position)
        {
            if (FindObjectInScene(objectName) != null)
            {
                return;
            }

            GameObject dummyObject = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(dummyObject, gameObject.scene);
            dummyObject.transform.position = position;
            dummyObject.transform.rotation = Quaternion.identity;
            dummyObject.transform.localScale = Vector3.one;
            dummyObject.AddComponent<TestKhiDamageDummy>();
        }

        private bool EnsureDamageTrap()
        {
            bool changed = false;
            GameObject trapObject = FindObjectInScene(DamageTrapObjectName);
            if (trapObject == null)
            {
                trapObject = new GameObject(DamageTrapObjectName);
                SceneManager.MoveGameObjectToScene(trapObject, gameObject.scene);
                trapObject.transform.position = damageTrapPosition;
                trapObject.transform.rotation = Quaternion.identity;
                trapObject.transform.localScale = Vector3.one;
                changed = true;
            }

            TestKhiDamageTrap trap = trapObject.GetComponent<TestKhiDamageTrap>();
            if (trap == null)
            {
                trap = trapObject.AddComponent<TestKhiDamageTrap>();
                changed = true;
            }

            trap.ConfigureForTest();
            return changed;
        }

        private bool EnsureReviveZone()
        {
            bool changed = false;
            GameObject zoneObject = FindObjectInScene(ReviveZoneObjectName);
            if (zoneObject == null)
            {
                zoneObject = new GameObject(ReviveZoneObjectName);
                SceneManager.MoveGameObjectToScene(zoneObject, gameObject.scene);
                zoneObject.transform.position = reviveZonePosition;
                zoneObject.transform.rotation = Quaternion.identity;
                zoneObject.transform.localScale = Vector3.one;
                changed = true;
            }

            BoxCollider2D boxCollider = zoneObject.GetComponent<BoxCollider2D>();
            if (boxCollider == null)
            {
                boxCollider = zoneObject.AddComponent<BoxCollider2D>();
                changed = true;
            }
            boxCollider.isTrigger = true;
            boxCollider.size = reviveZoneSize;

            TestKhiReviveZone zone = zoneObject.GetComponent<TestKhiReviveZone>();
            if (zone == null)
            {
                zone = zoneObject.AddComponent<TestKhiReviveZone>();
                changed = true;
            }

            SerializedFieldSet(zone, "holdDuration", reviveZoneHoldDuration);
            return changed;
        }

        private static void SerializedFieldSet(UnityEngine.Object target, string fieldName, object value)
        {
            if (target == null || string.IsNullOrEmpty(fieldName))
            {
                return;
            }

            System.Reflection.FieldInfo field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private bool EnsureSingleInteractableObject()
        {
            if (!IsSceneReady())
            {
                return false;
            }

            TestKhiInteractionZone keepZone = null;
            bool changed = false;
            TestKhiInteractionZone[] zones = FindObjectsByType<TestKhiInteractionZone>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);

            foreach (TestKhiInteractionZone zone in zones)
            {
                if (zone == null || zone.gameObject.scene != gameObject.scene || !IsManagedInteractableName(zone.gameObject.name))
                {
                    continue;
                }

                if (keepZone == null)
                {
                    keepZone = zone;
                    continue;
                }
            }

            if (keepZone == null)
            {
                return changed;
            }

            if (keepZone.gameObject.name != InteractableObjectName)
            {
                keepZone.gameObject.name = InteractableObjectName;
                changed = true;
            }

            TestKhiInteractionZone[] duplicateComponents = keepZone.GetComponents<TestKhiInteractionZone>();
            for (int i = 1; i < duplicateComponents.Length; i++)
            {
                DestroySceneObject(duplicateComponents[i]);
                changed = true;
            }

            keepZone.ConfigureForTest();
            changed |= ConfigureInteractableObject(keepZone.gameObject);
            return changed;
        }

        private bool ConfigureInteractableObject(GameObject interactableObject)
        {
            if (interactableObject == null)
            {
                return false;
            }

            bool changed = false;
            SpriteRenderer triggerRenderer = interactableObject.GetComponent<SpriteRenderer>();
            if (triggerRenderer == null)
            {
                triggerRenderer = interactableObject.AddComponent<SpriteRenderer>();
                triggerRenderer.sprite = CreateSprite(InteractableObjectName + "Sprite", Color.white);
                changed = true;
            }

            triggerRenderer.color = new Color(0.2f, 0.55f, 0.95f, 0.85f);
            triggerRenderer.sortingOrder = 4;

            BoxCollider2D triggerCollider = interactableObject.GetComponent<BoxCollider2D>();
            if (triggerCollider == null)
            {
                triggerCollider = interactableObject.AddComponent<BoxCollider2D>();
                changed = true;
            }

            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector2(1.5f, 1.5f);

            TestKhiInteractionZone interactionZone = interactableObject.GetComponent<TestKhiInteractionZone>();
            if (interactionZone == null)
            {
                interactionZone = interactableObject.AddComponent<TestKhiInteractionZone>();
                changed = true;
            }

            GameObject doorObject = EnsureSingleDirectChild(interactableObject.transform, DoorObjectName, ref changed);
            if (doorObject == null)
            {
                doorObject = new GameObject(DoorObjectName);
                doorObject.transform.SetParent(interactableObject.transform);
                changed = true;
            }

            doorObject.transform.localPosition = new Vector3(0.95f, 0f, 0f);
            doorObject.transform.localRotation = Quaternion.identity;
            doorObject.transform.localScale = Vector3.one;

            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            if (obstacleLayer >= 0)
            {
                doorObject.layer = obstacleLayer;
            }

            SpriteRenderer doorRenderer = doorObject.GetComponent<SpriteRenderer>();
            if (doorRenderer == null)
            {
                doorRenderer = doorObject.AddComponent<SpriteRenderer>();
                doorRenderer.sprite = CreateSprite(DoorObjectName + "Sprite", Color.white);
                changed = true;
            }

            doorRenderer.sortingOrder = 5;

            BoxCollider2D doorCollider = doorObject.GetComponent<BoxCollider2D>();
            if (doorCollider == null)
            {
                doorCollider = doorObject.AddComponent<BoxCollider2D>();
                changed = true;
            }

            doorCollider.isTrigger = false;
            doorCollider.size = new Vector2(0.9f, 1.8f);

            TestKhiDoorAction doorAction = interactableObject.GetComponent<TestKhiDoorAction>();
            if (doorAction == null)
            {
                doorAction = interactableObject.AddComponent<TestKhiDoorAction>();
                changed = true;
            }

            doorAction.Configure(doorRenderer, doorCollider);
            interactionZone.ConfigureForTest();
            changed |= EnsureDoorwayInTilemap();
            return changed;
        }

        private bool EnsureDoorwayInTilemap()
        {
            GameObject wallsObject = FindObjectInScene("Walls");
            Tilemap walls = wallsObject != null ? wallsObject.GetComponent<Tilemap>() : null;
            if (walls == null)
            {
                return false;
            }

            bool changed = false;
            for (int x = 3; x <= 4; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    if (walls.GetTile(cell) == null)
                    {
                        continue;
                    }

                    walls.SetTile(cell, null);
                    changed = true;
                }
            }

            if (changed)
            {
                walls.RefreshAllTiles();
                TilemapCollider2D tilemapCollider = walls.GetComponent<TilemapCollider2D>();
                if (tilemapCollider != null)
                {
                    tilemapCollider.enabled = false;
                    tilemapCollider.enabled = true;
                }
            }

            return changed;
        }

        private static bool IsManagedInteractableName(string objectName)
        {
            return objectName == InteractableObjectName || objectName.StartsWith(InteractableObjectName + " (");
        }

        private static void DestroySceneObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
                return;
            }

#if UNITY_EDITOR
            DestroyImmediate(target);
#endif
        }

        private InputActionAsset ResolveInputActions()
        {
            if (inputActions != null)
            {
                return inputActions;
            }

#if UNITY_EDITOR
            inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputActionsAssetPath);
#endif

            return inputActions;
        }

        private void CreateTilemap()
        {
            if (!gameObject.scene.IsValid() || FindTilemapGridInScene() != null)
            {
                return;
            }

            GameObject gridObject = new GameObject(TilemapGridName);
            SceneManager.MoveGameObjectToScene(gridObject, gameObject.scene);
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            Tilemap floorTilemap = CreateTilemapLayer(gridObject.transform, "Floor", 0, "Ground");
            Tilemap wallTilemap = CreateTilemapLayer(gridObject.transform, "Walls", 1, "Obstacles");

            Tile floorTile = CreateTile("TestKhi Floor Tile", new Color(0.18f, 0.22f, 0.25f));
            Tile accentFloorTile = CreateTile("TestKhi Accent Floor Tile", new Color(0.23f, 0.28f, 0.31f));
            Tile wallTile = CreateTile("TestKhi Wall Tile", new Color(0.38f, 0.42f, 0.46f));

            int halfWidth = mapWidth / 2;
            int halfHeight = mapHeight / 2;

            for (int x = -halfWidth; x < halfWidth; x++)
            {
                for (int y = -halfHeight; y < halfHeight; y++)
                {
                    Vector3Int position = new Vector3Int(x, y, 0);
                    bool isBorder = x == -halfWidth || x == halfWidth - 1 || y == -halfHeight || y == halfHeight - 1;
                    bool isObstacle = (x == -3 && y >= -1 && y <= 2) || (x == 4 && y >= -2 && y <= 1);

                    floorTilemap.SetTile(position, ((x + y) % 2 == 0) ? floorTile : accentFloorTile);

                    if (isBorder || isObstacle)
                    {
                        wallTilemap.SetTile(position, wallTile);
                    }
                }
            }

            floorTilemap.gameObject.AddComponent<TilemapCollider2D>();
            wallTilemap.gameObject.AddComponent<TilemapCollider2D>();
        }

        private static Tilemap CreateTilemapLayer(Transform parent, string objectName, int sortingOrder, string unityLayerName)
        {
            GameObject layer = new GameObject(objectName);
            layer.transform.SetParent(parent);
            int unityLayer = LayerMask.NameToLayer(unityLayerName);
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
            tile.sprite = CreateSprite(tileName + "Sprite", color);
            tile.color = Color.white;
            return tile;
        }

        private static Sprite CreateSprite(string spriteName, Color color)
        {
            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                name = spriteName + "Texture"
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

        private GameObject FindTilemapGridInScene()
        {
            return FindObjectInScene(TilemapGridName);
        }

        private GameObject FindObjectInScene(string objectName)
        {
            Scene scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                GameObject found = FindChildByName(rootObject.transform, objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private bool IsSceneReady()
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static GameObject FindChildByName(Transform current, string objectName)
        {
            if (current.name == objectName)
            {
                return current.gameObject;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                GameObject found = FindChildByName(current.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static GameObject FindDirectChild(Transform parent, string objectName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == objectName)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static GameObject EnsureSingleDirectChild(Transform parent, string objectName, ref bool changed)
        {
            GameObject keepObject = null;

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name != objectName && !child.name.StartsWith(objectName + " ("))
                {
                    continue;
                }

                if (keepObject == null)
                {
                    keepObject = child.gameObject;
                    continue;
                }

                DestroySceneObject(child.gameObject);
                changed = true;
            }

            if (keepObject != null && keepObject.name != objectName)
            {
                keepObject.name = objectName;
                changed = true;
            }

            return keepObject;
        }

        private static void EnsureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            EnsurePlayerCameraRig(camera);
        }

        private static void EnsureHitStopController()
        {
            if (KhiHitStopController.Instance != null)
            {
                return;
            }

            GameObject host = new GameObject("KhiHitStopController");
            host.AddComponent<KhiHitStopController>();
        }

        private static void EnsurePlayerCameraRig(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            if (camera.GetComponent<KhiPlayerCamera>() == null)
            {
                camera.gameObject.AddComponent<KhiPlayerCamera>();
            }

            PixelPerfectCamera ppc = camera.GetComponent<PixelPerfectCamera>();
            if (ppc == null)
            {
                ppc = camera.gameObject.AddComponent<PixelPerfectCamera>();
            }

            ppc.assetsPPU = 16;
            ppc.refResolutionX = 320;
            ppc.refResolutionY = 180;
            ppc.upscaleRT = false;
            ppc.cropFrameX = false;
            ppc.cropFrameY = false;
        }
    }
}
