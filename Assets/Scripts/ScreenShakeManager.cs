using UnityEngine;
using System.Collections;

/// <summary>
/// Manages screen shake effects for the camera.
/// Call ShakeScreen() to trigger a shake effect.
/// </summary>
public class ScreenShakeManager : MonoBehaviour
{
    public static ScreenShakeManager Instance { get; private set; }

    private Camera mainCamera;
    private Vector3 originalPosition;
    private Coroutine currentShakeCoroutine;

    [Header("Screen Shake Settings")]
    public float defaultIntensity = 0.5f;
    public float defaultDuration = 0.5f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = GetComponent<Camera>();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Trigger a screen shake effect.
    /// </summary>
    /// <param name="intensity">How far the camera shakes (in units)</param>
    /// <param name="duration">How long the shake lasts (in seconds)</param>
    /// <param name="frequency">How fast the shake oscillates (higher = faster)</param>
    public void ShakeScreen(float intensity = -1f, float duration = -1f, float frequency = 10f)
    {
        originalPosition = mainCamera.transform.localPosition;
        intensity = intensity < 0 ? defaultIntensity : intensity;
        duration = duration < 0 ? defaultDuration : duration;

        // Stop current shake if one is running
        if (currentShakeCoroutine != null)
        {
            StopCoroutine(currentShakeCoroutine);
        }

        currentShakeCoroutine = StartCoroutine(PerformShake(intensity, duration, frequency));
    }

    private IEnumerator PerformShake(float intensity, float duration, float frequency)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;

            // Random shake offset
            Vector3 shakeOffset = Random.insideUnitSphere * intensity;

            // Fade out shake as duration ends
            float fadeOut = Mathf.Lerp(1f, 0f, progress);
            shakeOffset *= fadeOut;

            // Apply shake to camera
            mainCamera.transform.localPosition = originalPosition + shakeOffset;

            yield return null;
        }

        // Reset camera position
        mainCamera.transform.localPosition = originalPosition;
        currentShakeCoroutine = null;
    }

    /// <summary>
    /// Test method: shakes the screen every second
    /// </summary>
    private IEnumerator TestScreenShake()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            ShakeScreen(intensity: 0.5f, duration: 0.3f, frequency: 10f);
        }
    }
}
