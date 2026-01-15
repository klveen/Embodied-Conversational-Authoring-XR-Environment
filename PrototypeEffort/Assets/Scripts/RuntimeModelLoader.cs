using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using GLTFast;

/// <summary>
/// Loads .glb files at runtime and wraps them in an interactable shell with physics components.
/// Automatically generates colliders based on mesh bounds for proper XR interaction.
/// Works on Quest 3 by loading from StreamingAssets.
/// </summary>
public class RuntimeModelLoader : MonoBehaviour
{
    [Header("Prefab Template")]
    [Tooltip("Prefab with Rigidbody, XRGrabInteractable, and collider - model will be parented to this")]
    public GameObject interactableRootPrefab;

    [Header("Physics Settings")]
    [Tooltip("Default mass for spawned objects")]
    public float defaultMass = 1f;
    
    [Tooltip("Use gravity for spawned objects")]
    public bool useGravity = true;

    [Header("Collider Settings")]
    [Tooltip("Padding added to bounding box collider (in meters)")]
    public float colliderPadding = 0.02f;

    [Tooltip("Show debug bounding box visual")]
    public bool showBoundingBox = false;
    
    [Tooltip("Material for bounding box visualization (needs to be transparent)")]
    public Material boundingBoxMaterial;

    [Header("Model Scale")]
    [Tooltip("Scale multiplier for imported models")]
    public float modelScale = 1f;
    
    [Header("Fallback Material")]
    [Tooltip("Basic material to use if shader replacement fails")]
    public Material fallbackMaterial;
    
    [Header("Spawn Stability")]
    [Tooltip("Height offset above raycast point to prevent clipping through walls")]
    public float spawnHeightOffset = 0.15f;
    
    [Header("Network Loading (Quest)")]
    [Tooltip("Flask server URL for loading GLB files over network")]
    public string flaskServerURL = "http://192.168.178.74:5000";

    /// <summary>
    /// Loads a GLB model, automatically choosing local file (PC) or HTTP (Quest).
    /// </summary>
    /// <param name="glbPath">Full path for PC</param>
    /// <param name="modelId">Model ID for Quest HTTP loading</param>
    /// <param name="spawnPosition">World position to spawn at</param>
    /// <param name="spawnRotation">World rotation to spawn with</param>
    public async Task<GameObject> LoadModel(string glbPath, string modelId, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        // Debug: Log what we received
        Debug.Log($"[RuntimeModelLoader] LoadModel called with glbPath='{glbPath}', modelId='{modelId}' (length={modelId?.Length ?? 0})");
        
        // Check if running on Quest (Android)
        if (Application.platform == RuntimePlatform.Android)
        {
            // Quest: Load from HTTP
            if (string.IsNullOrEmpty(modelId))
            {
                Debug.LogError($"[RuntimeModelLoader] modelId is empty! Cannot construct URL. glbPath was: {glbPath}");
                return null;
            }
            string url = $"{flaskServerURL}/glb/{modelId}";
            Debug.Log($"[RuntimeModelLoader] Quest detected - loading from: {url}");
            return await LoadModelFromURL(url, spawnPosition, spawnRotation);
        }
        else
        {
            // PC: Load from local file
            Debug.Log($"[RuntimeModelLoader] PC detected - loading from: {glbPath}");
            return await LoadAndSpawnModel(glbPath, spawnPosition, spawnRotation);
        }
    }

    /// <summary>
    /// Loads a .glb file from an absolute path and wraps it in an interactable shell.
    /// Uses original GLB scale (no category-based scaling).
    /// </summary>
    /// <param name="glbFilePath">Full absolute path to the .glb file</param>
    /// <param name="spawnPosition">World position to spawn at</param>
    /// <param name="spawnRotation">World rotation to spawn with</param>
    /// <returns>The spawned GameObject with model attached</returns>
    public async Task<GameObject> LoadAndSpawnModel(string glbFilePath, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        // Create root shell from prefab
        GameObject rootObject = Instantiate(interactableRootPrefab, spawnPosition, spawnRotation);
        rootObject.name = System.IO.Path.GetFileNameWithoutExtension(glbFilePath);
        
        // HIDE the entire object until materials are ready (prevents pink flash)
        rootObject.SetActive(false);

        // Ensure root has required components
        SetupRootComponents(rootObject);

        // Use original GLB scale with optional global multiplier
        float finalScale = modelScale;
        
        // Create container for the model
        GameObject modelContainer = new GameObject("Model");
        modelContainer.transform.SetParent(rootObject.transform, false);
        modelContainer.transform.localPosition = Vector3.zero;
        modelContainer.transform.localRotation = Quaternion.identity;
        modelContainer.transform.localScale = Vector3.one * finalScale;

        try
        {
            // Normalize path for glTFast - use forward slashes and ensure it's a proper file path
            string fullPath = glbFilePath.Replace("\\", "/");
            
            // Verify file exists before attempting load
            if (!System.IO.File.Exists(glbFilePath))
            {
                Debug.LogError($"[RuntimeModelLoader] File does not exist: {glbFilePath}");
                Destroy(rootObject);
                return null;
            }
            
            Debug.Log($"[RuntimeModelLoader] Loading GLB from: {fullPath}");
            
            // Load the GLB file using GLTFast
            var gltfAsset = modelContainer.AddComponent<GltfAsset>();
            
            bool success = false;
            
            try
            {
                success = await gltfAsset.Load(fullPath);
                
                if (!success)
                {
                    Debug.LogError($"[RuntimeModelLoader] GLTFast Load returned false for: {glbFilePath}");
                    Destroy(rootObject);
                    return null;
                }
                
                Debug.Log($"[RuntimeModelLoader] GLB loaded successfully, waiting for instantiation...");
                
                // Wait for GLTFast to finish instantiating meshes
                await Task.Delay(200);
                
                // Verify renderers exist
                Renderer[] renderers = modelContainer.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    Debug.LogError($"[RuntimeModelLoader] No renderers found after GLB load!");
                    Destroy(rootObject);
                    return null;
                }
                
                Debug.Log($"[RuntimeModelLoader] Found {renderers.Length} renderers, applying simple material...");
                
                // Set correct layer for VR rendering
                SetLayerRecursive(rootObject, LayerMask.NameToLayer("Default"));
                
                // Apply fallback material if provided, otherwise skip material processing
                if (fallbackMaterial != null)
                {
                    foreach (Renderer r in renderers)
                    {
                        Material[] mats = new Material[r.materials.Length];
                        for (int i = 0; i < mats.Length; i++)
                        {
                            mats[i] = fallbackMaterial;
                        }
                        r.materials = mats;
                    }
                    Debug.Log($"[RuntimeModelLoader] Applied fallback material to {renderers.Length} renderers");
                }
                else
                {
                    Debug.LogWarning("[RuntimeModelLoader] No fallback material assigned - using GLB's original materials (may have issues)");
                }
                
                Debug.Log("[RuntimeModelLoader] Materials applied, applying rotation...");
                
                // Wait for bounds to be calculated properly
                await Task.Yield();
                await Task.Yield();
                
                // GLB files have Z-up, Unity needs Y-up: rotate -90° around X
                Debug.Log("[RuntimeModelLoader] Rotating model: Z-up to Y-up conversion");
                modelContainer.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                
                // Wait for transform update
                await Task.Yield();
                
                // Calculate bounds AFTER rotation
                Bounds combinedBounds = CalculateCombinedBounds(modelContainer);
                Debug.Log($"[RuntimeModelLoader] Bounds after rotation: center={combinedBounds.center}, size={combinedBounds.size}, min.y={combinedBounds.min.y}");
                
                // Setup collider first (before adjusting position)
                SetupCollider(rootObject, combinedBounds);
                
                // Adjust spawn position so object's bottom sits exactly on the spawn point
                // We need to move the object down by the bounds center offset, then up by the bounds extent
                // Net effect: move up by (extent - center.y) = move down by (center.y - extent)
                float boundsCenterY = combinedBounds.center.y;
                float boundsMinY = combinedBounds.min.y;
                float heightAdjustment = -boundsCenterY; // Move down to align bounds center with origin, then min.y sits on floor
                rootObject.transform.position += Vector3.up * heightAdjustment;
                Debug.Log($"[RuntimeModelLoader] Bounds center.y={boundsCenterY:F3}, min.y={boundsMinY:F3}, adjusted position by {heightAdjustment:F3}m");

                // Optional: Show bounding box visual
                if (showBoundingBox)
                {
                    CreateBoundingBoxVisual(rootObject, combinedBounds);
                }
                
                // Set kinematic BEFORE activating to prevent physics issues
                Rigidbody rb = rootObject.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    Debug.Log($"[RuntimeModelLoader] Set rigidbody to kinematic, position={rootObject.transform.position}");
                }
                
                // NOW show the object (materials are fixed, position adjusted, kinematic)
                rootObject.SetActive(true);
                Debug.Log($"[RuntimeModelLoader] Object activated at position: {rootObject.transform.position}");
                
                // DEBUG: Print transform hierarchy to understand rotation issues
                PrintTransformHierarchy(rootObject.transform, 0);
                
                // Wait longer for physics system to fully initialize colliders
                if (rb != null)
                {
                    await Task.Delay(300); // Increased delay to ensure colliders are ready
                    
                    // Verify collider is properly setup before enabling physics
                    BoxCollider collider = rootObject.GetComponent<BoxCollider>();
                    if (collider != null)
                    {
                        Debug.Log($"[RuntimeModelLoader] Collider ready - center: {collider.center}, size: {collider.size}");
                    }
                    
                    // Re-enable physics now that everything is stable
                    rb.isKinematic = false;
                    Debug.Log($"[RuntimeModelLoader] Physics enabled, final position: {rootObject.transform.position}");
                }

                Debug.Log($"[RuntimeModelLoader] Successfully spawned: {glbFilePath}");
                return rootObject;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RuntimeModelLoader] Exception during GLB load: {e.Message}");
                Debug.LogError($"Stack trace: {e.StackTrace}");
                
                // Don't destroy - keep the object even if there's an error after materials are fixed
                if (rootObject != null && modelContainer.GetComponentsInChildren<Renderer>().Length > 0)
                {
                    Debug.LogWarning("[RuntimeModelLoader] Keeping object despite error since renderers exist");
                    return rootObject;
                }
                
                Destroy(rootObject);
                return null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RuntimeModelLoader] Outer exception: {e.Message}\n{e.StackTrace}");
            
            if (rootObject != null)
                Destroy(rootObject);
                
            return null;
        }
    }

    /// <summary>
    /// Ensures the root object has all required components for XR interaction.
    /// </summary>
    private void SetupRootComponents(GameObject rootObject)
    {
        // Rigidbody
        Rigidbody rb = rootObject.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = rootObject.AddComponent<Rigidbody>();
        }
        rb.mass = defaultMass;
        rb.useGravity = useGravity;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        // START KINEMATIC - will be disabled after setup is complete
        rb.isKinematic = true;
        
        // Enhanced drag for smooth, stable movement (no flying or jittery furniture)
        rb.drag = 8f;  // Increased for smoother deceleration
        rb.angularDrag = 15f;  // Increased to prevent rotation wobble
        
        // FREEZE ROTATION: Keep objects perfectly upright (only allow horizontal spinning)
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        
        // Interpolation for smoother visual movement
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // XR Grab Interactable
        XRGrabInteractable grabInteractable = rootObject.GetComponent<XRGrabInteractable>();
        if (grabInteractable == null)
        {
            grabInteractable = rootObject.AddComponent<XRGrabInteractable>();
        }
        grabInteractable.throwOnDetach = true;
        grabInteractable.throwSmoothingDuration = 0.25f;
        grabInteractable.trackPosition = true;
        grabInteractable.trackRotation = true;
        
        // Single selection mode
        grabInteractable.selectMode = InteractableSelectMode.Single;
        
        // Velocity Tracking movement - objects maintain distance when grabbed with ray
        // Instantaneous would make them snap to hand position
        grabInteractable.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        
        // CRITICAL: Keep object at raycast distance, don't snap to controller
        grabInteractable.useDynamicAttach = true;  // Creates attach point at grab location
        grabInteractable.matchAttachPosition = true;  // Object stays at the distance you grabbed it
        grabInteractable.matchAttachRotation = true;  // Maintains rotation
        grabInteractable.snapToColliderVolume = false; // Don't snap to object center
        grabInteractable.retainTransformParent = true; // Keep object in world space
        
        // Smooth tracking with physics - optimized for best UX
        grabInteractable.smoothPosition = true;
        grabInteractable.smoothPositionAmount = 15f; // Higher = smoother but slightly delayed response
        grabInteractable.tightenPosition = 0.8f;  // Higher = tighter tracking when close to target
        
        grabInteractable.smoothRotation = true;
        grabInteractable.smoothRotationAmount = 15f;  // Higher = smoother rotation
        grabInteractable.tightenRotation = 0.8f;  // Higher = more stable rotation
        
        // No ease-in for immediate response
        grabInteractable.attachEaseInTime = 0f;
        
        // Distance calculation for ray grab
        grabInteractable.distanceCalculationMode = XRBaseInteractable.DistanceCalculationMode.ColliderPosition;
        
        // DON'T set kinematic while grabbed - VelocityTracking movement needs physics active
        // The object will move smoothly while grabbed using physics-based tracking

        // BoxCollider (will be sized later)
        if (rootObject.GetComponent<BoxCollider>() == null)
        {
            rootObject.AddComponent<BoxCollider>();
        }
        
        // Add FloorSnapper to automatically snap to floor on release (Sims-style behavior)
        FloorSnapper floorSnapper = rootObject.GetComponent<FloorSnapper>();
        if (floorSnapper == null)
        {
            floorSnapper = rootObject.AddComponent<FloorSnapper>();
        }
        
        // Add PlaneCollisionDetector for visual feedback and selective collision
        PlaneCollisionDetector collisionDetector = rootObject.GetComponent<PlaneCollisionDetector>();
        if (collisionDetector == null)
        {
            collisionDetector = rootObject.AddComponent<PlaneCollisionDetector>();
        }
        
        // Notify existing objects to ignore this new object
        NotifyExistingObjectsToIgnoreCollision(rootObject);
    }
    
    /// <summary>
    /// Tell all existing spawned objects to ignore collisions with this new object
    /// </summary>
    private void NotifyExistingObjectsToIgnoreCollision(GameObject newObject)
    {
        Collider newCollider = newObject.GetComponent<Collider>();
        if (newCollider == null) return;
        
        // Find all existing spawned objects
        PlaneCollisionDetector[] existingObjects = FindObjectsOfType<PlaneCollisionDetector>();
        
        foreach (PlaneCollisionDetector existing in existingObjects)
        {
            if (existing.gameObject == newObject) continue;
            
            Collider existingCollider = existing.GetComponent<Collider>();
            if (existingCollider != null)
            {
                Physics.IgnoreCollision(newCollider, existingCollider, true);
            }
        }
    }

    /// <summary>
    /// Calculates combined bounds of all renderers in the model hierarchy.
    /// </summary>
    private Bounds CalculateCombinedBounds(GameObject modelContainer)
    {
        MeshFilter[] meshFilters = modelContainer.GetComponentsInChildren<MeshFilter>();
        
        if (meshFilters.Length == 0)
        {
            Debug.LogWarning("[RuntimeModelLoader] No mesh filters found, using default bounds");
            return new Bounds(Vector3.zero, Vector3.one * 0.1f);
        }

        // Calculate bounds from actual mesh vertices, not renderer bounds
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;
        
        Transform rootTransform = modelContainer.transform.parent;
        
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null) continue;
            
            Mesh mesh = meshFilter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Transform meshTransform = meshFilter.transform;
            
            // Transform each vertex to root's local space
            foreach (Vector3 vertex in vertices)
            {
                Vector3 worldVertex = meshTransform.TransformPoint(vertex);
                Vector3 localVertex = rootTransform.InverseTransformPoint(worldVertex);
                
                min = Vector3.Min(min, localVertex);
                max = Vector3.Max(max, localVertex);
            }
        }
        
        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;
        
        Debug.Log($"[RuntimeModelLoader] Tight bounds: center={center}, size={size}");
        
        return new Bounds(center, size);
    }

    /// <summary>
    /// Auto-corrects model orientation to ensure proper "up" direction.
    /// Finds which axis has the most mass at one extreme (the "bottom").
    /// </summary>
    private void CorrectModelOrientation(GameObject modelContainer, Bounds bounds)
    {
        MeshFilter[] meshFilters = modelContainer.GetComponentsInChildren<MeshFilter>();
        if (meshFilters.Length == 0) return;
        
        Transform rootTransform = modelContainer.transform.parent;
        
        // Collect all vertices in local space
        List<Vector3> allVertices = new List<Vector3>();
        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;
            
            foreach (Vector3 vertex in mf.sharedMesh.vertices)
            {
                Vector3 worldVertex = mf.transform.TransformPoint(vertex);
                Vector3 localVertex = rootTransform.InverseTransformPoint(worldVertex);
                allVertices.Add(localVertex);
            }
        }
        
        if (allVertices.Count == 0) return;
        
        // Calculate center of mass
        Vector3 centerOfMass = Vector3.zero;
        foreach (Vector3 v in allVertices)
        {
            centerOfMass += v;
        }
        centerOfMass /= allVertices.Count;
        
        // For each axis, calculate how much mass is at the negative extreme
        // The axis with most mass at bottom should be the "down" direction
        float xBottomMass = 0, yBottomMass = 0, zBottomMass = 0;
        
        foreach (Vector3 v in allVertices)
        {
            // Weight by how far below center of mass (negative = below)
            if (v.x < centerOfMass.x) xBottomMass += (centerOfMass.x - v.x);
            if (v.y < centerOfMass.y) yBottomMass += (centerOfMass.y - v.y);
            if (v.z < centerOfMass.z) zBottomMass += (centerOfMass.z - v.z);
        }
        
        Debug.Log($"[RuntimeModelLoader] Bottom mass - X: {xBottomMass:F2}, Y: {yBottomMass:F2}, Z: {zBottomMass:F2}");
        
        // The axis with most bottom mass should point down (negative Y in Unity)
        float maxBottomMass = Mathf.Max(xBottomMass, yBottomMass, zBottomMass);
        
        if (Mathf.Approximately(maxBottomMass, xBottomMass))
        {
            // X has most bottom mass - rotate so -X becomes -Y
            Debug.Log("[RuntimeModelLoader] Correcting orientation: X axis has bottom, rotating 90° around Z");
            modelContainer.transform.localRotation = Quaternion.Euler(0, 0, 90);
        }
        else if (Mathf.Approximately(maxBottomMass, zBottomMass))
        {
            // Z has most bottom mass - rotate so -Z becomes -Y
            Debug.Log("[RuntimeModelLoader] Correcting orientation: Z axis has bottom, rotating -90° around X");
            modelContainer.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        }
        else
        {
            // Y already has most bottom mass - correct orientation
            Debug.Log("[RuntimeModelLoader] Orientation correct: Y axis already has bottom");
        }
    }

    /// <summary>
    /// Sets up the BoxCollider based on calculated mesh bounds.
    /// </summary>
    private void SetupCollider(GameObject rootObject, Bounds bounds)
    {
        BoxCollider boxCollider = rootObject.GetComponent<BoxCollider>();
        
        if (boxCollider != null)
        {
            // Apply bounds with padding
            boxCollider.center = bounds.center;
            boxCollider.size = bounds.size + Vector3.one * colliderPadding;
        }
    }

    /// <summary>
    /// Creates a wireframe bounding box visualization (edges only).
    /// </summary>
    private void CreateBoundingBoxVisual(GameObject rootObject, Bounds bounds)
    {
        GameObject boundingBoxVisual = new GameObject("BoundingBoxWireframe");
        boundingBoxVisual.layer = rootObject.layer;
        boundingBoxVisual.transform.SetParent(rootObject.transform, false);
        boundingBoxVisual.transform.localPosition = bounds.center;
        
        LineRenderer lineRenderer = boundingBoxVisual.AddComponent<LineRenderer>();
        
        // Use provided material or create a simple one
        if (boundingBoxMaterial != null)
        {
            lineRenderer.material = boundingBoxMaterial;
        }
        else
        {
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = Color.green;
            lineRenderer.material = mat;
        }
        
        // Line settings
        lineRenderer.startWidth = 0.005f;
        lineRenderer.endWidth = 0.005f;
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = false;
        
        // Calculate the 8 corners of the bounding box
        Vector3 min = bounds.min - bounds.center;
        Vector3 max = bounds.max - bounds.center;
        
        Vector3[] corners = new Vector3[8];
        corners[0] = new Vector3(min.x, min.y, min.z);
        corners[1] = new Vector3(max.x, min.y, min.z);
        corners[2] = new Vector3(max.x, max.y, min.z);
        corners[3] = new Vector3(min.x, max.y, min.z);
        corners[4] = new Vector3(min.x, min.y, max.z);
        corners[5] = new Vector3(max.x, min.y, max.z);
        corners[6] = new Vector3(max.x, max.y, max.z);
        corners[7] = new Vector3(min.x, max.y, max.z);
        
        // Draw 12 edges (bottom face, top face, vertical connections)
        Vector3[] linePoints = new Vector3[]
        {
            // Bottom face (4 edges)
            corners[0], corners[1],
            corners[1], corners[2],
            corners[2], corners[3],
            corners[3], corners[0],
            
            // Top face (4 edges)
            corners[4], corners[5],
            corners[5], corners[6],
            corners[6], corners[7],
            corners[7], corners[4],
            
            // Vertical edges (4 edges)
            corners[0], corners[4],
            corners[1], corners[5],
            corners[2], corners[6],
            corners[3], corners[7]
        };
        
        lineRenderer.positionCount = linePoints.Length;
        lineRenderer.SetPositions(linePoints);
        
        Debug.Log("[RuntimeModelLoader] Created wireframe bounding box");
    }
    
    /// <summary>
    /// Fixes materials for VR rendering - replaces complex shaders with mobile-friendly ones.
    /// </summary>
    private void FixMaterialsForVR(GameObject modelContainer)
    {
        Renderer[] renderers = modelContainer.GetComponentsInChildren<Renderer>();
        
        // Option 1: Use fallback material if provided
        if (fallbackMaterial != null)
        {
            Debug.Log($"[RuntimeModelLoader] Using fallback material: {fallbackMaterial.name}");
            
            int fixedCount = 0;
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = new Material[renderer.materials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = fallbackMaterial;
                    fixedCount++;
                }
                renderer.materials = materials;
            }
            
            Debug.Log($"[RuntimeModelLoader] Applied fallback material to {fixedCount} materials");
            return;
        }
        
        // Option 2: Try to find the best shader for the current render pipeline
        Shader replacementShader = Shader.Find("Universal Render Pipeline/Lit");
        if (replacementShader == null)
        {
            replacementShader = Shader.Find("Mobile/Diffuse");
            Debug.Log("[RuntimeModelLoader] Using Mobile/Diffuse shader");
        }
        else
        {
            Debug.Log("[RuntimeModelLoader] Using URP/Lit shader");
        }
        
        if (replacementShader == null)
        {
            Debug.LogError("[RuntimeModelLoader] No suitable replacement shader found! Assign fallback material in inspector.");
            return;
        }
        
        int materialCount = 0;
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;
            
            for (int i = 0; i < materials.Length; i++)
            {
                Material mat = materials[i];
                
                if (mat == null) continue;
                
                string shaderName = mat.shader.name;
                
                Debug.Log($"[RuntimeModelLoader] Material '{mat.name}' using shader: {shaderName}");
                
                // Create a new material with the replacement shader
                Material newMat = new Material(replacementShader);
                newMat.name = mat.name;
                
                // Try to preserve color
                if (mat.HasProperty("_BaseColor"))
                    newMat.color = mat.GetColor("_BaseColor");
                else if (mat.HasProperty("_Color"))
                    newMat.color = mat.color;
                else
                    newMat.color = Color.white;
                
                // Try to preserve main texture
                if (mat.HasProperty("_BaseColorTexture") && mat.GetTexture("_BaseColorTexture") != null)
                {
                    newMat.mainTexture = mat.GetTexture("_BaseColorTexture");
                }
                else if (mat.HasProperty("_MainTex") && mat.mainTexture != null)
                {
                    newMat.mainTexture = mat.mainTexture;
                }
                
                materials[i] = newMat;
                materialCount++;
                
                Debug.Log($"[RuntimeModelLoader] Replaced with new material, color: {newMat.color}");
            }
            
            // Apply modified materials back to renderer
            renderer.materials = materials;
        }
        
        Debug.Log($"[RuntimeModelLoader] Fixed {materialCount} materials");
    }
    
    /// <summary>
    /// Sets layer recursively for proper VR rendering.
    /// </summary>
    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    /// <summary>
    /// Loads a GLB model from full path (no category needed).
    /// </summary>
    /// <param name="glbPath">Full absolute path to GLB file</param>
    /// <param name="spawnPosition">World position to spawn at</param>
    /// <param name="spawnRotation">World rotation to spawn with</param>
    public async Task<GameObject> LoadModelFromPath(string glbPath, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        Debug.Log($"[RuntimeModelLoader] LoadModelFromPath called: {glbPath}");
        
        // Use the main LoadAndSpawnModel method with full path
        return await LoadAndSpawnModel(glbPath, spawnPosition, spawnRotation);
    }
    
    /// <summary>
    /// DEPRECATED: Use LoadModelFromPath instead.
    /// Legacy method for backward compatibility.
    /// </summary>
    [System.Obsolete("Use LoadModelFromPath with full path instead")]
    public async Task<GameObject> LoadShapeNetModel(string category, string fileName, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        Debug.LogWarning("[RuntimeModelLoader] LoadShapeNetModel is deprecated. Use LoadModelFromPath with full path instead.");
        await Task.CompletedTask; // Avoid async warning
        // Fallback behavior - won't work without proper path
        return null;
    }
    
    /// <summary>
    /// Loads a GLB model from a URL (for remote/cloud hosted models).
    /// Uses original GLB scale (no category-based scaling).
    /// </summary>
    /// <param name="url">Full URL to the .glb file (http:// or https://)</param>
    /// <param name="spawnPosition">World position to spawn at</param>
    /// <param name="spawnRotation">World rotation to spawn with</param>
    /// <returns>The spawned GameObject with model attached</returns>
    public async Task<GameObject> LoadModelFromURL(string url, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        // Create root shell from prefab
        GameObject rootObject = Instantiate(interactableRootPrefab, spawnPosition, spawnRotation);
        rootObject.name = System.IO.Path.GetFileNameWithoutExtension(url);
        
        // HIDE the entire object until materials are ready (prevents pink flash)
        rootObject.SetActive(false);

        // Ensure root has required components
        SetupRootComponents(rootObject);

        // Use original GLB scale with optional global multiplier
        float finalScale = modelScale;
        Debug.Log($"[RuntimeModelLoader] Loading from URL: {url}, Scale: {finalScale}x");
        
        // Create container for the model
        GameObject modelContainer = new GameObject("Model");
        modelContainer.transform.SetParent(rootObject.transform, false);
        modelContainer.transform.localPosition = Vector3.zero;
        modelContainer.transform.localRotation = Quaternion.identity;
        modelContainer.transform.localScale = Vector3.one * finalScale;

        try
        {
            Debug.Log($"[RuntimeModelLoader] Starting GLB download from: {url}");
            
            // Load the GLB file from URL using GLTFast
            var gltfAsset = modelContainer.AddComponent<GltfAsset>();
            
            Debug.Log("[RuntimeModelLoader] GltfAsset component added, calling Load()...");
            
            bool success = await gltfAsset.Load(url);
            
            Debug.Log($"[RuntimeModelLoader] Load() returned: {success}");
            
            if (!success)
            {
                Debug.LogError($"[RuntimeModelLoader] Failed to load GLB from URL: {url}");
                Debug.LogError($"[RuntimeModelLoader] Check Flask server is running and URL is accessible");
                Debug.LogError($"[RuntimeModelLoader] Try opening in browser: {url}");
                Destroy(rootObject);
                return null;
            }
            
            Debug.Log("[RuntimeModelLoader] GLB loaded successfully from URL, waiting for instantiation...");
            
            // Wait a frame for instantiation
            await Task.Yield();
            
            // Get all renderers from the loaded model
            Renderer[] renderers = modelContainer.GetComponentsInChildren<Renderer>();
            
            if (renderers.Length == 0)
            {
                Debug.LogError("[RuntimeModelLoader] No renderers found in loaded URL model");
                Destroy(rootObject);
                return null;
            }
            
            Debug.Log($"[RuntimeModelLoader] Found {renderers.Length} renderers, applying simple material...");
            
            // Set correct layer for VR rendering
            SetLayerRecursive(rootObject, LayerMask.NameToLayer("Default"));
            
            // Apply fallback material if provided
            if (fallbackMaterial != null)
            {
                foreach (Renderer r in renderers)
                {
                    Material[] mats = new Material[r.materials.Length];
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = fallbackMaterial;
                    }
                    r.materials = mats;
                }
                Debug.Log($"[RuntimeModelLoader] Applied fallback material to {renderers.Length} renderers");
            }
            
            Debug.Log("[RuntimeModelLoader] Materials applied, applying rotation and calculating bounds...");
            
            // Wait for bounds to be calculated properly
            await Task.Yield();
            await Task.Yield();
            
            // GLB files have Z-up, Unity needs Y-up: rotate -90° around X
            Debug.Log("[RuntimeModelLoader] Rotating model for URL load: Z-up to Y-up conversion");
            modelContainer.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            
            // Wait for transform update
            await Task.Yield();
            
            // Calculate bounds AFTER rotation and setup collider
            Bounds combinedBounds = CalculateCombinedBounds(modelContainer);
            Debug.Log($"[RuntimeModelLoader] Bounds after rotation: center={combinedBounds.center}, size={combinedBounds.size}, min.y={combinedBounds.min.y}");
            
            SetupCollider(rootObject, combinedBounds);
            
            // Adjust spawn position so object's bottom sits exactly on the spawn point
            // We need to move the object down by the bounds center offset
            float boundsCenterY = combinedBounds.center.y;
            float boundsMinY = combinedBounds.min.y;
            float heightAdjustment = -boundsCenterY; // Move down to align bounds center with origin, then min.y sits on floor
            rootObject.transform.position += Vector3.up * heightAdjustment;
            Debug.Log($"[RuntimeModelLoader] Bounds center.y={boundsCenterY:F3}, min.y={boundsMinY:F3}, adjusted position by {heightAdjustment:F3}m");

            // Optional: Show bounding box visual
            if (showBoundingBox)
            {
                CreateBoundingBoxVisual(rootObject, combinedBounds);
            }
            
            // NOW show the object (materials are fixed, no pink flash!)
            rootObject.SetActive(true);

            Debug.Log($"[RuntimeModelLoader] Successfully loaded model from URL: {url}");
            return rootObject;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RuntimeModelLoader] Exception loading from URL: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
            
            if (rootObject != null)
                Destroy(rootObject);
                
            return null;
        }
    }
    
    /// <summary>
    /// Debug helper to print transform hierarchy
    /// </summary>
    private void PrintTransformHierarchy(Transform t, int depth)
    {
        string indent = new string(' ', depth * 2);
        Debug.Log($"{indent}[{t.name}] Pos: {t.localPosition:F2}, Rot: {t.localRotation.eulerAngles:F1}, Scale: {t.localScale:F2}");
        
        foreach (Transform child in t)
        {
            PrintTransformHierarchy(child, depth + 1);
        }
    }
}
