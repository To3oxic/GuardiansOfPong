using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class UnitPlacer : MonoBehaviour
{
    public static UnitPlacer Instance;

    [Header("Placement Settings")]
    public float placementYLevel = 0.9f; // Configurable Y level for all placements. Adjust in Inspector if ground isn't at Y=0.
    public LayerMask placementLayerMask; // Set this in inspector to your ground/placeable surface layer
    [SerializeField] private GameObject fieldOfEffectPreviewPrefab; // The prefab for the visual field of effect (e.g., a circle)

    // No longer a public field, will be fetched from CardEffect
    private GameObject currentPreviewInstance; // Renamed from currentPreview for clarity
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

        if (fieldOfEffectPreviewPrefab == null)
        {
            Debug.LogError("[UnitPlacer] FieldOfEffectPreviewPrefab is not assigned in the Inspector!");
            callback?.Invoke(Vector3.zero, Vector3.forward); // Fail gracefully
            EndPlacing();
            return;
        }

        placementCallback = callback;
        isPlacing = true;
        float effectRadius = effectToPlace.GetEffectRadius();
        ShowPreview(Vector3.zero, effectRadius); // Initial position, will be updated
        Debug.Log($"[UnitPlacer] Started placing for effect: {effectToPlace.name} with radius: {effectRadius}");
    }

    private void Update()
    {
        if (!isPlacing || mainCam == null) return;

        Vector3? point = RaycastWorldPoint();
        if (point.HasValue && currentPreviewInstance != null)
        {
            Vector3 previewPosition = point.Value;
            previewPosition.y = placementYLevel;
            currentPreviewInstance.transform.position = previewPosition;
            Debug.Log($"[UnitPlacer] Raycast Hit: {point.Value}. Preview Position: {currentPreviewInstance.transform.position}, Scale: {currentPreviewInstance.transform.localScale}, Active: {currentPreviewInstance.activeSelf}");

            bool isBottomPlayer = IsLocalPaddleAtBottom();
            // The field of effect preview generally shouldn't need rotation unless it's directional itself.
            // If the original unit preview had specific rotation logic, it might be applied differently or not at all to the AoE preview.
            // currentPreviewInstance.transform.rotation = Quaternion.Euler(0f, isBottomPlayer ? 0f : 180f, 0f);
        }

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            Debug.Log($"[UnitPlacer] Mouse click detected. IsPointerOverUI: {IsPointerOverUI()}, point.HasValue: {point.HasValue}");
            if (point.HasValue)
            {
                Vector3 direction = IsLocalPaddleAtBottom() ? Vector3.forward : Vector3.back;

                Vector3 finalPlacementPoint = point.Value;
                finalPlacementPoint.y = placementYLevel;

                Debug.Log($"[UnitPlacer] Confirming placement. Final point for callback (Y forced to {placementYLevel}): {finalPlacementPoint}, Direction: {direction}");
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

    private void ShowPreview(Vector3 position, float radius)
    {
        if (fieldOfEffectPreviewPrefab == null)
        {
            Debug.LogError("[UnitPlacer] fieldOfEffectPreviewPrefab is null. Cannot show preview.");
            return;
        }
        currentPreviewInstance = Instantiate(fieldOfEffectPreviewPrefab, position, Quaternion.Euler(90f, 0f, 0f)); // Apply 90-degree X rotation
        // Assuming the preview prefab is a flat circle/cylinder on the XZ plane, scale X and Z.
        // If your preview prefab has a different default size or orientation, adjust this.
        currentPreviewInstance.transform.localScale = new Vector3(radius * 2f, currentPreviewInstance.transform.localScale.y, radius * 2f);
    }

    private void EndPlacing()
    {
        isPlacing = false;
        placementCallback = null;
        if (currentPreviewInstance != null)
            Destroy(currentPreviewInstance);
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
