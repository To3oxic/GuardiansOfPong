using System.Collections.Generic;

public static class DeckSelectionState
{
    public static readonly HashSet<string> SelectedCardIds = new HashSet<string>();
    public static string SelectedGuardianId;
    public static string SelectedLegendStoneId;

    public static void Clear()
    {
        SelectedCardIds.Clear();
        SelectedGuardianId = null;
        SelectedLegendStoneId = null;
    }

    public static DeckPayload BuildPayloadFromCurrent()
    {
        var deck = new DeckPayload
        {
            guardian_card = new DeckPayload.GuardianCardDto
            {
                registry_name = SelectedGuardianId ?? string.Empty,
                id = SelectedGuardianId ?? string.Empty
            },
            legend_stone = new DeckPayload.LegendStoneDto
            {
                registry_name = SelectedLegendStoneId ?? string.Empty,
                id = SelectedLegendStoneId ?? string.Empty
            }
        };

        // Add card registry names
        if (SelectedCardIds != null && SelectedCardIds.Count > 0)
        {
            deck.cards = new List<DeckPayload.CardDataDto>();
            foreach (var cardId in SelectedCardIds)
            {
                deck.cards.Add(new DeckPayload.CardDataDto
                {
                    registry_name = cardId,
                    id = cardId,
                    level = 5  // Default level 5 for local testing
                });
            }
        }

        return deck;
    }
}