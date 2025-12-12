using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


[RequireComponent(typeof(ARPlane))]
[RequireComponent(typeof(MeshRenderer))]

public class ARPlaneColorizerExample : MonoBehaviour
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
           ConfigurePlaneCollision();
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
            Color planeMatColor = Color.gray; //i.e. 'None'

            switch (m_ARPlane.classification)
            {
                case PlaneClassification.Floor:
                    planeMatColor = Color.green;
                    break;
                case PlaneClassification.Wall:
                    planeMatColor = Color.white;
                    break;
                case PlaneClassification.Ceiling:
                    planeMatColor = Color.red;
                    break;
                case PlaneClassification.Table:
                    planeMatColor = Color.yellow;
                    break;
                case PlaneClassification.Seat:
                    planeMatColor = Color.blue;
                    break;
                case PlaneClassification.Door:
                    planeMatColor = Color.magenta;
                    break;
                case PlaneClassification.Window:
                    planeMatColor = Color.cyan;
                    break;
            }

            planeMatColor.a = 0.33f;
            m_PlaneMeshRenderer.material.color = planeMatColor;
        }
}
