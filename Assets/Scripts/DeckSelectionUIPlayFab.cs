using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

/// <summary>
/// Updated DeckSelectionUI that integrates with PlayFab card ownership system.
/// Shows all cards but greys out cards the player doesn't own yet.
/// </summary>
public class DeckSelectionUIPlayFab : MonoBehaviour
{
    public Transform cardListContainer;
    public Transform legendListContainer;
    public Transform stoneListContainer;
    public GameObject cardButtonPrefab;
    public GameObject legendButtonPrefab;
    public GameObject stoneButtonPrefab;
    public Button startMatchButton;
    public GameObject connectionUI; // UI with Host/Client buttons
    public GameObject deckSelectionUI; // This panel
    public Color ownedCardColor = Color.white;
    public Color unownedCardColor = new Color(0.5f, 0.5f, 0.5f, 0.6f); // Greyed out

    private List<CardData> selectedCards = new List<CardData>();
    private CastingLegendData selectedLegend;
    private LegendStoneData selectedStone;
    private PlayerCardCollection playerCards;

    void Start()
    {
        connectionUI.SetActive(true);
        deckSelectionUI.SetActive(false);
        NetworkManager.Singleton.OnClientConnectedCallback += OnNetworkConnected;
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnNetworkConnected;
        }
    }

    void OnNetworkConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            connectionUI.SetActive(false);
            deckSelectionUI.SetActive(true);

            // Load player's cards and populate UI
            LoadAndPopulateUI();
        }
    }

    void LoadAndPopulateUI()
    {
        // Load player's card collection from PlayFab
        PlayFabBackendManager.Instance.LoadPlayerCardCollection((collection) =>
        {
            playerCards = collection;

            // Load master card library
            PlayFabBackendManager.Instance.LoadCardLibrary((library) =>
            {
                PopulateCardList();
                PopulateLegendList();
                PopulateStoneList();
            });
        });
    }

    void PopulateCardList()
    {
        // Clear existing buttons
        foreach (Transform child in cardListContainer)
        {
            Destroy(child.gameObject);
        }

        // Get all cards with ownership status
        var allCards = PlayFabBackendManager.Instance.GetAllCardsWithOwnershipStatus();

        foreach (var cardOwnership in allCards)
        {
            // Only show non-legend, non-stone cards
            if (cardOwnership.cardId.StartsWith("legend_") || cardOwnership.cardId.StartsWith("stone_"))
                continue;

            // Find the actual CardData for this card
            var cardData = DeckRegistry.Instance.allCards.Find(c =>
                c.cardName.ToLower().Replace(" ", "_") == cardOwnership.cardId.ToLower());

            if (cardData != null)
            {
                GameObject buttonObj = Instantiate(cardButtonPrefab, cardListContainer);
                Button button = buttonObj.GetComponent<Button>();
                Text buttonText = buttonObj.GetComponentInChildren<Text>();
                Image buttonImage = buttonObj.GetComponent<Image>();

                buttonText.text = cardOwnership.cardName;

                if (cardOwnership.owned)
                {
                    // Owned card - enable interaction
                    button.interactable = true;
                    buttonImage.color = ownedCardColor;
                    button.onClick.AddListener(() => ToggleCard(cardData));
                }
                else
                {
                    // Unowned card - grey out and disable
                    button.interactable = false;
                    buttonImage.color = unownedCardColor;
                    buttonText.text += " (Locked)";
                }
            }
        }
    }

    void ToggleCard(CardData card)
    {
        // Check if player actually owns this card
        if (!PlayFabBackendManager.Instance.OwnsCard(
            card.cardName.ToLower().Replace(" ", "_")))
        {
            return;
        }

        if (selectedCards.Contains(card))
        {
            selectedCards.Remove(card);
        }
        else if (selectedCards.Count < 8)
        {
            selectedCards.Add(card);
        }
    }

    void PopulateLegendList()
    {
        // Clear existing buttons
        foreach (Transform child in legendListContainer)
        {
            Destroy(child.gameObject);
        }

        var allCards = PlayFabBackendManager.Instance.GetAllCardsWithOwnershipStatus();

        foreach (var cardOwnership in allCards)
        {
            // Only show legend cards
            if (!cardOwnership.cardId.StartsWith("legend_"))
                continue;

            // Find the actual legend data
            var legend = DeckRegistry.Instance.allGuardians.Find(l =>
                $"legend_{l.legendName.ToLower().Replace(" ", "_")}" == cardOwnership.cardId);

            if (legend != null)
            {
                GameObject buttonObj = Instantiate(legendButtonPrefab, legendListContainer);
                Button button = buttonObj.GetComponent<Button>();
                Text buttonText = buttonObj.GetComponentInChildren<Text>();
                Image buttonImage = buttonObj.GetComponent<Image>();

                buttonText.text = cardOwnership.cardName;

                if (cardOwnership.owned)
                {
                    button.interactable = true;
                    buttonImage.color = ownedCardColor;
                    button.onClick.AddListener(() => selectedLegend = legend);
                }
                else
                {
                    button.interactable = false;
                    buttonImage.color = unownedCardColor;
                    buttonText.text += " (Locked)";
                }
            }
        }
    }

    void PopulateStoneList()
    {
        // Clear existing buttons
        foreach (Transform child in stoneListContainer)
        {
            Destroy(child.gameObject);
        }

        var allCards = PlayFabBackendManager.Instance.GetAllCardsWithOwnershipStatus();

        foreach (var cardOwnership in allCards)
        {
            // Only show stone cards
            if (!cardOwnership.cardId.StartsWith("stone_"))
                continue;

            // Find the actual stone data
            var stone = DeckRegistry.Instance.allLegendStones.Find(s =>
                $"stone_{s.stoneName.ToLower().Replace(" ", "_")}" == cardOwnership.cardId);

            if (stone != null)
            {
                GameObject buttonObj = Instantiate(stoneButtonPrefab, stoneListContainer);
                Button button = buttonObj.GetComponent<Button>();
                Text buttonText = buttonObj.GetComponentInChildren<Text>();
                Image buttonImage = buttonObj.GetComponent<Image>();

                buttonText.text = cardOwnership.cardName;

                if (cardOwnership.owned)
                {
                    button.interactable = true;
                    buttonImage.color = ownedCardColor;
                    button.onClick.AddListener(() => selectedStone = stone);
                }
                else
                {
                    button.interactable = false;
                    buttonImage.color = unownedCardColor;
                    buttonText.text += " (Locked)";
                }
            }
        }
    }

    public List<CardData> GetSelectedCards() => selectedCards;
    public CastingLegendData GetSelectedLegend() => selectedLegend;
    public LegendStoneData GetSelectedStone() => selectedStone;

    /// <summary>
    /// Finalize and save the selected deck to PlayFab before starting game.
    /// This prevents card manipulation by storing the exact deck server-side.
    /// </summary>
    public void FinalizeAndSaveDeck(string deckName, Action<bool> callback = null)
    {
        if (selectedCards.Count == 0 || selectedLegend == null || selectedStone == null)
        {
            callback?.Invoke(false);
            return;
        }

        // Collect IDs from selected cards
        var cardIds = new List<string>();

        // Add card IDs
        foreach (var card in selectedCards)
        {
            cardIds.Add(card.id);
        }

        // Add legend ID
        cardIds.Add(selectedLegend.id);

        // Add stone ID
        cardIds.Add(selectedStone.id);

        // Save to PlayFab using IDs
        PlayFabBackendManager.Instance.SaveDeck(deckName, cardIds, (success) =>
        {
            if (success)
            {
            }
            callback?.Invoke(success);
        });
    }
}