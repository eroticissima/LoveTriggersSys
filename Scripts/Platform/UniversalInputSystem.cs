// =============================================================================
// UniversalInputSystem.cs - Cross-Platform Input Management - FIXED
// =============================================================================

using UnityEngine;
using UnityEngine.XR;
using UnityEngine.EventSystems;
using System.Collections.Generic;

#if ENABLE_INPUT_SYSTEM && UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
#endif

public enum InputDevice
{
    Mouse,
    Keyboard,
    Gamepad,
    VR_LeftController,
    VR_RightController,
    VR_Head,
    Touch,
    Unknown
}

[System.Serializable]
public struct InputAction
{
    public string name;
    public KeyCode keyboardKey;
    public int mouseButton;
    public string gamepadButton;
    public string vrButton;
    public bool isPressed;
    public bool wasPressed;
    public float value;
    public Vector2 axis;
    public Vector3 position;
    public Quaternion rotation;
}

[System.Serializable]
public class InputMapping
{
    [Header("Love Trigger Actions")]
    public InputAction selectTrigger = new InputAction { name = "SelectTrigger", keyboardKey = KeyCode.Space, mouseButton = 0 };
    public InputAction cancelAction = new InputAction { name = "Cancel", keyboardKey = KeyCode.Escape, mouseButton = 1 };
    public InputAction nextTrigger = new InputAction { name = "NextTrigger", keyboardKey = KeyCode.Tab };
    public InputAction previousTrigger = new InputAction { name = "PreviousTrigger", keyboardKey = KeyCode.LeftShift };

    [Header("Navigation")]
    public InputAction moveUp = new InputAction { name = "MoveUp", keyboardKey = KeyCode.W };
    public InputAction moveDown = new InputAction { name = "MoveDown", keyboardKey = KeyCode.S };
    public InputAction moveLeft = new InputAction { name = "MoveLeft", keyboardKey = KeyCode.A };
    public InputAction moveRight = new InputAction { name = "MoveRight", keyboardKey = KeyCode.D };

    [Header("VR Specific")]
    public InputAction leftHandTrigger = new InputAction { name = "LeftHandTrigger", vrButton = "Trigger" };
    public InputAction rightHandTrigger = new InputAction { name = "RightHandTrigger", vrButton = "Trigger" };
    public InputAction leftHandGrip = new InputAction { name = "LeftHandGrip", vrButton = "Grip" };
    public InputAction rightHandGrip = new InputAction { name = "RightHandGrip", vrButton = "Grip" };
}

public class UniversalInputSystem : MonoBehaviour
{
    [Header("Input Configuration")]
    public InputMapping inputMapping;
    public bool enableVRHandTracking = true;
    public bool enableEyeTracking = false;
    public float handInteractionDistance = 0.5f;

    [Header("UI Interaction")]
    public LayerMask uiLayerMask = 1 << 5;
    public float raycastDistance = 100f;
    public bool showDebugRays = true;

    [Header("VR Controllers")]
    public Transform leftHandTransform;
    public Transform rightHandTransform;
    public Transform headTransform;
    public LineRenderer leftHandRay;
    public LineRenderer rightHandRay;

    [Header("Desktop Cursor")]
    public GameObject desktopCursor;
    public float cursorSpeed = 1000f;

    // Singleton
    public static UniversalInputSystem Instance { get; private set; }

    // Input State
    private Dictionary<string, InputAction> currentInputState = new Dictionary<string, InputAction>();
    private List<InputDevice> activeDevices = new List<InputDevice>();
    private Camera activeCamera;
    private bool isVRMode;
    private bool isInitialized = false;

    // VR Input
    private List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();
    private List<XRNode> trackedNodes = new List<XRNode>();

    // Events
    public System.Action<string, InputAction> OnInputAction;
    public System.Action<InputDevice> OnDeviceConnected;
    public System.Action<InputDevice> OnDeviceDisconnected;
    public System.Action<Vector3, Quaternion> OnHandPoseUpdate;
    public System.Action<GameObject> OnUIHover;
    public System.Action<GameObject> OnUIClick;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        InitializeInputSystem();

        // Subscribe to platform changes
        if (PlatformDetectionSystem.Instance != null)
        {
            PlatformDetectionSystem.Instance.OnPlatformDetected += OnPlatformChanged;
            OnPlatformChanged(PlatformDetectionSystem.Instance.CurrentPlatform);
        }
    }

    void InitializeInputSystem()
    {
        activeCamera = Camera.main;
        if (activeCamera == null)
            activeCamera = FindObjectOfType<Camera>();

        InitializeInputMapping();
        DetectActiveDevices();
        SetupVRComponents();

        isInitialized = true;

        Debug.Log("[UniversalInput] Input system initialized");
    }

    void InitializeInputMapping()
    {
        // Initialize input state dictionary
        currentInputState.Clear();

        var mappingFields = typeof(InputMapping).GetFields();
        foreach (var field in mappingFields)
        {
            if (field.FieldType == typeof(InputAction))
            {
                InputAction action = (InputAction)field.GetValue(inputMapping);
                currentInputState[action.name] = action;
            }
        }
    }

    void OnPlatformChanged(PlatformType platformType)
    {
        isVRMode = (platformType != PlatformType.Desktop && platformType != PlatformType.Mobile);

        SetupInputForPlatform(platformType);
        ConfigureUIInteraction();

        Debug.Log($"[UniversalInput] Configured for platform: {platformType}");
    }

    void SetupInputForPlatform(PlatformType platform)
    {
        switch (platform)
        {
            case PlatformType.Desktop:
                SetupDesktopInput();
                break;

            case PlatformType.VR_Oculus:
            case PlatformType.VR_SteamVR:
            case PlatformType.VR_OpenXR:
            case PlatformType.VR_Pico:
            case PlatformType.VR_Generic:
                SetupVRInput();
                break;

            case PlatformType.Mobile:
                SetupMobileInput();
                break;
        }
    }

    void SetupDesktopInput()
    {
        // Enable desktop cursor
        if (desktopCursor != null)
            desktopCursor.SetActive(true);

        // Disable VR components
        DisableVRComponents();

        // Set cursor mode
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void SetupVRInput()
    {
        // Disable desktop cursor
        if (desktopCursor != null)
            desktopCursor.SetActive(false);

        // Enable VR components
        EnableVRComponents();

        // Hide system cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialize XR input
        InitializeXRInput();
    }

    void SetupMobileInput()
    {
        // Disable desktop cursor
        if (desktopCursor != null)
            desktopCursor.SetActive(false);

        // Disable VR components
        DisableVRComponents();

        // Mobile-specific setup
        Cursor.visible = false;
    }

    void SetupVRComponents()
    {
        // Auto-find VR transforms if not assigned
        if (leftHandTransform == null)
            leftHandTransform = FindVRTransform("LeftHand");

        if (rightHandTransform == null)
            rightHandTransform = FindVRTransform("RightHand");

        if (headTransform == null)
            headTransform = FindVRTransform("Head");

        // Setup hand rays
        SetupHandRays();
    }

    Transform FindVRTransform(string name)
    {
        // Try common VR rig names
        string[] possibleNames = {
            name,
            $"[CameraRig]/{name}",
            $"OVRCameraRig/{name}",
            $"XR Origin/{name}",
            $"XR Rig/{name}",
            $"Camera Offset/{name}"
        };

        foreach (string possibleName in possibleNames)
        {
            GameObject found = GameObject.Find(possibleName);
            if (found != null)
                return found.transform;
        }

        return null;
    }

    void SetupHandRays()
    {
        if (leftHandTransform != null && leftHandRay == null)
        {
            leftHandRay = CreateHandRay(leftHandTransform, "LeftHandRay");
        }

        if (rightHandTransform != null && rightHandRay == null)
        {
            rightHandRay = CreateHandRay(rightHandTransform, "RightHandRay");
        }
    }

    LineRenderer CreateHandRay(Transform hand, string name)
    {
        GameObject rayObject = new GameObject(name);
        rayObject.transform.SetParent(hand);
        rayObject.transform.localPosition = Vector3.zero;
        rayObject.transform.localRotation = Quaternion.identity;

        LineRenderer ray = rayObject.AddComponent<LineRenderer>();
        ray.material = new Material(Shader.Find("Sprites/Default"));

        // FIXED: Use newer LineRenderer color API
        ray.startColor = Color.blue;
        ray.endColor = Color.blue;

        ray.startWidth = 0.01f;
        ray.endWidth = 0.01f;
        ray.positionCount = 2;
        ray.enabled = false;

        return ray;
    }

    void EnableVRComponents()
    {
        if (leftHandRay != null) leftHandRay.enabled = true;
        if (rightHandRay != null) rightHandRay.enabled = true;
    }

    void DisableVRComponents()
    {
        if (leftHandRay != null) leftHandRay.enabled = false;
        if (rightHandRay != null) rightHandRay.enabled = false;
    }

    void InitializeXRInput()
    {
        SubsystemManager.GetSubsystems(inputSubsystems);

        // Setup tracked nodes
        trackedNodes.Clear();
        trackedNodes.Add(XRNode.LeftHand);
        trackedNodes.Add(XRNode.RightHand);
        trackedNodes.Add(XRNode.Head);
    }

    void DetectActiveDevices()
    {
        activeDevices.Clear();

        // Always check for mouse and keyboard on desktop
        if (!isVRMode)
        {
            activeDevices.Add(InputDevice.Mouse);
            activeDevices.Add(InputDevice.Keyboard);
        }

        // Check for gamepad
        string[] joysticks = Input.GetJoystickNames();
        foreach (string joystick in joysticks)
        {
            if (!string.IsNullOrEmpty(joystick))
            {
                activeDevices.Add(InputDevice.Gamepad);
                break;
            }
        }

        // Check for VR controllers
        if (isVRMode)
        {
            activeDevices.Add(InputDevice.VR_LeftController);
            activeDevices.Add(InputDevice.VR_RightController);
            activeDevices.Add(InputDevice.VR_Head);
        }

        // Touch for mobile
        if (Application.isMobilePlatform)
        {
            activeDevices.Add(InputDevice.Touch);
        }
    }

    void Update()
    {
        if (!isInitialized) return;

        UpdateInputState();
        UpdateUIInteraction();
        UpdateVRTracking();
    }

    void UpdateInputState()
    {
        var keys = new List<string>(currentInputState.Keys);

        foreach (string actionName in keys)
        {
            InputAction action = currentInputState[actionName];
            InputAction previousAction = action;

            // Update based on platform
            if (isVRMode)
            {
                UpdateVRInput(ref action);
            }
            else
            {
                UpdateDesktopInput(ref action);
            }

            // Track press/release states
            action.wasPressed = previousAction.isPressed;

            // Fire events on state change
            if (action.isPressed && !action.wasPressed)
            {
                OnInputAction?.Invoke(actionName, action);
            }

            currentInputState[actionName] = action;
        }
    }

    void UpdateDesktopInput(ref InputAction action)
    {
        switch (action.name)
        {
            case "SelectTrigger":
                action.isPressed = Input.GetKey(action.keyboardKey) || Input.GetMouseButton(action.mouseButton);
                break;

            case "Cancel":
                action.isPressed = Input.GetKey(action.keyboardKey) || Input.GetMouseButton(action.mouseButton);
                break;

            case "NextTrigger":
                action.isPressed = Input.GetKey(action.keyboardKey);
                break;

            case "PreviousTrigger":
                action.isPressed = Input.GetKey(action.keyboardKey);
                break;

            case "MoveUp":
            case "MoveDown":
            case "MoveLeft":
            case "MoveRight":
                action.isPressed = Input.GetKey(action.keyboardKey);
                break;
        }

        // Update mouse position
        if (action.name == "SelectTrigger")
        {
            Vector3 mousePos = Input.mousePosition;
            action.position = activeCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, activeCamera.nearClipPlane));
        }
    }

    void UpdateVRInput(ref InputAction action)
    {
        switch (action.name)
        {
            case "LeftHandTrigger":
                action.isPressed = GetVRButtonState(XRNode.LeftHand, "Trigger");
                action.value = GetVRAxisValue(XRNode.LeftHand, "Trigger");
                GetVRNodePose(XRNode.LeftHand, out action.position, out action.rotation);
                break;

            case "RightHandTrigger":
                action.isPressed = GetVRButtonState(XRNode.RightHand, "Trigger");
                action.value = GetVRAxisValue(XRNode.RightHand, "Trigger");
                GetVRNodePose(XRNode.RightHand, out action.position, out action.rotation);
                break;

            case "LeftHandGrip":
                action.isPressed = GetVRButtonState(XRNode.LeftHand, "Grip");
                action.value = GetVRAxisValue(XRNode.LeftHand, "Grip");
                break;

            case "RightHandGrip":
                action.isPressed = GetVRButtonState(XRNode.RightHand, "Grip");
                action.value = GetVRAxisValue(XRNode.RightHand, "Grip");
                break;

            case "SelectTrigger":
                // Map to either hand trigger
                action.isPressed = GetVRButtonState(XRNode.LeftHand, "Trigger") ||
                                 GetVRButtonState(XRNode.RightHand, "Trigger");
                break;
        }
    }

    bool GetVRButtonState(XRNode node, string buttonName)
    {
        // Use legacy input system for broader compatibility
        var inputDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(node, inputDevices);

        if (inputDevices.Count > 0)
        {
            var device = inputDevices[0];
            if (buttonName == "Trigger")
            {
                bool buttonValue;
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out buttonValue))
                    return buttonValue;
            }
            else if (buttonName == "Grip")
            {
                bool buttonValue;
                if (device.TryGetFeatureValue(CommonUsages.gripButton, out buttonValue))
                    return buttonValue;
            }
        }
        return false;
    }

    float GetVRAxisValue(XRNode node, string axisName)
    {
        var inputDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(node, inputDevices);

        if (inputDevices.Count > 0)
        {
            var device = inputDevices[0];
            if (axisName == "Trigger")
            {
                float axisValue;
                if (device.TryGetFeatureValue(CommonUsages.trigger, out axisValue))
                    return axisValue;
            }
            else if (axisName == "Grip")
            {
                float axisValue;
                if (device.TryGetFeatureValue(CommonUsages.grip, out axisValue))
                    return axisValue;
            }
        }
        return 0f;
    }

    bool GetVRNodePose(XRNode node, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        var inputDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(node, inputDevices);

        if (inputDevices.Count > 0)
        {
            var device = inputDevices[0];
            bool hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out position);
            bool hasRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
            return hasPosition && hasRotation;
        }
        return false;
    }

    void UpdateUIInteraction()
    {
        if (isVRMode)
        {
            UpdateVRUIInteraction();
        }
        else
        {
            UpdateDesktopUIInteraction();
        }
    }

    void UpdateDesktopUIInteraction()
    {
        // Standard Unity EventSystem handles this
        GameObject hoveredObject = GetUIObjectUnderMouse();
        if (hoveredObject != null)
        {
            OnUIHover?.Invoke(hoveredObject);

            if (Input.GetMouseButtonDown(0))
            {
                OnUIClick?.Invoke(hoveredObject);
            }
        }
    }

    void UpdateVRUIInteraction()
    {
        // Check both hands for UI interaction
        CheckVRHandUIInteraction(leftHandTransform, leftHandRay);
        CheckVRHandUIInteraction(rightHandTransform, rightHandRay);
    }

    void CheckVRHandUIInteraction(Transform hand, LineRenderer ray)
    {
        if (hand == null || ray == null) return;

        RaycastHit hit;
        Ray handRay = new Ray(hand.position, hand.forward);

        if (Physics.Raycast(handRay, out hit, raycastDistance, uiLayerMask))
        {
            // Update ray visual
            ray.SetPosition(0, hand.position);
            ray.SetPosition(1, hit.point);

            OnUIHover?.Invoke(hit.collider.gameObject);

            // Check for trigger input
            if (GetVRButtonState(hand == leftHandTransform ? XRNode.LeftHand : XRNode.RightHand, "Trigger"))
            {
                OnUIClick?.Invoke(hit.collider.gameObject);
            }
        }
        else
        {
            // Show ray at max distance
            ray.SetPosition(0, hand.position);
            ray.SetPosition(1, hand.position + hand.forward * raycastDistance);
        }
    }

    GameObject GetUIObjectUnderMouse()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        if (results.Count > 0)
        {
            return results[0].gameObject;
        }

        return null;
    }

    void UpdateVRTracking()
    {
        if (!isVRMode) return;

        // Update hand poses
        if (leftHandTransform != null)
        {
            Vector3 leftPos;
            Quaternion leftRot;
            if (GetVRNodePose(XRNode.LeftHand, out leftPos, out leftRot))
            {
                leftHandTransform.position = leftPos;
                leftHandTransform.rotation = leftRot;
                OnHandPoseUpdate?.Invoke(leftPos, leftRot);
            }
        }

        if (rightHandTransform != null)
        {
            Vector3 rightPos;
            Quaternion rightRot;
            if (GetVRNodePose(XRNode.RightHand, out rightPos, out rightRot))
            {
                rightHandTransform.position = rightPos;
                rightHandTransform.rotation = rightRot;
                OnHandPoseUpdate?.Invoke(rightPos, rightRot);
            }
        }
    }

    void ConfigureUIInteraction()
    {
        // Configure EventSystem for current platform
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystem = eventSystemGO.AddComponent<EventSystem>();
        }

        // Remove conflicting input modules
        var inputModules = eventSystem.GetComponents<BaseInputModule>();
        foreach (var module in inputModules)
        {
            if (module != null)
                Destroy(module);
        }

        // Add appropriate input module
        eventSystem.gameObject.AddComponent<StandaloneInputModule>();
    }

    // Public API
    public bool IsActionPressed(string actionName)
    {
        return currentInputState.ContainsKey(actionName) && currentInputState[actionName].isPressed;
    }

    public bool WasActionPressed(string actionName)
    {
        return currentInputState.ContainsKey(actionName) &&
               currentInputState[actionName].isPressed &&
               !currentInputState[actionName].wasPressed;
    }

    public bool WasActionReleased(string actionName)
    {
        return currentInputState.ContainsKey(actionName) &&
               !currentInputState[actionName].isPressed &&
               currentInputState[actionName].wasPressed;
    }

    public float GetActionValue(string actionName)
    {
        return currentInputState.ContainsKey(actionName) ? currentInputState[actionName].value : 0f;
    }

    public Vector3 GetActionPosition(string actionName)
    {
        return currentInputState.ContainsKey(actionName) ? currentInputState[actionName].position : Vector3.zero;
    }

    public Vector2 GetActionAxis(string actionName)
    {
        return currentInputState.ContainsKey(actionName) ? currentInputState[actionName].axis : Vector2.zero;
    }

    public bool IsDeviceActive(InputDevice device)
    {
        return activeDevices.Contains(device);
    }

    [ContextMenu("Log Input State")]
    public void LogInputState()
    {
        Debug.Log("=== INPUT STATE DEBUG ===");
        Debug.Log($"VR Mode: {isVRMode}");
        Debug.Log($"Active Devices: {string.Join(", ", activeDevices)}");

        foreach (var kvp in currentInputState)
        {
            var action = kvp.Value;
            Debug.Log($"{kvp.Key}: Pressed={action.isPressed}, Value={action.value:F2}, Position={action.position}");
        }
        Debug.Log("========================");
    }
}