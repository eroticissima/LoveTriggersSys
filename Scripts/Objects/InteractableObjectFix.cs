using UnityEngine;
using LTSystem.Objects;

/// <summary>
/// Temporary fix for InteractableObject null reference issue
/// </summary>
public class InteractableObjectFix : MonoBehaviour
{
    void Start()
    {
        // Find the InteractableObject component
        var interactable = GetComponent<InteractableObject>();
        if (interactable == null) return;

        // Replace the problematic method
        FixPlayerDetection(interactable);
    }

    void FixPlayerDetection(InteractableObject interactable)
    {
        // We'll use Unity's message system to intercept the detection
        var detector = GetComponentInChildren<InteractionDetector>();
        if (detector != null)
        {
            // Disable the original detector
            detector.enabled = false;

            // Add our fixed detector
            var fixedDetector = detector.gameObject.AddComponent<FixedInteractionDetector>();
            fixedDetector.parentObject = interactable;
        }
    }
}

/// <summary>
/// Fixed interaction detector that handles null NetworkObject
/// </summary>
public class FixedInteractionDetector : MonoBehaviour
{
    public InteractableObject parentObject;

    void OnTriggerEnter(Collider other)
    {
        if (parentObject == null) return;

        var player = other.GetComponent<PlayerController>();
        if (player == null && other.transform.parent != null)
        {
            player = other.transform.parent.GetComponent<PlayerController>();
        }

        if (player != null)
        {
            Debug.Log($"[FixedDetector] Player detected: {player.name}");

            // Directly call the UI show method instead of relying on HasInputAuthority
            ShowUIForPlayer(player);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (parentObject == null) return;

        var player = other.GetComponent<PlayerController>();
        if (player == null && other.transform.parent != null)
        {
            player = other.transform.parent.GetComponent<PlayerController>();
        }

        if (player != null)
        {
            // Call the hide UI method
            HideUIForPlayer();
        }
    }

    void ShowUIForPlayer(PlayerController player)
    {
        // Use reflection to access private methods
        var method = parentObject.GetType().GetMethod("ShowInteractionUI",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method != null)
        {
            method.Invoke(parentObject, new object[] { player });
        }
    }

    void HideUIForPlayer()
    {
        var method = parentObject.GetType().GetMethod("HideInteractionUI",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method != null)
        {
            method.Invoke(parentObject, null);
        }
    }
}