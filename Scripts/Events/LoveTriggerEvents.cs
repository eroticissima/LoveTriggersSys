// =============================================================================
// LoveTriggerEvents.cs - NETWORK TYPES ONLY (No Duplicates)
// =============================================================================

using UnityEngine;
using Fusion;

namespace LTSystem.Events
{
    // NETWORK-SPECIFIC TYPES ONLY - LoveTriggerRequest is in separate file

    /// <summary>
    /// Network state for love trigger synchronization
    /// </summary>
    [System.Serializable]
    public struct LoveTriggerNetworkState : INetworkStruct
    {
        public bool isProcessing;
        public NetworkString<_32> currentTriggerID;
        public float triggerStartTime;
        public float triggerDuration;
        public byte triggerPhase; // 0=idle, 1=starting, 2=playing, 3=ending
    }

    /// <summary>
    /// Network data for love trigger transmission
    /// </summary>
    [System.Serializable]
    public struct NetworkLoveTriggerData : INetworkStruct
    {
        public NetworkString<_32> triggerID;
        public NetworkId sourceNetworkId;
        public NetworkId targetNetworkId;
        public float timestamp;
        public byte priority;
        public NetworkButtons inputButtons;
    }

    /// <summary>
    /// Network consent request data
    /// </summary>
    [System.Serializable]
    public struct NetworkConsentRequest : INetworkStruct
    {
        public NetworkString<_32> triggerID;
        public NetworkId requesterNetworkId;
        public NetworkId targetNetworkId;
        public float requestTime;
        public byte consentType; // 0=manual, 1=trusted, 2=auto
    }

    /// <summary>
    /// Network consent response data
    /// </summary>
    [System.Serializable]
    public struct NetworkConsentResponse : INetworkStruct
    {
        public NetworkString<_32> triggerID;
        public NetworkId requesterNetworkId;
        public NetworkId responderNetworkId;
        public bool isAccepted;
        public float responseTime;
        public NetworkString<_32> reason;
    }
}