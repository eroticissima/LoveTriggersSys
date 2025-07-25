using UnityEngine;
using UnityEngine.UI;
using LTSystem.Objects;

#if UNITY_EDITOR
using UnityEditor;

[System.Serializable]
public class InteractableObjectUIPrefabFixer : EditorWindow
{
    [MenuItem("Tools/Love Trigger System/Fix InteractableObjectUI Prefab")]
    static void ShowWindow()
    {
        GetWindow<InteractableObjectUIPrefabFixer>("Fix UI Prefab");
    }
    
    void OnGUI()
    {
        EditorGUILayout.LabelField("InteractableObjectUI Prefab Fixer", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Fix UI Prefab References", GUILayout.Height(40)))
        {
            FixUIPrefab();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("This will attempt to fix missing references in the InteractableObjectUI prefab.", MessageType.Info);
    }
    
    void FixUIPrefab()
    {
        // Load the prefab
        var prefabPath = "Assets/Resources/UI/InteractableObjectUI.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        
        if (prefab == null)
        {
            Debug.LogError($"Prefab not found at {prefabPath}");
            return;
        }
        
        // Get the UI component
        var uiComponent = prefab.GetComponent<InteractableObjectUI>();
        if (uiComponent == null)
        {
            Debug.LogError("No InteractableObjectUI component on prefab!");
            return;
        }
        
        // Use serialization to set private fields
        var serializedObject = new SerializedObject(uiComponent);
        
        // Find and set canvas
        var canvas = prefab.GetComponent<Canvas>();
        if (canvas != null)
        {
            var canvasProp = serializedObject.FindProperty("canvas");
            if (canvasProp != null)
            {
                canvasProp.objectReferenceValue = canvas;
                Debug.Log("✓ Set Canvas reference");
            }
        }
        
        // Find and set canvas group
        var canvasGroup = prefab.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            var canvasGroupProp = serializedObject.FindProperty("canvasGroup");
            if (canvasGroupProp != null)
            {
                canvasGroupProp.objectReferenceValue = canvasGroup;
                Debug.Log("✓ Set CanvasGroup reference");
            }
        }
        
        // Find Panel Container
        var panelContainer = prefab.transform.Find("Panel") ?? prefab.transform.Find("PanelContainer") ?? prefab.transform.Find("Panel Container");
        if (panelContainer != null)
        {
            var panelProp = serializedObject.FindProperty("panelContainer");
            if (panelProp != null)
            {
                panelProp.objectReferenceValue = panelContainer.GetComponent<RectTransform>();
                Debug.Log("✓ Set Panel Container reference");
            }
        }
        else
        {
            Debug.LogWarning("✗ No Panel Container found - please create one!");
        }
        
        // Find Button Container
        Transform buttonContainer = null;
        if (panelContainer != null)
        {
            buttonContainer = panelContainer.Find("ButtonContainer") ?? panelContainer.Find("Button Container") ?? panelContainer.Find("Buttons");
        }
        
        if (buttonContainer == null)
        {
            // Search entire hierarchy
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>())
            {
                if (t.name.ToLower().Contains("button") && t.name.ToLower().Contains("container"))
                {
                    buttonContainer = t;
                    break;
                }
            }
        }
        
        if (buttonContainer != null)
        {
            var buttonContainerProp = serializedObject.FindProperty("buttonContainer");
            if (buttonContainerProp != null)
            {
                buttonContainerProp.objectReferenceValue = buttonContainer;
                Debug.Log("✓ Set Button Container reference");
            }
        }
        else
        {
            Debug.LogWarning("✗ No Button Container found - please create one!");
        }
        
        // Apply changes
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(prefab);
        AssetDatabase.SaveAssets();
        
        Debug.Log("Prefab fixing complete! Check the console for any warnings.");
    }
}
#endif