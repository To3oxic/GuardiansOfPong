using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.Netcode;

[RequireComponent(typeof(CardHolderUI))]
public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private CardData cardData;
    private CardEffect cardEffect;
    private Camera mainCamera;
    private GameObject previewInstance;
    private Vector3 lastValidPosition;
    private Vector3 lastValidDirection;
    private Vector2 dragStartPosition;
    private float dragActivationThreshold = 100f; // Distance in pixels to activate non-placement cards
    private float cardDisappearDistance = 200f; // Distance in pixels before card disappears
    private bool isDragging = false;
    private bool isValidPlacement = false;
    private PaddleController localPaddle = null;
    private RectTransform cardRectTransform = null;
    private Vector2 cardStartPosition = Vector2.zero;
    private CanvasGroup cardCanvasGroup = null;

    [Header("Placement Preview")]
    public Color validPlacementColor = Color.white;
    public Color invalidPlacementColor = Color.red;
    public float previewYOffset = 0.1f; // Slight offset to avoid z-fighting

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    public void Initialize(CardData data)
    {
        PaddleController[] allPaddles = FindObjectsByType<PaddleController>(FindObjectsSortMode.None);
        foreach (var paddle in allPaddles)
        {
            if (paddle.IsOwner)
            {
                localPaddle = paddle;
                break;
            }
        }
        cardData = data;
        if (data != null)
        {
            cardEffect = data.effect;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (cardEffect == null) return;

        isDragging = true;
        dragStartPosition = eventData.position;

        // Get the RectTransform and CanvasGroup for UI card movement
        cardRectTransform = GetComponent<RectTransform>();
        if (cardRectTransform != null)
        {
            cardStartPosition = cardRectTransform.anchoredPosition;
        }

        // Get or add CanvasGroup for fade effect
        cardCanvasGroup = GetComponent<CanvasGroup>();
        if (cardCanvasGroup == null)
        {
            cardCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        cardCanvasGroup.alpha = 1f;

        // For placement cards, create effect preview
        if (cardEffect.RequiresPlacement())
        {
            GameObject previewPrefab = cardEffect.GetPreviewPrefab();
            if (previewPrefab != null)
            {
                previewInstance = Instantiate(previewPrefab);
                previewInstance.SetActive(false);

                // Remove or disable NetworkObject components to prevent accidental network spawning
                NetworkObject netObj = previewInstance.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    Destroy(netObj);
                }

                // Also disable any other network components that might interfere
                NetworkBehaviour[] networkBehaviours = previewInstance.GetComponentsInChildren<NetworkBehaviour>();
                foreach (var behaviour in networkBehaviours)
                {
                    if (behaviour != netObj) // Skip the already destroyed NetworkObject
                    {
                        behaviour.enabled = false;
                    }
                }
            }
        }
        // For non-placement cards, just start tracking drag distance
    }

    public void OnDrag(PointerEventData eventData)
    {
        bool isInValidPlayArea = false;
        if (!isDragging) return;

        // Get world position from screen point
        Ray ray = mainCamera.ScreenPointToRay(eventData.position);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 position = hit.point + Vector3.up * previewYOffset;
            Vector3 direction = Vector3.forward;

            // Check if placement is in valid play area using GameManager bounds

            if (GameManager.Instance != null)
            {
                float leftX = GameManager.Instance.LeftWallX;
                float rightX = GameManager.Instance.RightWallX;
                float player1GoalZ = GameManager.Instance.Player1GoalZ;
                float player2GoalZ = GameManager.Instance.Player2GoalZ;

                // Check if position is within field boundaries
                bool withinX = position.x >= leftX && position.x <= rightX;

                // Check placement zone based on local paddle position
                bool withinZ = false;
                if (localPaddle != null)
                {
                    float paddleZ = localPaddle.transform.position.z;
                    // Placement zone is between 0 and the paddle's Z position
                    if (paddleZ < 0)
                    {
                        withinZ = position.z >= paddleZ && position.z <= 0f;
                    }
                    else
                    {
                        withinZ = position.z >= 0f && position.z <= paddleZ;
                    }
                }

                isInValidPlayArea = withinX && withinZ;
            }
            else
            {
                // Fallback to hardcoded values if GameManager is not available
                float checkZ = position.z;
                if (NetworkManager.Singleton.LocalClientId == 1) // Client 1 (player 1)
                {
                    isInValidPlayArea = checkZ >= 0f && checkZ <= 40f &&
                                           position.x >= -25f && position.x <= 25f;
                }
                else // Client 2 (player 2)
                {
                    isInValidPlayArea = checkZ >= -40f && checkZ <= 0f &&
                                           position.x >= -25f && position.x <= 25f;
                }
            }
            position.y = 0;
            lastValidPosition = position;
            lastValidDirection = direction;
            isValidPlacement = isInValidPlayArea;

            // Choose color and alpha based on validity
            Color previewColor;

            if (isValidPlacement)
            {
                previewColor = validPlacementColor;
                previewColor.a = 0.4f;
            }
            else
            {
                // Invalid area - use invalid color with 20% alpha
                previewColor = invalidPlacementColor;
                previewColor.a = 0.4f;
            }

            // Show the shared placement preview
            float radius = cardEffect.GetEffectRadius();
            Vector3 screenPos = mainCamera.WorldToScreenPoint(position);
            CardManager.Instance?.ShowPlacementPreview(screenPos, radius, previewColor);

            // Update effect preview
            if (previewInstance != null)
            {
                previewInstance.transform.position = position;
                previewInstance.transform.forward = direction;
            }
        }
        else
        {
            isValidPlacement = false;
            // Show invalid placement (no raycast hit)
            float radius = cardEffect.GetEffectRadius();
            Color invalidColor = invalidPlacementColor;
            invalidColor.a = 0.2f;
            CardManager.Instance?.ShowPlacementPreview(eventData.position, radius, invalidColor);
        }

        // Move the UI card to follow the drag position
        if (cardRectTransform != null)
        {
            Vector2 dragDelta = eventData.position - dragStartPosition;
            cardRectTransform.anchoredPosition = cardStartPosition + dragDelta;

            // Calculate drag distance
            float dragDistance = Vector2.Distance(dragStartPosition, eventData.position);

            // Fade out and make clickable based on distance
            if (isInValidPlayArea)
            {
                // Card should disappear (fully transparent)
                cardCanvasGroup.alpha = 0f;
                cardCanvasGroup.blocksRaycasts = false;
            }
            else
            {
                // Fade out gradually based on distance
                cardCanvasGroup.alpha = 1f;
                cardCanvasGroup.blocksRaycasts = true;
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        // Hide the placement preview
        CardManager.Instance?.HidePlacementPreview();

        // Calculate drag distance
        float dragDistance = Vector2.Distance(dragStartPosition, eventData.position);

        // Reset card position and opacity
        if (cardRectTransform != null)
        {
            cardRectTransform.anchoredPosition = cardStartPosition;
        }
        if (cardCanvasGroup != null)
        {
            cardCanvasGroup.alpha = 1f;
            cardCanvasGroup.blocksRaycasts = true;
        }

        // For placement cards, check valid placement
        if (cardEffect.RequiresPlacement())
        {
            if (isValidPlacement)
            {
                // Get the CardHolderUI component
                CardHolderUI cardHolder = GetComponent<CardHolderUI>();
                if (cardHolder != null && CardManager.Instance != null)
                {
                    // Apply the card effect directly with the calculated position and direction
                    Vector3 position = lastValidPosition;
                    Vector3 direction = lastValidDirection;

                    // Check mana cost before applying
                    if (localPaddle != null && localPaddle.currentMana.Value >= (int)cardData.manaCost)
                    {
                        GameManager.Instance.ApplyCardEffectServerRpc(cardData.cardName, NetworkManager.Singleton.LocalClientId, position, direction);
                        localPaddle.ModifyManaServerRpc(-(int)cardData.manaCost);

                        // Remove the card from queue immediately after successfully sending ServerRpc
                        CardManager.Instance.HandleCardQueueAndRefreshSlots(cardData, cardHolder);
                    }
                    else
                    {
                    }
                }
            }
        }
        // For non-placement cards, check drag distance threshold
        else
        {
            if (dragDistance >= dragActivationThreshold)
            {
                CardHolderUI cardHolder = GetComponent<CardHolderUI>();
                if (cardHolder != null && CardManager.Instance != null)
                {
                    CardManager.Instance.OnCardPlayed(cardData, cardHolder);
                }
            }
            else
            {
            }
        }

        CleanupPreviews();
        isDragging = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // For non-placeable cards, activate immediately on click
        if (cardEffect != null && !cardEffect.RequiresPlacement())
        {
            CardHolderUI cardHolder = GetComponent<CardHolderUI>();

            if (cardHolder != null && CardManager.Instance != null)
            {
                CardManager.Instance.OnCardPlayed(cardData, cardHolder);
            }
        }
        else
        {
        }
    }

    private void CleanupPreviews()
    {
        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
        }
    }

    private void OnDisable()
    {
        CleanupPreviews();
        CardManager.Instance?.HidePlacementPreview();
    }
}