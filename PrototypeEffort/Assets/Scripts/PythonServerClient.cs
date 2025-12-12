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
    [SerializeField] private string serverUrl = "http://localhost:5000";
    
    [Header("API Endpoints")]
    [SerializeField] private string llmEndpoint = "/api/process_command";
    [SerializeField] private string healthEndpoint = "/ping";
    
    [Header("Timeout Settings")]
    [SerializeField] private float requestTimeout = 10f;
    
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
        string jsonData = JsonUtility.ToJson(new VoiceCommandRequest
        {
            command = voiceCommand,
            timestamp = DateTime.UtcNow.ToString("o")
        });
        
        Debug.Log($"[PythonServerClient] Sending to LLM: {voiceCommand}");
        
        using (UnityWebRequest request = new UnityWebRequest(serverUrl + llmEndpoint, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = (int)requestTimeout;
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Debug.Log($"[PythonServerClient] LLM Raw Response: {responseText}");
                
                try
                {
                    LLMResponse response = JsonUtility.FromJson<LLMResponse>(responseText);
                    Debug.Log($"[PythonServerClient] Parsed response: {response.response}");
                    onSuccess?.Invoke(response);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PythonServerClient] Parse error: {e.Message}");
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
        
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + healthEndpoint))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            bool success = request.result == UnityWebRequest.Result.Success;
            
            if (success)
            {
                Debug.Log($"[PythonServerClient] ✓ Connection successful: {request.downloadHandler.text}");
            }
            else
            {
                Debug.LogError($"[PythonServerClient] ✗ Connection failed: {request.error}");
            }
            
            onResult?.Invoke(success);
        }
    }
}

// ============================================================================
// DATA STRUCTURES - OPTION A (Simple Text Response)
// ============================================================================

[Serializable]
public class VoiceCommandRequest
{
    public string command;
    public string timestamp;
}

[Serializable]
public class LLMResponse
{
    // OPTION A: Simple text response from LLM
    public string response;        // Free-form text response from LLM
    public string command;         // Echo of the original command
    
    // OPTION B fields (will be populated when we upgrade)
    public string action;          // "spawn", "delete", "switch_mode", "query"
    public string objectName;      // "chair", "table", etc.
    public string assetId;         // ShapeNet ID
    public string assetUrl;        // URL to GLTF file
    public string category;        // Object category
    public int quantity;           // Number to spawn
    public string interactionMode; // "ray" or "direct"
    public string color;           // "red", "blue", etc.
    public float confidence;       // LLM confidence score
    
    // Optional fields
    public Vector3Data position;
    public Vector3Data rotation;
    public string description;
    public string[] tags;
    public ColorRGB colorRGB;
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
