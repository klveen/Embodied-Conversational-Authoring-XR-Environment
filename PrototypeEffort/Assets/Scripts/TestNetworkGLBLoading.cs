using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;

/// <summary>
/// Test script to verify GLB loading from Flask server in VR.
/// Spawns a test chair from the network when you point at a surface and press trigger.
/// Say "test network" to trigger the test.
/// </summary>
public class TestNetworkGLBLoading : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private string serverIP = "192.168.178.74";
    [SerializeField] private int serverPort = 5000;

    [Header("Test Settings")]
    [SerializeField] private string testCategory = "chair";
    [SerializeField] private string testFileName = "1006be65e7bc937e9141f9b58470d646.glb";
    [SerializeField] private float spawnDistance = 2f;

    [Header("VR References")]
    [SerializeField] private XRRayInteractor rightHandRay;

    private RuntimeModelLoader modelLoader;
    private VoiceObjectSpawner voiceSpawner;

    void Start()
    {
        modelLoader = FindObjectOfType<RuntimeModelLoader>();
        voiceSpawner = FindObjectOfType<VoiceObjectSpawner>();

        if (modelLoader == null)
        {
            Debug.LogError("[TestNetwork] RuntimeModelLoader not found in scene!");
            return;
        }

        // Try to find ray interactor if not assigned
        if (rightHandRay == null)
        {
            var rays = FindObjectsOfType<XRRayInteractor>();
            if (rays != null && rays.Length > 0)
            {
                foreach (var ray in rays)
                {
                    if (ray != null && ray.gameObject != null && ray.gameObject.name.Contains("Right"))
                    {
                        rightHandRay = ray;
                        Debug.Log($"[TestNetwork] Found right ray interactor: {ray.gameObject.name}");
                        break;
                    }
                }
            }
            
            if (rightHandRay == null)
            {
                Debug.LogWarning("[TestNetwork] Right hand ray interactor not found automatically. Assign it manually in Inspector.");
            }
        }

        Debug.Log("[TestNetwork] Ready. Say 'test network' to spawn a chair from Flask server.");
        Debug.Log($"[TestNetwork] Target URL: http://{serverIP}:{serverPort}/models/{testCategory}/{testFileName}");
    }

    /// <summary>
    /// Call this from VoiceObjectSpawner when "test network" is said
    /// </summary>
    public void TestLoadFromNetwork()
    {
        if (modelLoader == null)
        {
            Debug.LogError("[TestNetwork] Cannot test - RuntimeModelLoader not found!");
            return;
        }

        // Get spawn position from raycast or default
        Vector3 spawnPos;
        Quaternion spawnRot;

        if (rightHandRay != null)
        {
            try
            {
                if (rightHandRay.TryGetCurrent3DRaycastHit(out RaycastHit hit))
                {
                    spawnPos = hit.point + Vector3.up * 0.1f;
                    spawnRot = Quaternion.identity;
                    Debug.Log($"[TestNetwork] Spawning at raycast hit: {spawnPos}");
                }
                else
                {
                    // Raycast didn't hit anything, use fallback
                    spawnPos = GetFallbackPosition();
                    spawnRot = Quaternion.identity;
                    Debug.Log($"[TestNetwork] No raycast hit, using fallback position: {spawnPos}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TestNetwork] Error during raycast: {e.Message}. Using fallback position.");
                spawnPos = GetFallbackPosition();
                spawnRot = Quaternion.identity;
            }
        }
        else
        {
            // No ray interactor, use fallback
            spawnPos = GetFallbackPosition();
            spawnRot = Quaternion.identity;
            Debug.Log($"[TestNetwork] No ray interactor, using fallback position: {spawnPos}");
        }

        string url = $"http://{serverIP}:{serverPort}/models/{testCategory}/{testFileName}";
        Debug.Log($"<color=cyan>[TestNetwork] Testing network GLB loading...</color>");
        Debug.Log($"[TestNetwork] URL: {url}");

        StartCoroutine(LoadAndLogResult(url, spawnPos, spawnRot));
    }

    private Vector3 GetFallbackPosition()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            return cam.transform.position + cam.transform.forward * spawnDistance;
        }
        return new Vector3(0, 1.5f, 2);
    }

    private IEnumerator LoadAndLogResult(string url, Vector3 position, Quaternion rotation)
    {
        Debug.Log($"<color=yellow>[Network Test] Starting download from Flask server...</color>");
        
        var loadCoroutine = modelLoader.LoadModelFromURL(url, position, rotation, testCategory);
        yield return loadCoroutine;

        Debug.Log($"<color=green>[Network Test] Load completed! Check scene for spawned chair.</color>");
    }

    // Method to test different files - call from Inspector or code
    public void TestSpecificFile(string category, string fileName)
    {
        testCategory = category;
        testFileName = fileName;
        TestLoadFromNetwork();
    }
}
