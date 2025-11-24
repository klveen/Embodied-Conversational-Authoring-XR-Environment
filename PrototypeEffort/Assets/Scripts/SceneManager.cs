using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(ARPlaneManager))]
public class SceneManager : MonoBehaviour
{
    [Header("AR Plane Settings")]
    [SerializeField] private InputActionReference togglePlanesAction;
    private ARPlaneManager arPlaneManager;
    private bool isPlaneVisible = true;

    [Header("Cube Spawn Settings")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private InputActionReference spawnCubeAction;
    [SerializeField] private InteractorToggleManager interactorManager;
    
    [Header("Ray Interactor References")]
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.XRRayInteractor rightRayInteractor;
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.XRRayInteractor leftRayInteractor;

    void Start()
    {
        arPlaneManager = GetComponent<ARPlaneManager>();
        
        // Enable toggle planes action
        if (togglePlanesAction != null)
        {
            togglePlanesAction.action.performed += OnTogglePlanesAction;
        }
        
        // Enable spawn cube action
        if (spawnCubeAction != null)
        {
            spawnCubeAction.action.performed += OnSpawnCube;
        }
    }
    
    private void OnEnable()
    {
        togglePlanesAction.action?.Enable();
        spawnCubeAction.action?.Enable();
    }
    
    private void OnDisable()
    {
        togglePlanesAction.action?.Disable();
        spawnCubeAction.action?.Disable();
    }

    private void OnTogglePlanesAction(InputAction.CallbackContext context)
    {
        isPlaneVisible = !isPlaneVisible;
        float fillAlpha = isPlaneVisible ? 0.33f : 0f;
        float lineAlpha = isPlaneVisible ? 1.0f : 0f;

        foreach (var plane in arPlaneManager.trackables)
        {
            SetPlaneVisibility(plane, fillAlpha, lineAlpha);
        }
    }   
    
    private void SetPlaneVisibility(ARPlane plane, float fillAlpha, float lineAlpha)
    {
        var meshRenderer = plane.GetComponent<MeshRenderer>();
        var lineRenderer = plane.GetComponent<LineRenderer>();

        if (meshRenderer != null)
        {
            Color fillColor = meshRenderer.material.color;
            fillColor.a = fillAlpha;
            meshRenderer.material.color = fillColor;
        }

        if (lineRenderer != null)
        {
            Color startColor = lineRenderer.startColor;
            Color endColor = lineRenderer.endColor;

            startColor.a = lineAlpha;
            endColor.a = lineAlpha;

            lineRenderer.startColor = startColor;
            lineRenderer.endColor = endColor;
        }
    }
    
    private void OnSpawnCube(InputAction.CallbackContext context)
    {
        if (cubePrefab == null)
        {
            Debug.LogWarning("Cube prefab not assigned!");
            return;
        }
        
        // Determine which ray interactor to use (prioritize right, fallback to left)
        UnityEngine.XR.Interaction.Toolkit.XRRayInteractor activeRayInteractor = null;
        
        if (rightRayInteractor != null && rightRayInteractor.gameObject.activeInHierarchy)
        {
            activeRayInteractor = rightRayInteractor;
        }
        else if (leftRayInteractor != null && leftRayInteractor.gameObject.activeInHierarchy)
        {
            activeRayInteractor = leftRayInteractor;
        }
        
        if (activeRayInteractor == null)
        {
            Debug.LogWarning("No active ray interactor found! Make sure you're in ray mode.");
            return;
        }
        
        // Try to get raycast hit from the active ray interactor
        if (activeRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            // Get the prefab's bounds to calculate proper height offset
            Renderer prefabRenderer = cubePrefab.GetComponentInChildren<Renderer>();
            float heightOffset = 0f;
            
            if (prefabRenderer != null)
            {
                // Use half the height of the prefab's bounds so it sits on top of the plane
                heightOffset = prefabRenderer.bounds.extents.y;
            }
            
            // Calculate spawn position on top of the plane
            Vector3 spawnPosition = hit.point + (hit.normal * heightOffset);
            
            // Instantiate the prefab at the hit point
            GameObject spawnedObject = Instantiate(cubePrefab, spawnPosition, Quaternion.identity);
            
            // Optional: Align rotation with surface normal
            spawnedObject.transform.up = hit.normal;
            
            Debug.Log($"Spawned cube at {spawnPosition}");
        }
        else
        {
            Debug.LogWarning("Ray interactor did not hit any surface!");
        }
    }
}   