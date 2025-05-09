using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class WindOfChangePlacer : MonoBehaviour
{
    public static WindOfChangePlacer Instance;

    public GameObject arrowPreviewPrefab;
    private GameObject currentArrowPreview;

    private Vector3 startPoint;
    private bool isPlacing = false;
    private bool startSelected = false;
    private Action<Vector3, Vector3> placementCallback;

    private Camera mainCam;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        mainCam = Camera.main;
    }

    public void StartPlacing(Action<Vector3, Vector3> callback)
    {
        placementCallback = callback;
        isPlacing = true;
        startSelected = false;
        Debug.Log("[Placer] Started placing Wind of Change field...");
    }

    private void Update()
    {
        if (!isPlacing || mainCam == null) return;

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            Vector3? point = RaycastWorldPoint();
            if (point.HasValue)
            {
                startPoint = point.Value;
                startSelected = true;
                Debug.Log("[Placer] Start point selected.");
            }
        }
        else if (Input.GetMouseButtonUp(0) && startSelected)
        {
            Vector3? point = RaycastWorldPoint();
            if (point.HasValue)
            {
                Vector3 finalDir = point.Value - startPoint;
                if (finalDir.magnitude < 0.1f)
                {
                    Debug.LogWarning("[Placer] Drag too short. Cancelling.");
                    CancelPlacing();
                    return;
                }

                placementCallback?.Invoke(startPoint, finalDir.normalized);
                Debug.Log("[Placer] Placement confirmed.");
                EndPlacing();
            }
        }
    }

    private Vector3? RaycastWorldPoint()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

        // Try 3D physics
        if (Physics.Raycast(ray, out RaycastHit hit3D))
            return hit3D.point;

        // Fallback: 2D physics
        Vector2 ray2D = mainCam.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit2D = Physics2D.Raycast(ray2D, Vector2.zero);
        if (hit2D.collider != null)
            return hit2D.point;

        return null;
    }


    private void EndPlacing()
    {
        isPlacing = false;
        startSelected = false;
        placementCallback = null;

        if (currentArrowPreview != null)
            Destroy(currentArrowPreview);
    }

    private void CancelPlacing()
    {
        Debug.Log("[Placer] Cancelling placement.");
        EndPlacing();
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
