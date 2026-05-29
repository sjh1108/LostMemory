using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LostMemory.TestKhi
{
    [DefaultExecutionOrder(-100)]
    public class TestKhiInputManager : InputManager
    {
        private enum MovementInputSource
        {
            None,
            ActionMap,
            DirectFallback
        }

        private enum ButtonInputSource
        {
            None,
            ActionMap,
            DirectFallback
        }

        private struct ButtonInputState
        {
            public ButtonInputSource Source;
            public bool IsPressed;
            public bool WasPressedThisFrame;
            public bool WasReleasedThisFrame;
        }

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string inputActionsAssetPath = "Assets/InputSystem_Actions.inputactions";
        [SerializeField] private string actionMapName = "TestKhi";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string interactActionName = "Interact";
        [SerializeField] private string attackActionName = "Attack";
        [SerializeField] private string dashActionName = "Dash";
        [SerializeField] private bool enableDebugLogs = false;
        [SerializeField] private bool logMovementSourceToConsole = false;
        [SerializeField] private bool logInteractSourceToConsole = false;
        [SerializeField] private bool logAttackSourceToConsole = false;
        [SerializeField] private bool logDashSourceToConsole = false;
        [SerializeField] private float movementLogInterval = 1f;
        [SerializeField] private bool configureSceneForMovement = true;
        [SerializeField] private bool followPlayerWithMainCamera = true;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0f, -10f);
        [SerializeField] private float cameraOrthographicSize = 5f;
        [SerializeField] private float cameraFollowSharpness = 12f;
        [SerializeField] private float cameraSmoothTime = 0.12f;
        [SerializeField] private float cameraMaxSpeed = 100f;

        private InputActionMap _actionMap;
        private InputAction _moveAction;
        private InputAction _interactAction;
        private InputAction _attackAction;
        private InputAction _dashAction;
        private bool _charactersLinked;
        private bool _bindingStatusLogged;
        private bool _sceneMovementConfigured;
        private bool _collisionMatrixLogged;
        private float _nextMovementLogTime;
        private MovementInputSource _lastLoggedMovementSource = MovementInputSource.None;
        private ButtonInputSource _lastLoggedInteractSource = ButtonInputSource.None;
        private ButtonInputSource _lastLoggedAttackSource = ButtonInputSource.None;
        private ButtonInputSource _lastLoggedDashSource = ButtonInputSource.None;
        private Transform _followTarget;
        // 관전 모드 — Defeated 시 SpectatorCameraController 가 다른 alive 팀원 transform 으로 set.
        // null 이면 본인 _followTarget 추적 (기본 동작).
        private Transform _cameraOverrideTarget;
        private Camera _mainCamera;
        private Vector3 _cameraVelocity;
        private bool _cameraSnappedToTarget;
        private KhiPlayerCamera _overrideCamera;
        private KhiDownController _linkedDownController;

        public void Configure(InputActionAsset actions, string mapName)
        {
            inputActions = actions;
            actionMapName = mapName;
            BindActions();
        }

        protected override void Start()
        {
            base.Start();
            BindActions();
            LinkPlayerCharacters();
            ConfigureMovementScene();
            EnsureSpectatorCameraController();
        }

        // 17개 던전 씬마다 SpectatorCameraController 를 수동 부착하는 부담 회피.
        // 본 input manager 와 동일 GameObject 에 자동 추가. 이미 있으면 noop.
        // 솔로/네트워크 비활성 환경에선 SpectatorCameraController 가 AnyPlayerDefeated 만 listen 하므로 무동작.
        private void EnsureSpectatorCameraController()
        {
            if (GetComponent<SpectatorCameraController>() == null)
            {
                gameObject.AddComponent<SpectatorCameraController>();
            }
        }

        protected override void Update()
        {
            base.Update();

            // 기존 _followTarget 이 destroyed (Unity pseudo-null) 됐으면 재link 트리거.
            // 세션 leave→rejoin 또는 씬 전환 시 NGO 가 spawn 한 캐릭터가 한 번 destroy 된 후
            // 새로 spawn 되는 사이클에서 카메라 follow 가 풀리는 문제 대응.
            if (_charactersLinked && _followTarget == null)
            {
                _charactersLinked = false;
            }

            if (!_charactersLinked)
            {
                LinkPlayerCharacters();
            }

            if (!_sceneMovementConfigured)
            {
                ConfigureMovementScene();
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            FollowMainCamera();
        }

        protected virtual void OnEnable()
        {
            BindActions();
            EnsureActionsEnabled();
        }

        protected virtual void OnDisable()
        {
            _actionMap?.Disable();
        }

        protected override void Initialization()
        {
            base.Initialization();
            BindActions();
            EnsureActionsEnabled();
        }

        public override void SetMovement()
        {
            if (!InputDetectionActive)
            {
                _primaryMovement = Vector2.zero;
                return;
            }

            if (IsPlayerActionBlocked())
            {
                _primaryMovement = Vector2.zero;
                return;
            }

            BindActions();
            EnsureActionsEnabled();
            _primaryMovement = ReadMovement(out MovementInputSource source);
            LogMovementSource(source, _primaryMovement);
            _primaryMovement = ApplyCameraRotation(_primaryMovement);
        }

        public override void SetSecondaryMovement()
        {
            _secondaryMovement = Vector2.zero;
        }

        protected override void SetShootAxis()
        {
            ShootAxis = MoreMountains.Tools.MMInput.ButtonStates.Off;
            SecondaryShootAxis = MoreMountains.Tools.MMInput.ButtonStates.Off;
        }

        protected override void SetCameraRotationAxis()
        {
            _cameraRotationInput = 0f;
        }

        protected override void GetInputButtons()
        {
            if (IsPlayerActionBlocked())
            {
                ReleaseBlockedButtons();
                return;
            }

            BindActions();
            EnsureActionsEnabled();
            ProcessAttackButton();
            ProcessDashButton();
            ProcessInteractButton();
        }

        protected override void TestPrimaryAxis()
        {
            // The TestKhi action map already abstracts devices, so no mobile auto-switch check is needed here.
        }

        private void BindActions()
        {
            if (_moveAction != null && _interactAction != null && _attackAction != null && _dashAction != null)
            {
                EnsureActionsEnabled();
                return;
            }

            InputActionAsset actions = ResolveInputActions();
            if (actions == null)
            {
                return;
            }

            _actionMap = actions.FindActionMap(actionMapName, false);
            _moveAction = _actionMap?.FindAction(moveActionName, false);
            _interactAction = _actionMap?.FindAction(interactActionName, false);
            _attackAction = _actionMap?.FindAction(attackActionName, false);
            _dashAction = _actionMap?.FindAction(dashActionName, false);
            EnsureActionsEnabled();
            LogBindingStatus();
        }

        private void EnsureActionsEnabled()
        {
            if (_actionMap != null && !_actionMap.enabled)
            {
                _actionMap.Enable();
            }
        }

        private void LinkPlayerCharacters()
        {
            Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
            bool foundMatchingCharacter = false;
            _linkedDownController = null;

            foreach (Character character in characters)
            {
                if (character.CharacterType != Character.CharacterTypes.Player || character.PlayerID != PlayerID)
                {
                    continue;
                }

                character.SetInputManager(this);
                _followTarget = character.transform;
                KhiPlayerActionGate.TryResolveDownController(character, out _linkedDownController);
                EnsureCharacterButtonActivation(character);
                foundMatchingCharacter = true;
            }

            _charactersLinked = foundMatchingCharacter;
        }

        private bool IsPlayerActionBlocked()
        {
            return KhiPlayerActionGate.IsBlocked(_linkedDownController);
        }

        private void ReleaseBlockedButtons()
        {
            if (ShootButton != null &&
                ShootButton.State.CurrentState != MoreMountains.Tools.MMInput.ButtonStates.Off)
            {
                ShootButtonUp();
            }

            if (DashButton != null &&
                DashButton.State.CurrentState != MoreMountains.Tools.MMInput.ButtonStates.Off)
            {
                DashButtonUp();
            }

            if (InteractButton != null &&
                InteractButton.State.CurrentState != MoreMountains.Tools.MMInput.ButtonStates.Off)
            {
                InteractButtonUp();
            }
        }

        private static void EnsureCharacterButtonActivation(Character character)
        {
            if (character.FindAbility<CharacterButtonActivation>() != null)
            {
                return;
            }

            character.gameObject.AddComponent<CharacterButtonActivation>();
            character.CacheAbilities();
            Debug.Log("[TestKhiSetup] Added CharacterButtonActivation to TestKhi character.");
        }

        private Vector2 ReadMovement(out MovementInputSource source)
        {
            Vector2 movement = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            if (movement.sqrMagnitude > Mathf.Epsilon)
            {
                source = MovementInputSource.ActionMap;
                return Vector2.ClampMagnitude(movement, 1f);
            }

            movement = ReadDirectMovement();
            if (movement.sqrMagnitude > Mathf.Epsilon)
            {
                source = MovementInputSource.DirectFallback;
                return Vector2.ClampMagnitude(movement, 1f);
            }

            source = MovementInputSource.None;
            return Vector2.zero;
        }

        private void LogBindingStatus()
        {
            if (!enableDebugLogs || !logMovementSourceToConsole || _bindingStatusLogged)
            {
                return;
            }

            _bindingStatusLogged = true;

            if (_actionMap == null)
            {
                Debug.LogWarning($"[TestKhiInput] Action map '{actionMapName}' was not found. Direct input fallback will be used.");
                return;
            }

            if (_moveAction == null || _interactAction == null || _attackAction == null || _dashAction == null)
            {
                Debug.LogWarning($"[TestKhiInput] Actions bound with fallback. Move found={_moveAction != null}, Interact found={_interactAction != null}, Attack found={_attackAction != null}, Dash found={_dashAction != null}.");
                return;
            }

            Debug.Log($"[TestKhiInput] Bound actions '{actionMapName}/{moveActionName}', '{actionMapName}/{interactActionName}', '{actionMapName}/{attackActionName}', and '{actionMapName}/{dashActionName}'.");
        }

        private void LogMovementSource(MovementInputSource source, Vector2 movement)
        {
            if (!enableDebugLogs || !logMovementSourceToConsole)
            {
                return;
            }

            bool sourceChanged = source != _lastLoggedMovementSource;
            bool moving = movement.sqrMagnitude > Mathf.Epsilon;
            if (!sourceChanged && (!moving || Time.unscaledTime < _nextMovementLogTime))
            {
                return;
            }

            _lastLoggedMovementSource = source;
            _nextMovementLogTime = Time.unscaledTime + movementLogInterval;
            Debug.Log($"[TestKhiInput] Move source={source}, value={movement}");
        }

        private void ProcessInteractButton()
        {
            if (InteractButton == null)
            {
                return;
            }

            ButtonInputState state = ReadInteract();
            if (state.WasPressedThisFrame)
            {
                InteractButtonDown();
                LogInteractSource(state.Source, "Down");
                return;
            }

            if (state.WasReleasedThisFrame)
            {
                InteractButtonUp();
                LogInteractSource(state.Source, "Up");
                return;
            }

            if (state.IsPressed)
            {
                InteractButtonPressed();
            }
        }

        private void ProcessAttackButton()
        {
            if (ShootButton == null)
            {
                return;
            }

            ButtonInputState state = ReadAttack();
            if (state.WasPressedThisFrame)
            {
                ShootButtonDown();
                LogAttackSource(state.Source, "Down");
                return;
            }

            if (state.WasReleasedThisFrame)
            {
                ShootButtonUp();
                LogAttackSource(state.Source, "Up");
                return;
            }

            if (state.IsPressed)
            {
                ShootButtonPressed();
            }
        }

        private void ProcessDashButton()
        {
            if (DashButton == null)
            {
                return;
            }

            ButtonInputState state = ReadDash();
            if (state.WasPressedThisFrame)
            {
                DashButtonDown();
                LogDashSource(state.Source, "Down");
                return;
            }

            if (state.WasReleasedThisFrame)
            {
                DashButtonUp();
                LogDashSource(state.Source, "Up");
                return;
            }

            if (state.IsPressed)
            {
                DashButtonPressed();
            }
        }

        private ButtonInputState ReadAttack()
        {
            ButtonInputState state = default;
            if (_attackAction != null)
            {
                state.IsPressed = _attackAction.IsPressed();
                state.WasPressedThisFrame = _attackAction.WasPressedThisFrame();
                state.WasReleasedThisFrame = _attackAction.WasReleasedThisFrame();
                if (state.IsPressed || state.WasPressedThisFrame || state.WasReleasedThisFrame)
                {
                    state.Source = ButtonInputSource.ActionMap;
                    return state;
                }
            }

            state = ReadDirectAttack();
            if (state.IsPressed || state.WasPressedThisFrame || state.WasReleasedThisFrame)
            {
                state.Source = ButtonInputSource.DirectFallback;
                return state;
            }

            state.Source = ButtonInputSource.None;
            return state;
        }

        private static ButtonInputState ReadDirectAttack()
        {
            ButtonInputState state = default;
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                state.IsPressed |= mouse.leftButton.isPressed;
                state.WasPressedThisFrame |= mouse.leftButton.wasPressedThisFrame;
                state.WasReleasedThisFrame |= mouse.leftButton.wasReleasedThisFrame;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                state.IsPressed |= gamepad.buttonWest.isPressed;
                state.WasPressedThisFrame |= gamepad.buttonWest.wasPressedThisFrame;
                state.WasReleasedThisFrame |= gamepad.buttonWest.wasReleasedThisFrame;
            }

            return state;
        }

        private ButtonInputState ReadDash()
        {
            ButtonInputState state = default;
            if (_dashAction != null)
            {
                state.IsPressed = _dashAction.IsPressed();
                state.WasPressedThisFrame = _dashAction.WasPressedThisFrame();
                state.WasReleasedThisFrame = _dashAction.WasReleasedThisFrame();
                if (state.IsPressed || state.WasPressedThisFrame || state.WasReleasedThisFrame)
                {
                    state.Source = ButtonInputSource.ActionMap;
                    return state;
                }
            }

            state = ReadDirectDash();
            if (state.IsPressed || state.WasPressedThisFrame || state.WasReleasedThisFrame)
            {
                state.Source = ButtonInputSource.DirectFallback;
                return state;
            }

            state.Source = ButtonInputSource.None;
            return state;
        }

        private static ButtonInputState ReadDirectDash()
        {
            ButtonInputState state = default;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                state.IsPressed |= keyboard.spaceKey.isPressed;
                state.WasPressedThisFrame |= keyboard.spaceKey.wasPressedThisFrame;
                state.WasReleasedThisFrame |= keyboard.spaceKey.wasReleasedThisFrame;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                state.IsPressed |= gamepad.buttonEast.isPressed;
                state.WasPressedThisFrame |= gamepad.buttonEast.wasPressedThisFrame;
                state.WasReleasedThisFrame |= gamepad.buttonEast.wasReleasedThisFrame;
            }

            return state;
        }

        private ButtonInputState ReadInteract()
        {
            ButtonInputState state = default;
            if (_interactAction != null)
            {
                state.IsPressed = _interactAction.IsPressed();
                state.WasPressedThisFrame = _interactAction.WasPressedThisFrame();
                state.WasReleasedThisFrame = _interactAction.WasReleasedThisFrame();
                if (state.IsPressed || state.WasPressedThisFrame || state.WasReleasedThisFrame)
                {
                    state.Source = ButtonInputSource.ActionMap;
                    return state;
                }
            }

            state = ReadDirectInteract();
            if (state.IsPressed || state.WasPressedThisFrame || state.WasReleasedThisFrame)
            {
                state.Source = ButtonInputSource.DirectFallback;
                return state;
            }

            state.Source = ButtonInputSource.None;
            return state;
        }

        private static ButtonInputState ReadDirectInteract()
        {
            ButtonInputState state = default;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                state.IsPressed |= keyboard.fKey.isPressed;
                state.WasPressedThisFrame |= keyboard.fKey.wasPressedThisFrame;
                state.WasReleasedThisFrame |= keyboard.fKey.wasReleasedThisFrame;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                state.IsPressed |= gamepad.buttonSouth.isPressed;
                state.WasPressedThisFrame |= gamepad.buttonSouth.wasPressedThisFrame;
                state.WasReleasedThisFrame |= gamepad.buttonSouth.wasReleasedThisFrame;
            }

            return state;
        }

        private void LogInteractSource(ButtonInputSource source, string phase)
        {
            if (!enableDebugLogs || !logInteractSourceToConsole)
            {
                return;
            }

            if (source == ButtonInputSource.None && source == _lastLoggedInteractSource)
            {
                return;
            }

            _lastLoggedInteractSource = source;
            Debug.Log($"[TestKhiInput] Interact source={source}, phase={phase}");
        }

        private void LogAttackSource(ButtonInputSource source, string phase)
        {
            if (!enableDebugLogs || !logAttackSourceToConsole)
            {
                return;
            }

            if (source == ButtonInputSource.None && source == _lastLoggedAttackSource)
            {
                return;
            }

            _lastLoggedAttackSource = source;
            Debug.Log($"[TestKhiInput] Attack source={source}, phase={phase}");
        }

        private void LogDashSource(ButtonInputSource source, string phase)
        {
            if (!enableDebugLogs || !logDashSourceToConsole)
            {
                return;
            }

            if (source == ButtonInputSource.None && source == _lastLoggedDashSource)
            {
                return;
            }

            _lastLoggedDashSource = source;
            Debug.Log($"[TestKhiInput] Dash source={source}, phase={phase}");
        }

        private void ConfigureMovementScene()
        {
            if (!configureSceneForMovement)
            {
                _sceneMovementConfigured = true;
                return;
            }

            EnsureMainCamera();
            EnsureTilemapLayer("Floor", "Ground", true);
            EnsureTilemapLayer("Walls", "Obstacles", true);
            LogCollisionMatrixStatus();
            _sceneMovementConfigured = true;
        }

        private void EnsureMainCamera()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                _mainCamera = cameraObject.AddComponent<Camera>();
            }

            _mainCamera.orthographic = true;
            _mainCamera.orthographicSize = cameraOrthographicSize;
            if (_followTarget == null)
            {
                _mainCamera.transform.position = cameraOffset;
                _cameraSnappedToTarget = false;
            }
        }

        /// <summary>
        /// 관전 카메라 override. null 로 호출하면 기본 follow (_followTarget) 으로 복귀.
        /// SpectatorCameraController 가 본인 Defeated 시 호출.
        /// </summary>
        public void SetCameraOverrideTarget(Transform target)
        {
            _cameraOverrideTarget = target;
            _cameraSnappedToTarget = false; // 새 target 으로 잘 lerp 되도록 snap 한번 재유도.
        }

        public Transform CameraOverrideTarget => _cameraOverrideTarget;

        private void FollowMainCamera()
        {
            if (!followPlayerWithMainCamera)
            {
                return;
            }

            // override 가 있으면 우선. 없으면 본인 _followTarget.
            Transform effectiveTarget = _cameraOverrideTarget != null ? _cameraOverrideTarget : _followTarget;
            if (effectiveTarget == null)
            {
                return;
            }

            if (_mainCamera == null)
            {
                EnsureMainCamera();
            }

            if (_overrideCamera == null && _mainCamera != null)
            {
                _overrideCamera = _mainCamera.GetComponent<KhiPlayerCamera>();
            }

            if (_overrideCamera != null)
            {
                if (_overrideCamera.FollowTarget != effectiveTarget)
                {
                    _overrideCamera.SetFollowTarget(effectiveTarget);
                }

                return;
            }

            Vector3 targetPosition = effectiveTarget.position + cameraOffset;
            if (!_cameraSnappedToTarget)
            {
                _mainCamera.transform.position = targetPosition;
                _cameraVelocity = Vector3.zero;
                _cameraSnappedToTarget = true;
                return;
            }

            if (cameraSmoothTime <= 0f && cameraFollowSharpness <= 0f)
            {
                _mainCamera.transform.position = targetPosition;
                _cameraVelocity = Vector3.zero;
                return;
            }

            float smoothTime = cameraSmoothTime > 0f ? cameraSmoothTime : 1f / cameraFollowSharpness;
            _mainCamera.transform.position = Vector3.SmoothDamp(
                _mainCamera.transform.position,
                targetPosition,
                ref _cameraVelocity,
                smoothTime,
                cameraMaxSpeed,
                Time.deltaTime);
        }

        private void EnsureTilemapLayer(string objectName, string layerName, bool requireCollider)
        {
            GameObject target = GameObject.Find(objectName);
            if (target == null)
            {
                Debug.LogWarning($"[TestKhiSetup] Could not find '{objectName}'.");
                return;
            }

            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                Debug.LogWarning($"[TestKhiSetup] Layer '{layerName}' does not exist.");
                return;
            }

            SetLayerRecursively(target, layer);

            if (requireCollider && target.GetComponent<Tilemap>() != null && target.GetComponent<TilemapCollider2D>() == null)
            {
                target.AddComponent<TilemapCollider2D>();
            }

            if (enableDebugLogs && logMovementSourceToConsole)
            {
                Debug.Log($"[TestKhiSetup] {objectName} configured as layer '{layerName}'.");
            }
        }

        private void LogCollisionMatrixStatus()
        {
            if (_collisionMatrixLogged)
            {
                return;
            }

            _collisionMatrixLogged = true;

            int playerLayer = LayerMask.NameToLayer("Player");
            int groundLayer = LayerMask.NameToLayer("Ground");
            int obstaclesLayer = LayerMask.NameToLayer("Obstacles");

            if (playerLayer < 0 || groundLayer < 0 || obstaclesLayer < 0)
            {
                Debug.LogWarning("[TestKhiSetup] Player/Ground/Obstacles layers are not all defined.");
                return;
            }

            if (!Physics2D.GetIgnoreLayerCollision(playerLayer, groundLayer))
            {
                Debug.LogWarning("[TestKhiSetup] Player and Ground currently collide. Ground may block top-down movement.");
            }

            if (Physics2D.GetIgnoreLayerCollision(playerLayer, obstaclesLayer))
            {
                Debug.LogWarning("[TestKhiSetup] Player and Obstacles collision is disabled. Walls may not block movement.");
            }
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;

            for (int i = 0; i < target.transform.childCount; i++)
            {
                SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
            }
        }

        private static Vector2 ReadDirectMovement()
        {
            Vector2 movement = Vector2.zero;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    movement.y += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    movement.y -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    movement.x += 1f;
                }

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    movement.x -= 1f;
                }
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                movement += gamepad.leftStick.ReadValue();
            }

            return Vector2.ClampMagnitude(movement, 1f);
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
    }
}
