using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Reads inventory.csv to populate VoiceObjectMapping entries.
/// CSV format: fullId,category
/// Example: 100f39dce7690f59efb94709f30ce0d2,"Chair,Recliner"
/// </summary>
public class InventoryCSVReader
{
    public class InventoryEntry
    {
        public string fullId;           // Filename of GLB (e.g., "100f39dce7690f59efb94709f30ce0d2")
        public string mainCategory;     // Primary category (e.g., "Chair")
        public List<string> subCategories; // Additional categories (e.g., ["Recliner"])
        
        public InventoryEntry(string id, string categoryString)
        {
            fullId = id;
            subCategories = new List<string>();
            
            // Parse category string: "Chair,Recliner" or just "Chair"
            string[] categories = categoryString.Split(',');
            if (categories.Length > 0)
            {
                mainCategory = categories[0].Trim();
                
                // Add all categories as subcategories
                for (int i = 1; i < categories.Length; i++)
                {
                    string subCat = categories[i].Trim();
                    if (!string.IsNullOrEmpty(subCat))
                    {
                        subCategories.Add(subCat);
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets all categories (main + sub) as a combined list
        /// </summary>
        public List<string> GetAllCategories()
        {
            List<string> all = new List<string> { mainCategory };
            all.AddRange(subCategories);
            return all;
        }
    }
    
    /// <summary>
    /// Reads inventory.csv and returns list of inventory entries
    /// </summary>
    /// <param name="csvPath">Full path to inventory.csv</param>
    /// <returns>List of inventory entries</returns>
    public static List<InventoryEntry> ReadInventoryCSV(string csvPath)
    {
        List<InventoryEntry> entries = new List<InventoryEntry>();
        
        if (!File.Exists(csvPath))
        {
            Debug.LogError($"[InventoryCSVReader] File not found: {csvPath}");
            return entries;
        }
        
        try
        {
            string[] lines = File.ReadAllLines(csvPath);
            
            // Skip header line (fullId,category)
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                
                // Parse CSV line: fullId,category
                // Handle quoted categories like "Chair,Recliner"
                string[] parts = ParseCSVLine(line);
                
                if (parts.Length >= 2)
                {
                    string fullId = parts[0].Trim();
                    string category = parts[1].Trim().Trim('"'); // Remove quotes
                    
                    // Debug first entry to verify parsing
                    if (entries.Count == 0)
                    {
                        Debug.Log($"[InventoryCSVReader] First entry: ID='{fullId}' (len={fullId.Length}), Category='{category}'");
                    }
                    
                    entries.Add(new InventoryEntry(fullId, category));
                }
                else
                {
                    Debug.LogWarning($"[InventoryCSVReader] Skipping malformed line {i + 1}: {line}");
                }
            }
            
            Debug.Log($"[InventoryCSVReader] Successfully loaded {entries.Count} entries from {csvPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[InventoryCSVReader] Error reading CSV: {e.Message}");
        }
        
        return entries;
    }
    
    /// <summary>
    /// Parse CSV line handling quoted strings with commas
    /// </summary>
    private static string[] ParseCSVLine(string line)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        string current = "";
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current);
                current = "";
            }
            else
            {
                current += c;
            }
        }
        
        // Add last field
        if (!string.IsNullOrEmpty(current) || result.Count > 0)
        {
            result.Add(current);
        }
        
        return result.ToArray();
    }
    
    /// <summary>
    /// Converts inventory entries to VoiceObjectMapping list
    /// </summary>
    public static List<VoiceObjectMapping> ConvertToMappings(List<InventoryEntry> entries, string glbFolderPath)
    {
        List<VoiceObjectMapping> mappings = new List<VoiceObjectMapping>();
        int skippedCount = 0;
        
        foreach (var entry in entries)
        {
            // Create GLB path: C:/Users/s2733099/ShapenetData/GLB/{fullId}.glb
            string glbPath = Path.Combine(glbFolderPath, entry.fullId + ".glb");
            
            // Verify file exists
            if (!File.Exists(glbPath))
            {
                // Special logging for chair IDs to debug
                if (entry.mainCategory.ToLower() == "chair" && entry.fullId.Contains("66683c677"))
                {
                    Debug.LogError($"[InventoryCSVReader] CHAIR NOT FOUND: ID='{entry.fullId}' (len={entry.fullId.Length})");
                    Debug.LogError($"[InventoryCSVReader] Constructed path: '{glbPath}'");
                    Debug.LogError($"[InventoryCSVReader] Expected path: 'C:\\Users\\s2733099\\ShapenetData\\GLB\\66683c677e7b40593c2e50348f23d3d.glb'");
                }
                else if (skippedCount < 5) // Log first 5 missing files for debugging
                {
                    Debug.LogWarning($"[InventoryCSVReader] Missing: ID='{entry.fullId}' (length={entry.fullId.Length}), Path='{glbPath}'");
                }
                skippedCount++;
                continue;
            }
            
            VoiceObjectMapping mapping = new VoiceObjectMapping
            {
                objectName = entry.mainCategory, // Display name (e.g., "Chair")
                glbPath = glbPath, // Full path to GLB file (PC)
                modelId = entry.fullId, // ID for HTTP loading (Quest)
                keywords = new List<string>()
            };
            
            // Add all categories as keywords (lowercase for matching)
            foreach (string category in entry.GetAllCategories())
            {
                string keyword = category.ToLower();
                if (!mapping.keywords.Contains(keyword))
                {
                    mapping.keywords.Add(keyword);
                }
            }
            
            mappings.Add(mapping);
        }
        
        Debug.Log($"[InventoryCSVReader] Created {mappings.Count} mappings from inventory ({skippedCount} files not found)");
        return mappings;
    }
}
