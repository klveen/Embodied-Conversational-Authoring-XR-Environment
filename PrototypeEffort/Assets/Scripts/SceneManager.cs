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
    private bool isPlaneVisible = false;

    void Start()
    {
        arPlaneManager = GetComponent<ARPlaneManager>();
        
        // Set initial plane visibility (hidden by default)
        foreach (var plane in arPlaneManager.trackables)
        {
            SetPlaneVisibility(plane, 0f, 0f);
        }
        
        // Enable toggle planes action
        if (togglePlanesAction != null)
        {
            togglePlanesAction.action.performed += OnTogglePlanesAction;
        }
    }
    
    private void OnEnable()
    {
        togglePlanesAction.action?.Enable();
        
        // Subscribe to plane detection events to hide newly detected planes
        if (arPlaneManager != null)
        {
            arPlaneManager.planesChanged += OnPlanesChanged;
        }
    }
    
    private void OnDisable()
    {
        togglePlanesAction.action?.Disable();
        
        // Unsubscribe from plane events
        if (arPlaneManager != null)
        {
            arPlaneManager.planesChanged -= OnPlanesChanged;
        }
    }
    
    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        // Hide newly added planes if visibility is off
        if (!isPlaneVisible)
        {
            foreach (var plane in args.added)
            {
                SetPlaneVisibility(plane, 0f, 0f);
            }
        }
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
}   