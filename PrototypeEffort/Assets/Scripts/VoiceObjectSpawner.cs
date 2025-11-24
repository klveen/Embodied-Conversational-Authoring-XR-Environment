using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class VoiceObjectSpawner : MonoBehaviour
{
    [Header("Dictation Reference")]
    [SerializeField] private DictationManager dictationManager;
    
    [Header("Ray Interactor References")]
    [SerializeField] private XRRayInteractor rightRayInteractor;
    [SerializeField] private XRRayInteractor leftRayInteractor;
    
    [Header("Object Library")]
    [SerializeField] private GameObject defaultCubePrefab;
    [SerializeField] private List<VoiceObjectMapping> objectMappings = new List<VoiceObjectMapping>();
    
    private void Start()
    {
        // Subscribe to final transcript event
        if (dictationManager != null)
        {
            dictationManager.OnFinalTranscript.AddListener(ProcessVoiceCommand);
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe when destroyed
        if (dictationManager != null)
        {
            dictationManager.OnFinalTranscript.RemoveListener(ProcessVoiceCommand);
        }
    }
    
    private void ProcessVoiceCommand(string transcript)
    {
        if (string.IsNullOrEmpty(transcript))
        {
            Debug.LogWarning("Empty transcript received");
            return;
        }
        
        // Convert to lowercase for easier matching
        string command = transcript.ToLower().Trim();
        Debug.Log($"Processing voice command: '{command}'");
        
        // Try to find matching object
        GameObject prefabToSpawn = FindMatchingPrefab(command);
        
        if (prefabToSpawn != null)
        {
            SpawnObjectAtRaycast(prefabToSpawn, command);
        }
        else
        {
            Debug.LogWarning($"No matching object found for '{command}', spawning default cube");
            SpawnObjectAtRaycast(defaultCubePrefab, "default cube");
        }
    }
    
    private GameObject FindMatchingPrefab(string command)
    {
        // Check each mapping for keyword matches
        foreach (var mapping in objectMappings)
        {
            foreach (var keyword in mapping.keywords)
            {
                if (command.Contains(keyword.ToLower()))
                {
                    Debug.Log($"Matched keyword '{keyword}' to prefab '{mapping.prefab.name}'");
                    return mapping.prefab;
                }
            }
        }
        
        return null; // No match found
    }
    
    private void SpawnObjectAtRaycast(GameObject prefab, string objectName)
    {
        if (prefab == null)
        {
            Debug.LogWarning("Cannot spawn null prefab");
            return;
        }
        
        // Determine which ray interactor to use
        XRRayInteractor activeRayInteractor = GetActiveRayInteractor();
        
        if (activeRayInteractor == null)
        {
            Debug.LogWarning("No active ray interactor found! Make sure you're in ray mode.");
            return;
        }
        
        // Try to get raycast hit from the active ray interactor
        if (activeRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            // Get the prefab's bounds to calculate proper height offset
            Renderer prefabRenderer = prefab.GetComponentInChildren<Renderer>();
            float heightOffset = 0f;
            
            if (prefabRenderer != null)
            {
                // Use half the height of the prefab's bounds so it sits on top of the plane
                heightOffset = prefabRenderer.bounds.extents.y;
            }
            
            // Calculate spawn position on top of the plane
            Vector3 spawnPosition = hit.point + (hit.normal * heightOffset);
            
            // Instantiate the prefab at the hit point
            GameObject spawnedObject = Instantiate(prefab, spawnPosition, Quaternion.identity);
            
            // Optional: Align rotation with surface normal
            spawnedObject.transform.up = hit.normal;
            
            Debug.Log($"Spawned '{objectName}' at {spawnPosition}");
        }
        else
        {
            Debug.LogWarning("Ray interactor did not hit any surface! Point at an AR plane.");
        }
    }
    
    private XRRayInteractor GetActiveRayInteractor()
    {
        // Prioritize right, fallback to left
        if (rightRayInteractor != null && rightRayInteractor.gameObject.activeInHierarchy)
        {
            return rightRayInteractor;
        }
        else if (leftRayInteractor != null && leftRayInteractor.gameObject.activeInHierarchy)
        {
            return leftRayInteractor;
        }
        
        return null;
    }
}

[System.Serializable]
public class VoiceObjectMapping
{
    public string objectName;
    public GameObject prefab;
    public List<string> keywords = new List<string>();
}
