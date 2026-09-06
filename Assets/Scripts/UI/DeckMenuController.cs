using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeckMenuController : MonoBehaviour
{
    [Header("Refs")]
    public Transform cardsContent;      // Scroll -> Viewport -> Content
    public Transform selectedGrid;      // Grid container for selected cards
    public Transform legendsContent;    // Scroll -> Viewport -> Content
    public Transform stonesContent;     // Scroll -> Viewport -> Content
    public GameObject buttonPrefab;     // Button with TMP child for label
    public TextMeshProUGUI deckCountText;
    public Button confirmButton;

    [Header("Rules")]
    public int minCards = 6;
    public int maxCards = 8;

    void Start()
    {
        PopulateLists();
        RefreshSelected();
        if (confirmButton) confirmButton.onClick.AddListener(OnConfirm);
    }

    void PopulateLists()
    {
        // Cards
        foreach (Transform child in cardsContent) Destroy(child.gameObject);
        foreach (var card in DeckRegistry.Instance.allCards)
        {
            var go = Instantiate(buttonPrefab, cardsContent);
            var btn = go.GetComponent<Button>();
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            string cardId = card.id;
            label.text = cardId;

            btn.onClick.AddListener(() =>
            {
                if (DeckSelectionState.SelectedCardIds.Contains(cardId))
                {
                    DeckSelectionState.SelectedCardIds.Remove(cardId);
                }
                else
                {
                    if (DeckSelectionState.SelectedCardIds.Count >= maxCards) return;
                    DeckSelectionState.SelectedCardIds.Add(cardId);
                }
                RefreshSelected();
            });
        }

        // Legends
        foreach (Transform child in legendsContent) Destroy(child.gameObject);
        foreach (var guardian in DeckRegistry.Instance.allGuardians)
        {
            var go = Instantiate(buttonPrefab, legendsContent);
            var btn = go.GetComponent<Button>();
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            label.text = guardian.id;
            btn.onClick.AddListener(() =>
            {
                DeckSelectionState.SelectedGuardianId = guardian.id;
                RefreshSelected();
            });
        }

        // Stones
        foreach (Transform child in stonesContent) Destroy(child.gameObject);
        foreach (var stone in DeckRegistry.Instance.allLegendStones)
        {
            var go = Instantiate(buttonPrefab, stonesContent);
            var btn = go.GetComponent<Button>();
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            label.text = stone.id;
            btn.onClick.AddListener(() =>
            {
                DeckSelectionState.SelectedLegendStoneId = stone.id;
                RefreshSelected();
            });
        }
    }

    void RefreshSelected()
    {
        foreach (Transform child in selectedGrid) Destroy(child.gameObject);

        foreach (var id in DeckSelectionState.SelectedCardIds)
        {
            var go = Instantiate(buttonPrefab, selectedGrid);
            var btn = go.GetComponent<Button>();
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            if (btn) btn.interactable = false;
            label.text = name;
        }

        if (deckCountText)
            deckCountText.text = $"{DeckSelectionState.SelectedCardIds.Count}/{maxCards}";
    }

    public void OnConfirm()
    {
        bool valid = DeckSelectionState.SelectedCardIds.Count >= minCards
                     && DeckSelectionState.SelectedCardIds.Count <= maxCards
                     && !string.IsNullOrEmpty(DeckSelectionState.SelectedGuardianId)
                     && !string.IsNullOrEmpty(DeckSelectionState.SelectedLegendStoneId);

        if (!valid)
        {
            Debug.LogWarning("[DeckMenu] Selection invalid. Pick 6–8 cards, 1 legend, 1 stone.");
            return;
        }

        Debug.Log("[DeckMenu] Deck selection saved.");
        // Optionally mirror to DeckManager here if desired.
    }
}