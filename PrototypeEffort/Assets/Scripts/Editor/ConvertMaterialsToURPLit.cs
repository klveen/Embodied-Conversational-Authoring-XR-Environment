using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Converts all Measured Materials Library materials from "Lit (Advanced)" to standard "URP/Lit"
/// This fixes Quest build errors caused by incompatible custom shaders
/// </summary>
public class ConvertMaterialsToURPLit : EditorWindow
{
    [MenuItem("Tools/Check Material Shaders")]
    static void CheckMaterialShaders()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials/Measured Materials Library" });
        System.Collections.Generic.Dictionary<string, int> shaderCounts = new System.Collections.Generic.Dictionary<string, int>();
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            
            if (mat != null && mat.shader != null)
            {
                string shaderName = mat.shader.name;
                if (!shaderCounts.ContainsKey(shaderName))
                    shaderCounts[shaderName] = 0;
                shaderCounts[shaderName]++;
            }
        }
        
        Debug.Log("=== SHADER USAGE IN MEASURED MATERIALS ===");
        foreach (var kvp in shaderCounts)
        {
            Debug.Log($"{kvp.Value} materials using: {kvp.Key}");
        }
    }
    
    [MenuItem("Tools/Convert Materials to URP/Lit")]
    static void ConvertAllMaterials()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials/Measured Materials Library" });
        int converted = 0;
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        
        if (urpLit == null)
        {
            Debug.LogError("Could not find Universal Render Pipeline/Lit shader!");
            return;
        }
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            
            if (mat != null && mat.shader != null)
            {
                string shaderName = mat.shader.name;
                
                // Convert any non-standard URP shader
                if (!shaderName.Equals("Universal Render Pipeline/Lit") && 
                    !shaderName.Equals("Universal Render Pipeline/Simple Lit"))
                {
                    Debug.Log($"Converting {mat.name} from {shaderName}");
                    mat.shader = urpLit;
                    EditorUtility.SetDirty(mat);
                    converted++;
                }
            }
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"<color=green>Converted {converted} materials to URP/Lit shader</color>");
        EditorUtility.DisplayDialog("Conversion Complete", 
            $"Successfully converted {converted} materials to Universal Render Pipeline/Lit shader.\n\nYour materials are now Quest-compatible!", 
            "OK");
    }
}
