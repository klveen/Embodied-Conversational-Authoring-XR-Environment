using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Displays Unity debug logs on a TextMeshPro UI element in VR.
/// Attach this to a GameObject with a TextMeshProUGUI component.
/// </summary>
public class DebugLogDisplay : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int maxLines = 20;
    [SerializeField] private bool showTimestamps = true;
    [SerializeField] private bool showLogType = true;
    
    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color errorColor = Color.red;
    
    private TextMeshProUGUI textDisplay;
    private Queue<string> logQueue = new Queue<string>();
    
    private void Awake()
    {
        textDisplay = GetComponent<TextMeshProUGUI>();
        
        if (textDisplay == null)
        {
            Debug.LogError("DebugLogDisplay: No TextMeshProUGUI component found!");
            enabled = false;
            return;
        }
        
        // Subscribe to Unity's log callback
        Application.logMessageReceived += HandleLog;
        
        textDisplay.text = "Debug Log Display Active\n";
    }
    
    private void OnDestroy()
    {
        // Unsubscribe when destroyed
        Application.logMessageReceived -= HandleLog;
    }
    
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Format the log message
        string colorHex = GetColorHex(type);
        string prefix = "";
        
        if (showTimestamps)
        {
            prefix += $"[{System.DateTime.Now:HH:mm:ss}] ";
        }
        
        if (showLogType)
        {
            prefix += $"[{type}] ";
        }
        
        string formattedLog = $"<color={colorHex}>{prefix}{logString}</color>";
        
        // Add to queue
        logQueue.Enqueue(formattedLog);
        
        // Remove oldest if exceeds max lines
        while (logQueue.Count > maxLines)
        {
            logQueue.Dequeue();
        }
        
        // Update display
        UpdateDisplay();
    }
    
    private void UpdateDisplay()
    {
        if (textDisplay != null)
        {
            textDisplay.text = string.Join("\n", logQueue);
        }
    }
    
    private string GetColorHex(LogType type)
    {
        Color color;
        switch (type)
        {
            case LogType.Error:
            case LogType.Exception:
            case LogType.Assert:
                color = errorColor;
                break;
            case LogType.Warning:
                color = warningColor;
                break;
            default:
                color = normalColor;
                break;
        }
        
        return "#" + ColorUtility.ToHtmlStringRGB(color);
    }
    
    /// <summary>
    /// Clear all logs from display
    /// </summary>
    public void ClearLogs()
    {
        logQueue.Clear();
        textDisplay.text = "Logs Cleared\n";
    }
}
