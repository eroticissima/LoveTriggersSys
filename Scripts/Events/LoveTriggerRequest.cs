// =============================================================================
// LoveTriggerRequest.cs - CLEAN VERSION (No Duplicates)
// =============================================================================

using UnityEngine;

namespace LTSystem.Events
{
    /// <summary>
    /// Represents a request to execute a love trigger action
    /// Used for both simple and complex trigger scenarios
    /// </summary>
    [System.Serializable]
    public class LoveTriggerRequest
    {
        [Header("Basic Request Info")]
        public string triggerID;
        public GameObject source;
        public GameObject target;

        [Header("Advanced Properties")]
        public LoveTriggerSO Trigger; // Full trigger data when available
        public float RequestTime;

        [Header("Metadata")]
        public bool isNetworked = false;
        public int requesterId = -1;
        public int targetId = -1;

        // Convenience property for priority
        public TriggerPriority Priority => Trigger != null ? Trigger.priority : TriggerPriority.Normal;

        // Simple constructor (backward compatibility)
        public LoveTriggerRequest(string triggerID, GameObject source, GameObject target)
        {
            this.triggerID = triggerID;
            this.source = source;
            this.target = target;
            this.RequestTime = Time.time;
            this.isNetworked = false;
        }

        // Full constructor with LoveTriggerSO
        public LoveTriggerRequest(LoveTriggerSO trigger, GameObject source, GameObject target)
        {
            this.Trigger = trigger;
            this.triggerID = trigger?.triggerID ?? "";
            this.source = source;
            this.target = target;
            this.RequestTime = Time.time;
            this.isNetworked = false;
        }

        // Network constructor
        public LoveTriggerRequest(string triggerID, int sourceId, int targetId, bool networked = true)
        {
            this.triggerID = triggerID;
            this.requesterId = sourceId;
            this.targetId = targetId;
            this.isNetworked = networked;
            this.RequestTime = Time.time;
        }

        // Validation methods
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(triggerID))
                return false;

            if (!isNetworked)
            {
                return source != null;
            }
            else
            {
                return requesterId >= 0;
            }
        }

        public bool RequiresTarget()
        {
            if (Trigger != null)
            {
                return Trigger.animationType == AnimationType.Partner ||
                       Trigger.animationType == AnimationType.Synchronized;
            }

            // Default assumption for unknown triggers
            return target != null || targetId >= 0;
        }

        public bool RequiresConsent()
        {
            return Trigger?.requiresConsent ?? false;
        }

        // Helper methods
        public float GetElapsedTime()
        {
            return Time.time - RequestTime;
        }

        public string GetDisplayName()
        {
            return Trigger?.triggerName ?? triggerID ?? "Unknown Trigger";
        }

        public string GetCategory()
        {
            return Trigger?.category ?? "Default";
        }

        // Debug information
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"LoveTriggerRequest:");
            sb.AppendLine($"  ID: {triggerID}");
            sb.AppendLine($"  Source: {(source?.name ?? $"ID:{requesterId}")}");
            sb.AppendLine($"  Target: {(target?.name ?? $"ID:{targetId}")}");
            sb.AppendLine($"  Networked: {isNetworked}");
            sb.AppendLine($"  Time: {RequestTime:F2}");
            sb.AppendLine($"  Valid: {IsValid()}");

            if (Trigger != null)
            {
                sb.AppendLine($"  Trigger: {Trigger.triggerName}");
                sb.AppendLine($"  Category: {Trigger.category}");
                sb.AppendLine($"  Priority: {Trigger.priority}");
                sb.AppendLine($"  Requires Consent: {Trigger.requiresConsent}");
            }

            return sb.ToString();
        }

        // Static factory methods for common scenarios
        public static LoveTriggerRequest CreateLocal(LoveTriggerSO trigger, GameObject source, GameObject target = null)
        {
            return new LoveTriggerRequest(trigger, source, target);
        }

        public static LoveTriggerRequest CreateNetworked(string triggerID, int sourceId, int targetId = -1)
        {
            return new LoveTriggerRequest(triggerID, sourceId, targetId, true);
        }

        public static LoveTriggerRequest CreateFromID(string triggerID, GameObject source, GameObject target = null)
        {
            return new LoveTriggerRequest(triggerID, source, target);
        }
    }

    /// <summary>
    /// Response to a love trigger request (for consent systems)
    /// </summary>
    [System.Serializable]
    public class LoveTriggerResponse
    {
        public LoveTriggerRequest Request;
        public bool IsAccepted;
        public string Reason;
        public float ResponseTime;

        public GameObject Target => Request?.target;
        public GameObject Source => Request?.source;

        public LoveTriggerResponse(LoveTriggerRequest request, bool isAccepted, string reason = "")
        {
            Request = request;
            IsAccepted = isAccepted;
            Reason = reason;
            ResponseTime = Time.time;
        }

        public float GetResponseTime()
        {
            if (Request != null)
                return ResponseTime - Request.RequestTime;
            return 0f;
        }

        public override string ToString()
        {
            return $"LoveTriggerResponse: {(IsAccepted ? "ACCEPTED" : "DENIED")} - {Reason}";
        }
    }
}