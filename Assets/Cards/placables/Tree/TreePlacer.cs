using UnityEngine;
using UnityEngine.EventSystems;
using System;
using Unity.Netcode;

public class TreePlacer : MonoBehaviour
{
    public static TreePlacer Instance;

    public GameObject treePreviewPrefab;
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

    public void StartPlacing(Action<Vector3, Vector3> callback)
    {
        placementCallback = callback;
        isPlacing = true;
        ShowPreview(Vector3.zero);
        Debug.Log("[TreePlacer] Started placing tree...");
    }

    private void Update()
    {
        if (!isPlacing || mainCam == null) return;

        Vector3? point = RaycastWorldPoint();
        if (point.HasValue && currentPreview != null)
        {
            currentPreview.transform.position = point.Value;
        }

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            if (point.HasValue)
            {
                placementCallback?.Invoke(point.Value, Vector3.zero); // direction not needed
                Debug.Log("[TreePlacer] Placement confirmed.");
                EndPlacing();
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("[TreePlacer] Placement cancelled.");
            CancelPlacing();
        }
    }

    private Vector3? RaycastWorldPoint()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit3D)) return hit3D.point;

        Vector2 ray2D = mainCam.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit2D = Physics2D.Raycast(ray2D, Vector2.zero);
        if (hit2D.collider != null) return hit2D.point;

        return null;
    }

    private void ShowPreview(Vector3 position)
    {
        if (treePreviewPrefab == null) return;
        currentPreview = Instantiate(treePreviewPrefab, position, Quaternion.LookRotation(Vector3.forward));
    }

    private void EndPlacing()
    {
        isPlacing = false;
        placementCallback = null;
        if (currentPreview != null) Destroy(currentPreview);
    }

    private void CancelPlacing() => EndPlacing();

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
