using UnityEngine;
using LTSystem.Objects;

public class ForceUIVisible : MonoBehaviour
{
    void Start()
    {
        // Force the UI to be visible
        gameObject.SetActive(true);

        var canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        Debug.Log($"[ForceUIVisible] UI forced visible: {gameObject.name}");

        // Log all children
        Debug.Log($"[ForceUIVisible] Child count: {transform.childCount}");
        foreach (Transform child in transform)
        {
            Debug.Log($"  - Child: {child.name} (Active: {child.gameObject.activeSelf})");
        }
    }

    void Update()
    {
        // Keep it visible
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            Debug.LogWarning("[ForceUIVisible] UI was deactivated, forcing it back on!");
        }
    }
}