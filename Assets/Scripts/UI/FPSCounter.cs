using UnityEngine;
using TMPro;

/// <summary>
/// Simple FPS counter for debugging mobile performance
/// Attach to a Canvas with a TextMeshProUGUI child
/// </summary>
public class FPSCounter : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fpsText;
    private float deltaTime = 0f;
    private int frameCount = 0;
    private float updateInterval = 0.5f; // Update every 0.5 seconds
    private float timeSinceLastUpdate = 0f;

    private void Start()
    {
        // Auto-find TextMeshProUGUI if not assigned
        if (fpsText == null)
        {
            fpsText = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (fpsText == null)
        {
            enabled = false;
        }
    }

    private void Update()
    {
        deltaTime += Time.unscaledDeltaTime;
        frameCount++;
        timeSinceLastUpdate += Time.unscaledDeltaTime;

        if (timeSinceLastUpdate >= updateInterval)
        {
            float fps = frameCount / deltaTime;
            fpsText.text = $"FPS: {fps:F1}";

            // Color code: Green if good, Yellow if okay, Red if bad
            if (fps >= 50)
                fpsText.color = Color.green;
            else if (fps >= 30)
                fpsText.color = Color.yellow;
            else
                fpsText.color = Color.red;

            frameCount = 0;
            deltaTime = 0f;
            timeSinceLastUpdate = 0f;
        }
    }
}
