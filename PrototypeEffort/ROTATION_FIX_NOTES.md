# Rotation and Floor Collision Fix

## Problem
After supervisor converted GLB models with Z-up orientation, chairs were:
1. Spawning on their side (wrong rotation)
2. Falling through the floor

## Root Cause
The Jupyter notebook converted ShapeNet models to:
- **up = Z+** (vertical axis)
- **front = X+** (forward direction)

Unity uses:
- **up = Y+** (vertical axis)  
- **forward = Z+** (forward direction)
- **right = X+** (right direction)

## Solution Applied

### 1. Correct Rotation (RuntimeModelLoader.cs line ~197)
Changed from:
```csharp
modelContainer.transform.localRotation = Quaternion.Euler(-90, 0, 0);
```

To:
```csharp
modelContainer.transform.localRotation = Quaternion.Euler(-90, 90, 0);
```

This properly converts:
- **-90° around X**: Makes Z-up become Y-up
- **+90° around Y**: Makes X-front become Z-forward

### 2. Fixed Floor Collision Issues

**Changes made:**
- Rigidbody now starts as **kinematic** (line ~283)
- Increased spawn delay from 100ms to **300ms** (line ~225)  
- Added detailed logging to debug position and collider setup
- Bounds are calculated **after** rotation (not before)

**Why objects fell through floor:**
- Gravity was enabled before colliders were fully initialized
- Physics system needs time to register new colliders
- Kinematic mode prevents falling during setup

## Testing Different Rotations

If chairs still appear incorrectly oriented, try these alternatives in RuntimeModelLoader.cs (line ~197):

```csharp
// Option 1: Current (X-front to Z-forward)
modelContainer.transform.localRotation = Quaternion.Euler(-90, 90, 0);

// Option 2: If chair faces backward
modelContainer.transform.localRotation = Quaternion.Euler(-90, -90, 0);

// Option 3: If chair is rotated 180°
modelContainer.transform.localRotation = Quaternion.Euler(-90, 270, 0);

// Option 4: Simple Z-up to Y-up only (original)
modelContainer.transform.localRotation = Quaternion.Euler(-90, 0, 0);

// Option 5: Different axis order
modelContainer.transform.localRotation = Quaternion.Euler(0, 90, 90);
```

## Debugging Tips

### Check Console Logs
Look for these messages:
```
[RuntimeModelLoader] Converting from ShapeNet (X-front, Z-up) to Unity (Z-forward, Y-up)
[RuntimeModelLoader] Bounds after rotation: center=..., size=..., min.y=...
[RuntimeModelLoader] Adjusted spawn position up by X.XXXm to sit bottom on surface
[RuntimeModelLoader] Collider ready - center: ..., size: ...
[RuntimeModelLoader] Physics enabled, final position: ...
```

### Visual Debugging
Enable bounding box visualization:
1. Select the RuntimeModelLoader component in Inspector
2. Check "Show Bounding Box"
3. Assign a transparent material to "Bounding Box Material"

### Common Issues

**Chair still on its side:**
- Try different rotation options above
- Check if notebook applied additional rotations

**Chair falls through floor:**
- Increase delay in line ~225 from 300ms to 500ms
- Check if AR plane is detected (look for floor classification in logs)
- Ensure PlaneCollisionDetector is working

**Chair spawns underground:**
- Check the bounds calculation - min.y should be negative for bottom of object
- Verify spawn position in logs

## Supervisor's Notebook Reference

The conversion in the notebook did:
```python
# Rotate mesh so up -> Z+ and front -> X+
up = parse_axis(up_str)      # From metadata
front = parse_axis(front_str)
# Build rotation matrix to align axes
```

Models in `inventory.csv` should all have this consistent orientation now.
