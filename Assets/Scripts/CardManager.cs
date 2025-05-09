using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class CardManager : MonoBehaviour
{
    public Transform cardPanel;
    public GameObject cardButtonPrefab;

    private Queue<CardData> cardQueue = new();
    private PaddleController localPaddle;
    private List<Button> cardButtons = new();

    private float boost = 0f;
    private const int visibleCards = 4;

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
        PopulateDeckUI();

        if (localDeck.selectedCastingLegend != null)
        {
            boost = localDeck.selectedCastingLegend.damageBoostPercent;
        }
        GameManager.Instance.SendClientReadyServerRpc();

    }

    void PopulateDeckUI()
    {
        foreach (Transform child in cardPanel)
        {
            Destroy(child.gameObject);
        }
        cardButtons.Clear();

        CardData[] cardsArray = cardQueue.ToArray();

        for (int i = 0; i < Mathf.Min(visibleCards, cardsArray.Length); i++)
        {
            if (cardPanel == null)
            {
                Debug.LogError("[CardManager] cardPanel is not assigned!");
                continue;
            }

            CardData card = cardsArray[i];
            GameObject holderObj = Instantiate(cardButtonPrefab, cardPanel);
            Button btn = holderObj.GetComponent<Button>();

            // Get CardHolderUI component
            CardHolderUI holderUI = holderObj.GetComponent<CardHolderUI>();
            if (holderUI != null)
            {
                holderUI.Setup(card);
            }
            else
            {
                Debug.LogWarning("[CardManager] CardHolderUI component not found on card prefab.");
            }

            btn.onClick.AddListener(() => OnCardPlayed(card));
            cardButtons.Add(btn);
        }
    }



    void OnCardPlayed(CardData card)
    {
        if (localPaddle != null && localPaddle.currentMana.Value >= (int)card.manaCost)
        {
            if (card.effect.RequiresPlacement())
            {
                // Delay card usage until placement is done
                card.effect.StartPlacement((position, direction) =>
                {
                    Debug.Log($"[CardManager] Client: Placement confirmed by UnitPlacer. Position: {position}, Direction: {direction}. Sending to server.");
                    GameManager.Instance.ApplyCardEffectServerRpc(card.cardName, NetworkManager.Singleton.LocalClientId, position, direction);
                    localPaddle.ModifyManaServerRpc(-(int)card.manaCost);

                    cardQueue.Dequeue();
                    cardQueue.Enqueue(ScriptableObject.Instantiate(card));
                    PopulateDeckUI();
                });
            }
            else
            {
                Debug.Log("Card played: " + card.cardName);
                Debug.Log($"[CardManager] Client: Non-placement card. Sending {card.cardName} to server.");
                GameManager.Instance.ApplyCardEffectServerRpc(card.cardName, NetworkManager.Singleton.LocalClientId);
                localPaddle.ModifyManaServerRpc(-(int)card.manaCost);

                cardQueue.Dequeue();
                cardQueue.Enqueue(ScriptableObject.Instantiate(card));
                PopulateDeckUI();
            }
        }
        else
        {
            Debug.Log("Not enough mana!");
        }
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
