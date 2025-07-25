// ============================================================================
// ILoveTriggerNetworkService.cs - FIXED INTERFACE WITH SETTABLE PROPERTIES
// ============================================================================

using System;
using Fusion;
using LTSystem.Events;

namespace LTSystem.Network
{
    public interface ILoveTriggerNetworkService
    {
        /// <summary> Maximum distance to send a love-trigger. </summary>
        float MaxTriggerDistance { get; set; } // FIXED: Made settable

        /// <summary> Whether mutual consent is required. </summary>
        bool RequireMutualConsent { get; set; } // FIXED: Made settable

        /// <summary> Enable debug logging. </summary>
        bool DebugMode { get; set; } // FIXED: Made settable

        /// <summary> The database of all available triggers. </summary>
        LTSystem.LoveTriggerDatabase Database { get; set; } // FIXED: Made settable with proper namespace

        /// <summary> True if a trigger is currently playing. </summary>
        bool IsProcessing { get; }

        /// <summary>
        /// Requests a love-trigger by ID on a target NetworkObject.
        /// This will handle RPCs, validation, and playback.
        /// </summary>
        void RequestLoveTrigger(string triggerID, NetworkObject target);

        /// <summary> Fired when the trigger has finished playing locally. </summary>
        event Action<LoveTriggerRequest> OnTriggerComplete;
    }
}