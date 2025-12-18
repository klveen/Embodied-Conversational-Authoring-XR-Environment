# Bounding Box Highlight Setup

## Creating the Translucent Blue Material

### **Quick Setup**

1. **Create Material**:
   - Right-click in Project → Create → Material
   - Name it: `HighlightBoundingBox`

2. **Configure Material**:
   - **Rendering Mode**: Transparent (or Fade)
   - **Color**: Blue (e.g., RGB: 0, 150, 255)
   - **Alpha**: 100-150 (semi-transparent)
   - **Metallic**: 0
   - **Smoothness**: 0.5

3. **Assign to VoiceObjectSpawner**:
   - Select VoiceObjectSpawner GameObject
   - In Inspector → Object Highlighting
   - Drag `HighlightBoundingBox` material to **Highlight Material** field
   - Set **Bounding Box Padding**: 0.05 (adjust as needed)

---

## Material Settings (Detailed)

### **For Standard Shader (URP/Built-in)**

1. **Surface Type**: Transparent
2. **Blend Mode**: Alpha
3. **Base Color**: 
   - RGB: `(0, 150, 255)` - Bright blue
   - Alpha: `100` (out of 255) - Semi-transparent
4. **Metallic**: `0`
5. **Smoothness**: `0.3-0.5`
6. **Emission** (optional): 
   - Enable it for a glow
   - Color: Same blue `(0, 150, 255)`
   - Intensity: `0.5-1.0`

### **For URP Lit Shader**

1. **Workflow Mode**: Metallic
2. **Surface Type**: Transparent
3. **Blending Mode**: Alpha
4. **Base Map**: White (or leave empty)
5. **Base Color**: 
   - R: `0`, G: `0.59`, B: `1.0`, A: `0.4`
6. **Render Face**: Both (so box is visible from inside)

---

## How It Works

### **When You Point at an Object**

```
Update() → Every frame
    ↓
UpdateRaycastHighlight()
    ↓
Ray hits spawned object?
    ↓ YES
HighlightObject()
    1. Calculate bounds of all renderers
    2. Create cube primitive
    3. Scale to match bounds + padding
    4. Apply translucent material
    5. Parent to object (follows movement)
    ↓ NO
ClearHighlight()
    - Destroy bounding box
```

### **Visual Result**

- Translucent blue cube surrounds the entire object
- Padding creates slight gap between object and box
- Box rotates with object
- Box disappears when you look away

---

## Customization Options

### **Change Box Color**

In the material:
- **Blue**: (0, 150, 255)
- **Green**: (0, 255, 100)
- **Yellow**: (255, 255, 0)
- **Cyan**: (0, 255, 255)
- **Purple**: (150, 0, 255)

### **Adjust Transparency**

In the material's Base Color Alpha:
- **Very transparent**: 50-80
- **Normal**: 100-150 (recommended)
- **Opaque**: 200-255

### **Adjust Box Size**

In VoiceObjectSpawner Inspector:
- **Bounding Box Padding**: 
  - `0.02` - Tight fit
  - `0.05` - Normal (default)
  - `0.10` - Loose fit (more visible)

### **Add Glow Effect**

Enable Emission in material:
1. Check **Emission**
2. **Emission Color**: Same as base color
3. **Emission Intensity**: 0.5-2.0

---

## Testing

### **Test 1: Basic Highlight**
1. Spawn a chair
2. Point controller at it
3. **Expected**: Blue translucent box appears around chair

### **Test 2: Multiple Objects**
1. Spawn chair and table
2. Point at chair → box appears
3. Point at table → box switches to table
4. Point at floor → box disappears

### **Test 3: Complex Objects**
1. Spawn a large sofa or desk
2. Point at it
3. **Expected**: Box perfectly encompasses entire object with padding

---

## Troubleshooting

### **No Box Appears**

**Problem**: Pointing at object but no highlight

**Solutions**:
1. ✅ Check if **Highlight Material** is assigned in Inspector
2. ✅ Make sure objects have "SpawnedObject" tag
3. ✅ Verify material is transparent (Alpha < 255)
4. ✅ Check Console for warnings

### **Box Too Small/Large**

**Problem**: Box doesn't fit object properly

**Solutions**:
- Too small: Increase **Bounding Box Padding** (try 0.1)
- Too large: Decrease **Bounding Box Padding** (try 0.02)
- Weird shape: Object might have hidden renderers

### **Box Not Visible**

**Problem**: System works but can't see the box

**Solutions**:
1. Increase material Alpha (make less transparent)
2. Change color to more visible (try yellow)
3. Enable Emission in material
4. Check if box is being created (Debug.Log in HighlightObject)

### **Box Doesn't Follow Object**

**Problem**: Object moves but box stays in place

**This shouldn't happen** - box is parented to object. If it does:
- Check Console for errors
- Verify object has Transform component

### **Performance Issues**

**Problem**: Frame drops when looking at objects

**Solutions**:
- Use simpler material (no emission)
- Reduce padding (smaller box = less render cost)
- Already optimized: Only one box at a time

---

## Advanced: Wireframe Box

For a **wireframe look** instead of solid translucent:

### **Option A: Use Line Renderer** (More Complex)
Would require custom script to draw lines along box edges.

### **Option B: Use Transparent Unlit Shader**
1. Create shader with:
   - Rim lighting (edges brighter)
   - Very low alpha in center
   - Bright alpha at edges
2. Gives "holographic" effect

### **Option C: Keep Current + Increase Transparency**
1. Set material Alpha to 30-50 (very transparent)
2. Enable strong Emission
3. Result: Box outline is visible, interior barely visible

---

## For Your Supervisor

### **Why Bounding Box vs Glow?**

**Advantages**:
1. **Clearer boundaries** - Easy to see full object extent
2. **Works with any shader** - No emission keyword issues
3. **Performance** - Single cube vs modifying all materials
4. **Customizable** - Easy to swap materials without code changes

**UX Benefits**:
- Clear visual affordance (this object is selectable)
- Works in bright/dark environments
- Industry standard (similar to CAD software selection)

### **Technical Achievement**

- Dynamic bounds calculation across complex hierarchies
- Parent-child relationship for synchronized movement
- Memory efficient (one cube vs cloned materials)
- Fully customizable through Inspector (no code changes needed)

---

## Quick Material Recipe

**Copy-paste these values**:

```
Shader: Universal Render Pipeline/Lit
Surface Type: Transparent
Blending Mode: Alpha
Base Color: RGBA(0, 0.6, 1.0, 0.4)
Metallic: 0
Smoothness: 0.3
Render Face: Both
```

**Or for Built-in Pipeline**:

```
Shader: Standard
Rendering Mode: Transparent
Color: RGBA(0, 150, 255, 100)
Metallic: 0
Smoothness: 0.3
```

---

## Alternative Materials to Try

1. **Neon Style**:
   - Color: Bright cyan (0, 255, 255)
   - Alpha: 150
   - Emission: Same color, intensity 2.0
   - Result: Glowing tech look

2. **Subtle Selection**:
   - Color: White (255, 255, 255)
   - Alpha: 50 (very transparent)
   - Emission: Light blue, intensity 0.5
   - Result: Minimal but visible

3. **Warning Style**:
   - Color: Yellow (255, 255, 0)
   - Alpha: 120
   - No emission
   - Result: Caution/attention grabbing

4. **Hologram Style**:
   - Color: Cyan (0, 255, 255)
   - Alpha: 80
   - Emission: Same, intensity 1.5
   - Smoothness: 0.8 (shiny)
   - Result: Sci-fi aesthetic
