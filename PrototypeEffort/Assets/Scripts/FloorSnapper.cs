using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

/// <summary>
/// Snaps objects to the nearest floor plane when released.
/// Only snaps to planes classified as Floor (not walls, tables, etc.)
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class FloorSnapper : MonoBehaviour
{
    [Header("Snap Settings")]
    [SerializeField] private float maxSnapDistance = 5f;
    [SerializeField] private float hoverHeight = 0.01f;
    [SerializeField] private bool smoothSnap = true;
    [SerializeField] private float snapSpeed = 5f;
    [SerializeField] private bool keepUpright = true;
    
    private XRGrabInteractable grabInteractable;
    private ARPlaneManager planeManager;
    private bool isSnapping = false;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    
    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        planeManager = FindObjectOfType<ARPlaneManager>();
        
        if (planeManager == null)
        {
            Debug.LogWarning("[FloorSnapper] ARPlaneManager not found in scene!");
        }
    }
    
    void Start()
    {
        // Auto-snap to floor after spawn (delayed to allow physics setup)
        StartCoroutine(InitialSnapToFloor());
    }
    
    void OnEnable()
    {
        grabInteractable.selectExited.AddListener(OnReleased);
    }
    
    void OnDisable()
    {
        grabInteractable.selectExited.RemoveListener(OnReleased);
    }
    
    private void OnReleased(SelectExitEventArgs args)
    {
        SnapToNearestFloor();
    }
    
    void Update()
    {
        if (isSnapping && smoothSnap)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            
            // Make kinematic during snap to prevent physics interference
            if (rb != null && !rb.isKinematic)
            {
                rb.isKinematic = true;
            }
            
            // Smoothly move to target position
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, Time.deltaTime * snapSpeed);
            
            // Keep upright if enabled
            if (keepUpright)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * snapSpeed * 100f);
            }
            
            // Stop snapping when reached target
            if (Vector3.Distance(transform.position, targetPosition) < 0.001f)
            {
                transform.position = targetPosition;
                if (keepUpright)
                {
                    transform.rotation = targetRotation;
                }
                isSnapping = false;
                
                // Re-enable physics and clear velocities
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }
    }
    
    /// <summary>
    /// Find the nearest floor plane and snap object to it
    /// </summary>
    public void SnapToNearestFloor()
    {
        if (planeManager == null)
        {
            Debug.LogWarning("[FloorSnapper] No ARPlaneManager, cannot snap to floor");
            return;
        }
        
        ARPlane nearestFloor = FindNearestFloorPlane();
        
        if (nearestFloor != null)
        {
            // Get the bottom of the object (assuming collider bounds)
            Collider col = GetComponent<Collider>();
            float objectBottomOffset = col != null ? col.bounds.extents.y : 0f;
            
            // Keep X and Z position, only adjust Y to floor height
            targetPosition = transform.position;
            targetPosition.y = nearestFloor.center.y + hoverHeight + objectBottomOffset;
            
            // Keep object upright (no tilt)
            if (keepUpright)
            {
                targetRotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
            }
            
            if (smoothSnap)
            {
                // Start smooth snapping
                isSnapping = true;
                Debug.Log($"[FloorSnapper] Smoothly snapping {gameObject.name} to floor at Y={targetPosition.y}");
            }
            else
            {
                // Instant snap
                transform.position = targetPosition;
                if (keepUpright)
                {
                    transform.rotation = targetRotation;
                }
                Debug.Log($"[FloorSnapper] Snapped {gameObject.name} to floor at Y={targetPosition.y}");
            }
        }
        else
        {
            Debug.LogWarning($"[FloorSnapper] No floor plane found within {maxSnapDistance}m of {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Find the nearest ARPlane that is classified as Floor
    /// </summary>
    private ARPlane FindNearestFloorPlane()
    {
        ARPlane nearestFloor = null;
        float nearestDistance = maxSnapDistance;
        
        Vector3 objectPosition = transform.position;
        
        // Check all tracked planes
        foreach (var plane in planeManager.trackables)
        {
            // Only consider planes classified as Floor
            if (plane.classification != PlaneClassification.Floor)
                continue;
            
            // Calculate distance to this floor plane
            float distance = Vector3.Distance(objectPosition, plane.center);
            
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestFloor = plane;
            }
        }
        
        if (nearestFloor != null)
        {
            Debug.Log($"[FloorSnapper] Found floor plane at distance {nearestDistance:F2}m, classification: {nearestFloor.classification}");
        }
        
        return nearestFloor;
    }
    
    /// <summary>
    /// Get all detected floor planes (for debugging)
    /// </summary>
    public List<ARPlane> GetAllFloorPlanes()
    {
        List<ARPlane> floors = new List<ARPlane>();
        
        if (planeManager == null) return floors;
        
        foreach (var plane in planeManager.trackables)
        {
            if (plane.classification == PlaneClassification.Floor)
            {
                floors.Add(plane);
            }
        }
        
        Debug.Log($"[FloorSnapper] Found {floors.Count} floor planes in scene");
        return floors;
    }
    
    /// <summary>
    /// Initial snap to floor after object is spawned
    /// Waits for physics to settle and rigidbody to become non-kinematic
    /// </summary>
    private System.Collections.IEnumerator InitialSnapToFloor()
    {
        // Wait for object to finish spawn setup (kinematic delay)
        yield return new WaitForSeconds(0.6f);
        
        // Check if object was grabbed during spawn delay
        if (grabInteractable != null && grabInteractable.isSelected)
        {
            Debug.Log("[FloorSnapper] Object grabbed during spawn, skipping initial snap");
            yield break;
        }
        
        // Snap to nearest floor
        Debug.Log("[FloorSnapper] Performing initial floor snap after spawn");
        SnapToNearestFloor();
    }
}
