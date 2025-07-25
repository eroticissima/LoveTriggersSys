using UnityEngine;
using UnityEngine.UI; // Added for CanvasScaler, Image, etc.
using LTSystem.Objects;

/// <summary>
/// Helper component to ensure InteractableObjectUI loads correctly
/// </summary>
[RequireComponent(typeof(InteractableObject))]
public class InteractableUILoader : MonoBehaviour
{
    [Header("UI Configuration")]
    public GameObject uiPrefabOverride; // Drag prefab here if Resources loading fails
    public bool createDefaultUIIfMissing = true;

    private InteractableObject interactable;

    void Awake()
    {
        interactable = GetComponent<InteractableObject>();
        EnsureUICanLoad();
    }

    void EnsureUICanLoad()
    {
        // First try Resources
        var resourceUI = Resources.Load<GameObject>("UI/InteractableObjectUI");

        if (resourceUI == null && uiPrefabOverride == null && createDefaultUIIfMissing)
        {
            Debug.LogWarning("[UILoader] No UI prefab found, creating default...");
            CreateDefaultUIPrefab();
        }
        else if (uiPrefabOverride != null)
        {
            Debug.Log("[UILoader] Using override UI prefab");
            // Store in Resources folder if in Editor
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                string path = "Assets/Resources/UI/";
                if (!System.IO.Directory.Exists(path))
                {
                    System.IO.Directory.CreateDirectory(path);
                }
                
                UnityEditor.PrefabUtility.SaveAsPrefabAsset(uiPrefabOverride, 
                    path + "InteractableObjectUI.prefab");
                UnityEditor.AssetDatabase.Refresh();
            }
#endif
        }
    }

    void CreateDefaultUIPrefab()
    {
        // Create UI structure
        GameObject uiRoot = new GameObject("InteractableObjectUI_Default");

        // Add Canvas
        Canvas canvas = uiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // Add required components
        uiRoot.AddComponent<CanvasScaler>();
        uiRoot.AddComponent<GraphicRaycaster>();
        CanvasGroup canvasGroup = uiRoot.AddComponent<CanvasGroup>();

        // Create panel
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(uiRoot.transform);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(300, 400);
        panelRect.anchoredPosition = Vector2.zero;

        // Add background
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.8f);

        // Create button container
        GameObject buttonContainer = new GameObject("ButtonContainer");
        buttonContainer.transform.SetParent(panel.transform);
        RectTransform containerRect = buttonContainer.AddComponent<RectTransform>();
        containerRect.sizeDelta = new Vector2(280, 380);
        containerRect.anchoredPosition = Vector2.zero;

        // Add InteractableObjectUI script
        InteractableObjectUI uiScript = uiRoot.AddComponent<InteractableObjectUI>();

        // Use reflection to set private fields (for demonstration)
        var uiType = typeof(InteractableObjectUI);

        var canvasField = uiType.GetField("canvas",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        canvasField?.SetValue(uiScript, canvas);

        var canvasGroupField = uiType.GetField("canvasGroup",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        canvasGroupField?.SetValue(uiScript, canvasGroup);

        var panelField = uiType.GetField("panelContainer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        panelField?.SetValue(uiScript, panelRect);

        var containerField = uiType.GetField("buttonContainer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        containerField?.SetValue(uiScript, buttonContainer.transform);

        // Save to Resources
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            string path = "Assets/Resources/UI/";
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(uiRoot, 
                path + "InteractableObjectUI.prefab");
            UnityEditor.AssetDatabase.Refresh();
            
            Debug.Log("[UILoader] Created default UI prefab at: " + path);
        }
#endif

        // Destroy the temporary object
        if (Application.isPlaying)
        {
            Destroy(uiRoot);
        }
        else
        {
            DestroyImmediate(uiRoot);
        }
    }
}