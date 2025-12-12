using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Creates LLM Response Display UI in VR scene
/// Menu: GameObject > XR > Create LLM Response Display
/// </summary>
public class LLMDisplaySetup : MonoBehaviour
{
    [MenuItem("GameObject/XR/Create LLM Response Display")]
    private static void CreateLLMDisplay()
    {
        // Find XR Origin camera
        Camera xrCamera = FindObjectOfType<Camera>();
        if (xrCamera == null)
        {
            Debug.LogError("No camera found in scene. Make sure XR Origin is present.");
            return;
        }
        
        // Create Canvas
        GameObject canvasObj = new GameObject("LLM Response Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Position canvas in front of camera (top-left corner)
        canvasObj.transform.SetParent(xrCamera.transform);
        canvasObj.transform.localPosition = new Vector3(-0.3f, 0.25f, 1f);  // Top-left, 1m away
        canvasObj.transform.localRotation = Quaternion.identity;
        canvasObj.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        
        // Set canvas size
        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(600, 200);
        
        // Create background panel
        GameObject panelObj = new GameObject("Background Panel");
        panelObj.transform.SetParent(canvasObj.transform);
        
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;
        
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.7f);  // Semi-transparent black
        
        // Add CanvasGroup for fading
        CanvasGroup canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        
        // Create Text (TextMeshPro)
        GameObject textObj = new GameObject("Response Text");
        textObj.transform.SetParent(panelObj.transform);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = new Vector2(-20, -20);  // 10px padding
        textRect.anchoredPosition = Vector2.zero;
        
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "";
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        
        // Add LLMResponseDisplay component
        LLMResponseDisplay displayComponent = canvasObj.AddComponent<LLMResponseDisplay>();
        
        // Auto-assign references using reflection
        var responseTextField = displayComponent.GetType().GetField("responseText", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var canvasGroupField = displayComponent.GetType().GetField("canvasGroup", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (responseTextField != null)
            responseTextField.SetValue(displayComponent, text);
        if (canvasGroupField != null)
            canvasGroupField.SetValue(displayComponent, canvasGroup);
        
        // Select the new canvas
        Selection.activeGameObject = canvasObj;
        
        Debug.Log("✓ LLM Response Display created! Assign it to VoiceObjectSpawner.llmDisplay field.");
        Debug.Log("Position: Top-left corner of VR view");
        Debug.Log("Canvas is parented to camera - moves with player");
    }
}
