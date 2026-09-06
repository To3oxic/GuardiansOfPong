using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
public class CardManager : MonoBehaviour
{
    // public Transform cardPanel; // Old panel, to be replaced
    // public GameObject cardButtonPrefab; // Still needed if CardHolderUI instances are not direct prefabs
    // New UI slot references - Assign these in the Inspector
    public CardHolderUI leftHolderSlot1;
    public CardHolderUI leftHolderSlot2;
    public CardHolderUI rightHolderSlot1;
    public CardHolderUI rightHolderSlot2;

    [Header("Placement Preview")]
    public GameObject placementPreviewObject; // Assign the gradient circle GameObject from the main canvas

    private CircularCardDeck cardDeck = new CircularCardDeck();
    private PaddleController localPaddle;
    // private List<Button> cardButtons = new(); // May not be needed if we directly interact with CardHolderUI buttons
    private float boost = 0f;
    private const int visibleCards = 4; // Reinstated: defines the number of cards in hand/slots

    public static CardManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        // Make sure the placement preview is initially disabled
        if (placementPreviewObject != null)
        {
            placementPreviewObject.SetActive(false);
        }
    }
    void Start()
    {
    }
    public void StartCardUIInitialization()
    {
        StartCoroutine(InitializeCardUI());
    }
    IEnumerator InitializeCardUI()
    {
        float timeout = 10f;
        float elapsed = 0f;
        while (localPaddle == null && elapsed < timeout)
        {
            PaddleController[] paddles = FindObjectsByType<PaddleController>(FindObjectsSortMode.None);
            foreach (var paddle in paddles)
            {
                if (paddle.IsOwner)
                {
                    localPaddle = paddle;
                    break;
                }
            }
            if (localPaddle != null)
            {
                break;
            }
            elapsed += 0.2f;
            yield return new WaitForSeconds(0.2f);
        }
        if (localPaddle == null)
        {
            yield break;
        }
        var localDeck = DeckManager.Instance?.GetSelectedDeck();
        if (localDeck == null || localDeck.selectedCards.Count == 0)
        {
            yield break;
        }
        cardDeck.Clear();
        foreach (var card in localDeck.selectedCards)
        {
            cardDeck.AddCard(card);
        }
        // PopulateDeckUI(); // Old call
        InitialPopulateCardSlots(); // New initial population method
        if (localDeck.selectedCastingLegend != null)
        {
            boost = localDeck.selectedCastingLegend.damageBoostPercent;
        }
        GameManager.Instance.SendClientReadyServerRpc();
    }

    void InitialPopulateCardSlots()
    {
        CardHolderUI[] slots = { leftHolderSlot1, leftHolderSlot2, rightHolderSlot1, rightHolderSlot2 };
        CardData[] cardsFromDeck = cardDeck.GetVisibleCards(slots.Length);

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                continue;
            }
            if (i < cardsFromDeck.Length)
            {
                CardData cardToDisplay = cardsFromDeck[i];
                slots[i].Setup(cardToDisplay);

                // Get the CardDragHandler component
                CardDragHandler dragHandler = slots[i].GetComponent<CardDragHandler>();
                if (dragHandler != null)
                {
                    dragHandler.Initialize(cardToDisplay);
                }
            }
            else
            {
                slots[i].Setup(null); // Clear the slot if we don't have enough cards
            }
        }
    }
    public void OnCardPlayed(CardData card, CardHolderUI playedSlot) // Added playedSlot parameter
    {
        Debug.Log($"[CardManager] OnCardPlayed called for card: {card?.cardName ?? "NULL"}, Mana: {localPaddle?.currentMana.Value ?? 0}/{card?.manaCost ?? 0}");

        if (localPaddle != null && localPaddle.currentMana.Value >= (int)card.manaCost)
        {
            if (card.effect.RequiresPlacement())
            {
                Debug.Log($"[CardManager] Card {card.cardName} requires placement");
                card.effect.StartPlacement((position, direction) =>
                {
                    // Double-check placement is in valid area (z: 0-40, x: -25 to 25)
                    bool isValidPlacement = position.z >= 0f && position.z <= 40f &&
                                          position.x >= -25f && position.x <= 25f;

                    if (isValidPlacement)
                    {
                        Debug.Log($"[CardManager] Valid placement at {position}, calling ApplyCardEffectServerRpc");
                        GameManager.Instance.ApplyCardEffectServerRpc(card.cardName, NetworkManager.Singleton.LocalClientId, position, direction);
                        HandleCardQueueAndRefreshSlots(card, playedSlot);
                    }
                    else
                    {
                        Debug.LogWarning($"[CardManager] Card placement rejected - position {position} is outside valid area");
                    }
                });
            }
            else
            {
                // Non-placement cards are always valid
                Debug.Log($"[CardManager] Non-placement card {card.cardName} - calling ApplyCardEffectServerRpc");
                GameManager.Instance.ApplyCardEffectServerRpc(card.cardName, NetworkManager.Singleton.LocalClientId);
                HandleCardQueueAndRefreshSlots(card, playedSlot);
            }
        }
        else
        {
            // Optionally, provide feedback to the player here (e.g., sound effect, UI flash)
            Debug.LogWarning($"[CardManager] Insufficient mana to play card {card.cardName}. Required: {card.manaCost}, Available: {localPaddle?.currentMana.Value ?? 0}");
        }
    }
    public void HandleCardQueueAndRefreshSlots(CardData playedCardData, CardHolderUI playedSlot)
    {
        // Step 1: Immediately empty the specific slot that was used
        playedSlot.Setup(null);

        // Step 2: Remove the card and cycle it to the end in one operation
        cardDeck.RemoveAndCycleCard(playedCardData);

        // Step 3: Repopulate all slots with the new order
        InitialPopulateCardSlots();
    }
    public CardData GetNextCard()
    {
        if (cardDeck.Count > visibleCards)
        {
            CardNode cardNode = cardDeck.GetCardAt(visibleCards);
            return cardNode?.cardData;
        }
        return null;
    }

    // Placement preview control methods
    public void ShowPlacementPreview(Vector3 screenPosition, float radius, Color color)
    {
        if (placementPreviewObject != null)
        {
            placementPreviewObject.SetActive(true);
            placementPreviewObject.transform.position = screenPosition;

            // Scale based on radius
            placementPreviewObject.transform.localScale = new Vector3(radius, radius, 1f);
            Debug.Log($"[CardManager] Showing placement preview at {screenPosition} with radius {radius}.");

            // Set color
            Image previewImage = placementPreviewObject.GetComponent<Image>();
            if (previewImage != null)
            {
                previewImage.color = color;
            }
        }
    }

    public void HidePlacementPreview()
    {
        if (placementPreviewObject != null)
        {
            placementPreviewObject.SetActive(false);
        }
    }
}
