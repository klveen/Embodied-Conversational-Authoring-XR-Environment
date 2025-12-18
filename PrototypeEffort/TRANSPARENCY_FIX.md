# Fix: Material Not Transparent

## Problem
Alpha is set to 100 but material appears completely opaque (not see-through).

## Root Cause
**The shader isn't in Transparent mode!** Alpha values only work when the shader is configured for transparency.

---

## Solution: Configure Shader for Transparency

### **For URP (Universal Render Pipeline)**

1. **Select your material** in Project
2. In Inspector, find **Surface Type** dropdown
3. Change from **Opaque** → **Transparent** ✅

**Full settings**:
```
Shader: Universal Render Pipeline/Lit
Surface Type: Transparent (NOT Opaque!)
Blending Mode: Alpha
Render Face: Both
Base Color: 
  - R: 0
  - G: 0.6
  - B: 1.0
  - A: 0.4 (this is 40% transparent)
```

### **For Built-in Render Pipeline**

1. **Select your material** in Project
2. In Inspector, find **Rendering Mode** dropdown
3. Change from **Opaque** → **Transparent** or **Fade** ✅

**Full settings**:
```
Shader: Standard
Rendering Mode: Transparent (NOT Opaque!)
Color: 
  - R: 0
  - G: 150
  - B: 255
  - A: 100 (out of 255 = 39% transparent)
Metallic: 0
Smoothness: 0.3
```

---

## Step-by-Step Fix

### **1. Check Current Mode**

Select material → Inspector → Look for:
- **URP**: "Surface Type" at top
- **Built-in**: "Rendering Mode" dropdown

If it says **"Opaque"** → That's the problem!

### **2. Change to Transparent**

- **URP**: Surface Type → **Transparent**
- **Built-in**: Rendering Mode → **Transparent**

### **3. Adjust Alpha**

Now alpha will work! Adjust the **A** value:
- **URP**: Base Color → A slider → 0.4 (40% transparent)
- **Built-in**: Color → A slider → 100/255 (40% transparent)

### **4. Test**

Point at a spawned object:
- Should see **blue translucent box**
- Can see object **through** the box ✅

---

## Alpha Values Guide

Once shader is in Transparent mode:

| Alpha Value (URP) | Alpha Value (Built-in) | Transparency |
|-------------------|------------------------|--------------|
| 1.0 | 255 | Opaque (not see-through) |
| 0.7 | 180 | Slightly transparent |
| 0.5 | 128 | Half transparent |
| **0.4** | **100** | **Good for highlight** ✅ |
| 0.3 | 75 | Very transparent |
| 0.1 | 25 | Almost invisible |
| 0.0 | 0 | Completely invisible |

---

## Visual Comparison

### Before (Opaque Mode):
```
Alpha = 100 → Material is SOLID BLUE (can't see through)
```

### After (Transparent Mode):
```
Alpha = 100 → Material is TRANSLUCENT BLUE (can see object inside) ✅
```

---

## Common Mistakes

### **Mistake 1: Wrong Shader**
❌ Using "Unlit/Color" or other shader without transparency support
✅ Use "Universal Render Pipeline/Lit" or "Standard"

### **Mistake 2: Forgot to Switch Mode**
❌ Shader still in "Opaque" mode
✅ Must change to "Transparent" or "Fade"

### **Mistake 3: Alpha Too High**
❌ Alpha near 255/1.0 = almost opaque
✅ Use 100/255 or 0.4 for good transparency

### **Mistake 4: Wrong Render Face**
❌ "Render Face: Front" = can't see box from inside
✅ "Render Face: Both" = visible from all angles

---

## Quick Test

After fixing material:

1. **Hold material in hand** in Scene view
2. Put an object behind it
3. **Can you see through?** 
   - ✅ YES = Transparency working!
   - ❌ NO = Still in Opaque mode

---

## Alternative: Create New Material from Scratch

If still not working, start fresh:

### **URP:**
```
1. Right-click Project → Create → Material
2. Name: "TransparentBlue"
3. Shader: Universal Render Pipeline/Lit
4. Surface Type: Transparent
5. Base Color: RGBA(0, 150, 255, 100)
6. Done!
```

### **Built-in:**
```
1. Right-click Project → Create → Material
2. Name: "TransparentBlue"
3. Shader: Standard
4. Rendering Mode: Transparent
5. Color: RGBA(0, 150, 255, 100)
6. Done!
```

---

## Troubleshooting

### **Still Not Transparent After Changing Mode?**

1. **Save the material** (Ctrl+S)
2. **Restart Unity** (sometimes shader cache gets stuck)
3. **Check Render Pipeline** in Edit → Project Settings → Graphics
   - If using URP: Must use URP shaders
   - If using Built-in: Must use Built-in shaders

### **Box Appears Black Instead of Blue?**

- Check **Color/Base Color** is set to blue (not black!)
- Try enabling **Emission** with blue color

### **Box Too Transparent (Almost Invisible)?**

- Increase alpha: Try 0.6 (URP) or 150 (Built-in)
- Add **Emission**: Same blue color, intensity 0.5

### **Box Only Visible from One Side?**

- Change **Render Face** to **Both**
- Or **Cull Mode**: Off

---

## Recommended Final Settings

Copy these exactly:

### **URP (Recommended)**
```
Material: HighlightBoundingBox
Shader: Universal Render Pipeline/Lit
Surface Type: Transparent
Blending Mode: Alpha
Render Face: Both
Base Map: White (or none)
Base Color: 
  R: 0.0
  G: 0.588
  B: 1.0
  A: 0.4
Metallic: 0
Smoothness: 0.3
```

### **Built-in**
```
Material: HighlightBoundingBox
Shader: Standard
Rendering Mode: Transparent
Color: 
  R: 0
  G: 150
  B: 255
  A: 100
Metallic: 0
Smoothness: 0.3
```

---

## Why This Happens

Unity has different **rendering queues**:
- **Opaque**: Renders first, no transparency, no alpha
- **Transparent**: Renders last, supports alpha blending

When material is in **Opaque** mode, Unity **ignores** the alpha value completely!

You must **explicitly** tell Unity to render it transparently by changing the mode.
