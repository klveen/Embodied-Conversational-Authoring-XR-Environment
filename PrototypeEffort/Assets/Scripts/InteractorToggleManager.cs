using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

public class InteractorToggleManager : MonoBehaviour
{
    [Header("Left Controller")]
    [SerializeField] private XRRayInteractor leftRayInteractor;
    [SerializeField] private XRDirectInteractor leftDirectInteractor;

    [Header("Right Controller")]
    [SerializeField] private XRRayInteractor rightRayInteractor;
    [SerializeField] private XRDirectInteractor rightDirectInteractor;


    [Header("Input Actions")]
    [SerializeField] private InputActionReference toggleInteractorButton; // Only use right controller's button

    private bool useRay = false; // Shared state for both hands


    private void OnEnable()
    {
        if (toggleInteractorButton != null)
            toggleInteractorButton.action.performed += ToggleBothInteractors;

        toggleInteractorButton?.action.Enable();

        SetInteractorMode(useRay);
    }


    private void OnDisable()
    {
        if (toggleInteractorButton != null)
            toggleInteractorButton.action.performed -= ToggleBothInteractors;
    }


    private void ToggleBothInteractors(InputAction.CallbackContext ctx)
    {
        useRay = !useRay;
        SetInteractorMode(useRay);
    }


    private void SetInteractorMode(bool useRay)
    {
        if (leftRayInteractor != null) leftRayInteractor.gameObject.SetActive(useRay);
        if (leftDirectInteractor != null) leftDirectInteractor.gameObject.SetActive(!useRay);
        if (rightRayInteractor != null) rightRayInteractor.gameObject.SetActive(useRay);
        if (rightDirectInteractor != null) rightDirectInteractor.gameObject.SetActive(!useRay);
    }
}
