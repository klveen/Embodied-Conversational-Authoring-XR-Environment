using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Handles communication with the Python server for LLM processing and asset retrieval
/// </summary>
public class PythonServerClient : MonoBehaviour
{
    [Header("Server Configuration")]
    // TODO: Update this with your actual Python server URL
    [SerializeField] private string serverUrl = "http://localhost:5000"; // Change to your supervisor's server address
    
    [Header("API Endpoints")]
    // TODO: Update these endpoints based on your supervisor's API
    [SerializeField] private string llmEndpoint = "/api/process_command"; // Endpoint that processes voice commands with LLM
    // TODO: Use this endpoint if you need a separate asset retrieval endpoint
    // [SerializeField] private string assetEndpoint = "/api/get_asset"; // Endpoint that returns asset information
    
    [Header("Timeout Settings")]
    [SerializeField] private float requestTimeout = 10f; // Timeout for HTTP requests
    
    // Singleton pattern for easy access
    public static PythonServerClient Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Sends a voice command to the Python server for LLM processing
    /// </summary>
    /// <param name="voiceCommand">The transcribed voice command</param>
    /// <param name="onSuccess">Callback when LLM returns a result</param>
    /// <param name="onError">Callback if request fails</param>
    public void ProcessVoiceCommand(string voiceCommand, Action<LLMResponse> onSuccess, Action<string> onError)
    {
        StartCoroutine(ProcessVoiceCommandCoroutine(voiceCommand, onSuccess, onError));
    }
    
    private IEnumerator ProcessVoiceCommandCoroutine(string voiceCommand, Action<LLMResponse> onSuccess, Action<string> onError)
    {
        // TODO: Adjust the JSON structure based on your supervisor's API requirements
        // This is a common format, but your server might expect different fields
        string jsonData = JsonUtility.ToJson(new VoiceCommandRequest
        {
            command = voiceCommand,
            timestamp = DateTime.UtcNow.ToString("o")
        });
        
        Debug.Log($"[PythonServerClient] Sending voice command to server: {voiceCommand}");
        
        using (UnityWebRequest request = new UnityWebRequest(serverUrl + llmEndpoint, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            // TODO: If your server requires authentication, add headers here
            // request.SetRequestHeader("Authorization", "Bearer YOUR_API_KEY");
            
            request.timeout = (int)requestTimeout;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Debug.Log($"[PythonServerClient] LLM Response: {responseText}");
                
                try
                {
                    // TODO: Adjust this based on the actual response format from your supervisor's server
                    LLMResponse response = JsonUtility.FromJson<LLMResponse>(responseText);
                    onSuccess?.Invoke(response);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PythonServerClient] Failed to parse LLM response: {e.Message}");
                    onError?.Invoke($"Failed to parse response: {e.Message}");
                }
            }
            else
            {
                string errorMsg = $"Request failed: {request.error}";
                Debug.LogError($"[PythonServerClient] {errorMsg}");
                onError?.Invoke(errorMsg);
            }
        }
    }
    
    /// <summary>
    /// Downloads a GLTF asset from the provided URL
    /// </summary>
    /// <param name="assetUrl">URL or path to the GLTF file</param>
    /// <param name="onSuccess">Callback with the downloaded byte array</param>
    /// <param name="onError">Callback if download fails</param>
    public void DownloadGLTFAsset(string assetUrl, Action<byte[]> onSuccess, Action<string> onError)
    {
        StartCoroutine(DownloadGLTFAssetCoroutine(assetUrl, onSuccess, onError));
    }
    
    private IEnumerator DownloadGLTFAssetCoroutine(string assetUrl, Action<byte[]> onSuccess, Action<string> onError)
    {
        Debug.Log($"[PythonServerClient] Downloading GLTF asset from: {assetUrl}");
        
        // TODO: Check if the URL is relative or absolute
        // If relative, prepend server URL
        string fullUrl = assetUrl.StartsWith("http") ? assetUrl : serverUrl + assetUrl;
        
        using (UnityWebRequest request = UnityWebRequest.Get(fullUrl))
        {
            request.timeout = (int)requestTimeout;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                byte[] assetData = request.downloadHandler.data;
                Debug.Log($"[PythonServerClient] Successfully downloaded GLTF asset ({assetData.Length} bytes)");
                onSuccess?.Invoke(assetData);
            }
            else
            {
                string errorMsg = $"Asset download failed: {request.error}";
                Debug.LogError($"[PythonServerClient] {errorMsg}");
                onError?.Invoke(errorMsg);
            }
        }
    }
    
    /// <summary>
    /// Test connection to the Python server
    /// </summary>
    public void TestConnection(Action<bool> onResult)
    {
        StartCoroutine(TestConnectionCoroutine(onResult));
    }
    
    private IEnumerator TestConnectionCoroutine(Action<bool> onResult)
    {
        Debug.Log($"[PythonServerClient] Testing connection to: {serverUrl}");
        
        // TODO: Update with a health check endpoint if available
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/health"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            bool success = request.result == UnityWebRequest.Result.Success;
            Debug.Log($"[PythonServerClient] Connection test: {(success ? "SUCCESS" : "FAILED")}");
            onResult?.Invoke(success);
        }
    }
}

// ============================================================================
// DATA STRUCTURES
// TODO: Modify these based on your supervisor's actual API format
// ============================================================================

[Serializable]
public class VoiceCommandRequest
{
    public string command;
    public string timestamp;
    // TODO: Add any other fields your server expects, such as:
    // public string userId;
    // public string sessionId;
    // public Dictionary<string, string> metadata;
}

[Serializable]
public class LLMResponse
{
    // TODO: Update these fields based on what your supervisor's LLM returns
    public string objectName;        // Name of the object to spawn (e.g., "chair", "table")
    public string assetId;           // ShapeNet ID or unique identifier
    public string assetUrl;          // URL to the GLTF file
    public string category;          // Object category (optional)
    public float confidence;         // LLM confidence score (optional)
    
    // Optional: Position/rotation if LLM determines placement
    public Vector3Data position;
    public Vector3Data rotation;
    
    // Optional: Additional metadata
    public string description;
    public string[] tags;
    
    // NEW: Interaction mode control
    // TODO: Ask supervisor if LLM should control interaction mode
    // Values: "ray", "direct", or null/empty to not change
    public string interactionMode;
    
    // NEW: Color change support
    // TODO: Ask supervisor if LLM should handle color changes
    // Can be color name ("red", "blue") or RGB values
    public string color;
    public ColorRGB colorRGB;  // Alternative: RGB values
}

[Serializable]
public class Vector3Data
{
    public float x;
    public float y;
    public float z;
    
    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

[Serializable]
public class ColorRGB
{
    public float r;  // 0-1 or 0-255 (normalize in code)
    public float g;
    public float b;
    public float a = 1f;  // Alpha/transparency
    
    public Color ToColor()
    {
        // Handle both 0-1 and 0-255 ranges
        float maxValue = Mathf.Max(r, g, b);
        if (maxValue > 1f)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a > 1f ? a / 255f : a);
        }
        return new Color(r, g, b, a);
    }
}
