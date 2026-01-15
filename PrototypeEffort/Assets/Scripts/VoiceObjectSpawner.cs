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
    
    [Header("Direct Interactor References")]
    [SerializeField] private XRDirectInteractor rightDirectInteractor;
    [SerializeField] private XRDirectInteractor leftDirectInteractor;
    
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
    
    [Header("Inventory Data")]
    [Tooltip("Path to inventory.csv (e.g., C:/Users/s2733099/ShapenetData/inventory.csv)")]
    [SerializeField] private string inventoryCSVPath = "C:/Users/s2733099/ShapenetData/inventory.csv";
    
    [Tooltip("Path to GLB folder (e.g., C:/Users/s2733099/ShapenetData/GLB)")]
    [SerializeField] private string glbFolderPath = "C:/Users/s2733099/ShapenetData/GLB";
    
    [Header("Object Library")]
    [SerializeField] private List<VoiceObjectMapping> objectMappings = new List<VoiceObjectMapping>();
    
    [Header("Runtime GLB Loading")]
    [Tooltip("Reference to RuntimeModelLoader for loading .glb files at runtime")]
    [SerializeField] private RuntimeModelLoader runtimeModelLoader;
    
    // Object naming system
    private Dictionary<string, int> objectCounters = new Dictionary<string, int>();
    private Dictionary<GameObject, ObjectMetadata> objectMetadata = new Dictionary<GameObject, ObjectMetadata>();
    
    private class ObjectMetadata
    {
        public string objectType;    // "Chair", "Table", etc.
        public int instanceNumber;   // 1, 2, 3...
        public string color;         // "Brown", "Red", "Blue", null
        public string modelId;       // Original model ID
    }
    
    private void Start()
    {
        // Subscribe to final transcript event
        if (dictationManager != null)
        {
            dictationManager.OnFinalTranscript.AddListener(ProcessVoiceCommand);
        }
        
        // Load inventory from CSV
        LoadInventoryFromCSV();
        
        // Set initial welcome message
        if (llmResponseText != null)
        {
            llmResponseText.text = "I will be your personal design companion, please hold A in order to talk to me. Try saying something like: \"place a pink chair\", and I can help you!";
        }
    }
    
    /// <summary>
    /// Loads inventory.csv and populates objectMappings
    /// </summary>
    private async void LoadInventoryFromCSV()
    {
        string csvPath = inventoryCSVPath;
        
        // On Quest, download CSV from Flask server
        if (Application.platform == RuntimePlatform.Android)
        {
            csvPath = System.IO.Path.Combine(Application.temporaryCachePath, "inventory.csv");
            string csvURL = $"{pythonClient.ServerUrl}/inventory.csv";
            
            Debug.Log($"[VoiceObjectSpawner] Quest detected - downloading CSV from: {csvURL}");
            
            using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(csvURL))
            {
                var operation = www.SendWebRequest();
                
                // Wait for completion
                while (!operation.isDone)
                {
                    await System.Threading.Tasks.Task.Yield();
                }
                
                if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[VoiceObjectSpawner] Failed to download CSV: {www.error}");
                    return;
                }
                
                System.IO.File.WriteAllText(csvPath, www.downloadHandler.text);
                Debug.Log($"[VoiceObjectSpawner] Downloaded CSV to: {csvPath}");
            }
        }
        
        Debug.Log($"[VoiceObjectSpawner] Loading inventory from: {csvPath}");
        
        // Read inventory entries
        List<InventoryCSVReader.InventoryEntry> entries = InventoryCSVReader.ReadInventoryCSV(csvPath);
        
        if (entries.Count == 0)
        {
            Debug.LogError("[VoiceObjectSpawner] No inventory entries loaded!");
            return;
        }
        
        // Convert to mappings
        objectMappings = InventoryCSVReader.ConvertToMappings(entries, glbFolderPath);
        
        Debug.Log($"[VoiceObjectSpawner] Loaded {objectMappings.Count} object mappings from inventory");
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
                    
                    // LLM provides the specific modelId - use it directly!
                    if (!string.IsNullOrEmpty(response.modelId))
                    {
                        // Construct glbPath for PC (Quest uses modelId directly)
                        string glbPath = System.IO.Path.Combine(glbFolderPath, response.modelId + ".glb");
                        
                        Debug.Log($"[VoiceObjectSpawner] LLM selected model: name='{response.objectName}', modelId='{response.modelId}', relativePos='{response.relativePosition}'");
                        
                        if (runtimeModelLoader != null)
                        {
                            await SpawnGLBAtRaycastWithColor(glbPath, response.objectName, response.modelId, spawnColor, response.relativePosition);
                        }
                    }
                    else
                    {
                        // Fallback: Find matching object randomly (old behavior)
                        List<VoiceObjectMapping> matches = FindAllMatchingMappings(response.objectName);
                        if (matches.Count > 0)
                        {
                            VoiceObjectMapping selected = matches[Random.Range(0, matches.Count)];
                            
                            Debug.Log($"[VoiceObjectSpawner] Fallback - Selected mapping: name='{selected.objectName}', glbPath='{selected.glbPath}', modelId='{selected.modelId}' (length={selected.modelId?.Length ?? 0})");
                            
                            if (!string.IsNullOrEmpty(selected.glbPath) && runtimeModelLoader != null)
                            {
                                await SpawnGLBAtRaycastWithColor(selected.glbPath, selected.objectName, selected.modelId, spawnColor, response.relativePosition);
                            }
                            else if (selected.prefab != null)
                            {
                                SpawnObjectAtRaycastWithColor(selected.prefab, selected.objectName, spawnColor, response.relativePosition);
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"No matching object found for '{response.objectName}'");
                            UpdateLLMResponseText($"I don't have a {response.objectName} available.");
                        }
                    }
                }
                else if (response.action == "delete")
                {
                    // Debug: Log what we received
                    Debug.Log($"[Delete] Received - objectName: '{response.objectName}', color: '{response.color}'");
                    
                    UpdateLLMResponseText("Deleting object...");
                    DeleteHeldObject(response.objectName, response.color);
                }
                else if (response.action == "scale")
                {
                    float scaleFactor = response.scaleFactor;
                    UpdateLLMResponseText($"Scaling object by {scaleFactor}x...");
                    ScaleHeldObject(scaleFactor);
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
            DeleteHeldObject();
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
                await SpawnGLBAtRaycast(selectedMapping.glbPath, selectedMapping.objectName, selectedMapping.modelId);
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
    
    private async System.Threading.Tasks.Task SpawnGLBAtRaycast(string glbPath, string objectName, string modelId)
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
            Quaternion spawnRotation = Quaternion.identity; // Keep objects upright
            
            Debug.Log($"Loading GLB model: {objectName}");
            
            // Use LoadModel which auto-detects PC vs Quest
            GameObject spawnedObject = await runtimeModelLoader.LoadModel(
                glbPath,
                modelId,
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
            Debug.LogWarning("Ray interactor did not hit any surface! Point at an AR plane.");
        }
    }
    
    // New method with color support and optional relative positioning
    private void SpawnObjectAtRaycastWithColor(GameObject prefab, string objectName, Color? color, string relativePosition = null)
    {
        if (prefab == null)
        {
            Debug.LogWarning("Cannot spawn null prefab");
            ClearCachedSpawnPoint();
            return;
        }
        
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        
        // PRIORITY 1: Use relative position if specified ("in front of me", etc.)
        if (!string.IsNullOrEmpty(relativePosition))
        {
            // Calculate bounds estimate for distance scaling
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 0.5f); // Default size
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }
            
            (spawnPosition, spawnRotation) = CalculateRelativeSpawnPosition(relativePosition, bounds);
            Debug.Log($"Using relative position: {relativePosition}");
        }
        // PRIORITY 2: Use cached spawn point if available (captured when dictation ended)
        else if (hasCachedSpawnPoint && cachedSpawnPosition.HasValue && cachedSpawnRotation.HasValue)
        {
            spawnPosition = cachedSpawnPosition.Value;
            spawnRotation = cachedSpawnRotation.Value;
            Debug.Log("Using cached spawn point from when you spoke");
        }
        // PRIORITY 3: Fallback to current raycast position
        else
        {
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
                spawnRotation = Quaternion.identity; // Keep objects upright
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
    
    // Async version for GLB spawning with color and optional relative positioning
    private async System.Threading.Tasks.Task SpawnGLBAtRaycastWithColor(string glbPath, string objectName, string modelId, Color? color, string relativePosition = null)
    {
        if (runtimeModelLoader == null)
        {
            Debug.LogError("RuntimeModelLoader is not assigned!");
            ClearCachedSpawnPoint();
            return;
        }
        
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        
        // PRIORITY 1: Use relative position if specified (\"in front of me\", etc.)
        if (!string.IsNullOrEmpty(relativePosition))
        {
            // Use default bounds for GLB (will be adjusted after loading)
            Bounds estimatedBounds = new Bounds(Vector3.zero, Vector3.one * 0.5f);
            (spawnPosition, spawnRotation) = CalculateRelativeSpawnPosition(relativePosition, estimatedBounds);
            Debug.Log($"Using relative position: {relativePosition}");
        }
        // PRIORITY 2: Use cached spawn point if available (captured when dictation ended)
        else if (hasCachedSpawnPoint && cachedSpawnPosition.HasValue && cachedSpawnRotation.HasValue)
        {
            spawnPosition = cachedSpawnPosition.Value;
            spawnRotation = cachedSpawnRotation.Value;
            Debug.Log("Using cached spawn point from when you spoke");
        }
        // PRIORITY 3: Fallback to current raycast position
        else
        {
            XRRayInteractor activeRayInteractor = GetActiveRayInteractor();
            if (activeRayInteractor == null)
            {
                Debug.LogWarning("No active ray interactor found!");
                ClearCachedSpawnPoint();
                return;
            }
            
            if (activeRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
            {
                // Don't add height offset here - RuntimeModelLoader will adjust based on bounding box
                spawnPosition = hit.point;
                spawnRotation = Quaternion.identity; // Keep objects upright
            }
            else
            {
                Debug.LogWarning("No valid spawn position found!");
                ClearCachedSpawnPoint();
                return;
            }
        }
        
        // Find the mapping to get modelId - no longer needed, passed as parameter
        
        // Load GLB using platform-aware method
        GameObject spawnedObject = await runtimeModelLoader.LoadModel(
            glbPath,
            modelId,
            spawnPosition,
            spawnRotation
        );
            
        if (spawnedObject != null)
        {
            // Tag for highlighting system
            spawnedObject.tag = "SpawnedObject";
            
            // Generate unique name and track metadata
            string colorName = color.HasValue ? ColorToName(color.Value) : null;
            string uniqueName = GenerateUniqueName(objectName, colorName);
            spawnedObject.name = uniqueName;
            
            // Store metadata for future updates
            objectMetadata[spawnedObject] = new ObjectMetadata
            {
                objectType = objectName,
                instanceNumber = objectCounters.ContainsKey(objectName) ? objectCounters[objectName] : 1,
                color = colorName,
                modelId = modelId
            };
            
            // Apply color only if specified by LLM
            if (color.HasValue)
            {
                ApplyColorToObject(spawnedObject, color.Value);
            }
            
            Debug.Log($"Spawned GLB '{uniqueName}'" + (color.HasValue ? $" with color {color.Value}" : ""));
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
            foreach (Material mat in renderer.materials)
            {
                mat.color = color;
            }
        }
        
        // Update object name if metadata exists
        if (objectMetadata.ContainsKey(obj))
        {
            ObjectMetadata meta = objectMetadata[obj];
            meta.color = ColorToName(color);
            obj.name = GenerateNameFromMetadata(meta);
        }
    }
    
    /// <summary>
    /// Generates a unique name for spawned objects: "Chair_1_Brown" or "Table_2" (no color)
    /// </summary>
    private string GenerateUniqueName(string objectType, string colorName)
    {
        // Capitalize first letter
        string capitalizedType = char.ToUpper(objectType[0]) + objectType.Substring(1);
        
        // Increment counter
        if (!objectCounters.ContainsKey(objectType))
        {
            objectCounters[objectType] = 0;
        }
        objectCounters[objectType]++;
        
        int instanceNum = objectCounters[objectType];
        
        // Format: Chair_1_Brown or Chair_1 (if no color)
        if (!string.IsNullOrEmpty(colorName))
        {
            return $"{capitalizedType}_{instanceNum}_{colorName}";
        }
        else
        {
            return $"{capitalizedType}_{instanceNum}";
        }
    }
    
    /// <summary>
    /// Generates name from metadata (for updates)
    /// </summary>
    private string GenerateNameFromMetadata(ObjectMetadata meta)
    {
        string capitalizedType = char.ToUpper(meta.objectType[0]) + meta.objectType.Substring(1);
        
        if (!string.IsNullOrEmpty(meta.color))
        {
            return $"{capitalizedType}_{meta.instanceNumber}_{meta.color}";
        }
        else
        {
            return $"{capitalizedType}_{meta.instanceNumber}";
        }
    }
    
    /// <summary>
    /// Converts Unity Color to readable name
    /// </summary>
    private string ColorToName(Color color)
    {
        // Check common colors with tolerance
        if (ColorDistance(color, Color.red) < 0.3f) return "Red";
        if (ColorDistance(color, Color.blue) < 0.3f) return "Blue";
        if (ColorDistance(color, Color.green) < 0.3f) return "Green";
        if (ColorDistance(color, Color.yellow) < 0.3f) return "Yellow";
        if (ColorDistance(color, Color.white) < 0.3f) return "White";
        if (ColorDistance(color, Color.black) < 0.3f) return "Black";
        if (ColorDistance(color, new Color(0.65f, 0.16f, 0.16f)) < 0.3f) return "Brown";
        if (ColorDistance(color, new Color(1f, 0.65f, 0f)) < 0.3f) return "Orange";
        if (ColorDistance(color, new Color(0.5f, 0f, 0.5f)) < 0.3f) return "Purple";
        if (ColorDistance(color, new Color(1f, 0.75f, 0.8f)) < 0.3f) return "Pink";
        if (ColorDistance(color, Color.gray) < 0.3f) return "Gray";
        
        return "Colored"; // Fallback for unknown colors
    }
    
    /// <summary>
    /// Calculate distance between two colors
    /// </summary>
    private float ColorDistance(Color a, Color b)
    {
        return Mathf.Sqrt(
            Mathf.Pow(a.r - b.r, 2) +
            Mathf.Pow(a.g - b.g, 2) +
            Mathf.Pow(a.b - b.b, 2)
        );
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
    
    /// <summary>
    /// Calculate spawn position relative to user's camera/position
    /// </summary>
    /// <param name="relativePosition">"front", "behind", "left", "right"</param>
    /// <param name="bounds">Object bounds for distance scaling</param>
    /// <returns>Position and rotation for spawning</returns>
    private (Vector3 position, Quaternion rotation) CalculateRelativeSpawnPosition(string relativePosition, Bounds bounds)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main camera not found!");
            return (Vector3.zero, Quaternion.identity);
        }
        
        Vector3 cameraPos = mainCamera.transform.position;
        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;
        
        // Calculate base distance (1-1.5m, scaled by object size)
        // Larger objects spawn farther away
        float objectSize = Mathf.Max(bounds.size.x, bounds.size.z); // Use horizontal size
        float baseDistance = 1.0f; // Base distance in meters
        float distanceScale = Mathf.Clamp(objectSize / 0.5f, 1.0f, 1.5f); // Scale based on size
        float spawnDistance = baseDistance * distanceScale;
        
        Vector3 spawnPos;
        Vector3 directionToCamera;
        
        switch (relativePosition?.ToLower())
        {
            case "front":
                // Spawn in front of user
                spawnPos = cameraPos + forward * spawnDistance;
                directionToCamera = (cameraPos - spawnPos).normalized;
                break;
                
            case "behind":
                // Spawn behind user
                spawnPos = cameraPos - forward * spawnDistance;
                directionToCamera = (cameraPos - spawnPos).normalized;
                break;
                
            case "left":
                // Spawn to user's left
                spawnPos = cameraPos - right * spawnDistance;
                directionToCamera = (cameraPos - spawnPos).normalized;
                break;
                
            case "right":
                // Spawn to user's right
                spawnPos = cameraPos + right * spawnDistance;
                directionToCamera = (cameraPos - spawnPos).normalized;
                break;
                
            default:
                Debug.LogWarning($"Unknown relative position: {relativePosition}");
                return (cameraPos + forward * 1.5f, Quaternion.identity);
        }
        
        // Project down to find floor (AR plane)
        // Start from slightly above spawn position to ensure we hit the plane
        Vector3 rayStart = new Vector3(spawnPos.x, cameraPos.y, spawnPos.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10f))
        {
            // Found floor, use that Y position
            spawnPos.y = hit.point.y + 0.05f; // Slight offset above floor
        }
        else
        {
            // No floor found, use camera Y as fallback
            spawnPos.y = cameraPos.y - 1.0f; // Assume floor is 1m below camera
        }
        
        // Calculate rotation so object faces the user
        // Remove Y component for horizontal-only rotation
        directionToCamera.y = 0;
        directionToCamera.Normalize();
        
        Quaternion rotation = Quaternion.LookRotation(directionToCamera);
        
        Debug.Log($"[RelativeSpawn] Position: {relativePosition}, Distance: {spawnDistance:F2}m, Pos: {spawnPos}, Facing user: {directionToCamera}");
        
        return (spawnPos, rotation);
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
    
    /// <summary>
    /// Delete the object currently being held/grabbed OR pointed at with ray
    /// If no held/pointed object, search by description (objectName + color)
    /// </summary>
    private void DeleteHeldObject(string objectName = null, string color = null)
    {
        // PRIORITY 1: Try to get held or pointed object
        GameObject objectToDelete = GetTargetObject(out string method);
        
        if (objectToDelete != null)
        {
            Debug.Log($"[VoiceObjectSpawner] Deleting object via {method}: {objectToDelete.name}");
            
            // Remove from metadata if exists
            if (objectMetadata.ContainsKey(objectToDelete))
            {
                objectMetadata.Remove(objectToDelete);
            }
            
            Destroy(objectToDelete);
            UpdateLLMResponseText($"Deleted {objectToDelete.name}!");
            return;
        }
        
        // PRIORITY 2: Search by description if provided
        if (!string.IsNullOrEmpty(objectName))
        {
            List<GameObject> matches = FindObjectsByDescription(objectName, color);
            
            if (matches.Count > 0)
            {
                // Delete closest object to camera
                GameObject closest = GetClosestObject(matches);
                
                Debug.Log($"[VoiceObjectSpawner] Deleting object by description ({objectName}, {color}): {closest.name}");
                
                // Remove from metadata
                if (objectMetadata.ContainsKey(closest))
                {
                    objectMetadata.Remove(closest);
                }
                
                Destroy(closest);
                UpdateLLMResponseText($"Deleted {closest.name}!");
                return;
            }
            else
            {
                // No matches found
                string searchDesc = !string.IsNullOrEmpty(color) ? $"{color} {objectName}" : objectName;
                Debug.LogWarning($"[VoiceObjectSpawner] No {searchDesc} found to delete!");
                UpdateLLMResponseText($"I couldn't find a {searchDesc} to delete.");
                return;
            }
        }
        
        // No object found and no description provided
        Debug.LogWarning("[VoiceObjectSpawner] No object is being held or pointed at!");
        UpdateLLMResponseText("Please grab or point at an object, or describe it (e.g., 'delete the pink chair').");
    }
    
    /// <summary>
    /// Scale the object currently being held/grabbed OR pointed at with ray
    /// </summary>
    private void ScaleHeldObject(float scaleFactor)
    {
        GameObject objectToScale = GetTargetObject(out string method);
        
        if (objectToScale != null)
        {
            // Clamp scale factor
            float clampedFactor = Mathf.Clamp(scaleFactor, 0.1f, 5.0f);
            
            // Apply uniform scale
            Vector3 newScale = objectToScale.transform.localScale * clampedFactor;
            
            // Clamp total scale to prevent objects from becoming too small or too large
            float maxScaleComponent = Mathf.Max(newScale.x, newScale.y, newScale.z);
            float minScaleComponent = Mathf.Min(newScale.x, newScale.y, newScale.z);
            
            if (maxScaleComponent > 10.0f || minScaleComponent < 0.01f)
            {
                Debug.LogWarning($"[VoiceObjectSpawner] Scale clamped - would result in too large or too small object");
                UpdateLLMResponseText("Can't scale that much - object would be too big or too small!");
                return;
            }
            
            objectToScale.transform.localScale = newScale;
            
            Debug.Log($"[VoiceObjectSpawner] Scaled {objectToScale.name} via {method} by {clampedFactor}x. New scale: {newScale}");
            UpdateLLMResponseText($"Scaled to {clampedFactor}x!");
        }
        else
        {
            Debug.LogWarning("[VoiceObjectSpawner] No object is being held or pointed at!");
            UpdateLLMResponseText("Please grab or point at an object, then say 'make it bigger' or 'make it smaller'.");
        }
    }
    
    /// <summary>
    /// Get the target object - either held by hand OR pointed at with ray
    /// Priority: Held object first (right hand, then left), then raycast
    /// </summary>
    private GameObject GetTargetObject(out string method)
    {
        method = "";
        
        // PRIORITY 1: Check if holding an object with RIGHT hand (most people are right-handed)
        if (rightDirectInteractor != null && rightDirectInteractor.enabled && rightDirectInteractor.hasSelection)
        {
            var selectedObjects = rightDirectInteractor.interactablesSelected;
            if (selectedObjects.Count > 0)
            {
                method = "RIGHT HAND";
                return selectedObjects[0].transform.gameObject;
            }
        }
        
        // PRIORITY 2: Check left hand
        if (leftDirectInteractor != null && leftDirectInteractor.enabled && leftDirectInteractor.hasSelection)
        {
            var selectedObjects = leftDirectInteractor.interactablesSelected;
            if (selectedObjects.Count > 0)
            {
                method = "LEFT HAND";
                return selectedObjects[0].transform.gameObject;
            }
        }
        
        // PRIORITY 2: Check what ray is pointing at
        XRRayInteractor activeRay = GetActiveRayInteractor();
        if (activeRay != null && activeRay.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            GameObject hitObject = hit.collider.gameObject;
            
            // Don't target AR planes
            if (hitObject.GetComponent<UnityEngine.XR.ARFoundation.ARPlane>() != null)
            {
                Debug.Log("[VoiceObjectSpawner] Ray hit AR plane - ignoring");
                return null;
            }
            
            method = "RAYCAST";
            return hitObject;
        }
        
        return null;
    }
    
    /// <summary>
    /// Find all spawned objects matching the description
    /// </summary>
    private List<GameObject> FindObjectsByDescription(string objectName, string color)
    {
        List<GameObject> matches = new List<GameObject>();
        
        foreach (var kvp in objectMetadata)
        {
            GameObject obj = kvp.Key;
            ObjectMetadata meta = kvp.Value;
            
            if (obj == null) continue; // Object was destroyed
            
            // Check if object type matches (case-insensitive)
            bool typeMatches = meta.objectType.Equals(objectName, System.StringComparison.OrdinalIgnoreCase);
            
            // If color specified, check color match
            bool colorMatches = true;
            if (!string.IsNullOrEmpty(color))
            {
                colorMatches = !string.IsNullOrEmpty(meta.color) && 
                              meta.color.Equals(color, System.StringComparison.OrdinalIgnoreCase);
            }
            
            if (typeMatches && colorMatches)
            {
                matches.Add(obj);
            }
        }
        
        Debug.Log($"[FindObjects] Found {matches.Count} matches for {objectName} {color}");
        return matches;
    }
    
    /// <summary>
    /// Get the closest object to the camera from a list
    /// </summary>
    private GameObject GetClosestObject(List<GameObject> objects)
    {
        if (objects.Count == 0) return null;
        if (objects.Count == 1) return objects[0];
        
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return objects[0];
        
        GameObject closest = objects[0];
        float closestDistance = Vector3.Distance(mainCamera.transform.position, closest.transform.position);
        
        for (int i = 1; i < objects.Count; i++)
        {
            float distance = Vector3.Distance(mainCamera.transform.position, objects[i].transform.position);
            if (distance < closestDistance)
            {
                closest = objects[i];
                closestDistance = distance;
            }
        }
        
        Debug.Log($"[GetClosest] Closest object: {closest.name} at {closestDistance:F2}m");
        return closest;
    }
    
    /// <summary>
    /// Find the Left Direct Interactor in the scene
    /// </summary>
    private XRDirectInteractor FindLeftDirectInteractor()
    {
        // Try to find in the XR Rig hierarchy
        GameObject leftController = GameObject.Find("Left Controller");
        if (leftController != null)
        {
            return leftController.GetComponent<XRDirectInteractor>();
        }
        return null;
    }
    
    /// <summary>
    /// Find the Right Direct Interactor in the scene
    /// </summary>
    private XRDirectInteractor FindRightDirectInteractor()
    {
        // Try to find in the XR Rig hierarchy
        GameObject rightController = GameObject.Find("Right Controller");
        if (rightController != null)
        {
            return rightController.GetComponent<XRDirectInteractor>();
        }
        return null;
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
            cachedSpawnRotation = Quaternion.identity; // Keep objects upright
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
        
        // Calculate bounding box in LOCAL SPACE (same method as RuntimeModelLoader)
        Bounds localBounds = CalculateCombinedBounds(obj);
        
        if (localBounds.size == Vector3.zero)
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
        
        // Parent it FIRST, then use local space positioning
        highlightBoundingBox.transform.SetParent(obj.transform, false);
        
        // Set position and size in LOCAL space (bounds are already in local space)
        highlightBoundingBox.transform.localPosition = localBounds.center;
        highlightBoundingBox.transform.localRotation = Quaternion.identity;
        highlightBoundingBox.transform.localScale = localBounds.size + Vector3.one * boundingBoxPadding;
        
        // Apply translucent material
        Renderer boxRenderer = highlightBoundingBox.GetComponent<Renderer>();
        if (boxRenderer != null)
        {
            boxRenderer.material = highlightMaterial;
        }
    }
    
    private Bounds CalculateCombinedBounds(GameObject obj)
    {
        MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>();
        
        if (meshFilters.Length == 0)
        {
            // Fallback to renderer bounds if no meshes
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(obj.transform.position, Vector3.zero);
            }
            
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        // Calculate bounds from actual mesh vertices in LOCAL SPACE (same as RuntimeModelLoader)
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;
        
        Transform rootTransform = obj.transform;
        
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
        
        // Return bounds in LOCAL space
        return new Bounds(center, size);
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
    
    [Tooltip("Path to GLB file (PC) or ID (Quest) - e.g., full path or just ID")]
    public string glbPath;
    
    [Tooltip("Model ID for network loading (Quest only)")]
    public string modelId;
    
    [Tooltip("Keywords that trigger spawning this object")]
    public List<string> keywords = new List<string>();
}
