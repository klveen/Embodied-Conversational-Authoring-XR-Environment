using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for VoiceObjectSpawner that adds inventory management buttons.
/// The old ShapeNet generation features have been replaced with inventory.csv loading.
/// </summary>
[CustomEditor(typeof(VoiceObjectSpawner))]
public class VoiceObjectSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();
        
        VoiceObjectSpawner spawner = (VoiceObjectSpawner)target;
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Inventory Management", EditorStyles.boldLabel);
        
        if (GUILayout.Button("🔄 Reload Inventory from CSV", GUILayout.Height(30)))
        {
            // Trigger reload via reflection since LoadInventoryFromCSV is private
            var method = typeof(VoiceObjectSpawner).GetMethod("LoadInventoryFromCSV", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(spawner, null);
                EditorUtility.DisplayDialog("Inventory Reloaded", 
                    "Inventory has been reloaded from CSV file.", "OK");
            }
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "The system now loads inventory from:\n" +
            "• inventory.csv - Contains model IDs and categories\n" +
            "• GLB folder - Contains .glb files matching the IDs\n\n" +
            "Configure paths in the Inspector fields above.",
            MessageType.Info
        );
    }
}
