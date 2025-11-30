# Runtime GLB Model Loading Setup Guide

## Overview
This system loads .glb files from disk at runtime and automatically wraps them in interactable shells with proper physics and XR grab functionality.

## Components Created

### 1. RuntimeModelLoader.cs
- Loads .glb files using GLTFast package
- Creates interactable shell with physics components
- Automatically generates BoxCollider based on mesh bounds
- Supports bounding box visualization for debugging

### 2. InteractableRoot Prefab (Manual Creation Required)

#### Steps to Create the Prefab:

1. **Create Empty GameObject** in Unity:
   - Right-click in Hierarchy → Create Empty
   - Name it: `InteractableModelRoot`

2. **Add Required Components**:
   - Add Component → Physics → Rigidbody
     - Mass: 1
     - Use Gravity: ✓ (checked)
     - Collision Detection: Continuous
   
   - Add Component → Physics → Box Collider
     - Leave default (will be auto-sized by RuntimeModelLoader)
   
   - Add Component → XR → XR Grab Interactable
     - Throw On Detach: ✓
     - Throw Smoothing Duration: 0.25
     - Track Position: ✓
     - Track Rotation: ✓

3. **Save as Prefab**:
   - Drag `InteractableModelRoot` from Hierarchy to Project window
   - Save in: `Assets/Prefabs/InteractableModelRoot.prefab`
   - Delete the GameObject from Hierarchy (keep only the prefab)

## Integration with VoiceObjectSpawner

The VoiceObjectSpawner has been updated to support runtime .glb loading:

### Setup in Unity Editor:

1. **Add RuntimeModelLoader Component**:
   - Select your scene's manager GameObject (or create one)
   - Add Component → Runtime Model Loader
   - Configure settings:
     - Interactable Root Prefab: Drag in the `InteractableModelRoot` prefab
     - Default Mass: 1
     - Use Gravity: ✓
     - Collider Padding: 0.02
     - Show Bounding Box: ✓ (for debugging, disable later)
     - Model Scale: 1

2. **Configure VoiceObjectSpawner**:
   - The spawner now has a `RuntimeModelLoader` reference field
   - Drag the GameObject with RuntimeModelLoader into this field
   - Add ShapeNet model mappings to the Object Mappings array:
     - Keywords: ["bed", "sleep", "bedroom"]
     - Prefab: Leave empty (will use runtime loading)
     - GLB Path: "bed/model_normalized.glb" (relative to shapenet_data folder)

## ShapeNet Model Path Structure

Your downloaded models are located at:
```
C:\Users\s2733099\shapenet_data\
├── bed\
│   ├── [model_id].glb
│   └── ...
├── chair\
│   ├── [model_id].glb
│   └── ...
└── ...
```

## Usage Example

### Option A: Voice Command (Integrated)
Simply say "spawn a bed" and the system will:
1. Detect the keyword "bed"
2. Find the GLB path from mappings
3. Load the model at runtime
4. Wrap it in an interactable shell
5. Place it at the raycast point

### Option B: Direct Script Call
```csharp
RuntimeModelLoader loader = GetComponent<RuntimeModelLoader>();
Vector3 spawnPos = new Vector3(0, 1, 2);
Quaternion spawnRot = Quaternion.identity;

// Load specific model
GameObject obj = await loader.LoadAndSpawnModel(
    "C:\\Users\\s2733099\\shapenet_data\\bed\\model.glb",
    spawnPos,
    spawnRot
);

// Or use convenience method for ShapeNet
GameObject obj2 = await loader.LoadShapeNetModel(
    "chair",           // category
    "model.glb",       // filename
    spawnPos,
    spawnRot
);
```

## Features

### Automatic Collider Generation
- Calculates combined bounds from all meshes in the model
- Creates BoxCollider sized to fit the entire model
- Adds configurable padding for better grab interaction
- Works with models of any size or complexity

### Physics & Interaction
- Rigidbody added automatically
- XRGrabInteractable configured for proper throw behavior
- Collision detection set to Continuous for fast-moving objects
- Supports both position and rotation tracking

### Debug Visualization
- Enable `showBoundingBox` to see the collision bounds
- Green transparent box shows exact collider size
- Useful for verifying bounds calculation

## Testing Steps

1. **Test Basic Loading**:
   - Enable `showBoundingBox` in RuntimeModelLoader
   - Say "spawn a bed"
   - Verify green bounding box appears around model
   - Verify you can grab and throw the object

2. **Test Different Categories**:
   - Add mappings for chair, table, lamp, etc.
   - Test each category
   - Verify colliders fit properly

3. **Adjust Scale if Needed**:
   - ShapeNet models may vary in size
   - Use `modelScale` parameter to adjust
   - Typical range: 0.5 to 2.0

## Troubleshooting

### Model not appearing:
- Check console for error messages
- Verify GLB file path is correct
- Ensure shapenet_data folder exists at: `C:\Users\s2733099\shapenet_data\`

### Collider too small/large:
- Adjust `colliderPadding` in RuntimeModelLoader
- Enable `showBoundingBox` to visualize
- Check if model scale needs adjustment

### Can't grab object:
- Verify XRGrabInteractable component is present
- Check that InteractionLayerMask matches your XR Rig setup
- Ensure BoxCollider is not disabled

### Model appears but is invisible:
- Check if materials loaded correctly
- Some GLB files may need specific shader setup
- Verify lighting in your scene

## Next Steps

1. Create the InteractableModelRoot prefab following steps above
2. Add RuntimeModelLoader component to your scene
3. Update VoiceObjectSpawner with model mappings
4. Test with downloaded ShapeNet models
5. Adjust scale and collider settings as needed
