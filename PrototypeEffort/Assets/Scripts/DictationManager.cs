using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;
using Oculus.Voice;

public class DictationManager : MonoBehaviour
{
    [Header("Voice Settings")]
    [SerializeField] private AppVoiceExperience voiceExperience;
    
    [Header("Input Settings")]
    [SerializeField] private InputActionReference dictationButton;
    
    [Header("UI Settings")]
    [SerializeField] private TextMeshProUGUI transcriptText;
    [SerializeField] private TextMeshProUGUI statusText;
    
    [Header("Events")]
    public UnityEvent<string> OnFinalTranscript;
    public UnityEvent<string> OnPartialTranscript;
    
    private string currentTranscript = "";

    void Start()
    {
        if (voiceExperience != null)
        {
            voiceExperience.VoiceEvents.OnFullTranscription.AddListener(HandleFullTranscription);
            voiceExperience.VoiceEvents.OnPartialTranscription.AddListener(HandlePartialTranscription);
            voiceExperience.VoiceEvents.OnStartListening.AddListener(OnStartListening);
            voiceExperience.VoiceEvents.OnStoppedListening.AddListener(OnStopListening);
        }
        
        UpdateStatusUI("Ready to dictate");
    }

    private void OnEnable()
    {
        if (dictationButton != null)
        {
            dictationButton.action.started += OnDictationButtonPressed;
            dictationButton.action.canceled += OnDictationButtonReleased;
        }
        
        dictationButton?.action.Enable();
    }

    private void OnDisable()
    {
        if (dictationButton != null)
        {
            dictationButton.action.started -= OnDictationButtonPressed;
            dictationButton.action.canceled -= OnDictationButtonReleased;
        }
        
        dictationButton?.action.Disable();
        
        if (voiceExperience != null)
        {
            voiceExperience.VoiceEvents.OnFullTranscription.RemoveListener(HandleFullTranscription);
            voiceExperience.VoiceEvents.OnPartialTranscription.RemoveListener(HandlePartialTranscription);
            voiceExperience.VoiceEvents.OnStartListening.RemoveListener(OnStartListening);
            voiceExperience.VoiceEvents.OnStoppedListening.RemoveListener(OnStopListening);
        }
    }

    private void OnDictationButtonPressed(InputAction.CallbackContext context)
    {
        StartDictation();
    }

    private void OnDictationButtonReleased(InputAction.CallbackContext context)
    {
        StopDictation();
    }

    public void StartDictation()
    {
        if (voiceExperience != null && !voiceExperience.Active)
        {
            voiceExperience.Activate();
            currentTranscript = "";
            UpdateTranscriptUI("");
            UpdateStatusUI("Listening...");
            Debug.Log("Started dictation");
        }
    }

    public void StopDictation()
    {
        if (voiceExperience != null && voiceExperience.Active)
        {
            voiceExperience.Deactivate();
            UpdateStatusUI("Processing...");
            Debug.Log("Stopped dictation");
        }
    }

    private void HandlePartialTranscription(string text)
    {
        currentTranscript = text;
        UpdateTranscriptUI(text);
        OnPartialTranscript?.Invoke(text);
        Debug.Log($"Partial: {text}");
    }

    private void HandleFullTranscription(string text)
    {
        currentTranscript = text;
        UpdateTranscriptUI(text);
        UpdateStatusUI($"Final: {text}");
        OnFinalTranscript?.Invoke(text);
        Debug.Log($"Final: {text}");
    }

    private void OnStartListening()
    {
        UpdateStatusUI("Listening...");
    }

    private void OnStopListening()
    {
        UpdateStatusUI("Processing...");
    }

    private void UpdateTranscriptUI(string text)
    {
        if (transcriptText != null)
        {
            transcriptText.text = text;
        }
    }

    private void UpdateStatusUI(string status)
    {
        if (statusText != null)
        {
            statusText.text = status;
        }
    }

    // Public method for SceneManager to get the last transcript
    public string GetLastTranscript()
    {
        return currentTranscript;
    }
}
