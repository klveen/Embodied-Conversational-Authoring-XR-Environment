using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

public class InteractorToggleManager : MonoBehaviour
{
    [Header("Left Controller")]
    [SerializeField] private XRRayInteractor leftRayInteractor;
    [SerializeField] private XRDirectInteractor leftDirectInteractor;

    [Header("Right Controller")]
    [SerializeField] private XRRayInteractor rightRayInteractor;
    [SerializeField] private XRDirectInteractor rightDirectInteractor;


    [Header("Input Actions")]
    [SerializeField] private InputActionReference toggleInteractorButton; // Only use right controller's button

    private bool useRay = false; // Shared state for both hands
    
    // Singleton for easy access from other scripts (like VoiceObjectSpawner)
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
        if (toggleInteractorButton != null)
            toggleInteractorButton.action.performed += ToggleBothInteractors;

        toggleInteractorButton?.action.Enable();

        SetInteractorMode(useRay);
    }


    private void OnDisable()
    {
        if (toggleInteractorButton != null)
            toggleInteractorButton.action.performed -= ToggleBothInteractors;
    }


    private void ToggleBothInteractors(InputAction.CallbackContext ctx)
    {
        useRay = !useRay;
        SetInteractorMode(useRay);
    }


    private void SetInteractorMode(bool useRay)
    {
        if (leftRayInteractor != null) leftRayInteractor.gameObject.SetActive(useRay);
        if (leftDirectInteractor != null) leftDirectInteractor.gameObject.SetActive(!useRay);
        if (rightRayInteractor != null) rightRayInteractor.gameObject.SetActive(useRay);
        if (rightDirectInteractor != null) rightDirectInteractor.gameObject.SetActive(!useRay);
        
        this.useRay = useRay;
        Debug.Log($"[InteractorToggleManager] Switched to {(useRay ? "Ray" : "Direct")} mode");
    }
    
    // Public methods for external control (e.g., from LLM/Voice commands)
    
    /// <summary>
    /// Switch to Ray interaction mode (pointing from distance)
    /// </summary>
    public void SetRayMode()
    {
        SetInteractorMode(true);
    }
    
    /// <summary>
    /// Switch to Direct interaction mode (hand grabbing)
    /// </summary>
    public void SetDirectMode()
    {
        SetInteractorMode(false);
    }
    
    /// <summary>
    /// Get current interaction mode
    /// </summary>
    public bool IsRayMode()
    {
        return useRay;
    }
    
    /// <summary>
    /// Get current interaction mode as string
    /// </summary>
    public string GetCurrentMode()
    {
        return useRay ? "ray" : "direct";
    }
}
