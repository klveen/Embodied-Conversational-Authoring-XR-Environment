using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

/// <summary>
/// Detects collisions with AR planes and other objects.
/// Shows red transparent material when colliding with non-floor/wall planes or other objects.
/// Only blocks movement through floor and wall planes.
/// </summary>
public class PlaneCollisionDetector : MonoBehaviour
{
    [Header("Visual Feedback")]
    [SerializeField] private Material invalidMaterial; // Red semi-transparent material for invalid placement
    
    private Dictionary<Renderer, Material[]> originalMaterialsPerRenderer = new Dictionary<Renderer, Material[]>();
    private Dictionary<Renderer, Material[]> instancedMaterialsPerRenderer = new Dictionary<Renderer, Material[]>();
    private HashSet<Collider> collidingWith = new HashSet<Collider>();
    private Renderer[] renderers;
    private bool isShowingInvalid = false;
    
    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        StoreOriginalMaterials();
    }
    
    void Start()
    {
        // Set up physics layers
        // Objects can pass through table/ceiling/other planes, but not floor/wall
        SetupCollisionLayers();
    }
    
    void StoreOriginalMaterials()
    {
        foreach (Renderer r in renderers)
        {
            if (r != null && r.sharedMaterials != null)
            {
                // Store CURRENT materials (which may already have color applied)
                Material[] currentMats = r.materials; // This gets the current instanced materials
                originalMaterialsPerRenderer[r] = currentMats;
            }
        }
    }
    
    /// <summary>
    /// Call this after applying colors to update the stored "original" materials
    /// so collision detection doesn't override the user's color
    /// </summary>
    public void UpdateStoredMaterials()
    {
        foreach (Renderer r in renderers)
        {
            if (r != null)
            {
                // Update stored materials to current state (preserves user colors)
                originalMaterialsPerRenderer[r] = r.materials;
            }
        }
    }
    
    void SetupCollisionLayers()
    {
        // Ensure object is on Default layer (0)
        gameObject.layer = 0;
        
        // Add a trigger collider for detecting overlaps with other objects
        AddTriggerCollider();
        
        // Disable physics collisions with other spawned objects
        // but keep trigger detection for visual feedback
        DisableCollisionsWithOtherSpawnedObjects();
    }
    
    void AddTriggerCollider()
    {
        // Add a duplicate BoxCollider as trigger for detecting overlaps
        BoxCollider mainCollider = GetComponent<BoxCollider>();
        if (mainCollider != null)
        {
            BoxCollider triggerCollider = gameObject.AddComponent<BoxCollider>();
            triggerCollider.center = mainCollider.center;
            triggerCollider.size = mainCollider.size;
            triggerCollider.isTrigger = true;
        }
    }
    
    void DisableCollisionsWithOtherSpawnedObjects()
    {
        Collider myCollider = GetComponent<Collider>();
        if (myCollider == null) return;
        
        // Find all other spawned objects with PlaneCollisionDetector
        PlaneCollisionDetector[] allSpawnedObjects = FindObjectsOfType<PlaneCollisionDetector>();
        
        foreach (PlaneCollisionDetector other in allSpawnedObjects)
        {
            if (other == this) continue;
            
            Collider otherCollider = other.GetComponent<Collider>();
            if (otherCollider != null)
            {
                // Ignore physics collisions but triggers still work
                Physics.IgnoreCollision(myCollider, otherCollider, true);
            }
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        // Only process floor/wall collisions (physical)
        ARPlane plane = collision.collider.GetComponent<ARPlane>();
        if (plane != null && (plane.classification == PlaneClassification.Floor || 
                              plane.classification == PlaneClassification.Wall))
        {
            collidingWith.Add(collision.collider);
            CheckCollisionState(collision);
        }
    }
    
    void OnCollisionStay(Collision collision)
    {
        ARPlane plane = collision.collider.GetComponent<ARPlane>();
        if (plane != null && (plane.classification == PlaneClassification.Floor || 
                              plane.classification == PlaneClassification.Wall))
        {
            CheckCollisionState(collision);
        }
    }
    
    void OnCollisionExit(Collision collision)
    {
        collidingWith.Remove(collision.collider);
        UpdateVisualState();
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (!collidingWith.Contains(other))
        {
            collidingWith.Add(other);
            CheckTriggerState(other);
            
            // Ensure trigger colliders don't interfere with physics
            Collider myCollider = GetComponent<Collider>();
            if (myCollider != null && other.isTrigger)
            {
                Physics.IgnoreCollision(myCollider, other, false); // Don't ignore, but ensure no physics response
            }
        }
    }
    
    void OnTriggerStay(Collider other)
    {
        // Only check if we're not already showing invalid
        if (!isShowingInvalid)
        {
            CheckTriggerState(other);
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        collidingWith.Remove(other);
        UpdateVisualState();
    }
    
    void UpdateVisualState()
    {
        // If no more collisions/triggers, restore original appearance
        if (collidingWith.Count == 0 && isShowingInvalid)
        {
            RestoreOriginalMaterials();
            isShowingInvalid = false;
        }
    }
    
    void CheckCollisionState(Collision collision)
    {
        CheckCollider(collision.collider);
    }
    
    void CheckTriggerState(Collider other)
    {
        CheckCollider(other);
    }
    
    void CheckCollider(Collider col)
    {
        // Check if colliding with an AR plane
        ARPlane plane = col.GetComponent<ARPlane>();
        
        if (plane != null)
        {
            // Get plane classification
            PlaneClassification classification = plane.classification;
            
            // Only show red for non-floor/wall planes (table, ceiling, etc.)
            if (classification != PlaneClassification.Floor && 
                classification != PlaneClassification.Wall)
            {
                SetInvalidAppearance();
            }
            // Floor and wall are allowed, don't change appearance
        }
        else
        {
            // Colliding with another spawned object
            // Check if it has this component (means it's a spawned object)
            if (col.GetComponent<PlaneCollisionDetector>() != null)
            {
                SetInvalidAppearance();
            }
        }
    }
    
    void SetInvalidAppearance()
    {
        if (isShowingInvalid) return; // Already showing invalid
        
        isShowingInvalid = true;
        
        if (invalidMaterial == null)
        {
            Debug.LogWarning("[PlaneCollisionDetector] Invalid material not assigned in Inspector! Assign a red semi-transparent material.");
            return;
        }
        
        // Replace all materials with the invalid material
        foreach (Renderer r in renderers)
        {
            if (r != null)
            {
                Material[] mats = new Material[r.materials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = invalidMaterial;
                }
                r.materials = mats;
            }
        }
    }
    
    void RestoreOriginalMaterials()
    {
        foreach (Renderer r in renderers)
        {
            if (r != null && originalMaterialsPerRenderer.ContainsKey(r))
            {
                // Restore the materials (which preserve user-applied colors)
                r.materials = originalMaterialsPerRenderer[r];
            }
        }
    }
    
    void OnDestroy()
    {
        // Clean up instanced materials
        foreach (var matArray in instancedMaterialsPerRenderer.Values)
        {
            foreach (var mat in matArray)
            {
                if (mat != null)
                {
                    Destroy(mat);
                }
            }
        }
    }
}
