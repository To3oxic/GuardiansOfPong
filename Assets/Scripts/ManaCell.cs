using UnityEngine;
using UnityEngine.UI;

public class ManaCell : MonoBehaviour
{
    [SerializeField] private Image fillImage;  // The image to animate alpha on

    private float animationTimer = 0f;
    private const float ANIMATION_DURATION = 3f;
    private bool isAnimating = false;
    private RectTransform rectTransform;
    private const float SCALE_UP_SIZE = 80f;
    private const float SCALE_DOWN_SIZE = 60f;

    public bool filled { get; set; } = false;
    public ManaCell next { get; set; }
    public ManaCell previous { get; set; }

    private void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        if (fillImage == null)
        {
            Debug.LogWarning($"[ManaCell] No fill image assigned on {gameObject.name}");
        }
    }

    private void Update()
    {
        if (isAnimating && fillImage != null)
        {
            animationTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(animationTimer / ANIMATION_DURATION);

            // Animate alpha from 0 to 1
            Color fillColor = fillImage.color;
            fillColor.a = progress;
            fillImage.color = fillColor;

            // Animation finished
            if (progress >= 1f)
            {
                // Reset size back to normal
                if (rectTransform != null)
                {
                    rectTransform.sizeDelta = new Vector2(SCALE_DOWN_SIZE, SCALE_DOWN_SIZE);
                }

                isAnimating = false;
                filled = true;
            }
        }
    }

    /// <summary>
    /// Start the fill animation on this cell
    /// </summary>
    public void PlayFillAnimation()
    {
        if (fillImage == null)
        {
            Debug.LogWarning($"[ManaCell] Cannot animate - fill image not assigned on {gameObject.name}");
            return;
        }

        // Instantly make the cell bigger
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(SCALE_UP_SIZE, SCALE_UP_SIZE);
        }

        // Reset fill image alpha to 0
        Color fillColor = fillImage.color;
        fillColor.a = 0f;
        fillImage.color = fillColor;

        // Start animation
        animationTimer = 0f;
        isAnimating = true;

        Debug.Log($"[ManaCell] Starting fill animation on {gameObject.name}");
    }

    /// <summary>
    /// Reset the cell to empty state
    /// </summary>
    public void ResetCell()
    {
        filled = false;
        isAnimating = false;
        animationTimer = 0f;

        // Hide fill image
        if (fillImage != null)
        {
            Color fillColor = fillImage.color;
            fillColor.a = 0f;
            fillImage.color = fillColor;
        }

        // Reset size
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(SCALE_DOWN_SIZE, SCALE_DOWN_SIZE);
        }
    }

    /// <summary>
    /// Instantly fill the cell without animation
    /// </summary>
    public void InstantFill()
    {
        filled = true;
        isAnimating = false;

        if (fillImage != null)
        {
            Color fillColor = fillImage.color;
            fillColor.a = 1f;
            fillImage.color = fillColor;
        }
    }
}
