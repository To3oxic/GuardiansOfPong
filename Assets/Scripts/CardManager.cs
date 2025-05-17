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

    private Queue<CardData> cardQueue = new();
    private PaddleController localPaddle;
    // private List<Button> cardButtons = new(); // May not be needed if we directly interact with CardHolderUI buttons

    private float boost = 0f;
    private const int visibleCards = 4; // Reinstated: defines the number of cards in hand/slots

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
                Debug.Log("[CardManager] Local paddle found!");
                break;
            }

            elapsed += 0.2f;
            yield return new WaitForSeconds(0.2f);
        }

        if (localPaddle == null)
        {
            Debug.LogWarning("[CardManager] Local paddle not found within time.");
            yield break;
        }

        var localDeck = DeckManager.Instance?.GetSelectedDeck();
        if (localDeck == null || localDeck.selectedCards.Count == 0)
        {
            Debug.LogWarning("[CardManager] No local deck found.");
            yield break;
        }

        cardQueue.Clear();
        foreach (var card in localDeck.selectedCards)
        {
            cardQueue.Enqueue(ScriptableObject.Instantiate(card));
        }
        // PopulateDeckUI(); // Old call
        InitialPopulateCardSlots(); // New initial population method

        if (localDeck.selectedCastingLegend != null)
        {
            boost = localDeck.selectedCastingLegend.damageBoostPercent;
        }
        GameManager.Instance.SendClientReadyServerRpc();

    }

    // void PopulateDeckUI() // Old method, to be replaced
    // {
    //     foreach (Transform child in cardPanel)
    //     {
    //         Destroy(child.gameObject);
    //     }
    //     cardButtons.Clear();
    //
    //     CardData[] cardsArray = cardQueue.ToArray();
    //
    //     for (int i = 0; i < Mathf.Min(visibleCards, cardsArray.Length); i++)
    //     {
    //         if (cardPanel == null)
    //         {
    //             Debug.LogError("[CardManager] cardPanel is not assigned!");
    //             continue;
    //         }
    //
    //         CardData card = cardsArray[i];
    //         GameObject holderObj = Instantiate(cardButtonPrefab, cardPanel);
    //         Button btn = holderObj.GetComponent<Button>();
    //
    //         // Get CardHolderUI component
    //         CardHolderUI holderUI = holderObj.GetComponent<CardHolderUI>();
    //         if (holderUI != null)
    //         {
    //             holderUI.Setup(card);
    //         }
    //         else
    //         {
    //             Debug.LogWarning("[CardManager] CardHolderUI component not found on card prefab.");
    //         }
    //
    //         btn.onClick.AddListener(() => OnCardPlayed(card)); // This needs to change to pass slot info
    //         cardButtons.Add(btn);
    //     }
    // }

    void InitialPopulateCardSlots()
    {
        CardHolderUI[] slots = { leftHolderSlot1, leftHolderSlot2, rightHolderSlot1, rightHolderSlot2 };
        CardData[] cardsFromQueue = cardQueue.ToArray();

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                Debug.LogError($"[CardManager] Slot {i} is not assigned in the Inspector!");
                continue;
            }

            if (i < cardsFromQueue.Length)
            {
                CardData cardToDisplay = cardsFromQueue[i];
                slots[i].Setup(cardToDisplay);
                Button btn = slots[i].GetComponent<Button>(); // Assuming CardHolderUI is on the same GameObject as the Button
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners(); // Clear previous listeners
                    // Pass the card and the slot identifier (could be the CardHolderUI itself or an index/enum)
                    // For simplicity, passing the CardHolderUI instance to identify the slot.
                    CardHolderUI slot = slots[i]; // Capture the slot variable for the lambda
                    btn.onClick.AddListener(() => OnCardPlayed(cardToDisplay, slot));
                }
                else
                {
                    Debug.LogWarning($"[CardManager] Button component not found on slot {i}.");
                }
            }
            else
            {
                // slots[i].Clear(); // If not enough cards in queue, clear the slot - TODO: Implement Clear() in CardHolderUI.cs
                Button btn = slots[i].GetComponent<Button>();
                if (btn != null) btn.onClick.RemoveAllListeners();
            }
        }
    }


    void OnCardPlayed(CardData card, CardHolderUI playedSlot) // Added playedSlot parameter
    {
        if (localPaddle != null && localPaddle.currentMana.Value >= (int)card.manaCost)
        {
            if (card.effect.RequiresPlacement())
            {
                card.effect.StartPlacement((position, direction) =>
                {
                    Debug.Log($"[CardManager] Client: Placement confirmed. Position: {position}, Direction: {direction}. Sending to server.");
                    GameManager.Instance.ApplyCardEffectServerRpc(card.cardName, NetworkManager.Singleton.LocalClientId, position, direction);
                    localPaddle.ModifyManaServerRpc(-(int)card.manaCost);
                    HandleCardQueueAndRefreshSlots(card, playedSlot);
                });
            }
            else
            {
                Debug.Log("Card played: " + card.cardName);
                Debug.Log($"[CardManager] Client: Non-placement card. Sending {card.cardName} to server.");
                GameManager.Instance.ApplyCardEffectServerRpc(card.cardName, NetworkManager.Singleton.LocalClientId);
                localPaddle.ModifyManaServerRpc(-(int)card.manaCost);
                HandleCardQueueAndRefreshSlots(card, playedSlot);
            }
        }
        else
        {
            Debug.Log("Not enough mana!");
            // Optionally, provide feedback to the player here (e.g., sound effect, UI flash)
        }
    }

    void HandleCardQueueAndRefreshSlots(CardData playedCardData, CardHolderUI playedSlot)
    {
        // 1. Remove the actual played card instance from the queue
        // This is tricky because the queue contains instantiated ScriptableObjects.
        // We need to find and remove the one that matches playedCardData (e.g., by cardName or a unique ID if you add one).
        // For simplicity, assuming the first card in the queue that matches the name is the one.
        // A more robust solution might involve giving CardData instances a unique ID upon instantiation for the queue.

        List<CardData> tempList = new List<CardData>(cardQueue);
        CardData cardToRemove = null;
        foreach (CardData cDataInQueue in tempList)
        {
            // If ScriptableObject.Instantiate(card) was used, card references won't match.
            // We need a reliable way to identify the *specific instance* that was in that slot.
            // For now, let's assume playedCardData IS the instance from the slot.
            // The current PopulateDeckUI gives the card from the queue array to the slot.
            if (cDataInQueue == playedCardData) // This relies on playedCardData being the exact instance from the queue
            {
                cardToRemove = cDataInQueue;
                break;
            }
        }

        if (cardToRemove != null)
        {
            // Rebuild queue without the removed card to maintain order, then add it to the end.
            Queue<CardData> newQueue = new Queue<CardData>();
            foreach (CardData c in cardQueue)
            {
                if (c != cardToRemove)
                {
                    newQueue.Enqueue(c);
                }
            }
            newQueue.Enqueue(ScriptableObject.Instantiate(cardToRemove)); // Add a new instance of the played card to the end
            cardQueue = newQueue;
        }
        else
        {
            // Fallback: if we couldn't find the exact instance, dequeue and enqueue a new one (original behavior)
            // This might lead to card order issues if multiple same cards are in hand.
            Debug.LogWarning($"[CardManager] Could not find exact card instance '{playedCardData.cardName}' in queue to remove. Resorting to Dequeue/Enqueue.");
            cardQueue.Dequeue(); // Removes the front card, which *should* be the one played if logic is correct
            cardQueue.Enqueue(ScriptableObject.Instantiate(playedCardData)); // Add a new instance to the end
        }


        // Identify which holder the playedSlot belongs to
        bool isLeftHolder = playedSlot == leftHolderSlot1 || playedSlot == leftHolderSlot2;
        bool isRightHolder = playedSlot == rightHolderSlot1 || playedSlot == rightHolderSlot2;

        CardData[] currentQueueSnapshot = cardQueue.ToArray(); // Cards available to draw

        if (isLeftHolder)
        {
            if (playedSlot == leftHolderSlot1)
            {
                // Card from leftHolderSlot1 was played.
                // Move card from leftHolderSlot2 to leftHolderSlot1
                if (leftHolderSlot2.gameObject.activeSelf && leftHolderSlot2.GetComponent<Button>().onClick.GetPersistentEventCount() > 0) // Check if slot2 has a card
                {
                    // We need the CardData instance that was in leftHolderSlot2
                    // This requires CardHolderUI to store or provide access to its current CardData
                    // For now, let's assume we re-fetch from the queue based on what *should* be there.
                    // This part is tricky without CardHolderUI storing its current CardData.
                    // Let's simplify for now: refresh based on the new queue state.
                    // The "shifting" will be implicitly handled by re-populating from the queue.
                }
            }
            // else: card from leftHolderSlot2 was played. Slot 2 becomes empty.
        }
        else if (isRightHolder)
        {
            if (playedSlot == rightHolderSlot1)
            {
                // Card from rightHolderSlot1 was played.
                // Move card from rightHolderSlot2 to rightHolderSlot1
            }
            // else: card from rightHolderSlot2 was played. Slot 2 becomes empty.
        }

        // The described "shifting" logic is complex if we don't have CardHolderUI store its current card.
        // The original PopulateDeckUI() is simpler and achieves the "new card from queue, used to last"
        // but without the specific intra-holder shift.
        // For a true shift, CardHolderUI would need to expose its current CardData.

        // Let's stick to a re-population of all slots for now, which matches the old behavior's outcome
        // regarding card cycling, but uses the new slot structure.
        // A more advanced shift will require changes to CardHolderUI.
        InitialPopulateCardSlots(); // Re-populate all slots based on the updated queue.
                                    // This will correctly place the "next" card from the queue into an
                                    // effectively empty slot and show the new order.
    }


    public CardData GetNextCard()
    {
        if (cardQueue.Count > visibleCards)
        {
            CardData[] cards = cardQueue.ToArray();
            return cards[visibleCards];
        }
        return null;
    }
}
