using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


[RequireComponent(typeof(ARPlane))]
[RequireComponent(typeof(MeshRenderer))]

public class ARPlaneColorizer : MonoBehaviour
{
        ARPlane m_ARPlane;
        MeshRenderer m_PlaneMeshRenderer;
        
        void Awake()
        {
            m_ARPlane = GetComponent<ARPlane>();
            m_PlaneMeshRenderer = GetComponent<MeshRenderer>();
        }

        void Start()
        {
           UpdatePlaneColor();
           HidePlaneOutline(); // Hide outline at start
           ConfigurePlaneCollision();
        }
        
        private PlaneClassification lastClassification = PlaneClassification.None;
        
        void Update()
        {
            // Only update if classification changed
            if (m_ARPlane.classification != lastClassification)
            {
                ConfigurePlaneCollision();
                lastClassification = m_ARPlane.classification;
            }
        }
        
        void ConfigurePlaneCollision()
        {
            // Only floor and wall planes should have physical collision
            // Tables, ceilings, etc. should be pass-through (trigger only)
            Collider planeCollider = GetComponent<Collider>();
            
            if (planeCollider != null)
            {
                if (m_ARPlane.classification == PlaneClassification.Floor ||
                    m_ARPlane.classification == PlaneClassification.Wall)
                {
                    // Floor and wall: solid collision
                    planeCollider.isTrigger = false;
                }
                else
                {
                    // Table, ceiling, etc.: pass through but still detect
                    planeCollider.isTrigger = true;
                }
            }
        }
        
        void UpdatePlaneColor()
        {
            // Start planes invisible (SceneManager will control visibility)
            Color planeMatColor = Color.white;
            planeMatColor.a = 0f; // Start completely transparent
            m_PlaneMeshRenderer.material.color = planeMatColor;
        }
        
        void HidePlaneOutline()
        {
            // Hide the LineRenderer outline at start
            LineRenderer lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                Color startColor = lineRenderer.startColor;
                Color endColor = lineRenderer.endColor;
                startColor.a = 0f; // Transparent
                endColor.a = 0f;   // Transparent
                lineRenderer.startColor = startColor;
                lineRenderer.endColor = endColor;
            }
        }
}
