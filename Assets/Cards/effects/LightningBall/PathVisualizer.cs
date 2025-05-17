using UnityEngine;
using System.Collections.Generic;
using System.Collections; // For Coroutines

[RequireComponent(typeof(LineRenderer))] // Ensure a LineRenderer is attached
public class PathVisualizer : MonoBehaviour
{
    public static PathVisualizer Instance { get; private set; }

    private LineRenderer lineRenderer;
    private Coroutine activePathCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PathVisualizer] Another instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            Debug.LogError("[PathVisualizer] LineRenderer component not found!");
            enabled = false; // Disable script if no LineRenderer
            return;
        }
        // Initial setup for the LineRenderer (can be customized further in Inspector)
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply")); // Simple emissive material
        lineRenderer.startColor = Color.yellow;
        lineRenderer.endColor = Color.cyan;
        lineRenderer.enabled = false; // Start disabled
        lineRenderer.positionCount = 0;
    }

    public void ShowPath(List<Vector3> pathPoints, float displayDuration)
    {
        if (lineRenderer == null)
        {
            Debug.LogError("[PathVisualizer] LineRenderer is null, cannot show path.");
            return;
        }
        if (pathPoints == null || pathPoints.Count < 2)
        {
            Debug.LogWarning("[PathVisualizer] Path has too few points to draw.");
            return;
        }

        // Stop any existing path display coroutine
        if (activePathCoroutine != null)
        {
            StopCoroutine(activePathCoroutine);
        }

        lineRenderer.positionCount = pathPoints.Count;
        lineRenderer.SetPositions(pathPoints.ToArray());
        lineRenderer.enabled = true;

        Debug.Log($"[PathVisualizer] Showing path with {pathPoints.Count} points for {displayDuration} seconds.");
        activePathCoroutine = StartCoroutine(HidePathAfterDelay(displayDuration));
    }

    private IEnumerator HidePathAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
            lineRenderer.positionCount = 0; // Clear points for next use
            Debug.Log("[PathVisualizer] Path hidden after delay.");
        }
        activePathCoroutine = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}