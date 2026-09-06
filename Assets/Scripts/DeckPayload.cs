using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[System.Serializable]
public class LegendStone
{
    public string id;
    public string registry_name;
    public string card_type;
    public int level;
}

[System.Serializable]
public class GuardianCard
{
    public string id;
    public string registry_name;
    public string card_type;
    public int level;
}

[System.Serializable]
public class DeckPayload : INetworkSerializable
{
    // Nested DTO classes for construction
    [System.Serializable]
    public class CardDataDto
    {
        public string id;
        public string registry_name;
        public string card_type;
        public int level;
    }

    [System.Serializable]
    public class LegendStoneDto
    {
        public string id;
        public string registry_name;
        public string card_type;
        public int level;
    }

    [System.Serializable]
    public class GuardianCardDto
    {
        public string id;
        public string registry_name;
        public string card_type;
        public int level;
    }

    public string id;
    public string deck_name;
    public string floorId;
    public List<CardDataDto> cards = new List<CardDataDto>();
public LegendStoneDto legend_stone;
    public GuardianCardDto guardian_card;

    // Helper properties for easy access
    public List<string> cardIds
    {
        get
        {
            var ids = new List<string>();
            if (cards != null)
            {
                foreach (var card in cards)
                {
                    ids.Add(card.id);
                }
            }
            return ids;
        }
    }

    public string castingLegendId => guardian_card?.id ?? string.Empty;
    public string legendStoneId => legend_stone?.id ?? string.Empty;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        // Handle floor ID
        string fId = floorId ?? string.Empty;
        serializer.SerializeValue(ref fId);
        if (serializer.IsReader) floorId = fId;

        // Handle legend ID and level
string castingLegendIdVal = castingLegendId ?? string.Empty;
        int legendLevel = guardian_card?.level ?? 1;
        serializer.SerializeValue(ref castingLegendIdVal);
        serializer.SerializeValue(ref legendLevel);

        // Handle stone ID and level
        string legendStoneIdVal = legendStoneId ?? string.Empty;
        int stoneLevel = legend_stone?.level ?? 1;
        serializer.SerializeValue(ref legendStoneIdVal);
        serializer.SerializeValue(ref stoneLevel);

        // When receiving, create the objects if needed
        if (serializer.IsReader)
        {
            guardian_card = new GuardianCardDto { id = castingLegendIdVal, registry_name = castingLegendIdVal, level = legendLevel };
            legend_stone = new LegendStoneDto { id = legendStoneIdVal, registry_name = legendStoneIdVal, level = stoneLevel };
        }

        Debug.Log($"[DeckPayload] Serializing deck - Legend: '{castingLegendIdVal}' (Lvl {legendLevel}), Stone: '{legendStoneIdVal}' (Lvl {stoneLevel}), Cards: {cardIds?.Count ?? 0}");

        // Serialize list size
        int count = cardIds?.Count ?? 0;
        serializer.SerializeValue(ref count);

        if (serializer.IsReader)
        {
            // Reconstruct cards from network data
            cards = new List<CardDataDto>(count);
            for (int i = 0; i < count; i++)
            {
                string cardId = string.Empty;
                int cardLevel = 1;
                serializer.SerializeValue(ref cardId);
                serializer.SerializeValue(ref cardLevel);
                cards.Add(new CardDataDto { id = cardId, registry_name = cardId, level = cardLevel });
            }
            Debug.Log($"[DeckPayload] Deserialized deck - Legend: '{castingLegendIdVal}' (Lvl {legendLevel}), Stone: '{legendStoneIdVal}' (Lvl {stoneLevel}), Cards: {cards.Count}");
            if (cards.Count > 0)
            {
                Debug.Log($"[DeckPayload] Card IDs and levels: {string.Join(", ", cardIds)}");
            }
        }
        else
        {
            // Send card IDs and levels
            for (int i = 0; i < count; i++)
            {
                string cardId = cardIds[i] ?? string.Empty;
                int cardLevel = cards[i]?.level ?? 1;
                serializer.SerializeValue(ref cardId);
                serializer.SerializeValue(ref cardLevel);
            }
            if (cardIds != null && cardIds.Count > 0)
            {
                Debug.Log($"[DeckPayload] Sending cards: {string.Join(", ", cardIds)}");
            }
        }
    }

    /// <summary>
    /// Get the level of a card by its ID
    /// </summary>
    public int GetCardLevel(string cardId)
    {
        if (cards == null) return 1;
        var cardDto = cards.Find(c => c.id == cardId);
        return cardDto?.level ?? 1;
    }
}
