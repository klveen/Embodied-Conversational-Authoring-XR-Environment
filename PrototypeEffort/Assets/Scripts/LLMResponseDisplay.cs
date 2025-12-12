using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Displays LLM responses in VR view (corner overlay)
/// Automatically fades out after a few seconds
/// </summary>
public class LLMResponseDisplay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI responseText;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Display Settings")]
    [SerializeField] private float displayDuration = 5f;  // How long to show response
    [SerializeField] private float fadeOutDuration = 1f;  // Fade out animation time
    [SerializeField] private int maxCharacters = 200;     // Truncate long responses
    
    private Coroutine fadeCoroutine;
    
    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        
        // Start hidden
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }
    
    /// <summary>
    /// Display LLM response text in corner of view
    /// </summary>
    public void ShowResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
            return;
        
        // Truncate if too long
        string displayText = response;
        if (displayText.Length > maxCharacters)
        {
            displayText = displayText.Substring(0, maxCharacters) + "...";
        }
        
        // Update text
        if (responseText != null)
        {
            responseText.text = $"<b>LLM:</b> {displayText}";
        }
        
        // Cancel existing fade
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        
        // Show and start fade timer
        fadeCoroutine = StartCoroutine(ShowAndFadeOut());
    }
    
    /// <summary>
    /// Show text with error styling
    /// </summary>
    public void ShowError(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return;
        
        if (responseText != null)
        {
            responseText.text = $"<color=red><b>Error:</b> {errorMessage}</color>";
        }
        
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        
        fadeCoroutine = StartCoroutine(ShowAndFadeOut());
    }
    
    /// <summary>
    /// Show user's voice command (for feedback)
    /// </summary>
    public void ShowUserCommand(string command)
    {
        if (string.IsNullOrEmpty(command))
            return;
        
        if (responseText != null)
        {
            responseText.text = $"<color=cyan><b>You:</b> {command}</color>";
        }
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
        
        // Don't auto-fade user commands, wait for LLM response
    }
    
    private IEnumerator ShowAndFadeOut()
    {
        // Fade in instantly
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
        
        // Wait for display duration
        yield return new WaitForSeconds(displayDuration);
        
        // Fade out gradually
        float elapsed = 0f;
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
            
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }
            
            yield return null;
        }
        
        // Ensure fully hidden
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }
    
    /// <summary>
    /// Clear display immediately
    /// </summary>
    public void Clear()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        
        if (responseText != null)
        {
            responseText.text = "";
        }
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }
}
