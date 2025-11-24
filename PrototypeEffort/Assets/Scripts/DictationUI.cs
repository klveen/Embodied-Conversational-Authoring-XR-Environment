using UnityEngine;
using TMPro;

public class DictationUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI transcriptText;
    [SerializeField] private TextMeshProUGUI statusText;
    
    [Header("Settings")]
    [SerializeField] private Color listeningColor = Color.green;
    [SerializeField] private Color processingColor = Color.yellow;
    [SerializeField] private Color readyColor = Color.white;

    public void UpdateTranscript(string text)
    {
        if (transcriptText != null)
        {
            transcriptText.text = text;
        }
    }

    public void UpdateStatus(string status, StatusType type = StatusType.Ready)
    {
        if (statusText != null)
        {
            statusText.text = status;
            
            switch (type)
            {
                case StatusType.Listening:
                    statusText.color = listeningColor;
                    break;
                case StatusType.Processing:
                    statusText.color = processingColor;
                    break;
                case StatusType.Ready:
                default:
                    statusText.color = readyColor;
                    break;
            }
        }
    }
}

public enum StatusType
{
    Ready,
    Listening,
    Processing
}
