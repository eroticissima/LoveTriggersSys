using UnityEngine;
using LTSystem.Objects;
using System.Reflection;

[RequireComponent(typeof(InteractableObject))]
public class UICreationDebug : MonoBehaviour
{
    private InteractableObject interactable;

    void Start()
    {
        interactable = GetComponent<InteractableObject>();

        // Check if we have triggers assigned
        var triggers = interactable.GetAvailableTriggers();
        Debug.Log($"[UIDebug] InteractableObject has {triggers?.Length ?? 0} triggers assigned");

        if (triggers != null)
        {
            foreach (var trigger in triggers)
            {
                if (trigger == null)
                    Debug.LogError("[UIDebug] NULL trigger in array!");
                else
                    Debug.Log($"[UIDebug] Trigger: {trigger.triggerName} (ID: {trigger.triggerID})");
            }
        }
    }

    [ContextMenu("Force Create UI")]
    void ForceCreateUI()
    {
        Debug.Log("[UIDebug] === FORCE CREATE UI TEST ===");

        // Try to load the UI prefab
        var uiPrefab = Resources.Load<InteractableObjectUI>("UI/InteractableObjectUI");
        if (uiPrefab == null)
        {
            Debug.LogError("[UIDebug] Failed to load UI prefab from Resources/UI/InteractableObjectUI");
            return;
        }

        Debug.Log("[UIDebug] UI Prefab loaded successfully");

        // Check prefab components
        var prefabCanvas = uiPrefab.GetComponent<Canvas>();
        var prefabCanvasGroup = uiPrefab.GetComponent<CanvasGroup>();

        Debug.Log($"[UIDebug] Prefab has Canvas: {prefabCanvas != null}");
        Debug.Log($"[UIDebug] Prefab has CanvasGroup: {prefabCanvasGroup != null}");

        // Use reflection to check private fields
        var uiType = typeof(InteractableObjectUI);
        var fields = uiType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var field in fields)
        {
            if (field.Name == "buttonContainer" || field.Name == "panelContainer" || field.Name == "buttonPrefab")
            {
                var value = field.GetValue(uiPrefab);
                Debug.Log($"[UIDebug] Prefab field '{field.Name}' = {(value != null ? "SET" : "NULL")}");
            }
        }

        // Try to instantiate
        var uiInstance = Instantiate(uiPrefab);
        if (uiInstance != null)
        {
            Debug.Log("[UIDebug] UI instantiated successfully");

            // Try to set it up
            var player = FindObjectOfType<PlayerController>();
            var triggers = interactable.GetAvailableTriggers();

            if (player != null && triggers != null && triggers.Length > 0)
            {
                Debug.Log("[UIDebug] Calling Setup on UI...");
                try
                {
                    uiInstance.Setup(interactable, player, triggers);
                    Debug.Log("[UIDebug] Setup completed!");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[UIDebug] Setup failed: {e.Message}\n{e.StackTrace}");
                }
            }
            else
            {
                Debug.LogError($"[UIDebug] Cannot setup - Player: {player != null}, Triggers: {triggers?.Length ?? 0}");
            }
        }
    }

    [ContextMenu("Check UI Prefab Fields")]
    void CheckUIPrefabFields()
    {
        var uiPrefab = Resources.Load<GameObject>("UI/InteractableObjectUI");
        if (uiPrefab == null)
        {
            Debug.LogError("[UIDebug] No UI prefab found!");
            return;
        }

        // Check all components
        Debug.Log("[UIDebug] === UI PREFAB STRUCTURE ===");
        LogGameObjectHierarchy(uiPrefab.transform, 0);
    }

    void LogGameObjectHierarchy(Transform t, int depth)
    {
        string indent = new string(' ', depth * 2);
        Debug.Log($"{indent}{t.name} - Components: {string.Join(", ", t.GetComponents<Component>().GetType().Name)}");

        foreach (Transform child in t)
        {
            LogGameObjectHierarchy(child, depth + 1);
        }
    }
}
