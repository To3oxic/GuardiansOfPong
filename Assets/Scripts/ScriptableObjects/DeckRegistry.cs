using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "DeckRegistry", menuName = "Decks/Deck Registry")]
public class DeckRegistry : ScriptableObject
{
    public List<CardData> allCards;
    public List<CastingLegendData> allGuardians;
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

    public CardData GetCardById(string id)
    {
        return allCards.FirstOrDefault(c => c.id == id);
    }

    public CastingLegendData GetCastingLegendByName(string name)
    {
        // Compare against the data field 'legendName' (not the Unity Object name)
        return allGuardians.FirstOrDefault(l => l.legendName == name);
    }

    public CastingLegendData GetCastingLegendById(string id)
    {
        return allGuardians.FirstOrDefault(l => l.id == id);
    }

    public LegendStoneData GetLegendStoneByName(string name)
    {
        return allLegendStones.FirstOrDefault(s => s.stoneName == name);
    }

    public LegendStoneData GetLegendStoneById(string id)
    {
        return allLegendStones.FirstOrDefault(s => s.id == id);
    }
}
