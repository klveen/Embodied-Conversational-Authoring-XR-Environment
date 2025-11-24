using UnityEngine;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(ARPlane))]
[RequireComponent(typeof(MeshCollider))]
public class ARPlanePhysics : MonoBehaviour
{
    private ARPlane arPlane;
    private MeshCollider meshCollider;
    private MeshFilter meshFilter;

    void Awake()
    {
        arPlane = GetComponent<ARPlane>();
        meshCollider = GetComponent<MeshCollider>();
        meshFilter = GetComponent<MeshFilter>();
    }

    void OnEnable()
    {
        arPlane.boundaryChanged += OnBoundaryChanged;
    }

    void OnDisable()
    {
        arPlane.boundaryChanged -= OnBoundaryChanged;
    }

    void OnBoundaryChanged(ARPlaneBoundaryChangedEventArgs args)
    {
        UpdateMeshCollider();
    }

    void UpdateMeshCollider()
    {
        if (meshFilter != null && meshFilter.mesh != null)
        {
            meshCollider.sharedMesh = meshFilter.mesh;
        }
    }
}
