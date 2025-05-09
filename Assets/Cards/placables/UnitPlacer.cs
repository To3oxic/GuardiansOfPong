using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class UnitPlacer : MonoBehaviour
{
    public static UnitPlacer Instance;

    // No longer a public field, will be fetched from CardEffect
    private GameObject currentPreviewPrefab;
    private GameObject currentPreview;
    private bool isPlacing = false;
    private Action<Vector3, Vector3> placementCallback;
    private Camera mainCam;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        mainCam = Camera.main;
    }

    // Modified method signature to take CardEffect
    public void StartPlacing(CardEffect effectToPlace, Action<Vector3, Vector3> callback)
    {
        if (effectToPlace == null)
        {
            Debug.LogError("[UnitPlacer] effectToPlace is null.");
            callback?.Invoke(Vector3.zero, Vector3.forward); // Fail gracefully
            return;
        }

        currentPreviewPrefab = effectToPlace.GetPreviewPrefab();
        if (currentPreviewPrefab == null)
        {
            Debug.LogError("[UnitPlacer] Preview prefab is null for card: " + effectToPlace.name);
            callback?.Invoke(Vector3.zero, Vector3.forward); // Fail gracefully
            EndPlacing(); // Clean up if we can't place
            return;
        }

        placementCallback = callback;
        isPlacing = true;
        ShowPreview(Vector3.zero); // Initial position, will be updated
        Debug.Log("[UnitPlacer] Started placing for effect: " + effectToPlace.name);
    }

    private void Update()
    {
        if (!isPlacing || mainCam == null) return;

        Vector3? point = RaycastWorldPoint();
        if (point.HasValue && currentPreview != null)
        {
            // Make preview also sit at Y=0
            Vector3 previewPosition = point.Value;
            previewPosition.y = 0f;
            currentPreview.transform.position = previewPosition;
            // Debug.Log($"[UnitPlacer] Preview at: {point.Value}, CurrentPreview actual pos: {currentPreview.transform.position}"); 
            Debug.Log($"[UnitPlacer] Raw raycast point: {point.Value}, Preview forced to Y=0 at: {currentPreview.transform.position}");

            bool isBottomPlayer = IsLocalPaddleAtBottom();
            currentPreview.transform.rotation = Quaternion.Euler(0f, isBottomPlayer ? 0f : 180f, 0f);
        }

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            if (point.HasValue)
            {
                Vector3 direction = IsLocalPaddleAtBottom() ? Vector3.forward : Vector3.back;

                Vector3 finalPlacementPoint = point.Value; // Get X and Z from raycast
                finalPlacementPoint.y = 0f; // Force Y to 0 for actual placement

                Debug.Log($"[UnitPlacer] Confirming placement. Final point for callback (Y forced to 0): {finalPlacementPoint}, Direction: {direction}");
                placementCallback?.Invoke(finalPlacementPoint, direction);
                Debug.Log("[UnitPlacer] Placement confirmed.");
                EndPlacing();
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape)) CancelPlacing();
    }

    private Vector3? RaycastWorldPoint()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit)) return hit.point;
        return null;
    }

    private void ShowPreview(Vector3 position)
    {
        // Use currentPreviewPrefab obtained from CardEffect
        if (currentPreviewPrefab == null) return;
        currentPreview = Instantiate(currentPreviewPrefab, position, Quaternion.identity);
    }

    private void EndPlacing()
    {
        isPlacing = false;
        placementCallback = null;
        if (currentPreview != null)
            Destroy(currentPreview);
    }

    private void CancelPlacing() => EndPlacing();

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private bool IsLocalPaddleAtBottom()
    {
        var paddles = FindObjectsByType<PaddleController>(FindObjectsSortMode.None);
        foreach (var paddle in paddles)
        {
            if (paddle.IsOwner)
                return paddle.transform.position.z < 0;
        }

        Debug.LogWarning("[UnitPlacer] Could not determine paddle position.");
        return true;
    }
}
