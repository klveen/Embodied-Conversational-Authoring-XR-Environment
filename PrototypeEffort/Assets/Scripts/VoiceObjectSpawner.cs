using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class VoiceObjectSpawner : MonoBehaviour
{
    [Header("Dictation Reference")]
    [SerializeField] private DictationManager dictationManager;
    
    [Header("Ray Interactor References")]
    [SerializeField] private XRRayInteractor rightRayInteractor;
    [SerializeField] private XRRayInteractor leftRayInteractor;
    
    [Header("Python Server Integration")]
    // TODO: Toggle this on when ready to use Python server + LLM
    [SerializeField] private bool usePythonServer = false;
    [SerializeField] private PythonServerClient pythonClient;
    
    [Header("Object Library (Local Fallback)")]
    [SerializeField] private GameObject defaultCubePrefab;
    [SerializeField] private List<VoiceObjectMapping> objectMappings = new List<VoiceObjectMapping>();
    
    [Header("Runtime GLB Loading")]
    [Tooltip("Reference to RuntimeModelLoader for loading .glb files at runtime")]
    [SerializeField] private RuntimeModelLoader runtimeModelLoader;
    
    private void Start()
    {
        // Subscribe to final transcript event
        if (dictationManager != null)
        {
            dictationManager.OnFinalTranscript.AddListener(ProcessVoiceCommand);
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe when destroyed
        if (dictationManager != null)
        {
            dictationManager.OnFinalTranscript.RemoveListener(ProcessVoiceCommand);
        }
    }
    
    private void ProcessVoiceCommand(string transcript)
    {
        if (string.IsNullOrEmpty(transcript))
        {
            Debug.LogWarning("Empty transcript received");
            return;
        }
        
        string command = transcript.ToLower().Trim();
        Debug.Log($"Processing voice command: '{command}'");
        
        // TODO: Switch between local keyword matching and Python server based on toggle
        if (usePythonServer && pythonClient != null)
        {
            // Use Python server + LLM
            ProcessVoiceCommandWithPython(command);
        }
        else
        {
            // Use local keyword matching (current system)
            ProcessVoiceCommandLocally(command);
        }
    }
    
    // NEW: Process command using Python server and LLM
    private void ProcessVoiceCommandWithPython(string command)
    {
        Debug.Log($"Sending command to Python server: '{command}'");
        
        pythonClient.ProcessVoiceCommand(
            command,
            // Success callback - LLM returned a result
            (LLMResponse response) => 
            {
                Debug.Log($"LLM Response - Object: {response.objectName}, AssetID: {response.assetId}");
                
                // NEW: Handle interaction mode changes from LLM
                HandleInteractionModeChange(response);
                
                // TODO: Tomorrow, implement these options based on supervisor's setup:
                
                // OPTION 1: If assets are pre-imported in Unity
                // GameObject prefab = FindPrefabByAssetId(response.assetId);
                // if (prefab != null) SpawnObjectAtRaycast(prefab, response.objectName);
                
                // OPTION 2: If using runtime GLTF loading
                // pythonClient.DownloadGLTFAsset(response.assetUrl, 
                //     (byte[] gltfData) => LoadAndSpawnGLTF(gltfData, response.objectName),
                //     (error) => Debug.LogError($"Failed to download GLTF: {error}"));
                
                // TEMPORARY: For now, use local fallback
                ProcessVoiceCommandLocally(response.objectName);
            },
            // Error callback
            (string error) => 
            {
                Debug.LogError($"Python server error: {error}");
                // Fallback to local keyword matching
                ProcessVoiceCommandLocally(command);
            }
        );
    }
    
    // NEW: Handle interaction mode changes from LLM
    private void HandleInteractionModeChange(LLMResponse response)
    {
        if (string.IsNullOrEmpty(response.interactionMode))
            return; // No mode change requested
        
        string mode = response.interactionMode.ToLower().Trim();
        
        if (InteractorToggleManager.Instance == null)
        {
            Debug.LogWarning("InteractorToggleManager not found! Cannot change interaction mode.");
            return;
        }
        
        switch (mode)
        {
            case "ray":
            case "raycast":
            case "point":
            case "pointer":
                InteractorToggleManager.Instance.SetRayMode();
                Debug.Log("LLM switched to Ray mode");
                break;
                
            case "direct":
            case "grab":
            case "hand":
            case "touch":
                InteractorToggleManager.Instance.SetDirectMode();
                Debug.Log("LLM switched to Direct mode");
                break;
                
            default:
                Debug.LogWarning($"Unknown interaction mode from LLM: '{response.interactionMode}'");
                break;
        }
    }
    
    // EXISTING: Local keyword matching (renamed for clarity)
    private async void ProcessVoiceCommandLocally(string command)
    {
        List<VoiceObjectMapping> matchedMappings = FindAllMatchingMappings(command);
        
        if (matchedMappings.Count > 0)
        {
            // Pick a random mapping from matches
            VoiceObjectMapping selectedMapping = matchedMappings[Random.Range(0, matchedMappings.Count)];
            
            Debug.Log($"Found {matchedMappings.Count} matches for '{command}', selected: {selectedMapping.objectName}");
            
            // Check if this mapping uses runtime GLB loading
            if (!string.IsNullOrEmpty(selectedMapping.glbPath) && runtimeModelLoader != null)
            {
                // Load GLB at runtime
                await SpawnGLBAtRaycast(selectedMapping.glbPath, selectedMapping.objectName);
            }
            else if (selectedMapping.prefab != null)
            {
                // Use pre-imported prefab
                SpawnObjectAtRaycast(selectedMapping.prefab, command);
            }
            else
            {
                Debug.LogWarning($"Mapping for '{selectedMapping.objectName}' has no prefab or GLB path!");
                SpawnObjectAtRaycast(defaultCubePrefab, "default cube");
            }
        }
        else
        {
            Debug.LogWarning($"No matching object found for '{command}', spawning default cube");
            SpawnObjectAtRaycast(defaultCubePrefab, "default cube");
        }
    }
    
    private List<VoiceObjectMapping> FindAllMatchingMappings(string command)
    {
        List<VoiceObjectMapping> matches = new List<VoiceObjectMapping>();
        
        // Check each mapping for keyword matches
        foreach (var mapping in objectMappings)
        {
            foreach (var keyword in mapping.keywords)
            {
                if (command.Contains(keyword.ToLower()))
                {
                    matches.Add(mapping);
                    break; // Don't add the same mapping multiple times
                }
            }
        }
        
        return matches;
    }
    
    private async System.Threading.Tasks.Task SpawnGLBAtRaycast(string glbPath, string objectName)
    {
        if (runtimeModelLoader == null)
        {
            Debug.LogError("RuntimeModelLoader is not assigned!");
            return;
        }
        
        // Determine which ray interactor to use
        XRRayInteractor activeRayInteractor = GetActiveRayInteractor();
        
        if (activeRayInteractor == null)
        {
            Debug.LogWarning("No active ray interactor found! Make sure you're in ray mode.");
            return;
        }
        
        // Try to get raycast hit from the active ray interactor
        if (activeRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            Vector3 spawnPosition = hit.point;
            Quaternion spawnRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            
            Debug.Log($"Loading GLB model from: {glbPath}");
            
            // Parse category and filename from path (e.g., "bed/model.glb")
            string[] pathParts = glbPath.Split('/');
            if (pathParts.Length >= 2)
            {
                string category = pathParts[0];
                string fileName = pathParts[1];
                
                GameObject spawnedObject = await runtimeModelLoader.LoadShapeNetModel(
                    category,
                    fileName,
                    spawnPosition,
                    spawnRotation
                );
                
                if (spawnedObject != null)
                {
                    Debug.Log($"Successfully spawned GLB model '{objectName}' at {spawnPosition}");
                }
                else
                {
                    Debug.LogError($"Failed to spawn GLB model from: {glbPath}");
                }
            }
            else
            {
                Debug.LogError($"Invalid GLB path format: {glbPath}. Expected format: 'category/filename.glb'");
            }
        }
        else
        {
            Debug.LogWarning("Ray interactor did not hit any surface! Point at an AR plane.");
        }
    }
    
    private void SpawnObjectAtRaycast(GameObject prefab, string objectName)
    {
        if (prefab == null)
        {
            Debug.LogWarning("Cannot spawn null prefab");
            return;
        }
        
        // Determine which ray interactor to use
        XRRayInteractor activeRayInteractor = GetActiveRayInteractor();
        
        if (activeRayInteractor == null)
        {
            Debug.LogWarning("No active ray interactor found! Make sure you're in ray mode.");
            return;
        }
        
        // Try to get raycast hit from the active ray interactor
        if (activeRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            // Get the prefab's bounds to calculate proper height offset
            Renderer prefabRenderer = prefab.GetComponentInChildren<Renderer>();
            float heightOffset = 0f;
            
            if (prefabRenderer != null)
            {
                // Use half the height of the prefab's bounds so it sits on top of the plane
                heightOffset = prefabRenderer.bounds.extents.y;
            }
            
            // Calculate spawn position on top of the plane
            Vector3 spawnPosition = hit.point + (hit.normal * heightOffset);
            
            // Instantiate the prefab at the hit point
            GameObject spawnedObject = Instantiate(prefab, spawnPosition, Quaternion.identity);
            
            // Optional: Align rotation with surface normal
            spawnedObject.transform.up = hit.normal;
            
            Debug.Log($"Spawned '{objectName}' at {spawnPosition}");
        }
        else
        {
            Debug.LogWarning("Ray interactor did not hit any surface! Point at an AR plane.");
        }
    }
    
    private XRRayInteractor GetActiveRayInteractor()
    {
        // Prioritize right, fallback to left
        if (rightRayInteractor != null && rightRayInteractor.gameObject.activeInHierarchy)
        {
            return rightRayInteractor;
        }
        else if (leftRayInteractor != null && leftRayInteractor.gameObject.activeInHierarchy)
        {
            return leftRayInteractor;
        }
        
        return null;
    }
}

[System.Serializable]
public class VoiceObjectMapping
{
    [Tooltip("Display name for this object")]
    public string objectName;
    
    [Tooltip("Pre-imported prefab (optional - leave empty if using GLB path)")]
    public GameObject prefab;
    
    [Tooltip("Path to GLB file (optional - e.g., 'bed/model.glb' relative to shapenet_data folder)")]
    public string glbPath;
    
    [Tooltip("Keywords that trigger spawning this object")]
    public List<string> keywords = new List<string>();
}
