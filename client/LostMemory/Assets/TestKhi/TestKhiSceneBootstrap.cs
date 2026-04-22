using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

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

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string inputActionsAssetPath = "Assets/InputSystem_Actions.inputactions";
        [SerializeField] private string actionMapName = "TestKhi";
        [SerializeField] private bool spawnOnAwake = true;
        [SerializeField] private bool createTilemapOnAwake = true;
        [SerializeField] private bool createPersistentTilemapInEditMode = true;
        [SerializeField] private int mapWidth = 18;
        [SerializeField] private int mapHeight = 10;
        [SerializeField] private Vector2 playerStart = Vector2.zero;
        [SerializeField] private Vector2 interactablePosition = new Vector2(2f, 0f);

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                CreatePersistentTilemapIfNeeded();
                return;
            }

            EnsureCamera();

            if (createTilemapOnAwake)
            {
                CreateTilemap();
            }

            if (spawnOnAwake)
            {
                SpawnTestObjects();
            }
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                return;
            }

            EditorApplication.delayCall -= CreatePersistentTilemapIfNeeded;
            EditorApplication.delayCall += CreatePersistentTilemapIfNeeded;
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.delayCall -= CreatePersistentTilemapIfNeeded;
            }
        }
#endif

        private void SpawnTestObjects()
        {
            if (FindObjectOfType<TestKhiPlayerController>() == null)
            {
                CreatePlayer();
            }

            if (FindObjectOfType<TestKhiInteractable>() == null)
            {
                CreateInteractable();
            }
        }

        private void CreatePlayer()
        {
            GameObject player = new GameObject("TestKhi Player");
            player.transform.position = playerStart;

            SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateSprite("TestKhiPlayerSprite", new Color(0.2f, 0.65f, 1f));
            renderer.sortingOrder = 10;

            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            BoxCollider2D collider = player.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.75f, 0.75f);

            TestKhiPlayerController controller = player.AddComponent<TestKhiPlayerController>();
            controller.Configure(ResolveInputActions(), actionMapName);
        }

        private void CreateInteractable()
        {
            GameObject target = new GameObject("TestKhi Interactable");
            target.transform.position = interactablePosition;

            SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateSprite("TestKhiInteractableSprite", new Color(1f, 0.75f, 0.2f));
            renderer.sortingOrder = 5;

            target.AddComponent<TestKhiInteractable>();
        }

        private void CreateTilemap()
        {
            if (FindTilemapGridInScene() != null)
            {
                return;
            }

            GameObject gridObject = new GameObject(TilemapGridName);
            SceneManager.MoveGameObjectToScene(gridObject, gameObject.scene);
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            Tilemap floorTilemap = CreateTilemapLayer(gridObject.transform, "Floor", 0);
            Tilemap wallTilemap = CreateTilemapLayer(gridObject.transform, "Walls", 1);

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

            wallTilemap.gameObject.AddComponent<TilemapCollider2D>();
        }

        private void CreatePersistentTilemapIfNeeded()
        {
            if (!createPersistentTilemapInEditMode || Application.isPlaying || this == null)
            {
                return;
            }

            if (!gameObject.scene.IsValid() || FindTilemapGridInScene() != null)
            {
                return;
            }

            CreateTilemap();

#if UNITY_EDITOR
            Scene scene = gameObject.scene;
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
#endif
        }

        private GameObject FindTilemapGridInScene()
        {
            Scene scene = gameObject.scene;
            if (!scene.IsValid())
            {
                return null;
            }

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                GameObject found = FindChildByName(rootObject.transform, TilemapGridName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
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

        private static Tilemap CreateTilemapLayer(Transform parent, string layerName, int sortingOrder)
        {
            GameObject layer = new GameObject(layerName);
            layer.transform.SetParent(parent);

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
        }
    }
}
