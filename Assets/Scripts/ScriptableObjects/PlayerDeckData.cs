using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewPlayerDeck", menuName = "Decks/Player Deck")]
public class PlayerDeckData : ScriptableObject
{
    [Tooltip("Must contain between 6 and 8 cards.")]
    [Min(6)]
    public List<CardData> selectedCards = new List<CardData>(8);
    public CastingLegendData selectedCastingLegend;
    public LegendStoneData selectedLegendStone;
}
