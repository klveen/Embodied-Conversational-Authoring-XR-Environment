using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Custom editor for VoiceObjectSpawner that adds "Auto-Generate ShapeNet Mappings" button.
/// </summary>
[CustomEditor(typeof(VoiceObjectSpawner))]
public class VoiceObjectSpawnerEditor : Editor
{
    private string shapeNetDataPath = "";
    private int maxModelsPerCategory = 5;
    private bool showAutoGenerateSettings = false;
    
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();
        
        VoiceObjectSpawner spawner = (VoiceObjectSpawner)target;
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("ShapeNet Auto-Generation", EditorStyles.boldLabel);
        
        showAutoGenerateSettings = EditorGUILayout.Foldout(showAutoGenerateSettings, "Auto-Generate Settings");
        
        if (showAutoGenerateSettings)
        {
            EditorGUI.indentLevel++;
            
            // Auto-detect path if empty
            if (string.IsNullOrEmpty(shapeNetDataPath))
            {
                shapeNetDataPath = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                    "shapenet_data"
                );
            }
            
            shapeNetDataPath = EditorGUILayout.TextField("ShapeNet Data Path", shapeNetDataPath);
            maxModelsPerCategory = EditorGUILayout.IntField("Max Models Per Category", maxModelsPerCategory);
            
            EditorGUI.indentLevel--;
        }
        
        if (GUILayout.Button("🔄 Auto-Generate ShapeNet Mappings", GUILayout.Height(30)))
        {
            GenerateShapeNetMappings(spawner);
        }
        
        if (GUILayout.Button("ℹ️ Scan ShapeNet Data (Info Only)", GUILayout.Height(25)))
        {
            ScanShapeNetData();
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "Auto-Generate will scan your shapenet_data folder and create mappings for:\n" +
            "basket, bed, bench, bookshelf, cabinet, chair, clock, lamp, sofa, table\n\n" +
            "All models in each category will share the category keyword for random selection.",
            MessageType.Info
        );
    }
    
    private void GenerateShapeNetMappings(VoiceObjectSpawner spawner)
    {
        if (!Directory.Exists(shapeNetDataPath))
        {
            EditorUtility.DisplayDialog(
                "Path Not Found",
                $"ShapeNet data path not found:\n{shapeNetDataPath}\n\nPlease check the path and try again.",
                "OK"
            );
            return;
        }
        
        List<string> categories = new List<string>
        {
            "basket", "bed", "bench", "bookshelf", "cabinet",
            "chair", "clock", "lamp", "sofa", "table"
        };
        
        // STEP 1: Copy GLB files to StreamingAssets (matching maxModelsPerCategory)
        string streamingAssetsPath = Path.Combine(Application.dataPath, "StreamingAssets", "shapenet_data");
        CopyGLBFilesToStreamingAssets(categories, streamingAssetsPath);
        
        // STEP 2: Clear existing mappings
        SerializedObject serializedSpawner = new SerializedObject(spawner);
        SerializedProperty mappingsProperty = serializedSpawner.FindProperty("objectMappings");
        mappingsProperty.ClearArray();
        
        int totalAdded = 0;
        
        // STEP 3: Generate mappings from StreamingAssets (now matches the files we just copied)
        foreach (string category in categories)
        {
            string categoryPath = Path.Combine(streamingAssetsPath, category);
            
            if (!Directory.Exists(categoryPath))
            {
                Debug.LogWarning($"Category folder not found in StreamingAssets: {categoryPath}");
                continue;
            }
            
            string[] glbFiles = Directory.GetFiles(categoryPath, "*.glb");
            
            if (glbFiles.Length == 0)
            {
                Debug.LogWarning($"No GLB files found in StreamingAssets: {categoryPath}");
                continue;
            }
            
            int modelsToAdd = glbFiles.Length; // Use exactly what's in StreamingAssets
            
            for (int i = 0; i < modelsToAdd; i++)
            {
                string glbFile = glbFiles[i];
                string fileName = Path.GetFileName(glbFile);
                
                // Add new mapping entry
                mappingsProperty.arraySize++;
                SerializedProperty newMapping = mappingsProperty.GetArrayElementAtIndex(mappingsProperty.arraySize - 1);
                
                newMapping.FindPropertyRelative("objectName").stringValue = $"{CapitalizeFirst(category)} #{i + 1}";
                newMapping.FindPropertyRelative("prefab").objectReferenceValue = null;
                newMapping.FindPropertyRelative("glbPath").stringValue = $"{category}/{fileName}";
                
                // Add keywords
                SerializedProperty keywordsProperty = newMapping.FindPropertyRelative("keywords");
                keywordsProperty.ClearArray();
                
                // Add category keyword
                keywordsProperty.arraySize = 1;
                keywordsProperty.GetArrayElementAtIndex(0).stringValue = category.ToLower();
                
                totalAdded++;
            }
            
            Debug.Log($"Added {modelsToAdd} models from category: {category}");
        }
        
        serializedSpawner.ApplyModifiedProperties();
        
        // Refresh AssetDatabase so Unity sees new StreamingAssets files
        AssetDatabase.Refresh();
        
        if (totalAdded > 0)
        {
            EditorUtility.DisplayDialog(
                "Mappings Generated!",
                $"Successfully generated {totalAdded} mappings!\n\n" +
                $"• Copied {maxModelsPerCategory} models per category to StreamingAssets\n" +
                $"• Created {totalAdded} object mappings\n" +
                $"• Cleared previous mappings\n\n" +
                "Ready to build for Quest 3!",
                "OK"
            );
            
            Debug.Log($"✓ Generated {totalAdded} ShapeNet mappings and copied GLB files to StreamingAssets");
        }
        else
        {
            EditorUtility.DisplayDialog(
                "No Mappings Generated",
                "No GLB files were found. Please check:\n\n" +
                "1. ShapeNet data path is correct\n" +
                "2. GLB files were downloaded\n" +
                "3. Categories exist in the folder",
                "OK"
            );
        }
    }
    
    private void CopyGLBFilesToStreamingAssets(List<string> categories, string streamingAssetsPath)
    {
        if (!Directory.Exists(streamingAssetsPath))
        {
            Directory.CreateDirectory(streamingAssetsPath);
        }
        
        int totalCopied = 0;
        
        foreach (string category in categories)
        {
            string sourceCategory = Path.Combine(shapeNetDataPath, category);
            string destCategory = Path.Combine(streamingAssetsPath, category);
            
            if (!Directory.Exists(sourceCategory))
            {
                Debug.LogWarning($"Source category not found: {sourceCategory}");
                continue;
            }
            
            // Get all GLB files from source
            string[] sourceFiles = Directory.GetFiles(sourceCategory, "*.glb");
            
            if (sourceFiles.Length == 0)
            {
                Debug.LogWarning($"No GLB files in source: {sourceCategory}");
                continue;
            }
            
            // Determine how many to copy
            int filesToCopy = (maxModelsPerCategory > 0) 
                ? Mathf.Min(sourceFiles.Length, maxModelsPerCategory) 
                : sourceFiles.Length;
            
            // Create destination folder
            if (!Directory.Exists(destCategory))
            {
                Directory.CreateDirectory(destCategory);
            }
            else
            {
                // Clear existing files in this category folder
                foreach (string existingFile in Directory.GetFiles(destCategory, "*.glb"))
                {
                    File.Delete(existingFile);
                }
            }
            
            // Copy files
            for (int i = 0; i < filesToCopy; i++)
            {
                string sourceFile = sourceFiles[i];
                string fileName = Path.GetFileName(sourceFile);
                string destFile = Path.Combine(destCategory, fileName);
                
                File.Copy(sourceFile, destFile, true);
                totalCopied++;
            }
            
            Debug.Log($"Copied {filesToCopy} GLB files for category: {category}");
        }
        
        Debug.Log($"Total GLB files copied to StreamingAssets: {totalCopied}");
    }
    
    private void ScanShapeNetData()
    {
        if (!Directory.Exists(shapeNetDataPath))
        {
            EditorUtility.DisplayDialog(
                "Path Not Found",
                $"ShapeNet data path not found:\n{shapeNetDataPath}",
                "OK"
            );
            return;
        }
        
        List<string> categories = new List<string>
        {
            "basket", "bed", "bench", "bookshelf", "cabinet",
            "chair", "clock", "lamp", "sofa", "table"
        };
        
        Debug.Log($"=== Scanning ShapeNet Data ===");
        Debug.Log($"Path: {shapeNetDataPath}");
        Debug.Log("");
        
        int totalModels = 0;
        
        foreach (string category in categories)
        {
            string categoryPath = Path.Combine(shapeNetDataPath, category);
            
            if (Directory.Exists(categoryPath))
            {
                string[] glbFiles = Directory.GetFiles(categoryPath, "*.glb");
                totalModels += glbFiles.Length;
                Debug.Log($"📁 {category}: {glbFiles.Length} models");
            }
            else
            {
                Debug.Log($"❌ {category}: folder not found");
            }
        }
        
        Debug.Log("");
        Debug.Log($"Total models available: {totalModels}");
        
        EditorUtility.DisplayDialog(
            "ShapeNet Scan Complete",
            $"Found {totalModels} models across {categories.Count} categories.\n\n" +
            "Check the Console for detailed breakdown.",
            "OK"
        );
    }
    
    private string CapitalizeFirst(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return char.ToUpper(text[0]) + text.Substring(1);
    }
}
