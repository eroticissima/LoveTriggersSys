// =============================================================================
// UniversalLTMenuController.cs - Multi-Platform Love Trigger Menu - FIXED FOR UNITY 2023.2.19f1
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using LTSystem.Network;

public class UniversalLTMenuController : NetworkBehaviour
{
    [Header("UI Configuration")]
    public GameObject buttonPrefab;
    public Transform desktopMenuContainer;
    public Transform vrMenuContainer;
    public Canvas desktopCanvas;
    public Canvas vrCanvas;

    [Header("Platform Adaptation")]
    public bool autoConfigureForPlatform = true;
    public bool adaptUIScale = true;
    public bool adaptButtonLayout = true;

    [Header("Desktop Settings")]
    public Vector2 desktopButtonSize = new Vector2(150f, 150f);
    public float desktopPaddingX = 20f;
    public float desktopPaddingY = 20f;
    public int desktopButtonsPerRow = 4;

    [Header("VR Settings")]
    public Vector2 vrButtonSize = new Vector2(0.4f, 0.4f);
    public float vrPaddingX = 0.2f;
    public float vrPaddingY = 0.2f;
    public int vrButtonsPerRow = 3;
    public float vrUIDistance = 2f;
    public bool followVRCamera = true;

    [Header("Mobile Settings")]
    public Vector2 mobileButtonSize = new Vector2(120f, 120f);
    public float mobilePaddingX = 15f;
    public float mobilePaddingY = 15f;
    public int mobileButtonsPerRow = 3;

    [Header("Interaction")]
    public LayerMask uiLayerMask = 1 << 5;
    public float handInteractionDistance = 0.5f;
    public bool enableHapticFeedback = true;

    [Header("Visual Feedback")]
    public Color availableButtonColor = Color.white;
    public Color unavailableButtonColor = Color.gray;
    public Color consentRequiredColor = Color.yellow;
    public Color highlightColor = Color.cyan;
    public float highlightScale = 1.1f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip buttonHoverSound;
    public AudioClip buttonClickSound;
    public AudioClip menuOpenSound;
    public AudioClip menuCloseSound;

    [Header("Network Detection")]
    public float npcDetectionRadius = 5f;
    public string npcTag = "NPC";
    public string playerTag = "Player";
    public float refreshInterval = 0.5f;

    [Header("Filtering")]
    public bool showOnlyCompatible = true;
    public string categoryFilter = "";
    public bool showUnavailableTriggers = true;

    [Header("Debug")]
    public bool debugMode = true;

    // Platform State
    private PlatformType currentPlatform;
    private UIMode currentUIMode;
    private bool isVRMode;
    private bool isInitialized = false;

    // Current UI Elements
    private Transform activeMenuContainer;
    private Canvas activeCanvas;
    private Camera activeCamera;
    private Vector2 currentButtonSize;
    private float currentPaddingX;
    private float currentPaddingY;
    private int currentButtonsPerRow;

    // Network Components
    private NetworkedLoveTriggerManager currentTargetManager;
    private NetworkObject selectedPartner;
    private List<LoveTriggerSO> currentTriggers = new List<LoveTriggerSO>();
    private List<Button> createdButtons = new List<Button>();
    private Dictionary<string, float> triggerCooldowns = new Dictionary<string, float>();

    // Input State
    private Button currentHoveredButton;
    private GameObject lastHoveredObject;
    private bool isMenuVisible = true;

    // VR Specific
    private Vector3 menuTargetPosition;
    private Quaternion menuTargetRotation;

    public override void Spawned()
    {
        if (!Object.HasInputAuthority) return;

        InitializeMenuController();

        // Subscribe to platform and input events
        if (PlatformDetectionSystem.Instance != null)
        {
            PlatformDetectionSystem.Instance.OnPlatformDetected += OnPlatformChanged;
            OnPlatformChanged(PlatformDetectionSystem.Instance.CurrentPlatform);
        }

        if (UniversalInputSystem.Instance != null)
        {
            UniversalInputSystem.Instance.OnUIHover += OnUIHover;
            UniversalInputSystem.Instance.OnUIClick += OnUIClick;
            UniversalInputSystem.Instance.OnInputAction += OnInputAction;
        }

        StartCoroutine(RefreshMenuPeriodically());
        FindValidTargets();
    }

    void InitializeMenuController()
    {
        // Find active camera
        activeCamera = Camera.main;
        if (activeCamera == null)
            activeCamera = FindObjectOfType<Camera>();

        // Setup audio
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Auto-setup UI containers if not assigned
        SetupUIContainers();

        isInitialized = true;

        if (debugMode)
            Debug.Log("[UniversalLTMenu] Menu controller initialized");
    }

    void SetupUIContainers()
    {
        // Desktop canvas setup
        if (desktopCanvas == null)
        {
            GameObject desktopCanvasGO = new GameObject("DesktopCanvas");
            desktopCanvasGO.transform.SetParent(transform);
            desktopCanvas = desktopCanvasGO.AddComponent<Canvas>();
            desktopCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            desktopCanvasGO.AddComponent<CanvasScaler>();
            desktopCanvasGO.AddComponent<GraphicRaycaster>();
        }

        if (desktopMenuContainer == null)
        {
            GameObject containerGO = new GameObject("DesktopMenuContainer");
            containerGO.transform.SetParent(desktopCanvas.transform);
            desktopMenuContainer = containerGO.transform;

            RectTransform rect = containerGO.AddComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(800f, 600f);
        }

        // VR canvas setup
        if (vrCanvas == null)
        {
            GameObject vrCanvasGO = new GameObject("VRCanvas");
            vrCanvasGO.transform.SetParent(transform);
            vrCanvas = vrCanvasGO.AddComponent<Canvas>();
            vrCanvas.renderMode = RenderMode.WorldSpace;
            vrCanvas.worldCamera = activeCamera;
            vrCanvasGO.AddComponent<CanvasScaler>();
            vrCanvasGO.AddComponent<GraphicRaycaster>();

            // Set VR-appropriate scale
            vrCanvas.transform.localScale = Vector3.one * 0.001f;
        }

        if (vrMenuContainer == null)
        {
            GameObject containerGO = new GameObject("VRMenuContainer");
            containerGO.transform.SetParent(vrCanvas.transform);
            vrMenuContainer = containerGO.transform;

            RectTransform rect = containerGO.AddComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1000f, 800f);
        }
    }

    void OnPlatformChanged(PlatformType platform)
    {
        currentPlatform = platform;
        isVRMode = (platform != PlatformType.Desktop && platform != PlatformType.Mobile && platform != PlatformType.Console);

        ConfigureUIForPlatform(platform);
        PositionMenuForPlatform();

        if (debugMode)
            Debug.Log($"[UniversalLTMenu] Configured for platform: {platform}");
    }

    void ConfigureUIForPlatform(PlatformType platform)
    {
        switch (platform)
        {
            case PlatformType.Desktop:
                SetupDesktopUI();
                break;

            case PlatformType.VR_Oculus:
            case PlatformType.VR_SteamVR:
            case PlatformType.VR_OpenXR:
            case PlatformType.VR_Pico:
            case PlatformType.VR_Generic:
                SetupVRUI();
                break;

            case PlatformType.Mobile:
                SetupMobileUI();
                break;

            default:
                SetupDesktopUI();
                break;
        }
    }

    void SetupDesktopUI()
    {
        // Enable desktop UI
        if (desktopCanvas != null) desktopCanvas.gameObject.SetActive(true);
        if (vrCanvas != null) vrCanvas.gameObject.SetActive(false);

        activeMenuContainer = desktopMenuContainer;
        activeCanvas = desktopCanvas;
        currentButtonSize = desktopButtonSize;
        currentPaddingX = desktopPaddingX;
        currentPaddingY = desktopPaddingY;
        currentButtonsPerRow = desktopButtonsPerRow;

        // Configure canvas for desktop
        if (desktopCanvas != null)
        {
            desktopCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            desktopCanvas.worldCamera = null;
        }

        if (debugMode)
            Debug.Log("[UniversalLTMenu] Desktop UI configured");
    }

    void SetupVRUI()
    {
        // Enable VR UI
        if (desktopCanvas != null) desktopCanvas.gameObject.SetActive(false);
        if (vrCanvas != null) vrCanvas.gameObject.SetActive(true);

        activeMenuContainer = vrMenuContainer;
        activeCanvas = vrCanvas;
        currentButtonSize = vrButtonSize * 100f; // Convert to UI units
        currentPaddingX = vrPaddingX * 100f;
        currentPaddingY = vrPaddingY * 100f;
        currentButtonsPerRow = vrButtonsPerRow;

        // Configure canvas for VR
        if (vrCanvas != null)
        {
            vrCanvas.renderMode = RenderMode.WorldSpace;
            vrCanvas.worldCamera = activeCamera;
        }

        if (debugMode)
            Debug.Log("[UniversalLTMenu] VR UI configured");
    }

    void SetupMobileUI()
    {
        // Use desktop UI with mobile settings
        if (desktopCanvas != null) desktopCanvas.gameObject.SetActive(true);
        if (vrCanvas != null) vrCanvas.gameObject.SetActive(false);

        activeMenuContainer = desktopMenuContainer;
        activeCanvas = desktopCanvas;
        currentButtonSize = mobileButtonSize;
        currentPaddingX = mobilePaddingX;
        currentPaddingY = mobilePaddingY;
        currentButtonsPerRow = mobileButtonsPerRow;

        if (debugMode)
            Debug.Log("[UniversalLTMenu] Mobile UI configured");
    }

    void PositionMenuForPlatform()
    {
        if (isVRMode && activeCamera != null && vrCanvas != null)
        {
            // Position VR menu in front of camera
            Vector3 forward = activeCamera.transform.forward;
            Vector3 position = activeCamera.transform.position + forward * vrUIDistance;

            menuTargetPosition = position;
            menuTargetRotation = Quaternion.LookRotation(forward);

            vrCanvas.transform.position = menuTargetPosition;
            vrCanvas.transform.rotation = menuTargetRotation;
        }
    }

    IEnumerator RefreshMenuPeriodically()
    {
        while (true)
        {
            yield return new WaitForSeconds(refreshInterval);

            if (isVRMode && followVRCamera)
            {
                UpdateVRMenuPosition();
            }

            FindValidTargets();
            RefreshButtonStates();
            UpdateCooldowns();
        }
    }

    void UpdateVRMenuPosition()
    {
        if (!isVRMode || activeCamera == null || vrCanvas == null) return;

        // Smoothly follow VR camera
        Vector3 forward = activeCamera.transform.forward;
        Vector3 targetPos = activeCamera.transform.position + forward * vrUIDistance;
        Quaternion targetRot = Quaternion.LookRotation(forward);

        vrCanvas.transform.position = Vector3.Lerp(vrCanvas.transform.position, targetPos, Time.deltaTime * 2f);
        vrCanvas.transform.rotation = Quaternion.Slerp(vrCanvas.transform.rotation, targetRot, Time.deltaTime * 2f);
    }

    void FindValidTargets()
    {
        if (!Object.HasInputAuthority) return;

        var nearbyObjects = new List<NetworkObject>();
        var colliders = Physics.OverlapSphere(transform.position, npcDetectionRadius);

        foreach (var col in colliders)
        {
            if (col.gameObject == gameObject) continue;

            var networkObj = col.GetComponent<NetworkObject>();
            var triggerManager = col.GetComponent<NetworkedLoveTriggerManager>();

            if (networkObj != null && triggerManager != null)
            {
                nearbyObjects.Add(networkObj);
            }
        }

        if (nearbyObjects.Count > 0)
        {
            NetworkObject newTarget = GetClosestTarget(nearbyObjects);

            if (newTarget != selectedPartner)
            {
                selectedPartner = newTarget;
                currentTargetManager = selectedPartner.GetComponent<NetworkedLoveTriggerManager>();

                if (debugMode)
                    Debug.Log($"[UniversalLTMenu] Found/switched to partner: {selectedPartner.name}");

                PopulateMenu();
                PlaySound(menuOpenSound);
            }
        }
        else
        {
            if (selectedPartner != null)
            {
                selectedPartner = null;
                currentTargetManager = null;
                ClearMenu();
                PlaySound(menuCloseSound);
            }
        }
    }

    NetworkObject GetClosestTarget(List<NetworkObject> targets)
    {
        NetworkObject closest = null;
        float closestDistance = float.MaxValue;

        foreach (var target in targets)
        {
            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = target;
            }
        }

        return closest;
    }

    void PopulateMenu()
    {
        ClearMenu();

        if (currentTargetManager == null || activeMenuContainer == null) return;

        GetAvailableTriggers();

        if (currentTriggers.Count == 0) return;

        if (debugMode)
            Debug.Log($"[UniversalLTMenu] Populating {currentPlatform} menu with {currentTriggers.Count} triggers");

        for (int i = 0; i < currentTriggers.Count; i++)
        {
            CreateTriggerButton(currentTriggers[i], i);
        }
    }

    void CreateTriggerButton(LoveTriggerSO trigger, int index)
    {
        if (buttonPrefab == null || activeMenuContainer == null) return;

        GameObject btnObj = Instantiate(buttonPrefab, activeMenuContainer);


        // Platform-appropriate positioning and sizing
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        if (rect == null)
            rect = btnObj.AddComponent<RectTransform>();

        rect.sizeDelta = currentButtonSize;

        int col = index % currentButtonsPerRow;
        int row = index / currentButtonsPerRow;
        float x = col * (currentButtonSize.x + currentPaddingX);
        float y = -row * (currentButtonSize.y + currentPaddingY);
        rect.anchoredPosition = new Vector2(x, y);

        // Configure button
        Button btn = btnObj.GetComponent<Button>();
        if (btn == null)
            btn = btnObj.AddComponent<Button>();

        TMP_Text label = btnObj.GetComponentInChildren<TMP_Text>();
        if (label == null)
        {
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform);
            label = labelObj.AddComponent<TMP_Text>();
            label.text = "Button";
            label.fontSize = 18;
            label.color = Color.black;
            label.alignment = TextAlignmentOptions.Center;

            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;
        }

        Image btnImage = btnObj.GetComponent<Image>();
        if (btnImage == null)
            btnImage = btnObj.AddComponent<Image>();

        if (label != null)
        {
            label.text = trigger.triggerName;

            // Platform-specific font size
            if (isVRMode)
                label.fontSize = 24;
            else if (currentPlatform == PlatformType.Mobile)
                label.fontSize = 16;
            else
                label.fontSize = 18;

            // Add trigger info
            if (trigger.requiresConsent)
                label.text += "\n(Consent)";

            if (trigger.animationType == AnimationType.Partner)
                label.text += "\n(Partner)";
        }

        if (btnImage != null && trigger.icon != null)
        {
            btnImage.sprite = trigger.icon;
        }

        // Add click listener
        btn.onClick.AddListener(() => OnTriggerButtonClicked(trigger));

        // Platform-specific interaction setup
        if (isVRMode)
        {
            SetupVRButtonInteraction(btn, trigger);
        }
        else
        {
            SetupDesktopButtonInteraction(btn, trigger);
        }

        createdButtons.Add(btn);
        UpdateButtonState(btn, trigger);
    }

    void SetupVRButtonInteraction(Button button, LoveTriggerSO trigger)
    {
        // VR buttons need colliders for hand interaction
        if (button.GetComponent<Collider>() == null)
        {
            BoxCollider collider = button.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(currentButtonSize.x / 100f, currentButtonSize.y / 100f, 0.1f);
        }
    }

    void SetupDesktopButtonInteraction(Button button, LoveTriggerSO trigger)
    {
        // Desktop buttons use standard Unity UI events
        // Additional hover effects can be added here
    }

    void OnTriggerButtonClicked(LoveTriggerSO trigger)
    {
        if (!Object.HasInputAuthority) return;

        if (debugMode)
            Debug.Log($"[UniversalLTMenu] Button clicked: {trigger.triggerName} (Platform: {currentPlatform})");

        if (currentTargetManager == null)
        {
            Debug.LogError("[UniversalLTMenu] No target manager available!");
            return;
        }

        if (currentTargetManager.IsProcessing)
        {
            if (debugMode)
                Debug.LogWarning("[UniversalLTMenu] Target manager is busy");
            return;
        }

        // Request trigger through network system
        currentTargetManager.RequestLoveTrigger(trigger.triggerID, selectedPartner);

        // Set cooldown
        if (trigger.cooldownDuration > 0)
        {
            triggerCooldowns[trigger.triggerID] = Time.time + trigger.cooldownDuration;
        }

        PlaySound(buttonClickSound);

        // Platform-specific feedback
        if (isVRMode && enableHapticFeedback)
        {
            TriggerHapticFeedback();
        }
    }

    void OnUIHover(GameObject hoveredObject)
    {
        if (hoveredObject == lastHoveredObject) return;

        lastHoveredObject = hoveredObject;

        Button hoveredButton = hoveredObject?.GetComponent<Button>();

        if (hoveredButton != currentHoveredButton)
        {
            if (currentHoveredButton != null)
            {
                OnButtonHoverExit(currentHoveredButton);
            }

            currentHoveredButton = hoveredButton;

            if (currentHoveredButton != null)
            {
                OnButtonHoverEnter(currentHoveredButton);
            }
        }
    }

    void OnUIClick(GameObject clickedObject)
    {
        Button clickedButton = clickedObject?.GetComponent<Button>();
        if (clickedButton != null)
        {
            // The button's onClick will handle the actual trigger
            PlaySound(buttonClickSound);
        }
    }

    void OnInputAction(string actionName, InputAction action)
    {
        switch (actionName)
        {
            case "SelectTrigger":
                if (action.isPressed && !action.wasPressed)
                {
                    if (currentHoveredButton != null)
                    {
                        currentHoveredButton.onClick.Invoke();
                    }
                }
                break;

            case "Cancel":
                if (action.isPressed && !action.wasPressed)
                {
                    ToggleMenuVisibility();
                }
                break;
        }
    }

    void OnButtonHoverEnter(Button button)
    {
        // Visual feedback
        if (isVRMode)
        {
            button.transform.localScale = Vector3.one * highlightScale;
        }

        var colors = button.colors;
        Color originalColor = colors.normalColor;
        colors.normalColor = Color.Lerp(originalColor, highlightColor, 0.3f);
        button.colors = colors;

        PlaySound(buttonHoverSound);

        if (isVRMode && enableHapticFeedback)
        {
            TriggerHapticFeedback(0.1f);
        }
    }

    void OnButtonHoverExit(Button button)
    {
        // Restore original appearance
        if (isVRMode)
        {
            button.transform.localScale = Vector3.one;
        }

        var trigger = GetTriggerForButton(button);
        if (trigger != null)
        {
            UpdateButtonState(button, trigger);
        }
    }

    LoveTriggerSO GetTriggerForButton(Button button)
    {
        int index = createdButtons.IndexOf(button);
        if (index >= 0 && index < currentTriggers.Count)
            return currentTriggers[index];
        return null;
    }

    void GetAvailableTriggers()
    {
        currentTriggers.Clear();

        if (currentTargetManager == null) return;

        // FIXED: NetworkedLoveTriggerManager uses Database property (capital D)
        var database = currentTargetManager.Database;

        if (database == null)
        {
            if (debugMode)
                Debug.LogWarning("[UniversalLTMenu] No database found in NetworkedLoveTriggerManager!");
            return;
        }

        // Get all triggers from the database
        LoveTriggerSO[] allTriggers = database.triggers;

        if (allTriggers == null || allTriggers.Length == 0)
        {
            if (debugMode)
                Debug.LogWarning("[UniversalLTMenu] Database has no triggers!");
            return;
        }

        if (debugMode)
            Debug.Log($"[UniversalLTMenu] Found {allTriggers.Length} triggers in database");

        // Apply filters
        foreach (var trigger in allTriggers)
        {
            if (trigger == null) continue;

            bool passesFilter = string.IsNullOrEmpty(categoryFilter) || trigger.category == categoryFilter;

            if (passesFilter)
            {
                currentTriggers.Add(trigger);
            }
        }

        if (debugMode)
            Debug.Log($"[UniversalLTMenu] Found {currentTriggers.Count} triggers after filtering");
    }

    void RefreshButtonStates()
    {
        for (int i = 0; i < createdButtons.Count && i < currentTriggers.Count; i++)
        {
            UpdateButtonState(createdButtons[i], currentTriggers[i]);
        }
    }

    void UpdateButtonState(Button button, LoveTriggerSO trigger)
    {
        if (trigger == null) return;

        bool isAvailable = IsTriggerAvailable(trigger);
        bool isOnCooldown = triggerCooldowns.ContainsKey(trigger.triggerID) &&
                           Time.time < triggerCooldowns[trigger.triggerID];

        button.interactable = isAvailable;

        ColorBlock colors = button.colors;

        if (isOnCooldown)
        {
            colors.normalColor = unavailableButtonColor;
        }
        else if (trigger.requiresConsent)
        {
            colors.normalColor = consentRequiredColor;
        }
        else if (isAvailable)
        {
            colors.normalColor = availableButtonColor;
        }
        else
        {
            colors.normalColor = unavailableButtonColor;
        }

        button.colors = colors;

        // Update text with cooldown info
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null && isOnCooldown)
        {
            float remainingTime = triggerCooldowns[trigger.triggerID] - Time.time;
            label.text = $"{trigger.triggerName}\n({remainingTime:F1}s)";
        }
    }

    bool IsTriggerAvailable(LoveTriggerSO trigger)
    {
        if (triggerCooldowns.ContainsKey(trigger.triggerID))
        {
            if (Time.time < triggerCooldowns[trigger.triggerID])
                return false;
        }

        if (currentTargetManager != null && currentTargetManager.IsProcessing)
            return false;

        return true;
    }

    void UpdateCooldowns()
    {
        var expiredCooldowns = new List<string>();

        foreach (var kvp in triggerCooldowns)
        {
            if (Time.time >= kvp.Value)
            {
                expiredCooldowns.Add(kvp.Key);
            }
        }

        foreach (string triggerID in expiredCooldowns)
        {
            triggerCooldowns.Remove(triggerID);
        }
    }

    void ClearMenu()
    {
        foreach (Transform child in activeMenuContainer)
        {
            Destroy(child.gameObject);
        }
        createdButtons.Clear();
        currentHoveredButton = null;
    }

    void TriggerHapticFeedback(float intensity = 0.3f)
    {
        // Platform-specific haptic feedback
        if (isVRMode)
        {
            // VR controller haptic feedback
            if (debugMode)
                Debug.Log($"[UniversalLTMenu] VR haptic feedback: {intensity}");
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // Public API
    public void ToggleMenuVisibility()
    {
        isMenuVisible = !isMenuVisible;
        if (activeCanvas != null)
        {
            activeCanvas.gameObject.SetActive(isMenuVisible);
        }

        PlaySound(isMenuVisible ? menuOpenSound : menuCloseSound);
    }

    public void SetCategoryFilter(string category)
    {
        categoryFilter = category;
        PopulateMenu();
    }

    public void ForcePlatformConfiguration(PlatformType platform)
    {
        OnPlatformChanged(platform);
    }

    [ContextMenu("Toggle VR Mode")]
    public void ToggleVRMode()
    {
        if (isVRMode)
        {
            ForcePlatformConfiguration(PlatformType.Desktop);
        }
        else
        {
            ForcePlatformConfiguration(PlatformType.VR_Generic);
        }
    }

    [ContextMenu("Reposition VR Menu")]
    public void RepositionVRMenu()
    {
        if (isVRMode)
        {
            PositionMenuForPlatform();
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw detection radius
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, npcDetectionRadius);

        // Draw VR UI position
        if (isVRMode && activeCamera != null)
        {
            Gizmos.color = Color.green;
            Vector3 vrUIPos = activeCamera.transform.position + activeCamera.transform.forward * vrUIDistance;
            Gizmos.DrawWireCube(vrUIPos, Vector3.one * 0.5f);
        }
    }

    [ContextMenu("Debug Current Setup")]
    public void DebugCurrentSetup()
    {
        Debug.Log("========== UniversalLTMenuController Debug Report ==========");
        Debug.Log($"Platform: {currentPlatform}");
        Debug.Log($"VR Mode: {isVRMode}");
        Debug.Log($"Active Camera: {(activeCamera != null ? activeCamera.name : "NULL")}");
        Debug.Log($"Active Canvas: {(activeCanvas != null ? activeCanvas.name : "NULL")}");
        Debug.Log($"Active Menu Container: {(activeMenuContainer != null ? activeMenuContainer.name : "NULL")}");
        Debug.Log($"Selected Partner: {(selectedPartner != null ? selectedPartner.name : "NULL")}");
        Debug.Log($"Current Target Manager: {(currentTargetManager != null ? "EXISTS" : "NULL")}");

        // Enhanced trigger debugging
        if (currentTargetManager != null)
        {
            Debug.Log($"=== NetworkedLoveTriggerManager Info ===");

            // Check Database property (capital D)
            var database = currentTargetManager.Database;
            if (database != null)
            {
                Debug.Log($"Database exists: YES");
                Debug.Log($"Database name: {database.name}");

                if (database.triggers != null)
                {
                    Debug.Log($"Database.triggers: {database.triggers.Length} triggers");
                    for (int i = 0; i < Mathf.Min(3, database.triggers.Length); i++)
                    {
                        if (database.triggers[i] != null)
                            Debug.Log($"  - {database.triggers[i].triggerName} (ID: {database.triggers[i].triggerID})");
                    }
                }
            }
        }
    }
}