using UnityEngine;
using System.Collections;

public class ScrollController : MonoBehaviour
{
    [Header("Scroll Settings")]
    [SerializeField] private float scrollSpeed = 5f; // Camera movement speed
    [SerializeField] private float snapThreshold = 0.5f; // Distance threshold to snap
    [SerializeField] private bool useMouseScroll = true;
    [SerializeField] private bool useSwipe = true;

    private Transform gameBoardZone;
    private Transform cardSelectionZone;

    private Camera mainCamera;
    private Vector3 gameboardCameraPos;
    private Vector3 cardSelectionCameraPos;

    private bool isScrolling = false;
    private int currentZone = 0; // 0 = GameBoard, 1 = CardSelection

    private Vector2 touchStartPos;
    private const float SWIPE_THRESHOLD = 50f;

    public void Initialize(Transform boardZone, Transform cardZone)
    {
        gameBoardZone = boardZone;
        cardSelectionZone = cardZone;
        mainCamera = Camera.main;

        // Calculate camera positions for each zone
        // Assuming zones are laid out horizontally
        gameboardCameraPos = mainCamera.transform.position;

        // Position camera to show CardSelection zone (shift right)
        cardSelectionCameraPos = gameboardCameraPos + Vector3.right * 20f; // Adjust based on your zone width

        // Start at GameBoard zone
        mainCamera.transform.position = gameboardCameraPos;
        currentZone = 0;
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        if (useMouseScroll && Input.mouseScrollDelta.y != 0)
        {
            HandleMouseScroll();
        }

        if (useSwipe && Input.touchCount > 0)
        {
            HandleSwipe();
        }

        // DEBUG: Arrow keys for testing
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            ScrollToZone(1);
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            ScrollToZone(0);
        }
    }

    private void HandleMouseScroll()
    {
        if (Input.mouseScrollDelta.y > 0 && currentZone > 0)
        {
            ScrollToZone(0);
        }
        else if (Input.mouseScrollDelta.y < 0 && currentZone < 1)
        {
            ScrollToZone(1);
        }
    }

    private void HandleSwipe()
    {
        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            touchStartPos = touch.position;
        }
        else if (touch.phase == TouchPhase.Ended)
        {
            Vector2 swipeDelta = touch.position - touchStartPos;

            // Swipe left (negative X) = move right in the scene
            if (swipeDelta.x < -SWIPE_THRESHOLD && currentZone < 1)
            {
                ScrollToZone(1);
            }
            // Swipe right (positive X) = move left in the scene
            else if (swipeDelta.x > SWIPE_THRESHOLD && currentZone > 0)
            {
                ScrollToZone(0);
            }
        }
    }

    public void ScrollToZone(int zoneIndex)
    {
        if (isScrolling || zoneIndex == currentZone)
            return;

        if (zoneIndex < 0 || zoneIndex > 1)
            return;

        currentZone = zoneIndex;

        Vector3 targetPos = (zoneIndex == 0) ? gameboardCameraPos : cardSelectionCameraPos;
        StartCoroutine(SmoothScroll(targetPos));
    }

    private IEnumerator SmoothScroll(Vector3 targetPos)
    {
        isScrolling = true;
        float elapsed = 0f;
        float duration = 0.8f; // Adjust for faster/slower scroll

        Vector3 startPos = mainCamera.transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Use easing for smooth feel (ease in-out)
            t = t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        mainCamera.transform.position = targetPos;
        isScrolling = false;
    }

    public int GetCurrentZone() => currentZone;
    public bool IsScrolling() => isScrolling;
}
