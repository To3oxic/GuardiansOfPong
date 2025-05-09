using UnityEngine;
using UnityEngine.UI;

public class CardHolderUI : MonoBehaviour
{
    public Image rarityBackground;
    public Image cardImage;

    public Sprite commonBG;
    public Sprite rareBG;
    public Sprite epicBG;

    public void Setup(CardData data)
    {
        // Set rarity background
        switch (data.rarity)
        {
            case CardRarity.Common:
                rarityBackground.sprite = commonBG;
                break;
            case CardRarity.Rare:
                rarityBackground.sprite = rareBG;
                break;
            case CardRarity.Epic:
                rarityBackground.sprite = epicBG;
                break;
        }

        // Set card image
        cardImage.sprite = data.cardImage;
    }
}
