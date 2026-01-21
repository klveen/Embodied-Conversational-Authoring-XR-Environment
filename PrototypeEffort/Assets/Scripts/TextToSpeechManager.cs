using UnityEngine;
using Meta.WitAi.TTS.Utilities;
using Meta.WitAi.TTS.Data;

/// <summary>
/// Manages text-to-speech playback using Meta Quest Voice SDK
/// Plays audio speech while preserving visual text display
/// </summary>
public class TextToSpeechManager : MonoBehaviour
{
    [Header("TTS Configuration")]
    [Tooltip("Reference to TTSSpeaker component (add to same GameObject)")]
    [SerializeField] private TTSSpeaker ttsSpeaker;
    
    [Header("Voice Settings")]
    [Tooltip("Voice preset to use (Charlie, Salli, etc.)")]
    [SerializeField] private string voicePreset = "Charlie";
    
    [Header("Playback Settings")]
    [Tooltip("Auto-stop previous speech when new speech starts")]
    [SerializeField] private bool stopPreviousSpeech = true;
    
    [Tooltip("Volume of TTS (0-1)")]
    [SerializeField] [Range(0f, 1f)] private float volume = 1.0f;
    
    private void Awake()
    {
        // Try to get TTSSpeaker if not assigned
        if (ttsSpeaker == null)
        {
            ttsSpeaker = GetComponent<TTSSpeaker>();
            
            if (ttsSpeaker == null)
            {
                Debug.LogError("[TextToSpeechManager] No TTSSpeaker component found! Add one to this GameObject.");
            }
        }
    }
    
    /// <summary>
    /// Speak the given text using Meta Quest Voice SDK
    /// </summary>
    /// <param name="text">Text to speak</param>
    public void Speak(string text)
    {
        if (ttsSpeaker == null)
        {
            Debug.LogError("[TextToSpeechManager] Cannot speak - TTSSpeaker not assigned!");
            return;
        }
        
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("[TextToSpeechManager] Cannot speak empty text");
            return;
        }
        
        // Stop previous speech if enabled
        if (stopPreviousSpeech && ttsSpeaker.IsSpeaking)
        {
            Debug.Log("[TextToSpeechManager] Stopping previous speech");
            ttsSpeaker.Stop();
        }
        
        // Speak the text
        Debug.Log($"[TextToSpeechManager] *** CALLING SPEAK: \"{text}\" ***");
        ttsSpeaker.Speak(text);
        Debug.Log($"[TextToSpeechManager] Speak() called successfully");
    }
    
    /// <summary>
    /// Stop current speech playback
    /// </summary>
    public void StopSpeaking()
    {
        if (ttsSpeaker != null)
        {
            ttsSpeaker.Stop();
            Debug.Log("[TextToSpeechManager] Stopped speech");
        }
    }
    
    /// <summary>
    /// Check if currently speaking
    /// </summary>
    public bool IsSpeaking()
    {
        if (ttsSpeaker == null) return false;
        return ttsSpeaker.IsSpeaking;
    }
}
