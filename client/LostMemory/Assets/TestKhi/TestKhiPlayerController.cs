using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LostMemory.TestKhi
{
    public class TestKhiPlayerController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string inputActionsAssetPath = "Assets/InputSystem_Actions.inputactions";
        [SerializeField] private string actionMapName = "TestKhi";
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float interactRadius = 1.25f;

        private InputActionMap _actionMap;
        private InputAction _moveAction;
        private InputAction _interactAction;
        private Rigidbody2D _rigidbody;
        private Vector2 _moveInput;
        private bool _isBound;

        public void Configure(InputActionAsset actions, string mapName)
        {
            inputActions = actions;
            actionMapName = mapName;
            BindActions();
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            BindActions();
        }

        private void OnEnable()
        {
            BindActions();
            _actionMap?.Enable();
        }

        private void OnDisable()
        {
            if (_interactAction != null)
            {
                _interactAction.performed -= OnInteract;
            }

            _actionMap?.Disable();
            _isBound = false;
        }

        private void Update()
        {
            if (_moveAction != null)
            {
                _moveInput = _moveAction.ReadValue<Vector2>();
            }
        }

        private void FixedUpdate()
        {
            Vector2 clampedInput = Vector2.ClampMagnitude(_moveInput, 1f);
            Vector2 delta = clampedInput * moveSpeed * Time.fixedDeltaTime;

            if (_rigidbody != null)
            {
                _rigidbody.MovePosition(_rigidbody.position + delta);
                return;
            }

            transform.position += new Vector3(delta.x, delta.y, 0f);
        }

        private void BindActions()
        {
            if (_isBound)
            {
                return;
            }

            InputActionAsset actions = ResolveInputActions();
            if (actions == null)
            {
                Debug.LogError("TestKhiPlayerController could not find InputSystem_Actions.inputactions.");
                return;
            }

            _actionMap = actions.FindActionMap(actionMapName, false);
            if (_actionMap == null)
            {
                Debug.LogError($"Action map '{actionMapName}' was not found.");
                return;
            }

            _moveAction = _actionMap.FindAction("Move", false);
            _interactAction = _actionMap.FindAction("Interact", false);

            if (_moveAction == null || _interactAction == null)
            {
                Debug.LogError($"Action map '{actionMapName}' needs Move and Interact actions.");
                return;
            }

            _interactAction.performed += OnInteract;
            _actionMap.Enable();
            _isBound = true;
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

        private void OnInteract(InputAction.CallbackContext context)
        {
            TestKhiInteractable nearest = FindNearestInteractable();
            if (nearest == null)
            {
                Debug.Log("No interactable object nearby.");
                return;
            }

            nearest.Interact(gameObject);
        }

        private TestKhiInteractable FindNearestInteractable()
        {
            TestKhiInteractable[] interactables = FindObjectsOfType<TestKhiInteractable>();
            TestKhiInteractable nearest = null;
            float nearestDistance = interactRadius;

            foreach (TestKhiInteractable interactable in interactables)
            {
                float distance = Vector2.Distance(transform.position, interactable.transform.position);
                if (distance <= nearestDistance)
                {
                    nearest = interactable;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }
    }
}
