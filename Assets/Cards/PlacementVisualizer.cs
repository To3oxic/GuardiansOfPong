using UnityEngine;

public class PlacementVisualizer : MonoBehaviour
{
    // Assign a child GameObject with a Quad/Sprite Renderer in the Inspector
    public Transform visualChild; 
    private bool _isShowing = false;

    void Awake()
    {
        if (visualChild == null && transform.childCount > 0)
        {
            visualChild = transform.GetChild(0); // Auto-assign if a child exists
        }
        else if (visualChild == null)
        {
            Debug.LogError("PlacementVisualizer: Visual Child not assigned and no child found!", this);
        }
        Hide(); // Start hidden
    }

    /// <summary>
    /// Shows the visualizer at a specific world position with a given radius.
    /// </summary>
    /// <param name="position">World position to show at.</param>
    /// <param name="radius">The radius of the circular visual (diameter of the Quad).</param>
    public void Show(Vector3 position, float radius)
    {
        transform.position = position;
        if (visualChild != null)
        {
            // Assuming the visual is a Quad/Sprite where scale directly maps to world size
            // If your visual's base size is 1 unit diameter, then radius * 2 gives the correct scale.
            visualChild.localScale = new Vector3(radius * 2f, radius * 2f, 1f); 
            visualChild.gameObject.SetActive(true);
        }
        _isShowing = true;
    }

    /// <summary>
    /// Updates the position and radius of an already showing visualizer.
    /// </summary>
    public void UpdateVisual(Vector3 position, float radius)
    {
        if (!_isShowing)
        {
            Show(position, radius);
            return;
        }
        transform.position = position;
        if (visualChild != null)
        {
            visualChild.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        }
    }

    /// <summary>
    /// Hides the visualizer.
    /// </summary>
    public void Hide()
    {
        if (visualChild != null)
        {
            visualChild.gameObject.SetActive(false);
        }
        _isShowing = false;
    }
}
