using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Automatically generates VoiceObjectMapping entries from ShapeNet GLB files.
/// Scans the shapenet_data folder and creates mappings based on folder structure.
/// </summary>
public class ShapeNetMappingGenerator : MonoBehaviour
{
    [Header("ShapeNet Data Path")]
    [Tooltip("Path to shapenet_data folder (usually in user home directory)")]
    [SerializeField] private string shapeNetDataPath = "";
    
    [Header("Category Settings")]
    [Tooltip("Which categories to scan (leave empty to scan all)")]
    [SerializeField] private List<string> categoriesToInclude = new List<string>
    {
        "basket", "bed", "bench", "bookshelf", "cabinet", 
        "chair", "clock", "lamp", "sofa", "table"
    };
    
    [Header("Generation Settings")]
    [Tooltip("Maximum number of models per category (0 = unlimited)")]
    [SerializeField] private int maxModelsPerCategory = 5;
    
    [Tooltip("Add category name as keyword (e.g., 'bed' for all bed models)")]
    [SerializeField] private bool addCategoryKeyword = true;
    
    [Header("Output")]
    [Tooltip("Reference to VoiceObjectSpawner to populate")]
    [SerializeField] private VoiceObjectSpawner voiceObjectSpawner;
    
    private void Start()
    {
        // Auto-detect shapenet_data path if not set
        if (string.IsNullOrEmpty(shapeNetDataPath))
        {
            shapeNetDataPath = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "shapenet_data"
            );
        }
    }
    
    /// <summary>
    /// Scans ShapeNet folder and generates mappings. Call this from Inspector or code.
    /// </summary>
    [ContextMenu("Generate Mappings from ShapeNet Data")]
    public void GenerateMappings()
    {
        if (voiceObjectSpawner == null)
        {
            Debug.LogError("VoiceObjectSpawner reference is not set!");
            return;
        }
        
        if (!Directory.Exists(shapeNetDataPath))
        {
            Debug.LogError($"ShapeNet data path not found: {shapeNetDataPath}");
            return;
        }
        
        List<VoiceObjectMapping> generatedMappings = new List<VoiceObjectMapping>();
        int totalModels = 0;
        
        // Scan each category folder
        foreach (string category in categoriesToInclude)
        {
            string categoryPath = Path.Combine(shapeNetDataPath, category);
            
            if (!Directory.Exists(categoryPath))
            {
                Debug.LogWarning($"Category folder not found: {categoryPath}");
                continue;
            }
            
            // Get all GLB files in this category
            string[] glbFiles = Directory.GetFiles(categoryPath, "*.glb");
            
            if (glbFiles.Length == 0)
            {
                Debug.LogWarning($"No GLB files found in: {categoryPath}");
                continue;
            }
            
            // Limit number of models if specified
            int modelsToAdd = (maxModelsPerCategory > 0) 
                ? Mathf.Min(glbFiles.Length, maxModelsPerCategory) 
                : glbFiles.Length;
            
            // Create mapping for each GLB file
            for (int i = 0; i < modelsToAdd; i++)
            {
                string glbFile = glbFiles[i];
                string fileName = Path.GetFileName(glbFile);
                string modelId = Path.GetFileNameWithoutExtension(glbFile);
                
                VoiceObjectMapping mapping = new VoiceObjectMapping
                {
                    objectName = $"{CapitalizeFirst(category)} #{i + 1}",
                    prefab = null, // Runtime loading, no prefab needed
                    glbPath = $"{category}/{fileName}",
                    keywords = new List<string>()
                };
                
                // Add category keyword (e.g., "bed" matches all beds)
                if (addCategoryKeyword)
                {
                    mapping.keywords.Add(category.ToLower());
                }
                
                // Add optional variant keywords based on position
                if (i == 0) mapping.keywords.Add($"first {category}");
                if (i == modelsToAdd - 1) mapping.keywords.Add($"last {category}");
                
                generatedMappings.Add(mapping);
                totalModels++;
            }
            
            Debug.Log($"Added {modelsToAdd} models from category: {category}");
        }
        
        // Apply to VoiceObjectSpawner
        if (generatedMappings.Count > 0)
        {
            Debug.Log($"Successfully generated {totalModels} mappings across {categoriesToInclude.Count} categories!");
            Debug.Log("Note: You need to manually copy these to VoiceObjectSpawner.objectMappings in the Inspector.");
            Debug.Log("Listing generated mappings:");
            
            foreach (var mapping in generatedMappings)
            {
                Debug.Log($"  - {mapping.objectName}: {mapping.glbPath} (Keywords: {string.Join(", ", mapping.keywords)})");
            }
        }
        else
        {
            Debug.LogWarning("No mappings were generated. Check your shapenet_data path and categories.");
        }
    }
    
    /// <summary>
    /// Prints information about available ShapeNet models.
    /// </summary>
    [ContextMenu("Scan ShapeNet Data (Info Only)")]
    public void ScanShapeNetData()
    {
        if (!Directory.Exists(shapeNetDataPath))
        {
            Debug.LogError($"ShapeNet data path not found: {shapeNetDataPath}");
            return;
        }
        
        Debug.Log($"Scanning ShapeNet data at: {shapeNetDataPath}");
        
        foreach (string category in categoriesToInclude)
        {
            string categoryPath = Path.Combine(shapeNetDataPath, category);
            
            if (Directory.Exists(categoryPath))
            {
                string[] glbFiles = Directory.GetFiles(categoryPath, "*.glb");
                Debug.Log($"Category '{category}': {glbFiles.Length} models found");
                
                // Show first 3 filenames as examples
                for (int i = 0; i < Mathf.Min(3, glbFiles.Length); i++)
                {
                    Debug.Log($"  - {Path.GetFileName(glbFiles[i])}");
                }
                if (glbFiles.Length > 3)
                {
                    Debug.Log($"  ... and {glbFiles.Length - 3} more");
                }
            }
            else
            {
                Debug.LogWarning($"Category '{category}': folder not found");
            }
        }
    }
    
    private string CapitalizeFirst(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return char.ToUpper(text[0]) + text.Substring(1);
    }
}
