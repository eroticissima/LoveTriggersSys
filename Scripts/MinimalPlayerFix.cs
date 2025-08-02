using UnityEngine;

/// <summary>
/// Minimal fix for PlayerController and Camera issues
/// Just add this to an empty GameObject in your scene
/// </summary>
public class MinimalPlayerFix : MonoBehaviour
{
    void Start()
    {
        // Fix camera conflicts
        StartCoroutine(FixCameraConflicts());
    }

    System.Collections.IEnumerator FixCameraConflicts()
    {
        yield return new WaitForSeconds(0.1f);

        // Disable LinkGameView cameras that cause conflicts
        var allGameObjects = FindObjectsOfType<GameObject>();
        foreach (var go in allGameObjects)
        {
            if (go.name.Contains("LinkGameViewCamera") || go.name.Contains("LinkGameView"))
            {
                go.SetActive(false);
                Debug.Log($"[MinimalFix] Disabled: {go.name}");
            }
        }

        Debug.Log("[MinimalFix] Camera conflicts resolved");
    }
}