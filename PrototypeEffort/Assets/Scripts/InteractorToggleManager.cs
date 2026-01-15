using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

/// <summary>
/// Manages XR Interaction Groups for controllers, allowing both Ray and Direct interactors
/// to be active simultaneously with proper priority handling.
/// Direct interactor takes priority when objects are in range, Ray interactor works otherwise.
/// </summary>
public class InteractorToggleManager : MonoBehaviour
{
    [Header("Left Controller")]
    [SerializeField] private XRInteractionGroup leftInteractionGroup;
    [SerializeField] private XRRayInteractor leftRayInteractor;
    [SerializeField] private XRDirectInteractor leftDirectInteractor;

    [Header("Right Controller")]
    [SerializeField] private XRInteractionGroup rightInteractionGroup;
    [SerializeField] private XRRayInteractor rightRayInteractor;
    [SerializeField] private XRDirectInteractor rightDirectInteractor;
    
    // Singleton for easy access from other scripts
    public static InteractorToggleManager Instance { get; private set; }

    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple InteractorToggleManagers detected!");
        }
    }

    private void OnEnable()
    {
        SetupInteractionGroups();
    }
    
    /// <summary>
    /// Sets up XRInteractionGroups with both Ray and Direct interactors active.
    /// Direct interactor has higher priority and blocks Ray when objects are in range.
    /// </summary>
    private void SetupInteractionGroups()
    {
        // Setup Left Controller Group
        if (leftInteractionGroup != null)
        {
            SetupGroup(leftInteractionGroup, leftDirectInteractor, leftRayInteractor, "Left");
        }
        else
        {
            Debug.LogWarning("[InteractorToggleManager] Left XRInteractionGroup not assigned!");
        }
        
        // Setup Right Controller Group
        if (rightInteractionGroup != null)
        {
            SetupGroup(rightInteractionGroup, rightDirectInteractor, rightRayInteractor, "Right");
        }
        else
        {
            Debug.LogWarning("[InteractorToggleManager] Right XRInteractionGroup not assigned!");
        }
        
        Debug.Log("[InteractorToggleManager] Interaction groups configured. Both Ray and Direct interactors are active with proper priority.");
    }
    
    private void SetupGroup(XRInteractionGroup group, XRDirectInteractor directInteractor, XRRayInteractor rayInteractor, string side)
    {
        // Ensure both interactors are enabled
        if (directInteractor != null) directInteractor.enabled = true;
        if (rayInteractor != null) rayInteractor.enabled = true;
        
        // Check if interactors are assigned
        if (directInteractor == null)
        {
            Debug.LogError($"[InteractorToggleManager] {side} Direct Interactor is NOT assigned! Assign it in the Inspector.");
            return;
        }
        
        if (rayInteractor == null)
        {
            Debug.LogError($"[InteractorToggleManager] {side} Ray Interactor is NOT assigned! Assign it in the Inspector.");
            return;
        }
        
        // Clear existing members
        group.ClearGroupMembers();
        
        // Add interactors to group in priority order (Direct first = higher priority)
        group.AddGroupMember(directInteractor);
        Debug.Log($"[InteractorToggleManager] Added {side} Direct Interactor ({directInteractor.gameObject.name}) to group");
        
        group.AddGroupMember(rayInteractor);
        Debug.Log($"[InteractorToggleManager] Added {side} Ray Interactor ({rayInteractor.gameObject.name}) to group");
    }
    
    /// <summary>
    /// Check if any Direct interactor is currently hovering or selecting an object
    /// </summary>
    public bool IsDirectInteractorActive()
    {
        bool leftActive = leftDirectInteractor != null && 
                         (leftDirectInteractor.hasHover || leftDirectInteractor.hasSelection);
        bool rightActive = rightDirectInteractor != null && 
                          (rightDirectInteractor.hasHover || rightDirectInteractor.hasSelection);
        
        return leftActive || rightActive;
    }
    
    /// <summary>
    /// Legacy method - returns false since we no longer have "ray mode" vs "direct mode"
    /// Both are always active, group handles priority
    /// </summary>
    public bool IsRayMode()
    {
        // If Direct interactor is NOT active, then Ray is effectively in control
        return !IsDirectInteractorActive();
    }
    
    /// <summary>
    /// Legacy methods kept for compatibility - no-ops since groups handle this now
    /// </summary>
    public void SetRayMode()
    {
        // No-op: Groups handle this automatically
        Debug.Log("[InteractorToggleManager] SetRayMode called - using XRInteractionGroup, no manual switching needed");
    }
    
    public void SetDirectMode()
    {
        // No-op: Groups handle this automatically
        Debug.Log("[InteractorToggleManager] SetDirectMode called - using XRInteractionGroup, no manual switching needed");
    }
    
    public string GetCurrentMode()
    {
        return IsDirectInteractorActive() ? "direct" : "ray";
    }
}
