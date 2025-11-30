# Python Server Integration - Setup Guide

## What I Created Today:

### 1. PythonServerClient.cs
- Handles HTTP communication with Python server
- Sends voice commands to LLM
- Downloads GLTF assets from server
- Includes connection testing

### 2. Modified VoiceObjectSpawner.cs
- Added toggle between local keywords and Python server
- New method: `ProcessVoiceCommandWithPython()` - sends to server
- Fallback to local system if server fails
- Prepared for GLTF runtime loading

### 3. GLTFAssetLoader.cs (Template)
- Placeholder for GLTF loading logic
- Prepare objects for AR (colliders, rigidbody)
- Ready for library integration

---

## TODO Tomorrow:

### Step 1: Get Info from Supervisor
Ask your supervisor for:
- [ ] Python server URL/IP address
- [ ] API endpoint paths (e.g., `/api/process_command`)
- [ ] Request/response JSON format examples
- [ ] Authentication requirements (API keys?)
- [ ] How assets are served (URLs, IDs, etc.)

### Step 2: Update PythonServerClient.cs
**Line 12:** Change `serverUrl`
```csharp
[SerializeField] private string serverUrl = "http://YOUR_SUPERVISORS_SERVER:PORT";
```

**Line 16-17:** Update endpoint paths
```csharp
[SerializeField] private string llmEndpoint = "/api/YOUR_ENDPOINT";
[SerializeField] private string assetEndpoint = "/api/YOUR_ASSET_ENDPOINT";
```

**Line 51-55:** Update request JSON format to match supervisor's API
```csharp
// Example - adjust based on actual format
string jsonData = JsonUtility.ToJson(new VoiceCommandRequest
{
    command = voiceCommand,
    // Add any other fields your server needs
});
```

**Line 81:** Update response parsing to match supervisor's format
```csharp
LLMResponse response = JsonUtility.FromJson<LLMResponse>(responseText);
```

**Line 138-147:** Update `LLMResponse` class fields
```csharp
[Serializable]
public class LLMResponse
{
    // Update these to match what your supervisor's LLM returns
    public string objectName;
    public string assetId;
    public string assetUrl;
    // Add/remove fields as needed
}
```

### Step 3: Choose Asset Loading Strategy

**OPTION A: Pre-import ShapeNet assets (Simpler)**
1. Import all GLTF files into Unity before building
2. Create prefabs from them
3. Map ShapeNet IDs to prefabs
4. In `VoiceObjectSpawner.cs`, line 62, uncomment:
```csharp
GameObject prefab = FindPrefabByAssetId(response.assetId);
if (prefab != null) SpawnObjectAtRaycast(prefab, response.objectName);
```

**OPTION B: Runtime GLTF loading (More flexible)**
1. Install GLTF loader package:
   - GLTFast: Unity Package Manager → Add from git URL → `https://github.com/atteneder/glTFast.git`
   - OR UnityGLTF from GitHub
2. Implement `GLTFAssetLoader.cs` → `LoadGLTF()` method
3. In `VoiceObjectSpawner.cs`, line 65, uncomment:
```csharp
pythonClient.DownloadGLTFAsset(response.assetUrl, 
    (byte[] gltfData) => LoadAndSpawnGLTF(gltfData, response.objectName),
    (error) => Debug.LogError($"Failed to download GLTF: {error}"));
```

### Step 4: Test Connection
In Unity:
1. Create empty GameObject named "PythonServerClient"
2. Add `PythonServerClient` component
3. Update `serverUrl` in Inspector
4. In Play mode, run this in Console or create test button:
```csharp
PythonServerClient.Instance.TestConnection((success) => {
    Debug.Log(success ? "Connected!" : "Connection failed!");
});
```

### Step 5: Wire Everything Together
1. In VoiceObjectSpawner Inspector:
   - Check "Use Python Server" checkbox
   - Assign PythonServerClient reference
2. Test with voice command
3. Check Console for logs

---

## Troubleshooting

### "Connection refused"
- Check server URL is correct
- Make sure Python server is running
- Check firewall settings
- Try `http://localhost:5000` if testing locally

### "Failed to parse response"
- Print the raw response: `Debug.Log(responseText);`
- Update `LLMResponse` class to match actual JSON structure
- Use online JSON validator to check format

### "Asset download failed"
- Check if asset URL is correct (absolute vs relative)
- Verify GLTF files are accessible from Unity
- Check network permissions on Quest

---

## Code Locations for Tomorrow

| What to Change | File | Line(s) |
|----------------|------|---------|
| Server URL | PythonServerClient.cs | 12 |
| API Endpoints | PythonServerClient.cs | 16-17 |
| Request Format | PythonServerClient.cs | 51-55 |
| Response Format | PythonServerClient.cs | 138-147 |
| Enable Python Mode | VoiceObjectSpawner.cs | Inspector checkbox |
| Asset Loading Logic | VoiceObjectSpawner.cs | 62-68 |
| GLTF Loader Implementation | GLTFAssetLoader.cs | 21-40 |

---

## Questions to Ask Supervisor

1. "What's the server URL and port?"
2. "What JSON format should I send for voice commands?"
3. "What does the LLM response look like?"
4. "How are GLTF assets provided - URLs, file paths, or IDs?"
5. "Do I need authentication headers?"
6. "Is there a health check endpoint I can use?"
7. "Are ShapeNet models already converted to GLTF?"
8. **NEW:** "Should the LLM be able to switch between ray and direct interaction modes?"

---

## NEW FEATURE: LLM-Controlled Interaction Mode

The LLM can now control whether users interact via ray (pointing) or direct (grabbing)!

### How It Works:
- User says: "Switch to grab mode" or "I want to point at objects"
- LLM includes `"interactionMode": "direct"` or `"interactionMode": "ray"` in response
- Unity automatically switches controller modes

### Supported Mode Values:
- **Ray mode:** "ray", "raycast", "point", "pointer"
- **Direct mode:** "direct", "grab", "hand", "touch"

### Setup:
1. In `LLMResponse` (PythonServerClient.cs line 166), the `interactionMode` field is ready
2. Ask your supervisor to include this field in LLM responses (optional)
3. If field is null/empty, mode doesn't change

### Example LLM Response:
```json
{
  "objectName": "chair",
  "assetId": "12345",
  "assetUrl": "/assets/chair.gltf",
  "interactionMode": "ray"  // <-- This switches to ray mode!
}
```

---

Good luck tomorrow! 🚀
