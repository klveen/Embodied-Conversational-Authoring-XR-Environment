using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.Networking;

/// <summary>
/// Loads .glb files at runtime and wraps them in an interactable shell with physics components.
/// Automatically generates colliders based on mesh bounds for proper XR interaction.
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

    [Header("Model Scale")]
    [Tooltip("Scale multiplier for imported models")]
    public float modelScale = 1f;

    /// <summary>
    /// Loads a .glb file from disk and wraps it in an interactable shell.
    /// NOTE: This is a placeholder. GLB runtime loading requires installing a package.
    /// For now, you should pre-import GLB files into Unity and create prefabs.
    /// </summary>
    /// <param name="glbFilePath">Full path to the .glb file</param>
    /// <param name="spawnPosition">World position to spawn at</param>
    /// <param name="spawnRotation">World rotation to spawn with</param>
    /// <returns>The spawned GameObject with model attached</returns>
    public async Task<GameObject> LoadAndSpawnModel(string glbFilePath, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        Debug.LogError("GLB Runtime loading is not yet implemented. Please install GLTFast package manually:");
        Debug.LogError("1. Download Git from: https://git-scm.com/download/win");
        Debug.LogError("2. Restart Unity after installing Git");
        Debug.LogError("3. Or manually import GLB files into Unity and use prefabs instead");
        
        await Task.Yield(); // Satisfy async requirement
        return null;
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

        // BoxCollider (will be sized later)
        if (rootObject.GetComponent<BoxCollider>() == null)
        {
            rootObject.AddComponent<BoxCollider>();
        }
    }

    /// <summary>
    /// Calculates combined bounds of all renderers in the model hierarchy.
    /// </summary>
    private Bounds CalculateCombinedBounds(GameObject modelContainer)
    {
        Renderer[] renderers = modelContainer.GetComponentsInChildren<Renderer>();
        
        if (renderers.Length == 0)
        {
            Debug.LogWarning("No renderers found in model, using default bounds");
            return new Bounds(Vector3.zero, Vector3.one * 0.1f);
        }

        // Start with first renderer's bounds
        Bounds bounds = renderers[0].bounds;

        // Expand to include all other renderers
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        // Convert world bounds to local space of the model container
        bounds.center = modelContainer.transform.InverseTransformPoint(bounds.center);
        bounds.size = Vector3.Scale(bounds.size, new Vector3(
            1f / modelContainer.transform.lossyScale.x,
            1f / modelContainer.transform.lossyScale.y,
            1f / modelContainer.transform.lossyScale.z
        ));

        return bounds;
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
    /// Creates a visual representation of the bounding box for debugging.
    /// </summary>
    private void CreateBoundingBoxVisual(GameObject rootObject, Bounds bounds)
    {
        GameObject boundingBoxVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boundingBoxVisual.name = "BoundingBoxVisual";
        boundingBoxVisual.transform.SetParent(rootObject.transform, false);
        boundingBoxVisual.transform.localPosition = bounds.center;
        boundingBoxVisual.transform.localScale = bounds.size;

        // Make it transparent
        Renderer renderer = boundingBoxVisual.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0f, 1f, 0f, 0.3f);
        mat.SetFloat("_Mode", 3); // Transparent rendering mode
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        renderer.material = mat;

        // Remove the collider from the visual
        Destroy(boundingBoxVisual.GetComponent<Collider>());
    }

    /// <summary>
    /// Convenience method to load from the shapenet_data folder.
    /// </summary>
    /// <param name="category">Category folder name (e.g., "bed", "chair")</param>
    /// <param name="fileName">GLB filename (e.g., "model.glb")</param>
    /// <param name="spawnPosition">World position to spawn at</param>
    /// <param name="spawnRotation">World rotation to spawn with</param>
    public async Task<GameObject> LoadShapeNetModel(string category, string fileName, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        // Build path to shapenet data folder
        string basePath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "shapenet_data");
        string fullPath = System.IO.Path.Combine(basePath, category, fileName);

        if (!System.IO.File.Exists(fullPath))
        {
            Debug.LogError($"GLB file not found: {fullPath}");
            return null;
        }

        return await LoadAndSpawnModel(fullPath, spawnPosition, spawnRotation);
    }
}
