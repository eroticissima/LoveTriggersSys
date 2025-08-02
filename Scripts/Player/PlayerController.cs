// =============================================================================
// PlayerController.cs - LEGACY COMPATIBILITY WRAPPER
// =============================================================================

using UnityEngine;
using LTSystem.Player;

/// <summary>
/// Legacy PlayerController - now redirects to appropriate platform-specific controller
/// This maintains backward compatibility while using the new architecture
/// </summary>
[System.Obsolete("Use ThirdPersonPlayerController or VRPlayerController directly")]
public class PlayerController : BasePlayerController
{
    [Header("Legacy Compatibility")]
    [SerializeField] private bool autoDetectPlayerType = true;
    [SerializeField] private PlayerType legacyPlayerType = PlayerType.ThirdPerson;

    protected override void InitializeBaseController()
    {
        // Auto-detect player type based on components
        if (autoDetectPlayerType)
        {
            DetectPlayerType();
        }
        else
        {
            playerType = legacyPlayerType;
        }

        base.InitializeBaseController();

        Debug.LogWarning("[PlayerController] Using legacy PlayerController. Consider migrating to ThirdPersonPlayerController or VRPlayerController for better performance and features.");
    }

    private void DetectPlayerType()
    {
        // Check for VR components
        if (GetComponentInChildren<RootMotion.FinalIK.VRIK>() != null ||
            transform.Find("XR Origin") != null ||
            transform.Find("XR Rig") != null ||
            transform.Find("[CameraRig]") != null)
        {
            playerType = PlayerType.VR;
            Debug.Log("[PlayerController] Auto-detected VR player type");
        }
        // Check for third person components
        else if (GetComponent<CharacterController>() != null ||
                 GetComponentInChildren<UnityEngine.InputSystem.PlayerInput>() != null)
        {
            playerType = PlayerType.ThirdPerson;
            Debug.Log("[PlayerController] Auto-detected Third Person player type");
        }
        else
        {
            playerType = PlayerType.ThirdPerson; // Default fallback
            Debug.Log("[PlayerController] Defaulted to Third Person player type");
        }
    }

    #region Legacy Implementation - Basic functionality only

    protected override void SetupInputSystem()
    {
        // Basic legacy input - limited functionality
        Debug.LogWarning("[PlayerController] Using legacy input system. Migrate to platform-specific controllers for full input support.");
    }

    protected override void SetupCameraSystem()
    {
        // Basic camera setup
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    protected override void SetupAnimatorController()
    {
        // Basic animator setup - handled in character instantiation
    }

    protected override void HandleMovementInput()
    {
        // Legacy movement - very basic
        if (!Object.HasInputAuthority) return;

        // Basic WASD movement for backward compatibility
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) move += Vector3.back;
        if (Input.GetKey(KeyCode.A)) move += Vector3.left;
        if (Input.GetKey(KeyCode.D)) move += Vector3.right;

        if (move.magnitude > 0.1f)
        {
            transform.Translate(move.normalized * 5f * Time.deltaTime);
        }
    }

    protected override void HandleInteractionInput()
    {
        // Legacy interaction
        if (!Object.HasInputAuthority) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            TriggerLoveAction("default");
        }
    }

    protected override string GetIdleStateName()
    {
        return playerType == PlayerType.VR ? "VRIK_Idle" : "Idle";
    }

    protected override bool ValidatePlatformComponents()
    {
        // Basic validation
        return true;
    }

    public void TriggerLoveAction(string triggerID)
    {
        if (triggerManager != null && !string.IsNullOrEmpty(triggerID))
        {
            // Find nearby targets
            var nearbyTargets = Physics.OverlapSphere(transform.position, maxTriggerDistance);
            foreach (var target in nearbyTargets)
            {
                var targetNetworkObject = target.GetComponent<NetworkObject>();
                var targetManager = target.GetComponent<NetworkedLoveTriggerManager>();

                if (targetNetworkObject != null && targetManager != null && target.gameObject != gameObject)
                {
                    triggerManager.RequestLoveTrigger(triggerID, targetNetworkObject);
                    break;
                }
            }
        }
    }

    #endregion

    #region Legacy API Compatibility

    public void OnCharacterSelected(string characterID)
    {
        SwitchCharacter(characterID);
    }

    public string GetCurrentCharacterID()
    {
        return currentCharacterData?.characterID ?? "";
    }

    public float GetDistanceToPlayer(PlayerController otherPlayer)
    {
        if (otherPlayer == null) return float.MaxValue;
        return Vector3.Distance(transform.position, otherPlayer.transform.position);
    }

    public bool IsInRangeOf(PlayerController otherPlayer)
    {
        return GetDistanceToPlayer(otherPlayer) <= maxTriggerDistance;
    }

    #endregion
}