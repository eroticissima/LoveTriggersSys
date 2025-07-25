using UnityEngine;
using LTSystem.Objects;
using System.Collections.Generic;

[RequireComponent(typeof(InteractableObject))]
public class PlayerDetectionDebug : MonoBehaviour
{
    private InteractableObject interactable;
    private List<Collider> nearbyColliders = new List<Collider>();

    void Start()
    {
        interactable = GetComponent<InteractableObject>();
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[PlayerDetection] Trigger Enter: {other.name} (Tag: {other.tag})");

        var player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            Debug.Log($"[PlayerDetection] ✓ PlayerController found on {other.name}");
        }
        else
        {
            Debug.Log($"[PlayerDetection] ✗ No PlayerController on {other.name}");
        }
    }

    void OnTriggerExit(Collider other)
    {
        Debug.Log($"[PlayerDetection] Trigger Exit: {other.name}");
    }

    void Update()
    {
        // Check for players in radius every second
        if (Time.frameCount % 60 == 0)
        {
            var colliders = Physics.OverlapSphere(transform.position, interactable.interactionRadius);
            Debug.Log($"[PlayerDetection] Objects in range ({interactable.interactionRadius}m): {colliders.Length}");

            foreach (var col in colliders)
            {
                if (col.GetComponent<PlayerController>() != null)
                {
                    Debug.Log($"[PlayerDetection] Player found: {col.name} at distance: {Vector3.Distance(transform.position, col.transform.position)}");
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        if (interactable == null) return;

        // Draw interaction radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactable.interactionRadius);
    }
}