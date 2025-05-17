using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "DeckRegistry", menuName = "Decks/Deck Registry")]
public class DeckRegistry : ScriptableObject
{
    public List<CardData> allCards;
    public List<CastingLegendData> allCastingLegends;
    public List<LegendStoneData> allLegendStones;

    public static DeckRegistry Instance;

    public void InitializeRegistry()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public CardData GetCardByName(string name)
    {
        return allCards.FirstOrDefault(c => c.cardName == name);
    }

    public CastingLegendData GetCastingLegendByName(string name)
    {
        return allCastingLegends.FirstOrDefault(l => l.name == name);
    }

    public LegendStoneData GetLegendStoneByName(string name)
    {
        return allLegendStones.FirstOrDefault(s => s.stoneName == name);
    }
}
