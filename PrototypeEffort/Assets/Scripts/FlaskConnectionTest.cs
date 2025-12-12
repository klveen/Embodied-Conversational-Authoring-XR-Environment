using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

/// <summary>
/// Tests connection to Flask server on PC.
/// Attach to any GameObject and check Console for results.
/// </summary>
public class FlaskConnectionTest : MonoBehaviour
{
    [Header("Server Configuration")]
    [SerializeField] private string serverIP = "192.168.178.74";
    [SerializeField] private int serverPort = 5000;
    
    [Header("Test Controls")]
    [SerializeField] private bool testOnStart = true;
    
    private string serverURL;
    
    private void Start()
    {
        serverURL = $"http://{serverIP}:{serverPort}";
        
        if (testOnStart)
        {
            TestConnection();
        }
    }
    
    [ContextMenu("Test Connection")]
    public void TestConnection()
    {
        StartCoroutine(TestPingEndpoint());
    }
    
    private IEnumerator TestPingEndpoint()
    {
        string url = $"{serverURL}/ping";
        
        Debug.Log($"[FlaskTest] Testing connection to: {url}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            // Allow insecure HTTP connections for local development
            request.certificateHandler = new AcceptAllCertificates();
            
            // Set timeout
            request.timeout = 5;
            
            // Send request
            yield return request.SendWebRequest();
            
            // Check result
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"✅ [FlaskTest] CONNECTION SUCCESS!");
                Debug.Log($"[FlaskTest] Response: {request.downloadHandler.text}");
            }
            else
            {
                Debug.LogError($"❌ [FlaskTest] CONNECTION FAILED!");
                Debug.LogError($"[FlaskTest] Error: {request.error}");
                Debug.LogError($"[FlaskTest] Response Code: {request.responseCode}");
                
                // Troubleshooting hints
                if (request.error.Contains("Cannot resolve"))
                {
                    Debug.LogWarning("[FlaskTest] Hint: Check if Flask server is running on PC");
                }
                else if (request.error.Contains("Connection refused"))
                {
                    Debug.LogWarning("[FlaskTest] Hint: Check Windows Firewall settings");
                }
                else if (request.error.Contains("timed out"))
                {
                    Debug.LogWarning("[FlaskTest] Hint: Check if Quest and PC are on same WiFi network");
                }
            }
        }
    }
}

// Certificate handler to allow HTTP connections (for local development only)
public class AcceptAllCertificates : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true; // Accept all certificates for local development
    }
}
