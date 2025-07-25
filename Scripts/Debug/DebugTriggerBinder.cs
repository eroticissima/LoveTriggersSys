using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using LTSystem.Network;

public class DebugTriggerBinder : MonoBehaviour
{
    public string triggerID;
    public NetworkedLoveTriggerManager triggerManager;

    void Start()
    {
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => {
                if (triggerManager != null)
                {
                    var target = FindClosestPartner();
                    if (target != null)
                    {
                        triggerManager.RequestLoveTrigger(triggerID, target);
                        Debug.Log($"[DebugTrigger] Requested trigger {triggerID} on {target.name}");
                    }
                }
            });
        }
    }

    NetworkObject FindClosestPartner()
    {
        float range = 5f;
        var colliders = Physics.OverlapSphere(transform.position, range);
        foreach (var col in colliders)
        {
            if (col.TryGetComponent(out NetworkedLoveTriggerManager manager) && manager != triggerManager)
            {
                return manager.GetComponent<NetworkObject>();
            }
        }
        return null;
    }
}
