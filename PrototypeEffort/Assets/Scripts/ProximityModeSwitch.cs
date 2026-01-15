using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// DEPRECATED: This script is no longer needed when using XRInteractionGroup.
/// XRInteractionGroup automatically handles priority between Ray and Direct interactors.
/// Both interactors can be active simultaneously - Direct takes priority when objects are in range.
/// 
/// You can safely disable or delete this component.
/// </summary>
public class ProximityModeSwitch : MonoBehaviour
{
    private void Awake()
    {
        Debug.LogWarning("[ProximityModeSwitch] This component is deprecated. Use XRInteractionGroup instead. Disabling...");
        enabled = false;
    }
}
