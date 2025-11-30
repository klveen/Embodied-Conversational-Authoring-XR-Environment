# Voice Color Changer - Setup Guide

## What It Does:
Changes the color of spawned objects by pointing at them with the ray and saying a color name.

---

## Features:

### 1. **Ray Mode Only**
- Only works when controllers are in ray (pointing) mode
- Automatically checks interaction mode before applying colors

### 2. **11 Built-in Colors**
- Red, Blue, Green, Yellow, White, Black, Orange, Purple, Pink, Gray, Brown
- Multi-language support (English and French keywords included)

### 3. **LLM Integration Ready**
- Toggle between local keywords and Python server
- LLM can return color names or RGB values

### 4. **Smart Material Handling**
- Works with Standard, URP, and custom shaders
- Stores original colors for potential restoration
- Applies to all child renderers

---

## Setup Instructions:

### Step 1: Add to Scene
1. Create empty GameObject named "VoiceColorChanger"
2. Add `VoiceColorChanger` component

### Step 2: Assign References
In Inspector:
- **Dictation Manager**: Drag your DictationManager GameObject
- **Right/Left Ray Interactor**: Drag from your controllers
- **Python Client**: (Optional) For LLM integration

### Step 3: Test Locally
1. Build and deploy to Quest
2. Spawn an object (cube/chair)
3. Point ray at the object
4. Say "red" or "blue" etc.
5. Object should change color!

---

## Usage Examples:

### Simple Color Change:
```
1. Spawn a cube
2. Point at cube with ray
3. Say "red"
→ Cube turns red
```

### Multiple Objects:
```
1. Spawn 3 chairs
2. Point at chair 1, say "blue"
3. Point at chair 2, say "green"
4. Point at chair 3, say "yellow"
→ Each chair has different color
```

---

## LLM Integration (Tomorrow):

### Option 1: Color Name (Simpler)
LLM returns:
```json
{
  "color": "red",
  "objectName": null  // Not spawning, just changing color
}
```

### Option 2: RGB Values (More Flexible)
LLM returns:
```json
{
  "colorRGB": {
    "r": 255,
    "g": 0,
    "b": 0,
    "a": 255
  }
}
```

### Combined Commands:
LLM can handle: "Spawn a red chair"
```json
{
  "objectName": "chair",
  "assetId": "12345",
  "color": "red"
}
```
This will spawn the chair AND make it red!

---

## Customization:

### Add More Colors:
In Inspector, expand "Color Mappings" and add:
- **Color Name**: "cyan"
- **Color**: Pick from color wheel
- **Keywords**: ["cyan", "light blue"]

### Add Multi-Language Support:
Add keywords in other languages:
- "red" → ["red", "rouge", "rojo", "rot"]

---

## Integration with VoiceObjectSpawner:

Both scripts listen to DictationManager independently:
- **VoiceObjectSpawner**: Spawns objects
- **VoiceColorChanger**: Changes colors

They work together! You can say:
- "Chair" → Spawns chair
- "Red" → Colors it red

Or with LLM: "Spawn a red chair" → Does both!

---

## Troubleshooting:

### "Color changing only works in ray mode!"
- Press A button to switch to ray mode
- Check InteractorToggleManager is set up

### "Ray did not hit any object!"
- Make sure you're pointing at an object
- Object must have a collider
- Check ray is visible and hitting the object

### "Object has no renderers!"
- Make sure object has a MeshRenderer
- Check if it's a parent with renderers on children

### Color doesn't change:
- Check material has `_Color` or `_BaseColor` property
- Some shaders might use different property names

---

## Tomorrow's LLM Updates:

1. **Update VoiceObjectSpawner** to apply color when spawning:
```csharp
// In ProcessVoiceCommandWithPython
if (!string.IsNullOrEmpty(response.color))
{
    // Apply color to spawned object
}
```

2. **Ask Supervisor**:
   - Should LLM handle color changes?
   - Color names or RGB values?
   - Combined spawn + color commands?

---

## Code Locations:

| What | File | Lines |
|------|------|-------|
| Color library | VoiceColorChanger.cs | 22-34 |
| Local matching | VoiceColorChanger.cs | 119-134 |
| Apply color | VoiceColorChanger.cs | 168-203 |
| LLM integration | VoiceColorChanger.cs | 93-111 |
| RGB support | PythonServerClient.cs | 195-209 |

---

Good luck! 🎨
