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
        
        // Get existing mappings
        SerializedObject serializedSpawner = new SerializedObject(spawner);
        SerializedProperty mappingsProperty = serializedSpawner.FindProperty("objectMappings");
        
        int totalAdded = 0;
        int startingCount = mappingsProperty.arraySize;
        
        foreach (string category in categories)
        {
            string categoryPath = Path.Combine(shapeNetDataPath, category);
            
            if (!Directory.Exists(categoryPath))
            {
                Debug.LogWarning($"Category folder not found: {categoryPath}");
                continue;
            }
            
            string[] glbFiles = Directory.GetFiles(categoryPath, "*.glb");
            
            if (glbFiles.Length == 0)
            {
                Debug.LogWarning($"No GLB files found in: {categoryPath}");
                continue;
            }
            
            int modelsToAdd = (maxModelsPerCategory > 0)
                ? Mathf.Min(glbFiles.Length, maxModelsPerCategory)
                : glbFiles.Length;
            
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
        
        if (totalAdded > 0)
        {
            EditorUtility.DisplayDialog(
                "Mappings Generated!",
                $"Successfully added {totalAdded} new mappings!\n\n" +
                $"Previous count: {startingCount}\n" +
                $"New count: {mappingsProperty.arraySize}\n\n" +
                "Check the Object Mappings list in the inspector.",
                "OK"
            );
            
            Debug.Log($"✓ Generated {totalAdded} ShapeNet mappings across {categories.Count} categories");
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
