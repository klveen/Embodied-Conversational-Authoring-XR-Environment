using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Changes the color of objects pointed at by raycast based on voice commands
/// Works in ray mode only, integrates with LLM
/// </summary>
public class VoiceColorChanger : MonoBehaviour
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
    
    [Header("Color Library")]
    [SerializeField] private List<ColorMapping> colorMappings = new List<ColorMapping>()
    {
        // Default color palette
        new ColorMapping { colorName = "red", color = Color.red, keywords = new List<string> { "red", "rouge" } },
        new ColorMapping { colorName = "blue", color = Color.blue, keywords = new List<string> { "blue", "bleu" } },
        new ColorMapping { colorName = "green", color = Color.green, keywords = new List<string> { "green", "vert" } },
        new ColorMapping { colorName = "yellow", color = Color.yellow, keywords = new List<string> { "yellow", "jaune" } },
        new ColorMapping { colorName = "white", color = Color.white, keywords = new List<string> { "white", "blanc" } },
        new ColorMapping { colorName = "black", color = Color.black, keywords = new List<string> { "black", "noir" } },
        new ColorMapping { colorName = "orange", color = new Color(1f, 0.5f, 0f), keywords = new List<string> { "orange" } },
        new ColorMapping { colorName = "purple", color = new Color(0.5f, 0f, 1f), keywords = new List<string> { "purple", "violet" } },
        new ColorMapping { colorName = "pink", color = new Color(1f, 0.75f, 0.8f), keywords = new List<string> { "pink", "rose" } },
        new ColorMapping { colorName = "gray", color = Color.gray, keywords = new List<string> { "gray", "grey", "gris" } },
        new ColorMapping { colorName = "brown", color = new Color(0.6f, 0.3f, 0f), keywords = new List<string> { "brown", "marron" } },
    };
    
    [Header("Visual Feedback")]
    [SerializeField] private bool showDebugRay = true;
    // TODO: Use this for visual highlight feedback duration if needed
    // [SerializeField] private float highlightDuration = 0.5f;
    
    private GameObject lastHighlightedObject;
    private Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();
    
    private void Start()
    {
        // Subscribe to final transcript event
        if (dictationManager != null)
        {
            dictationManager.OnFinalTranscript.AddListener(ProcessColorCommand);
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe when destroyed
        if (dictationManager != null)
        {
            dictationManager.OnFinalTranscript.RemoveListener(ProcessColorCommand);
        }
    }
    
    private void Update()
    {
        // Visual feedback: highlight object under raycast
        if (showDebugRay && InteractorToggleManager.Instance != null && InteractorToggleManager.Instance.IsRayMode())
        {
            UpdateRaycastHighlight();
        }
    }
    
    private void ProcessColorCommand(string transcript)
    {
        if (string.IsNullOrEmpty(transcript))
            return;
        
        string command = transcript.ToLower().Trim();
        Debug.Log($"[VoiceColorChanger] Processing color command: '{command}'");
        
        // TODO: Switch between local keyword matching and Python server based on toggle
        if (usePythonServer && pythonClient != null)
        {
            // Use Python server + LLM
            ProcessColorCommandWithPython(command);
        }
        else
        {
            // Use local keyword matching
            ProcessColorCommandLocally(command);
        }
    }
    
    // NEW: Process color command using Python server and LLM
    private void ProcessColorCommandWithPython(string command)
    {
        Debug.Log($"[VoiceColorChanger] Sending color command to Python server: '{command}'");
        
        pythonClient.ProcessVoiceCommand(
            command,
            // Success callback
            (LLMResponse response) => 
            {
                // TODO: Update LLMResponse to include a 'color' field
                // For now, parse the objectName as color
                if (!string.IsNullOrEmpty(response.objectName))
                {
                    Color? targetColor = ParseColorFromLLM(response.objectName);
                    if (targetColor.HasValue)
                    {
                        ApplyColorToRaycastTarget(targetColor.Value, response.objectName);
                    }
                }
            },
            // Error callback
            (string error) => 
            {
                Debug.LogError($"[VoiceColorChanger] Python server error: {error}");
                // Fallback to local keyword matching
                ProcessColorCommandLocally(command);
            }
        );
    }
    
    // EXISTING: Local keyword matching
    private void ProcessColorCommandLocally(string command)
    {
        // Check if we're in ray mode
        if (InteractorToggleManager.Instance == null || !InteractorToggleManager.Instance.IsRayMode())
        {
            Debug.LogWarning("[VoiceColorChanger] Color changing only works in ray mode!");
            return;
        }
        
        // Find matching color
        ColorMapping matchedColor = FindMatchingColor(command);
        
        if (matchedColor != null)
        {
            ApplyColorToRaycastTarget(matchedColor.color, matchedColor.colorName);
        }
        else
        {
            Debug.LogWarning($"[VoiceColorChanger] No matching color found for '{command}'");
        }
    }
    
    private ColorMapping FindMatchingColor(string command)
    {
        foreach (var mapping in colorMappings)
        {
            foreach (var keyword in mapping.keywords)
            {
                if (command.Contains(keyword.ToLower()))
                {
                    Debug.Log($"[VoiceColorChanger] Matched keyword '{keyword}' to color '{mapping.colorName}'");
                    return mapping;
                }
            }
        }
        return null;
    }
    
    private Color? ParseColorFromLLM(string colorString)
    {
        // Try to find matching color in our library
        ColorMapping mapping = FindMatchingColor(colorString);
        if (mapping != null)
        {
            return mapping.color;
        }
        
        // TODO: If LLM returns RGB values, parse them here
        // Example: "rgb(255,0,0)" or hex "#FF0000"
        
        return null;
    }
    
    private void ApplyColorToRaycastTarget(Color targetColor, string colorName)
    {
        XRRayInteractor activeRayInteractor = GetActiveRayInteractor();
        
        if (activeRayInteractor == null)
        {
            Debug.LogWarning("[VoiceColorChanger] No active ray interactor found!");
            return;
        }
        
        // Get object under raycast
        if (activeRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            GameObject targetObject = hit.collider.gameObject;
            
            // Get all renderers in the object (including children)
            Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>();
            
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[VoiceColorChanger] Object '{targetObject.name}' has no renderers!");
                return;
            }
            
            // Apply color to all renderers
            foreach (Renderer renderer in renderers)
            {
                // Store original color if not already stored
                if (!originalColors.ContainsKey(renderer))
                {
                    if (renderer.material.HasProperty("_Color"))
                    {
                        originalColors[renderer] = renderer.material.color;
                    }
                }
                
                // Apply new color
                if (renderer.material.HasProperty("_Color"))
                {
                    renderer.material.color = targetColor;
                }
                // For URP/Standard shader
                else if (renderer.material.HasProperty("_BaseColor"))
                {
                    renderer.material.SetColor("_BaseColor", targetColor);
                }
            }
            
            Debug.Log($"[VoiceColorChanger] Changed '{targetObject.name}' to {colorName}");
        }
        else
        {
            Debug.LogWarning("[VoiceColorChanger] Ray did not hit any object! Point at an object.");
        }
    }
    
    private void UpdateRaycastHighlight()
    {
        XRRayInteractor activeRayInteractor = GetActiveRayInteractor();
        
        if (activeRayInteractor == null)
            return;
        
        if (activeRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            GameObject hitObject = hit.collider.gameObject;
            
            // Update highlighted object
            if (lastHighlightedObject != hitObject)
            {
                lastHighlightedObject = hitObject;
                // Could add visual feedback here (outline, glow, etc.)
            }
        }
        else
        {
            lastHighlightedObject = null;
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
    
    // Public method to restore original colors (optional)
    public void RestoreOriginalColor(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        
        foreach (Renderer renderer in renderers)
        {
            if (originalColors.ContainsKey(renderer))
            {
                if (renderer.material.HasProperty("_Color"))
                {
                    renderer.material.color = originalColors[renderer];
                }
                else if (renderer.material.HasProperty("_BaseColor"))
                {
                    renderer.material.SetColor("_BaseColor", originalColors[renderer]);
                }
            }
        }
    }
}

[System.Serializable]
public class ColorMapping
{
    public string colorName;
    public Color color;
    public List<string> keywords = new List<string>();
}
