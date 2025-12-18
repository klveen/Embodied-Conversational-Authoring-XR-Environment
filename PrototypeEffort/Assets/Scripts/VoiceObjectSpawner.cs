using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class VoiceObjectSpawner : MonoBehaviour
{
    [Header("Dictation Reference")]
    [SerializeField] private DictationManager dictationManager;
    
    [Header("UI Display")]
    [SerializeField] private TMPro.TextMeshProUGUI userCommandText;  // Shows what user said
    [SerializeField] private TMPro.TextMeshProUGUI llmResponseText;  // Shows LLM's response
    
    [Header("Ray Interactor References")]
    [SerializeField] private XRRayInteractor rightRayInteractor;
    [SerializeField] private XRRayInteractor leftRayInteractor;
    
    [Header("Object Highlighting")]
    [SerializeField] private Material highlightMaterial; // Drag translucent blue material here
    [SerializeField] private float boundingBoxPadding = 0.05f; // Extra space around object
    
    private GameObject currentlyHighlightedObject;
    private GameObject highlightBoundingBox; // The visual bounding box
    
    // Cached spawn point (captured when dictation ends, before LLM processes)
    private Vector3? cachedSpawnPosition;
    private Quaternion? cachedSpawnRotation;
    private bool hasCachedSpawnPoint = false;
    
    [Header("Python Server Integration")]
    [SerializeField] private bool usePythonServer = false;
    [SerializeField] private PythonServerClient pythonClient;
    
    [Header("Object Library (Local Fallback)")]
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
        
        // Set initial welcome message
        if (llmResponseText != null)
        {
            llmResponseText.text = "I will be your personal design companion, please hold A in order to talk to me. Try saying something like: \"place a pink chair\", and I can help you!";
        }
    }
    
    private void Update()
    {
        // Continuously check what the raycast is pointing at and highlight it
        UpdateRaycastHighlight();
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
        
        // Update UI to show what user said
        UpdateUserCommandText(transcript);
        
        // Smart dual mode: simple commands run locally, complex commands use LLM
        // Local: delete, remove, test commands (fast response)
        // LLM: spawn with colors/customization ("blue couch", "red chair here")
        
        bool isSimpleCommand = command.Contains("delete") || 
                               command.Contains("remove") ||
                               (command.Contains("test") && (command.Contains("llm") || command.Contains("network") || command.Contains("ai")));
        
        if (isSimpleCommand || !usePythonServer || pythonClient == null)
        {
            // Run locally for fast response
            ProcessVoiceCommandLocally(command);
        }
        else
        {
            // CAPTURE SPAWN POINT NOW (before LLM processing)
            // This way user doesn't have to hold ray in same position while waiting
            CaptureCurrentSpawnPoint();
            
            // Show processing message
            UpdateLLMResponseText("Give me a second while I process your input...");
            
            // Use LLM for complex spawn commands with customization
            ProcessVoiceCommandWithPython(command);
        }
    }
    
    // NEW: Process command using Python server and LLM
    private void ProcessVoiceCommandWithPython(string command)
    {
        Debug.Log($"Sending command to Python server: '{command}'");
        
        pythonClient.ProcessVoiceCommand(
            command,
            // Success callback - LLM returned a result
            async (LLMResponse response) => 
            {
                Debug.Log($"LLM Response - Action: {response.action}, Object: {response.objectName}, Color: {response.color}");
                
                // Handle interaction mode changes from LLM
                HandleInteractionModeChange(response);
                
                // Handle different actions from LLM
                if (response.action == "spawn" && !string.IsNullOrEmpty(response.objectName))
                {
                    // Show spawn confirmation
                    string colorText = !string.IsNullOrEmpty(response.color) ? $"{response.color} " : "";
                    UpdateLLMResponseText($"Spawning {colorText}{response.objectName}...");
                    
                    // Parse color if specified
                    Color? spawnColor = null;
                    if (!string.IsNullOrEmpty(response.color))
                    {
                        spawnColor = ParseColor(response.color);
                    }
                    
                    // Find matching object and spawn with color
                    List<VoiceObjectMapping> matches = FindAllMatchingMappings(response.objectName);
                    if (matches.Count > 0)
                    {
                        VoiceObjectMapping selected = matches[Random.Range(0, matches.Count)];
                        
                        if (!string.IsNullOrEmpty(selected.glbPath) && runtimeModelLoader != null)
                        {
                            await SpawnGLBAtRaycastWithColor(selected.glbPath, selected.objectName, spawnColor);
                        }
                        else if (selected.prefab != null)
                        {
                            SpawnObjectAtRaycastWithColor(selected.prefab, selected.objectName, spawnColor);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"No matching object found for '{response.objectName}'");
                        UpdateLLMResponseText($"I don't have a {response.objectName} available.");
                    }
                }
                else if (response.action == "delete")
                {
                    UpdateLLMResponseText("Deleting object...");
                    DeleteObjectAtRaycast();
                }
                else if (response.action == "modify")
                {
                    // Modify action: user is pointing at object to modify
                    if (!string.IsNullOrEmpty(response.color))
                    {
                        UpdateLLMResponseText($"Changing color to {response.color}...");
                        ModifyObjectColorAtRaycast(response.color);
                    }
                    else
                    {
                        UpdateLLMResponseText("Spawning variation...");
                        ReplaceObjectWithVariation();
                    }
                }
                else if (response.action == "query" && !string.IsNullOrEmpty(response.response))
                {
                    // LLM is asking for clarification or responding conversationally
                    UpdateLLMResponseText(response.response);
                    Debug.Log($"LLM Query Response: {response.response}");
                }
                else
                {
                    // Unknown action or no response
                    UpdateLLMResponseText("I'm not sure what you mean. Try saying 'spawn a red chair', 'delete', or 'change color to blue'.");
                    Debug.Log($"LLM Response (unknown action): {response.response}");
                }
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
    
    private Color? ParseColor(string colorName)
    {
        switch (colorName.ToLower())
        {
            case "red": return Color.red;
            case "blue": return Color.blue;
            case "green": return Color.green;
            case "yellow": return Color.yellow;
            case "white": return Color.white;
            case "black": return Color.black;
            case "orange": return new Color(1f, 0.5f, 0f);
            case "purple": return new Color(0.5f, 0f, 0.5f);
            case "pink": return new Color(1f, 0.75f, 0.8f);
            case "brown": return new Color(0.6f, 0.3f, 0f);
            case "gray": case "grey": return Color.gray;
            default: 
                Debug.LogWarning($"Unknown color: {colorName}");
                return null;
        }
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
        // Check for LLM test command
        if ((command.Contains("start") && command.Contains("testing")) || 
            (command.Contains("test") && command.Contains("ai")) ||
            command.Contains("test llm"))
        {
            Debug.Log("[VoiceSpawner] Testing LLM connection...");
            TestLLMConnection();
            return;
        }
        
        // Check for delete command
        if (command.Contains("delete") || command.Contains("remove"))
        {
            DeleteObjectAtRaycast();
            return;
        }
        
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
            }
        }
        else
        {
            Debug.LogWarning($"No matching object found for '{command}'");
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
            // Spawn slightly above the hit point to prevent clipping through walls
            Vector3 spawnPosition = hit.point + hit.normal * runtimeModelLoader.spawnHeightOffset;
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
    
    // New method with color support
    private void SpawnObjectAtRaycastWithColor(GameObject prefab, string objectName, Color? color)
    {
        if (prefab == null)
        {
            Debug.LogWarning("Cannot spawn null prefab");
            ClearCachedSpawnPoint();
            return;
        }
        
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        
        // Use cached spawn point if available (captured when dictation ended)
        if (hasCachedSpawnPoint && cachedSpawnPosition.HasValue && cachedSpawnRotation.HasValue)
        {
            spawnPosition = cachedSpawnPosition.Value;
            spawnRotation = cachedSpawnRotation.Value;
            Debug.Log("Using cached spawn point from when you spoke");
        }
        else
        {
            // Fallback: check current raycast position
            XRRayInteractor activeRayInteractor = GetActiveRayInteractor();
            
            if (activeRayInteractor == null)
            {
                Debug.LogWarning("No active ray interactor found! Make sure you're in ray mode.");
                ClearCachedSpawnPoint();
                return;
            }
            
            if (activeRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
            {
                spawnPosition = hit.point + hit.normal * 0.1f;
                spawnRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }
            else
            {
                Debug.LogWarning("No valid spawn position found!");
                ClearCachedSpawnPoint();
                return;
            }
        }
        
        // Spawn the object
        GameObject spawnedObject = Instantiate(prefab, spawnPosition, spawnRotation);
        
        // Tag for highlighting system
        spawnedObject.tag = "SpawnedObject";
        
        // Apply color only if specified
        if (color.HasValue)
        {
            ApplyColorToObject(spawnedObject, color.Value);
        }
        
        Debug.Log($"Spawned '{objectName}'" + (color.HasValue ? $" with color {color.Value}" : ""));
        
        // Clear the cached spawn point after use
        ClearCachedSpawnPoint();
    }
    
    // Async version for GLB spawning with color
    private async System.Threading.Tasks.Task SpawnGLBAtRaycastWithColor(string glbPath, string objectName, Color? color)
    {
        if (runtimeModelLoader == null)
        {
            Debug.LogError("RuntimeModelLoader is not assigned!");
            ClearCachedSpawnPoint();
            return;
        }
        
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        
        // Use cached spawn point if available (captured when dictation ended)
        if (hasCachedSpawnPoint && cachedSpawnPosition.HasValue && cachedSpawnRotation.HasValue)
        {
            spawnPosition = cachedSpawnPosition.Value;
            spawnRotation = cachedSpawnRotation.Value;
            Debug.Log("Using cached spawn point from when you spoke");
        }
        else
        {
            // Fallback: check current raycast position
            XRRayInteractor activeRayInteractor = GetActiveRayInteractor();
            if (activeRayInteractor == null)
            {
                Debug.LogWarning("No active ray interactor found!");
                ClearCachedSpawnPoint();
                return;
            }
            
            if (activeRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
            {
                spawnPosition = hit.point + hit.normal * runtimeModelLoader.spawnHeightOffset;
                spawnRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }
            else
            {
                Debug.LogWarning("No valid spawn position found!");
                ClearCachedSpawnPoint();
                return;
            }
        }
        
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
                // Tag for highlighting system
                spawnedObject.tag = "SpawnedObject";
                
                // Apply color only if specified by LLM
                if (color.HasValue)
                {
                    ApplyColorToObject(spawnedObject, color.Value);
                }
                
                Debug.Log($"Spawned GLB '{objectName}'" + (color.HasValue ? $" with color {color.Value}" : ""));
            }
        }
        
        // Clear the cached spawn point after use
        ClearCachedSpawnPoint();
    }
    
    // Helper method to apply color to object's materials
    private void ApplyColorToObject(GameObject obj, Color color)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            // Skip the bounding box renderer (don't color it)
            if (renderer.gameObject.name == "HighlightBoundingBox")
            {
                continue;
            }
            
            foreach (Material mat in renderer.materials)
            {
                mat.color = color;
            }
        }
        
        // Tell PlaneCollisionDetector to update its stored materials
        // so it won't override our color when restoring after collision
        PlaneCollisionDetector collisionDetector = obj.GetComponent<PlaneCollisionDetector>();
        if (collisionDetector != null)
        {
            collisionDetector.UpdateStoredMaterials();
        }
    }
    
    // Legacy method without color (for local keyword matching)
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
            
            // Temporarily disable gravity and make kinematic until object settles
            Rigidbody rb = spawnedObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                // Re-enable physics after a short delay
                StartCoroutine(EnablePhysicsAfterDelay(rb, 0.5f));
            }
            
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
    
    private void DeleteObjectAtRaycast()
    {
        // Get the active ray interactor
        XRRayInteractor activeRayInteractor = GetActiveRayInteractor();
        
        if (activeRayInteractor == null)
        {
            Debug.LogWarning("[VoiceObjectSpawner] No active ray interactor found for deletion!");
            return;
        }
        
        // Try to get raycast hit from the active ray interactor
        if (activeRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            GameObject hitObject = hit.collider.gameObject;
            
            // Check if it's an AR plane (don't delete planes)
            if (hitObject.GetComponent<UnityEngine.XR.ARFoundation.ARPlane>() != null)
            {
                Debug.Log("[VoiceObjectSpawner] Cannot delete AR planes!");
                return;
            }
            
            // Delete the object
            Debug.Log($"[VoiceObjectSpawner] Deleting object: {hitObject.name}");
            Destroy(hitObject);
        }
        else
        {
            Debug.LogWarning("[VoiceObjectSpawner] Ray interactor did not hit any object to delete!");
        }
    }
    
    private System.Collections.IEnumerator EnablePhysicsAfterDelay(Rigidbody rb, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
    
    /// <summary>
    /// Test LLM connection with a simple prompt
    /// </summary>
    private void TestLLMConnection()
    {
        if (pythonClient == null)
        {
            Debug.LogError("[VoiceSpawner] PythonServerClient not assigned!");
            return;
        }
        
        Debug.Log("[VoiceSpawner] Testing LLM connection with ping...");
        
        // First test server connection
        pythonClient.TestConnection((bool success) =>
        {
            if (success)
            {
                Debug.Log("<color=green>[VoiceSpawner] ✓ Server connection successful!</color>");
                
                // Now test LLM with a simple prompt
                Debug.Log("[VoiceSpawner] Sending test prompt to LLM...");
                pythonClient.ProcessVoiceCommand(
                    "Hello, can you hear me?",
                    (LLMResponse response) =>
                    {
                        Debug.Log($"<color=green>[VoiceSpawner] ✓ LLM Response: {response.response}</color>");
                        Debug.Log("<color=cyan>LLM test successful! You can now use voice commands.</color>");
                    },
                    (string error) =>
                    {
                        Debug.LogError($"<color=red>[VoiceSpawner] ✗ LLM test failed: {error}</color>");
                    }
                );
            }
            else
            {
                Debug.LogError("<color=red>[VoiceSpawner] ✗ Server connection failed! Make sure llm_server.py is running.</color>");
            }
        });
    }
    
    // UI Update Methods
    private void UpdateUserCommandText(string text)
    {
        if (userCommandText != null)
        {
            userCommandText.text = $"You: \"{text}\"";
        }
    }
    
    private void UpdateLLMResponseText(string text)
    {
        if (llmResponseText != null)
        {
            llmResponseText.text = $"AI: {text}";
        }
    }
    
    // Modify Methods
    private void ModifyObjectColorAtRaycast(string colorName)
    {
        XRRayInteractor activeRayInteractor = GetActiveRayInteractor();
        if (activeRayInteractor == null)
        {
            Debug.LogWarning("No active ray interactor found!");
            UpdateLLMResponseText("Point at an object to change its color.");
            return;
        }
        
        if (activeRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            GameObject hitObject = hit.collider.gameObject;
            
            // Don't modify AR planes
            if (hitObject.GetComponent<UnityEngine.XR.ARFoundation.ARPlane>() != null)
            {
                Debug.Log("Cannot modify AR planes!");
                UpdateLLMResponseText("That's an AR plane, not furniture!");
                return;
            }
            
            // Parse color
            Color? newColor = ParseColor(colorName);
            if (newColor.HasValue)
            {
                ApplyColorToObject(hitObject, newColor.Value);
                Debug.Log($"Changed {hitObject.name} color to {colorName}");
            }
            else
            {
                UpdateLLMResponseText($"I don't recognize the color '{colorName}'.");
            }
        }
        else
        {
            Debug.LogWarning("Ray interactor did not hit any object!");
            UpdateLLMResponseText("Point at an object to change its color.");
        }
    }
    
    private void ReplaceObjectWithVariation()
    {
        XRRayInteractor activeRayInteractor = GetActiveRayInteractor();
        if (activeRayInteractor == null)
        {
            Debug.LogWarning("No active ray interactor found!");
            UpdateLLMResponseText("Point at an object to get a variation.");
            return;
        }
        
        if (activeRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            GameObject hitObject = hit.collider.gameObject;
            
            // Don't modify AR planes
            if (hitObject.GetComponent<UnityEngine.XR.ARFoundation.ARPlane>() != null)
            {
                Debug.Log("Cannot modify AR planes!");
                UpdateLLMResponseText("That's an AR plane, not furniture!");
                return;
            }
            
            // Store position and rotation
            Vector3 position = hitObject.transform.position;
            Quaternion rotation = hitObject.transform.rotation;
            
            // Try to find what type of object this is from its name
            string objectType = ExtractObjectTypeFromName(hitObject.name);
            
            if (!string.IsNullOrEmpty(objectType))
            {
                // Delete old object
                Destroy(hitObject);
                
                // Spawn new variation at same location
                List<VoiceObjectMapping> matches = FindAllMatchingMappings(objectType);
                if (matches.Count > 0)
                {
                    VoiceObjectMapping selected = matches[Random.Range(0, matches.Count)];
                    
                    if (selected.prefab != null)
                    {
                        GameObject newObject = Instantiate(selected.prefab, position, rotation);
                        Debug.Log($"Replaced with variation: {selected.objectName}");
                    }
                }
                else
                {
                    UpdateLLMResponseText($"I don't have another {objectType} model.");
                }
            }
            else
            {
                UpdateLLMResponseText("I'm not sure what type of object that is.");
            }
        }
        else
        {
            Debug.LogWarning("Ray interactor did not hit any object!");
            UpdateLLMResponseText("Point at an object to get a variation.");
        }
    }
    
    private string ExtractObjectTypeFromName(string name)
    {
        // Extract object type from name (e.g., "chair_01" → "chair")
        name = name.ToLower();
        foreach (var mapping in objectMappings)
        {
            if (name.Contains(mapping.objectName.ToLower()))
            {
                return mapping.objectName;
            }
        }
        return null;
    }
    
    // ==================== SPAWN POINT CACHING ====================
    
    private void CaptureCurrentSpawnPoint()
    {
        XRRayInteractor activeRayInteractor = GetActiveRayInteractor();
        
        if (activeRayInteractor == null)
        {
            Debug.LogWarning("No active ray interactor to capture spawn point");
            hasCachedSpawnPoint = false;
            return;
        }
        
        if (activeRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            cachedSpawnPosition = hit.point + hit.normal * 0.1f;
            cachedSpawnRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            hasCachedSpawnPoint = true;
            Debug.Log($"Captured spawn point at: {cachedSpawnPosition}");
        }
        else
        {
            // No valid surface - will use last spawn position as fallback
            hasCachedSpawnPoint = false;
            Debug.LogWarning("No surface hit to capture spawn point");
        }
    }
    
    private void ClearCachedSpawnPoint()
    {
        hasCachedSpawnPoint = false;
        cachedSpawnPosition = null;
        cachedSpawnRotation = null;
    }
    
    // ==================== OBJECT HIGHLIGHTING ====================
    
    private void UpdateRaycastHighlight()
    {
        XRRayInteractor activeRayInteractor = GetActiveRayInteractor();
        
        if (activeRayInteractor == null)
        {
            ClearHighlight();
            return;
        }
        
        // Check if raycast is hitting something
        if (activeRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            GameObject hitObject = hit.collider.gameObject;
            
            // ONLY highlight objects with SpawnedObject tag (furniture we spawned)
            // This prevents planes, walls, and other objects from being highlighted
            if (hitObject.CompareTag("SpawnedObject"))
            {
                // If we're pointing at a different object, switch highlight
                if (currentlyHighlightedObject != hitObject)
                {
                    ClearHighlight();
                    HighlightObject(hitObject);
                }
            }
            else
            {
                // Pointing at plane or non-furniture object - no highlight
                ClearHighlight();
            }
        }
        else
        {
            // Not pointing at anything
            ClearHighlight();
        }
    }
    
    private void HighlightObject(GameObject obj)
    {
        currentlyHighlightedObject = obj;
        
        if (highlightMaterial == null)
        {
            Debug.LogWarning("Highlight material not assigned! Assign a translucent material in Inspector.");
            return;
        }
        
        // Calculate bounding box that encompasses all renderers
        Bounds combinedBounds = CalculateCombinedBounds(obj);
        
        if (combinedBounds.size == Vector3.zero)
        {
            Debug.LogWarning("Object has no renderers or bounds");
            return;
        }
        
        // Create bounding box visualization
        highlightBoundingBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        highlightBoundingBox.name = "HighlightBoundingBox";
        
        // Remove collider (we don't want the box to be physical)
        Collider boxCollider = highlightBoundingBox.GetComponent<Collider>();
        if (boxCollider != null)
        {
            Destroy(boxCollider);
        }
        
        // Position and scale to match object bounds (with padding)
        highlightBoundingBox.transform.position = combinedBounds.center;
        highlightBoundingBox.transform.rotation = obj.transform.rotation;
        highlightBoundingBox.transform.localScale = combinedBounds.size + Vector3.one * boundingBoxPadding;
        
        // Apply translucent material
        Renderer boxRenderer = highlightBoundingBox.GetComponent<Renderer>();
        if (boxRenderer != null)
        {
            boxRenderer.material = highlightMaterial;
        }
        
        // Make it a child of the highlighted object so it moves with it
        highlightBoundingBox.transform.SetParent(obj.transform, true);
    }
    
    private Bounds CalculateCombinedBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        
        if (renderers.Length == 0)
        {
            return new Bounds(obj.transform.position, Vector3.zero);
        }
        
        // Start with first renderer's bounds
        Bounds bounds = renderers[0].bounds;
        
        // Encapsulate all other renderers
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        
        return bounds;
    }
    
    private void ClearHighlight()
    {
        if (highlightBoundingBox != null)
        {
            Destroy(highlightBoundingBox);
            highlightBoundingBox = null;
        }
        
        currentlyHighlightedObject = null;
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
