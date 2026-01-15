using UnityEngine;

/// <summary>
/// Debug script to inspect the transform hierarchy of loaded GLB models.
/// Attach this to a spawned object to see all rotations in the hierarchy.
/// </summary>
public class DebugModelHierarchy : MonoBehaviour
{
    [ContextMenu("Print Transform Hierarchy")]
    public void PrintHierarchy()
    {
        Debug.Log("=== TRANSFORM HIERARCHY ===");
        PrintTransformRecursive(transform, 0);
    }

    private void PrintTransformRecursive(Transform t, int depth)
    {
        string indent = new string(' ', depth * 2);
        Debug.Log($"{indent}{t.name}:");
        Debug.Log($"{indent}  Position: {t.localPosition}");
        Debug.Log($"{indent}  Rotation: {t.localRotation.eulerAngles}");
        Debug.Log($"{indent}  Scale: {t.localScale}");
        
        foreach (Transform child in t)
        {
            PrintTransformRecursive(child, depth + 1);
        }
    }
    
    void Start()
    {
        // Auto-print on spawn
        Invoke("PrintHierarchy", 1f);
    }
}
