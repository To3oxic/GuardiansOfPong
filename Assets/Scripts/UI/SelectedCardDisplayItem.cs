using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System; // For Action

public class SelectedCardDisplayItem : MonoBehaviour
{
    public Image cardImage;
    public TextMeshProUGUI cardNameText; // Optional
    public Button deselectButton;

    private CardData _cardData;
    private Action<CardData> _onDeselectCallback;

    public void Initialize(CardData cardData, Action<CardData> onDeselectCallback)
    {
        _cardData = cardData;
        _onDeselectCallback = onDeselectCallback;

        if (cardImage != null && _cardData.cardImage != null)
        {
            cardImage.sprite = _cardData.cardImage;
            cardImage.enabled = true;
        }
        else if (cardImage != null)
        {
            cardImage.enabled = false;
        }

        if (cardNameText != null)
        {
            cardNameText.text = _cardData.cardName;
            cardNameText.enabled = true;
        }

        if (deselectButton != null)
        {
            deselectButton.onClick.RemoveAllListeners();
            deselectButton.onClick.AddListener(HandleDeselect);
        }
    }

    void HandleDeselect()
    {
        _onDeselectCallback?.Invoke(_cardData);
    }
}