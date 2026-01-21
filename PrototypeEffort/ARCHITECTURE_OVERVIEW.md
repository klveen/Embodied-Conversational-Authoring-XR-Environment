# LLM-Driven Object Retrieval & Spawning System

## System Architecture Overview

This system enables contextually-aware furniture spawning in VR using Claude AI to select specific 3D models based on natural language commands.

---

## Component Overview

### 1. **Inventory Database** (`inventory.csv`)
- **Location**: `C:\Users\s2733099\ShapenetData\inventory.csv`
- **Format**: `modelId,categories`
- **Example**: `6625567b0c22800524dc97938ca5e893,"Chair,OfficeChair"`
- **Size**: 2,786 unique 3D models
- **Purpose**: Maps model IDs to categories/subcategories for LLM selection

### 2. **Flask Server** (`llm_server.py`)
- **URL**: `http://192.168.178.74:5000`
- **Functions**:
  - Loads `inventory.csv` at startup into memory
  - Receives voice commands from Unity
  - Sends inventory context + command to Claude AI
  - Returns selected `modelId` + action to Unity
  - Serves GLB files: `GET /glb/<modelId>`
  - Serves inventory CSV: `GET /inventory.csv`

### 3. **Claude AI (Anthropic API)**
- **Model**: `claude-sonnet-4-0`
- **Input**: Voice command + relevant inventory entries
- **Output**: JSON with `action`, `objectName`, `modelId`, `color`, etc.
- **Personality**: Friendly and conversational, keeps responses short and natural
- **Intelligence**: 
  - Chooses contextually appropriate models
    - "chair for my desk" → Selects OfficeChair variant
    - "chair to relax" → Selects Recliner variant
  - Translates materials to colors automatically
    - "make it wood" → Applies brown + explains substitution
  - Offers subcategory options when helpful
    - "spawn a chair" → "I have office chairs, dining chairs, and recliners - which would you like?"
  - Provides helpful guidance
    - "how do I place objects?" → Explains raycast and relative positioning
  - **Note**: Color modification only - model variation swapping not available

### 4. **Unity VR Application**
- **Platform Detection**: 
  - **PC**: Loads GLB from local file system
  - **Quest 3**: Downloads GLB via HTTP from Flask
- **Components**:
  - `VoiceObjectSpawner.cs` - Command processing
  - `RuntimeModelLoader.cs` - GLB loading & orientation correction
  - `InventoryCSVReader.cs` - CSV parsing (PC fallback)
  - `PythonServerClient.cs` - Flask communication

---

## Complete Data Flow

### Step-by-Step Execution

```
1. USER SPEAKS
   ↓
   "Put a chair in front of my desk"
   ↓

2. UNITY (VoiceObjectSpawner.cs)
   - DictationManager captures speech
   - Raycast captures spawn position
   - Sends to Flask: POST /api/process_command
   ↓

3. FLASK SERVER (llm_server.py)
   - Receives: {"command": "put a chair in front of my desk"}
   - Loads relevant inventory entries for "chair"
   - Builds context with ~150 chair model IDs
   ↓

4. CLAUDE AI (Anthropic API)
   - Receives prompt with inventory context
   - Analyzes: "desk" → needs OfficeChair
   - Tool call: spawn_furniture
   - Returns: {
       "action": "spawn",
       "objectName": "chair",
       "modelId": "6625567b0c22800524dc97938ca5e893",
       "color": null
     }
   ↓

5. FLASK RETURNS TO UNITY
   - Unity receives LLMResponse with modelId
   ↓

6. UNITY SPAWNS MODEL
   
   IF PC (Windows):
   - Constructs path: C:\Users\...\GLB\6625567b0c22800524dc97938ca5e893.glb
   - Loads via glTFast from local file
   
   IF QUEST 3 (Android):
   - Constructs URL: http://192.168.178.74:5000/glb/6625567b0c22800524dc97938ca5e893
   - Downloads GLB via HTTP
   - Loads via glTFast from downloaded data
   ↓

7. ORIENTATION CORRECTION
   - Analyzes mesh vertices
   - Finds axis with most "bottom mass"
   - Auto-rotates if needed
   ↓

8. PHYSICS & INTERACTION SETUP
   - Adds BoxCollider
   - Adds Rigidbody
   - Adds XRGrabInteractable
   - Spawns at raycast position
   ↓

9. OBJECT APPEARS IN VR
   - Properly oriented (bottom on floor)
   - Grabbable with VR controllers
   - Has physics enabled
```

---

## Automatic Orientation Correction

### Problem
ShapeNet models have inconsistent orientations:
- Some chairs have legs pointing along X axis
- Some tables have top facing Z axis
- Some sofas are rotated 90° or lying on their side

### Solution: Bottom Mass Analysis

**Algorithm** (`RuntimeModelLoader.CorrectModelOrientation`):

```
1. Collect all mesh vertices from the model
2. Calculate center of mass (average position)
3. For each axis (X, Y, Z):
   - Count vertices below center of mass
   - Weight by distance below center
   - Sum = "bottom mass" for that axis
4. The axis with highest bottom mass = natural "down" direction
5. Rotate model so that axis becomes Unity's -Y (down)
```

**Example**:
```
Chair loaded with legs pointing along X axis:
- X bottom mass: 8542.3 (legs below center)
- Y bottom mass: 234.1 (small amount)
- Z bottom mass: 891.5 (some below)

→ X has most bottom mass
→ Rotate 90° around Z axis
→ X axis becomes Y axis
→ Chair now stands upright!
```

**Why This Works**:
- **Chairs**: Legs are always at bottom → High bottom mass on leg axis
- **Tables**: Legs at bottom → High bottom mass on leg axis
- **Couches**: Base/feet at bottom → High bottom mass on base axis
- **Desks**: Legs at bottom → High bottom mass on leg axis
- **Beds**: Frame at bottom → High bottom mass on frame axis

**Implementation**:
```csharp
private void CorrectModelOrientation(GameObject modelContainer, Bounds bounds)
{
    // Calculate bottom mass for X, Y, Z axes
    // Rotate so axis with most bottom mass becomes -Y (down)
    
    if (X has most bottom mass)
        Rotate 90° around Z → X becomes Y
    else if (Z has most bottom mass)
        Rotate -90° around X → Z becomes Y
    else
        Y already correct, no rotation needed
}
```

---

## Key Design Decisions

### 1. **Why LLM Selects Model ID (Not Unity)**
- **Context Awareness**: LLM understands "chair for desk" vs "chair to relax"
- **Semantic Matching**: Can match "office" to "OfficeChair" subcategory
- **Scalability**: Adding new categories doesn't require Unity code changes
- **Natural Language**: Handles synonyms, descriptions, context automatically

### 2. **Why Flask Loads Inventory (Not Unity on Quest)**
- **Memory**: 2,786 entries too large for Quest startup
- **Network**: Only transfers selected model (not entire inventory)
- **Flexibility**: Flask can filter/sample inventory before sending to Claude
- **Updates**: Inventory changes don't require rebuilding Quest app

### 3. **Why Platform-Specific Loading**
- **PC Development**: Fast local file access for testing
- **Quest Deployment**: Can't access PC file system, requires HTTP
- **Shared Code**: Same `RuntimeModelLoader.LoadModel()` handles both
- **Automatic**: Detects `RuntimePlatform.Android` at runtime

### 4. **Why Bottom Mass (Not Bounding Box)**
- **Robust**: Works regardless of model proportions
- **Accurate**: Finds actual contact surface with floor
- **Universal**: Works for chairs, tables, desks, couches, beds
- **Physics-Based**: Uses actual mesh geometry, not approximate bounds

---

## File Structure

```
PrototypeEffort/
├── Assets/
│   └── Scripts/
│       ├── VoiceObjectSpawner.cs        # Main spawning controller
│       ├── RuntimeModelLoader.cs        # GLB loading + orientation
│       ├── InventoryCSVReader.cs        # CSV parsing
│       └── PythonServerClient.cs        # Flask communication
│
├── flask_server/
│   └── llm_server.py                    # Flask + Claude integration
│
└── ARCHITECTURE_OVERVIEW.md             # This file

External:
C:\Users\s2733099\ShapenetData\
├── inventory.csv                         # Model database (2,786 entries)
└── GLB\                                  # 3D model files
    ├── 03001627_abc123.glb
    ├── 6625567b0c22800524dc97938ca5e893.glb
    └── ... (2,786 files)
```

---

## API Endpoints

### Flask Server (`http://192.168.178.74:5000`)

| Endpoint | Method | Purpose | Request | Response |
|----------|--------|---------|---------|----------|
| `/ping` | GET | Health check | - | `{"status": "ok"}` |
| `/api/process_command` | POST | Process voice command | `{"command": "text"}` | `{"action": "spawn", "modelId": "...", ...}` |
| `/glb/<id>` | GET | Serve GLB file | - | Binary GLB file |
| `/inventory.csv` | GET | Serve inventory | - | CSV text file |

---

## Unity Data Structures

### LLMResponse
```csharp
public class LLMResponse
{
    public string action;       // "spawn", "delete", "modify", "scale", "query"
    public string objectName;   // "chair", "table", etc.
    public string modelId;      // "6625567b0c22800524dc97938ca5e893"
    public string color;        // "red", "light_blue", "dark_green", etc. (27 variations)
    public int quantity;        // 1, 2, 3...
    public float scaleFactor;   // 0.8 (smaller), 1.2 (bigger), etc.
    public string relativePosition; // "front", "behind", "left", "right"
    public string response;     // Conversational responses from LLM
}
```

### VoiceObjectMapping (Legacy Fallback)
```csharp
public class VoiceObjectMapping
{
    public string objectName;           // Display name
    public string glbPath;              // Full path (PC)
    public string modelId;              // ID (Quest)
    public List<string> keywords;       // Search keywords
}
```

---

## Color System

### Available Colors (27 Total)

**Color Variations** (light/dark for most colors):
- Red: `red`, `light_red`, `dark_red`
- Blue: `blue`, `light_blue`, `dark_blue`
- Green: `green`, `light_green`, `dark_green`
- Yellow: `yellow`, `light_yellow`, `dark_yellow`
- Orange: `orange`, `light_orange`, `dark_orange`
- Purple: `purple`, `light_purple`, `dark_purple`
- Pink: `pink`, `light_pink`, `dark_pink`
- Brown: `brown`, `light_brown`, `dark_brown`

**Basic Colors** (no variations):
- `white`, `black`, `gray`

### Material Intelligence

The LLM automatically translates material requests to colors:
- "wood"/"wooden" → brown ("Making it brown like wood!")
- "metal"/"steel"/"metallic" → gray ("Going with gray for that metallic look!")
- "gold"/"golden" → yellow ("Golden yellow coming up!")
- "silver" → gray
- "bronze"/"copper" → orange or brown
- "marble" → white

### Highlight Box Protection

When changing object colors, the translucent blue highlight bounding box is automatically excluded from color changes, maintaining consistent visual feedback.

---

## Configuration

### Unity Inspector Settings

**VoiceObjectSpawner**:
- `inventoryCSVPath`: `C:/Users/s2733099/ShapenetData/inventory.csv`
- `glbFolderPath`: `C:/Users/s2733099/ShapenetData/GLB`
- `pythonClient`: Reference to PythonServerClient component

**PythonServerClient**:
- `serverUrl`: `http://192.168.178.74:5000`

**RuntimeModelLoader**:
- `flaskServerURL`: `http://192.168.178.74:5000`
- `modelScale`: `1.0` (no scaling)

### Flask Configuration

**llm_server.py**:
```python
GLB_FOLDER = r"C:\Users\s2733099\ShapenetData\GLB"
INVENTORY_CSV = r"C:\Users\s2733099\ShapenetData\inventory.csv"
MODEL_NAME = "claude-sonnet-4-0"
```

---

## Performance Considerations

### Inventory Loading
- **Flask startup**: ~0.5s to load 2,786 entries
- **Claude context**: ~150 entries sent per request (filtered by category)
- **Token usage**: ~1,000 tokens per request (inventory + command)

### GLB Loading
- **PC**: <100ms (local file)
- **Quest**: 500-2000ms (HTTP download, file size dependent)
- **Average GLB size**: ~200KB - 2MB

### Orientation Correction
- **Vertex analysis**: 10-50ms (depends on mesh complexity)
- **One-time cost**: Only on initial spawn
- **Memory**: Temporary vertex array (~10-100KB)

---

## Error Handling

### Network Failures (Quest)
- Flask server unreachable → Error logged, spawn fails gracefully
- GLB download timeout → Error logged, object not spawned
- CSV download fails → Uses empty inventory, falls back to random selection

### Model Loading Failures
- GLB file missing → Error logged, spawn cancelled
- Corrupt GLB file → glTFast error, object cleaned up
- No mesh data → Warning logged, keeps object anyway

### Orientation Correction Failures
- No vertices found → Skips correction, uses default orientation
- Ambiguous bottom mass → Chooses axis with slight majority
- Invalid rotation → Logs warning, uses identity rotation

---

## Future Enhancements

### Potential Improvements
1. **Caching**: Store downloaded GLBs on Quest for faster re-spawning
2. **Preloading**: Download common models during app startup
3. **Smart Inventory**: Track which models look best, prioritize those
4. **Material Preservation**: Keep original textures/colors from GLB
5. **Multi-Model Scenes**: "put 4 chairs around the table"
6. **Spatial Reasoning**: "put chair to the left of the desk"

### Known Limitations
- No texture/material preservation (uses fallback material)
- Orientation correction assumes single contact surface
- No handling of asymmetric models (e.g., L-shaped couches)
- Flask serves single-threaded (one request at a time)

---

## Debugging Tips

### Check Flask Logs
```
[Inventory] Loaded 2786 models across 45 categories
[LLM Server] Received: put a chair in front of my desk
[GLB Server] Serving: 6625567b0c22800524dc97938ca5e893.glb (456789 bytes)
```

### Check Unity Logs
```
[VoiceObjectSpawner] LLM selected model: name='chair', modelId='6625567b...'
[RuntimeModelLoader] Quest detected - loading from: http://192.168.178.74:5000/glb/...
[RuntimeModelLoader] Bottom mass - X: 234.1, Y: 8542.3, Z: 891.5
[RuntimeModelLoader] Orientation correct: Y axis already has bottom
```

### Common Issues
| Problem | Cause | Solution |
|---------|-------|----------|
| 404 errors | Flask not serving GLB endpoint | Restart Flask server |
| Empty modelId | Old build without modelId support | Rebuild Unity app |
| Wrong orientation | Bottom mass calculation failed | Check mesh has vertices |
| Pink materials | Fallback material missing | Assign in RuntimeModelLoader inspector |

---

## Credits & Technologies

- **Unity XR Interaction Toolkit**: VR interactions
- **glTFast**: GLB/glTF runtime loading
- **Anthropic Claude**: AI model selection
- **Flask**: Python web server
- **ShapeNet**: 3D model dataset source

---

*Last Updated: December 19, 2025*
*System Version: 2.0 - LLM-Driven Context-Aware Spawning*
