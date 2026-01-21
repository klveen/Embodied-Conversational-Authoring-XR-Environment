# Complete Scripts Overview - AR Furniture Placement System

## Table of Contents
1. [Core LLM & Voice System](#core-llm--voice-system)
2. [Runtime Model Loading](#runtime-model-loading)
3. [AR Plane Management](#ar-plane-management)
4. [XR Interaction System](#xr-interaction-system)
5. [Physics & Placement](#physics--placement)
6. [Utility Scripts](#utility-scripts)
7. [Testing & Debug Scripts](#testing--debug-scripts)

---

## Core LLM & Voice System

### **VoiceObjectSpawner.cs** ⭐ CRITICAL
**Purpose**: Central orchestrator for voice-controlled furniture placement with LLM integration

**Key Responsibilities**:
- Voice command processing (local keyword matching + LLM server)
- Spawn furniture at raycast positions or relative to user ("in front of me")
- Object manipulation (delete, scale, color change, variation swapping)
- Object naming system (Chair_1_Brown, Table_2, etc.)
- Highlight system with translucent bounding boxes
- Spawn point caching (captures position when user speaks, uses it after LLM processing)

**Workflow**:
1. Receives voice transcript from DictationManager
2. **Simple commands** (delete, remove) → process locally (fast)
3. **Complex commands** (spawn pink chair) → send to Python LLM server
4. Captures spawn point BEFORE LLM processing (user doesn't wait with ray pointed)
5. LLM returns: action, objectName, color, modelId, relativePosition
6. Spawns object using RuntimeModelLoader or prefab
7. Updates UI text (visual + audio TTS)

**Important Features**:
- Dual mode: Ray interactor OR Direct grab for targeting objects
- Priority system: held object > pointed object > search by description
- Smart relative positioning ("in front", "behind", "left", "right")
- Object metadata tracking for color updates and identification
- TTS integration (speaks responses, auto-stops when user talks)
- **Color variations**: 27 colors with light/dark variants (e.g., light_blue, dark_red)
- **Material intelligence**: Translates materials to colors ("wood" → brown)
- **Highlight protection**: Bounding box stays blue when object color changes
- **Welcome message**: Explains voice recording from headset, not controller

**Connected Systems**:
- DictationManager → voice input
- PythonServerClient → LLM processing
- RuntimeModelLoader → GLB spawning
- TextToSpeechManager → audio feedback
- InventoryCSVReader → furniture catalog

---

### **DictationManager.cs** ⭐ CRITICAL
**Purpose**: Handles voice input using Meta Quest Voice SDK (Wit.ai)

**Key Responsibilities**:
- Manages AppVoiceExperience (Wit.ai integration)
- Button input handling (A button = record)
- Real-time transcription display (partial + final)
- Voice activity events (OnFinalTranscript, OnPartialTranscript)

**Workflow**:
1. User holds A button → starts voice recording
2. Partial transcripts update UI in real-time
3. User releases A button → final transcript sent to VoiceObjectSpawner
4. OnPartialTranscript triggers TTS stop (prevents AI talking over user)

**UI Integration**:
- Shows "Listening..." / "Processing..." status
- Displays live transcription as user speaks
- Connected to TextMeshPro UI elements

---

### **PythonServerClient.cs** ⭐ CRITICAL
**Purpose**: Network client for Python Flask LLM server communication

**Key Responsibilities**:
- Sends voice commands to Flask server (`http://192.168.178.74:5000/api/process_command`)
- Parses LLM responses (JSON → LLMResponse object)
- Health check endpoint (`/ping`) for connection testing
- Timeout handling (10s default)

**LLMResponse Structure**:
```csharp
{
    "action": "spawn" | "delete" | "scale" | "modify" | "query",
    "objectName": "chair",
    "color": "pink" | "light_blue" | "dark_red" | ...,  // 27 color variations
    "modelId": "abc123def456",
    "scaleFactor": 1.5,
    "relativePosition": "front" | "behind" | "left" | "right",
    "response": "Text response for conversational queries",
    "interactionMode": "ray" | "direct"
}
```

**Supported Colors** (27 total):
- Variations: red/light_red/dark_red, blue/light_blue/dark_blue, green/light_green/dark_green, yellow/light_yellow/dark_yellow, orange/light_orange/dark_orange, purple/light_purple/dark_purple, pink/light_pink/dark_pink, brown/light_brown/dark_brown
- Basic: white, black, gray

**Material to Color Mapping**:
- LLM automatically translates: "wood"→brown, "metal"→gray, "gold"→yellow, "silver"→gray, "marble"→white

**Platform Awareness**:
- PC: Uses local file paths for GLB loading
- Quest: Uses HTTP URLs for network loading from Flask server

**Error Handling**:
- Network failures → fallback to local keyword matching
- Timeout → returns error to callback
- JSON parsing errors → logged and reported

---

### **TextToSpeechManager.cs**
**Purpose**: Audio feedback using Meta Quest Voice SDK TTS

**Key Responsibilities**:
- Converts LLM text responses to speech
- Auto-stops previous speech when new response arrives
- Silent mode for loading messages ("Loading..." shown but not spoken)

**Voice Configuration**:
- Uses TTSSpeaker component
- Voice preset: "Charlie" (male) or "wit$Charlie" (customizable)
- Volume control (0-1 range)
- Spatial blend: 0 (2D audio, always audible)

**Integration**:
- Called by VoiceObjectSpawner.UpdateLLMResponseText(text, speak: true/false)
- Auto-stops when user starts speaking (via OnPartialTranscript event)
- Requires TTSWit component with Wit.ai configuration

---

## Runtime Model Loading

### **RuntimeModelLoader.cs** ⭐ CRITICAL
**Purpose**: Dynamic GLB loading with platform-aware network/local file support

**Key Responsibilities**:
- Loads .glb files at runtime using GLTFast
- Platform detection (PC = local files, Quest = HTTP network loading)
- Coordinate system conversion (ShapeNet Z-up → Unity Y-up)
- Automatic collider generation from mesh bounds
- Wraps models in interactable shell (Rigidbody + XRGrabInteractable)

**Critical Fix Applied**:
```csharp
// Rotation: ShapeNet Z-up to Unity Y-up
modelContainer.transform.localRotation = Quaternion.Euler(-90, 0, 0);

// Position: Calculate bounds AFTER rotation, then adjust to sit on ground
float heightAdjustment = -boundsCenterY;
rootObject.transform.position += Vector3.up * heightAdjustment;
```

**Workflow**:
1. LoadModel(glbPath, modelId, position, rotation) → auto-detects platform
2. **PC**: LoadAndSpawnModel(file://C:/path/to/model.glb)
3. **Quest**: LoadModelFromURL(http://192.168.178.74:5000/glb/modelId.glb)
4. GLTFast loads mesh → wraps in interactableRootPrefab
5. Applies -90° X rotation to fix orientation
6. Calculates bounds from mesh vertices (local space)
7. Adjusts position so object sits on ground
8. Adds components: Rigidbody, BoxCollider, FloorSnapper, PlaneCollisionDetector
9. 300ms delay → enable physics to prevent initial clipping

**Components Added to Spawned Objects**:
- **BoxCollider**: Generated from mesh bounds + padding
- **Rigidbody**: Mass=1kg, gravity enabled, starts kinematic
- **XRGrabInteractable**: Dynamic attach, velocity tracking, rotation constraints
- **FloorSnapper**: Auto-snaps to nearest floor plane on release
- **PlaneCollisionDetector**: Red highlight when clipping through surfaces

**Color Application**:
- `ApplyColorToObject()` changes all renderer materials EXCEPT highlight bounding box
- Preserves highlight box blue color while updating furniture
- Updates object metadata and naming (e.g., "Chair_1_Brown" → "Chair_1_Light_Blue")

**Bounding Box System**:
- Calculates combined bounds from all child meshes
- Uses actual vertex positions (not renderer bounds)
- Transforms to root local space for accurate sizing
- Padding: 0.02m around object

---

### **InventoryCSVReader.cs**
**Purpose**: Parses inventory.csv to populate furniture catalog

**CSV Format**:
```csv
fullId,category
100f39dce7690f59efb94709f30ce0d2,"Chair,Recliner"
abc123def456,"Table"
```

**Key Responsibilities**:
- Reads inventory.csv (downloaded from Flask server on Quest)
- Parses categories (main + subcategories)
- Converts to VoiceObjectMapping list
- Generates keywords for voice matching

**Example Mapping**:
```csharp
objectName: "Chair"
keywords: ["chair", "recliner", "seat"]
glbPath: "C:/ShapenetData/GLB/100f39dce7690f59efb94709f30ce0d2.glb"
modelId: "100f39dce7690f59efb94709f30ce0d2"
```

**Platform Handling**:
- **PC**: Reads from local file path
- **Quest**: Downloads CSV from Flask server to temporary cache

---

## AR Plane Management

### **SceneManager.cs**
**Purpose**: Controls AR plane visibility and detection

**Key Responsibilities**:
- Toggle plane visibility with joystick input
- Starts with planes INVISIBLE (0% alpha)
- Toggle states: 0% (hidden) ↔ 33% (semi-visible)
- Updates both mesh fill and LineRenderer outlines

**Visibility Levels**:
- **Hidden**: Fill alpha=0%, Line alpha=0% (default at startup)
- **Visible**: Fill alpha=33%, Line alpha=100%

**Event Handling**:
- Subscribes to ARPlaneManager.planesChanged
- Newly detected planes inherit current visibility state
- Prevents planes from appearing visible when detected

---

### **ARPlaneColorizer.cs**
**Purpose**: Sets initial appearance of AR planes when spawned

**Key Responsibilities**:
- Applies transparent material at spawn (alpha=0)
- Hides LineRenderer outlines (alpha=0)
- Configures collision based on plane type
- Updates when plane classification changes

**Collision Configuration**:
- **Floor/Wall**: Solid collision (isTrigger=false)
- **Table/Ceiling/Other**: Pass-through (isTrigger=true)

**Color System** (disabled, using transparency instead):
- Floor: Green (not used, now transparent)
- Wall: Blue
- Other: Red

---

### **PlaneCollisionDetector.cs**
**Purpose**: Visual feedback for invalid object placement

**Key Responsibilities**:
- Detects collisions with AR planes and other objects
- Shows red transparent material when clipping
- Restores original materials when collision resolved
- Spawned objects ignore each other (only collide with floor/walls)

**Material Management**:
- Stores original materials per renderer
- Creates instanced materials for color changes
- UpdateStoredMaterials() preserves user-applied colors

**Collision Logic**:
```csharp
OnTriggerEnter → Add to collidingWith set → Show invalid material
OnTriggerExit → Remove from set → If empty, restore materials
```

**Physics Setup**:
- Spawned objects on Layer 6
- Uses Physics.IgnoreCollision to prevent object-to-object blocking
- Floor/wall planes block movement, table/ceiling planes don't

---

## XR Interaction System

### **InteractorToggleManager.cs** ⭐ IMPORTANT
**Purpose**: Manages dual-mode interaction (Ray + Direct grabbing simultaneously)

**Key Responsibilities**:
- XRInteractionGroup priority management
- Direct interactor priority over Ray (when in range)
- Mode switching: Ray-only OR Direct-only modes
- Singleton access for LLM-driven mode changes

**Interaction Priority**:
1. **Direct Interactor** (priority=1): When hand near object
2. **Ray Interactor** (priority=0): When pointing from distance

**Mode Methods**:
- `SetDirectMode()`: Disables ray, enables only direct grab
- `SetRayMode()`: Disables direct, enables only ray
- LLM can trigger mode changes via VoiceObjectSpawner

**Why Both Active?**:
- User can grab object directly OR point from distance
- No need to manually switch modes
- Feels more natural and intuitive

---

### **FloorSnapper.cs**
**Purpose**: Automatic floor placement for furniture (Sims 4-style)

**Key Responsibilities**:
- Snaps objects to nearest floor plane on release
- Only snaps to Floor classification (not walls/tables)
- Initial snap after spawn (objects land on ground immediately)
- Smooth lerp movement option

**Snap Behavior**:
1. Object released in air → finds closest floor plane within 5m
2. Raycast down to find exact floor position
3. Smooth move to floor position (5 units/sec)
4. Optional: Keep object upright (align Y-up with surface normal)

**Settings**:
- maxSnapDistance: 5m (won't snap if too far from floor)
- hoverHeight: 0.01m (slight offset to prevent Z-fighting)
- keepUpright: true (prevents rotated furniture)

---

### **ProximityModeSwitch.cs**
**Purpose**: Automatic mode switching based on hand proximity (DEPRECATED)

**Status**: Replaced by InteractorToggleManager with simultaneous modes
**Original Purpose**: Auto-switch from ray to direct when hand near object

---

## Physics & Placement

### **ARPlanePhysics.cs**
**Purpose**: Configures physics colliders for AR planes

**Key Responsibilities**:
- Ensures AR planes have MeshCollider
- Convex=false for ground planes (accurate collision)
- Updates collider when plane mesh changes

---

### **PlacementController.cs**
**Purpose**: Grid-based placement system (UNUSED in current prototype)

**Status**: Prototype uses free placement with FloorSnapper instead
**Original Purpose**: Snap to grid points for precise alignment

---

### **PlacementSettings.cs**
**Purpose**: ScriptableObject for placement configuration (UNUSED)

---

## Utility Scripts

### **GLTFAssetLoader.cs**
**Purpose**: Simplified GLB loader wrapper (legacy, use RuntimeModelLoader instead)

---

### **ShapeNetMappingGenerator.cs**
**Purpose**: Editor utility to auto-generate VoiceObjectMapping entries

**Key Responsibilities**:
- Scans GLB folder for models
- Generates keywords from model names
- Creates mappings for inventory.csv

**Usage**: Editor-only, run once to populate initial inventory

---

### **XRObjectDeleter.cs**
**Purpose**: Simple delete with button press (replaced by voice commands)

**Status**: Legacy, VoiceObjectSpawner handles deletion now

---

### **VoiceColorChanger.cs**
**Purpose**: Standalone color change via voice (integrated into VoiceObjectSpawner)

**Status**: Legacy, functionality now in VoiceObjectSpawner.ModifyObjectColorAtRaycast()

---

## Testing & Debug Scripts

### **DebugLogDisplay.cs**
**Purpose**: On-screen log display for Quest testing

**Key Responsibilities**:
- Captures Debug.Log messages
- Displays in TextMeshPro UI
- Scrollable log history
- Useful for debugging without USB connection

---

### **DebugModelHierarchy.cs**
**Purpose**: Prints GameObject hierarchy structure to console

**Usage**: Debugging GLB model structure after import

---

### **FlaskConnectionTest.cs**
**Purpose**: Tests connection to Python Flask server

**Key Responsibilities**:
- Sends /ping request to verify server reachable
- Displays connection status in UI
- Useful for Quest network debugging

---

### **TestNetworkGLBLoading.cs**
**Purpose**: Standalone test for HTTP GLB loading

**Usage**: Testing network loading without full app

---

### **ARPlaneColorizerExample.cs**
**Purpose**: Example implementation of plane coloring

**Status**: Reference example, ARPlaneColorizer.cs is the active version

---

### **DictationUI.cs**
**Purpose**: UI controller for voice input display (alternative to built-in DictationManager UI)

---

## System Architecture Summary

### Data Flow: Voice Command → Object Spawn
```
User Speaks → DictationManager → VoiceObjectSpawner
                                         ↓
                         [Simple Command?] → Local Processing
                                         ↓
                         [Complex Command] → PythonServerClient
                                         ↓
                         Flask Server → LLM (GPT-4/Claude)
                                         ↓
                         LLMResponse → VoiceObjectSpawner
                                         ↓
                         RuntimeModelLoader → GLB Load
                                         ↓
                         Spawn Object → Apply Rotation/Position Fixes
                                         ↓
                         Add Components → FloorSnapper, Colliders, etc.
                                         ↓
                         TextToSpeechManager → Speak Response
```

### Critical File Paths
- **Inventory CSV**: `C:/Users/s2733099/ShapenetData/inventory.csv` (PC) or downloaded from Flask (Quest)
- **GLB Folder**: `C:/Users/s2733099/ShapenetData/GLB/` (PC)
- **Flask Server**: `http://192.168.178.74:5000` (Quest network loading)

### Coordinate System Conversions
- **ShapeNet/Blender**: X=front, Y=right, Z=up
- **Unity**: X=right, Y=up, Z=forward
- **Fix**: `Quaternion.Euler(-90, 0, 0)` applied to modelContainer

### Key Unity Layers
- **Layer 6**: Spawned furniture objects
- **Default Layer**: AR planes

### Dependencies
- **GLTFast**: Runtime GLB loading
- **XR Interaction Toolkit**: Grabbing, raycasting, controllers
- **AR Foundation**: Plane detection, tracking
- **Meta Voice SDK**: Speech-to-text (Wit.ai) and text-to-speech
- **TextMeshPro**: UI text rendering

---

## Current Issues & Limitations
1. ✅ **FIXED**: Chairs spawning on right side (rotation)
2. ✅ **FIXED**: Objects falling through floor (position adjustment)
3. ✅ **FIXED**: AR planes visible at startup (now start invisible)
4. ⚠️ **LIMITATION**: Delete by description not implemented (objectName + color search exists but not fully functional)
5. ⚠️ **LIMITATION**: TTS only works on Quest device, not in Unity Editor
6. ✅ **FIXED**: TTS auto-stops when user starts speaking
7. ✅ **FIXED**: Loading message no longer spoken (visual only)

---

## Future Enhancements (from user's vision)
- **Animation/Loading Indicator**: Show visual spinner during LLM processing
- **Full Delete by Description**: Enable "delete the pink chair" functionality
- **Model Variation Selection**: Allow users to ask for variations ("show me a different chair", "I want a bigger table")
  - LLM could present multiple model options before spawning
  - User specifies size/style preferences: "I want a small modern chair" vs "a large antique chair"
  - Pre-spawn preview system showing available variations
- **Multi-object Operations**: "Delete all chairs", "Change all tables to blue"
- **Undo/Redo System**: Restore deleted objects or revert changes
- **Save/Load Scenes**: Persist furniture arrangements
- **Collision-Based Placement**: Prevent overlapping furniture
- **Room Scanning**: Auto-detect room boundaries for better placement
