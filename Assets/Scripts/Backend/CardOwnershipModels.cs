using System.Collections.Generic;

// Card ownership and leveling system - manual upgrades only

/// Represents a player's card ownership and copy count
/// </summary>
[System.Serializable]
public class PlayerCardOwnership
{
    public string cardId;           // Card identifier (must match DeckRegistry)
    public string cardName;         // Card name
    public int level;               // Current card level (1-5, max 5)
    public bool owned;              // Whether player owns this card (level >= 1)
    public int quantity;            // Total number of copies owned
    public int timesAcquired;       // How many times this card was obtained
    public System.DateTime acquiredDate; // When player first got this card

    /// <summary>
    /// Get display string (Lv.1, Lv.2, Lv.5, etc.)
    /// </summary>
    public string GetDisplayStatus()
    {
        return $"Lv.{level}";
    }
}

/// <summary>
/// Collection of all cards a player owns
/// Stored in PlayFab Player Data under "player_cards"
/// </summary>
[System.Serializable]
public class PlayerCardCollection
{
    public List<PlayerCardOwnership> cards = new List<PlayerCardOwnership>();

    public bool OwnsCard(string cardId)
    {
        return cards.Exists(c => c.cardId == cardId && c.level >= 1);
    }

    public int GetCardLevel(string cardId)
    {
        var card = cards.Find(c => c.cardId == cardId);
        return card != null ? card.level : 0;
    }

    public int GetCardQuantity(string cardId)
    {
        var card = cards.Find(c => c.cardId == cardId);
        return card != null ? card.quantity : 0;
    }

    /// <summary>
    /// Award copies of a card to the player.
    /// Automatically unlocks card (sets to Level 1) on first copy received.
    /// Does NOT auto-level - player must manually upgrade with currency.
    /// </summary>
    public void AcquireCard(string cardId, string cardName, int copiesToAdd = 1)
    {
        var existing = cards.Find(c => c.cardId == cardId);

        if (existing == null)
        {
            // New card: unlock to Level 1
            cards.Add(new PlayerCardOwnership
            {
                cardId = cardId,
                cardName = cardName,
                level = 1,      // Unlock on first copy
                owned = true,
                quantity = copiesToAdd,
                timesAcquired = 1,
                acquiredDate = System.DateTime.UtcNow
            });
        }
        else
        {
            // Existing card: just add to quantity
            existing.quantity += copiesToAdd;
            existing.timesAcquired++;

            // Ensure unlocked
            if (existing.level < 1)
            {
                existing.level = 1;
                existing.owned = true;
            }
        }
    }

    /// <summary>
    /// Manually level up a card (player initiated with currency).
    /// Called when player spends currency to upgrade the card level.
    /// </summary>
    public bool LevelUpCard(string cardId, int maxLevel = 5)
    {
        var card = cards.Find(c => c.cardId == cardId);
        if (card == null)
            return false;

        // Can only level up if already owned and not at max
        if (card.level >= maxLevel)
            return false;

        card.level++;
        return true;
    }
}

/// <summary>
/// Master list of all cards in the game (stored in PlayFab Title Data)
/// This is read-only and defines what cards exist
/// </summary>
[System.Serializable]
public class CardLibrary
{
    public List<CardLibraryEntry> cards = new List<CardLibraryEntry>();

    public CardLibraryEntry GetCard(string cardId)
    {
        return cards.Find(c => c.id == cardId);
    }
}

/// <summary>
/// A card definition in the library
/// </summary>
[System.Serializable]
public class CardLibraryEntry
{
    public string id;               // Unique identifier (e.g., "card_1")
    public string name;             // Display name
    public string description;      // Card description
    public string cardType;         // Type of card (e.g., "unit", "spell", etc.)
    public int maxLevel;            // Maximum level this card can reach (default 5)
    public bool starterCard;        // If true, all new players get this card
}
