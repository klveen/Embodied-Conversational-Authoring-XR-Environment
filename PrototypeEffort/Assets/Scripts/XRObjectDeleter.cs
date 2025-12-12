using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

/// <summary>
/// Allows players to delete grabbed objects by pressing a button.
/// Works with both XR Direct Interactor and XR Ray Interactor.
/// Attach this to each controller (left and right hand).
/// </summary>
public class XRObjectDeleter : MonoBehaviour
{
    [Header("Delete Action")]
    [Tooltip("Input action to trigger object deletion (e.g., Grip, Trigger, Primary/Secondary Button)")]
    [SerializeField] private InputActionReference deleteAction;
    
    [Header("Delete Settings")]
    [Tooltip("Require holding the button for this duration to delete (prevents accidents)")]
    [SerializeField] private float holdDuration = 0.5f;
    
    [Tooltip("Show debug messages when deleting objects")]
    [SerializeField] private bool debugMode = true;
    
    private XRDirectInteractor directInteractor;
    private XRRayInteractor rayInteractor;
    private float holdTimer = 0f;
    private bool isHolding = false;
    private GameObject currentGrabbedObject = null;
    
    private void Awake()
    {
        directInteractor = GetComponent<XRDirectInteractor>();
        rayInteractor = GetComponent<XRRayInteractor>();
        
        if (directInteractor == null && rayInteractor == null)
        {
            Debug.LogError("[XRObjectDeleter] No XRDirectInteractor or XRRayInteractor found on this GameObject!");
        }
    }
    
    private void OnEnable()
    {
        if (deleteAction != null && deleteAction.action != null)
        {
            deleteAction.action.Enable();
        }
        
        if (directInteractor != null)
        {
            directInteractor.selectEntered.AddListener(OnSelectEntered);
            directInteractor.selectExited.AddListener(OnSelectExited);
        }
        
        if (rayInteractor != null)
        {
            rayInteractor.selectEntered.AddListener(OnSelectEntered);
            rayInteractor.selectExited.AddListener(OnSelectExited);
        }
    }
    
    private void OnDisable()
    {
        if (deleteAction != null && deleteAction.action != null)
        {
            deleteAction.action.Disable();
        }
        
        if (directInteractor != null)
        {
            directInteractor.selectEntered.RemoveListener(OnSelectEntered);
            directInteractor.selectExited.RemoveListener(OnSelectExited);
        }
        
        if (rayInteractor != null)
        {
            rayInteractor.selectEntered.RemoveListener(OnSelectEntered);
            rayInteractor.selectExited.RemoveListener(OnSelectExited);
        }
    }
    
    private void Update()
    {
        // Only process deletion if we're holding an object
        if (currentGrabbedObject == null || deleteAction == null || deleteAction.action == null)
        {
            ResetHoldTimer();
            return;
        }
        
        // Check if delete button is pressed
        bool buttonPressed = deleteAction.action.ReadValue<float>() > 0.5f;
        
        if (buttonPressed)
        {
            if (!isHolding)
            {
                isHolding = true;
                holdTimer = 0f;
                if (debugMode)
                {
                    Debug.Log($"[XRObjectDeleter] Started holding delete button for '{currentGrabbedObject.name}'");
                }
            }
            
            holdTimer += Time.deltaTime;
            
            // Delete object after holding for required duration
            if (holdTimer >= holdDuration)
            {
                DeleteCurrentObject();
            }
        }
        else
        {
            ResetHoldTimer();
        }
    }
    
    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        currentGrabbedObject = args.interactableObject.transform.gameObject;
        
        if (debugMode)
        {
            Debug.Log($"[XRObjectDeleter] Grabbed object: {currentGrabbedObject.name}");
        }
    }
    
    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (debugMode && currentGrabbedObject != null)
        {
            Debug.Log($"[XRObjectDeleter] Released object: {currentGrabbedObject.name}");
        }
        
        currentGrabbedObject = null;
        ResetHoldTimer();
    }
    
    private void DeleteCurrentObject()
    {
        if (currentGrabbedObject == null) return;
        
        string objectName = currentGrabbedObject.name;
        
        if (debugMode)
        {
            Debug.Log($"[XRObjectDeleter] Deleting object: {objectName}");
        }
        
        // Force release the object first (works for both direct and ray interactor)
        if (directInteractor != null && directInteractor.hasSelection)
        {
            directInteractor.interactionManager.SelectExit(directInteractor, directInteractor.interactablesSelected[0]);
        }
        else if (rayInteractor != null && rayInteractor.hasSelection)
        {
            rayInteractor.interactionManager.SelectExit(rayInteractor, rayInteractor.interactablesSelected[0]);
        }
        
        Destroy(currentGrabbedObject);
        currentGrabbedObject = null;
        ResetHoldTimer();
    }
    
    private void ResetHoldTimer()
    {
        holdTimer = 0f;
        isHolding = false;
    }
}
