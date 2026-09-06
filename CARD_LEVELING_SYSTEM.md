# Card Leveling System - Implementation Guide

## Overview

The card leveling system allows players to accumulate card copies to unlock and level up cards:

- **Locked**: Player has 0 copies (Level 0)
- **Unlocked Lv.1**: Player has 1 copy (Level 1)
- **Unlocked Lv.2**: Player has 1 + 2 = 3 total copies (Level 2)
- **Unlocked Lv.3**: Player has 1 + 2 + 3 = 6 total copies (Level 3)
- **Unlocked Lv.4**: Player has 1 + 2 + 3 + 4 = 10 total copies (Level 4)
- **Unlocked Lv.5**: Player has 1 + 2 + 3 + 4 + 5 = 15 total copies (Level 5 - Max)

---

## Data Structure

### PlayerCardOwnership
```csharp
public class PlayerCardOwnership
{
    public string cardId;           // Card ID
    public string cardName;         // Card name
    public int level;               // 0=locked, 1-5=unlocked
    public bool owned;              // level > 0
    public int quantity;            // Total copies owned
    public int timesAcquired;       // Total duplicates received
    public DateTime acquiredDate;   // First acquisition date
    
    // Methods:
    public string GetDisplayStatus();           // "Locked" or "Lv.1"
    public string GetNextLevelProgress(int needed); // "2/3" progress
}
```

### CardLibraryEntry
```csharp
public class CardLibraryEntry
{
    // ... existing fields ...
    public int copiesForLevel1 = 1;    // 1 copy to unlock
    public int copiesForLevel2 = 2;    // 2 MORE to level 2
    public int copiesForLevel3 = 3;    // 3 MORE to level 3
    public int copiesForLevel4 = 4;    // 4 MORE to level 4
    public int copiesForLevel5 = 5;    // 5 MORE to level 5
    
    public int GetCopiesRequiredForLevel(int level);       // Get requirement for level
    public int GetTotalCopiesNeededForLevel(int level);    // Get total cumulative (e.g., 6 for level 3)
}
```

### CardLevelingConfig
```csharp
public class CardLevelingConfig
{
    public string cardId;
    public int maxLevel = 5;
    public int copiesForLevel1 = 1;
    public int copiesForLevel2 = 2;
    public int copiesForLevel3 = 3;
    public int copiesForLevel4 = 4;
    public int copiesForLevel5 = 5;
    
    public int GetCopiesRequiredForLevel(int level);
    public int GetTotalCopiesNeededForLevel(int level);
}
```

---

## API Usage

### Award Card Copies to Player

```csharp
// Award 1 copy of a card to player
// Will auto-unlock if player gets their first copy
PlayFabBackendManager.Instance.AwardCardToPlayer(
    "card_fireball",           // Card ID
    "Fireball",                // Card name
    1,                         // Number of copies to award
    null,                      // Optional leveling config (uses defaults if null)
    (success) => {
        if (success) {
            Debug.Log("Card awarded and saved");
        }
    }
);

// Award 3 copies with auto-leveling enabled
var levelingConfig = new CardLevelingConfig {
    cardId = "card_fireball",
    maxLevel = 5,
    copiesForLevel1 = 1,
    copiesForLevel2 = 2,
    copiesForLevel3 = 3,
    copiesForLevel4 = 4,
    copiesForLevel5 = 5
};

PlayFabBackendManager.Instance.AwardCardToPlayer(
    "card_fireball",
    "Fireball",
    3,                      // Award 3 copies (might trigger level up)
    levelingConfig,
    (success) => { }
);
```

### Level Up Card Manually

```csharp
// Level up card by spending copies
// Example: To go from Lv.1 to Lv.2, need 2 copies
PlayFabBackendManager.Instance.LevelUpCard(
    "card_fireball",
    2,                      // Copies required for level up
    (success) => {
        if (success) {
            Debug.Log("Card leveled up!");
        } else {
            Debug.Log("Not enough copies");
        }
    }
);
```

### Get Card Information

```csharp
var collection = PlayFabBackendManager.Instance.GetCardCollection();

// Check if card is owned (level > 0)
bool owns = collection.OwnsCard("card_fireball");

// Get current level (0 = locked)
int level = collection.GetCardLevel("card_fireball");

// Get total copies owned
int quantity = collection.GetCardQuantity("card_fireball");

// Get display status
var card = collection.cards.Find(c => c.cardId == "card_fireball");
string status = card.GetDisplayStatus();     // "Locked" or "Lv.5"
string progress = card.GetNextLevelProgress(3); // "2/3"
```

---

## UI Integration Examples

### Card Button Display

```csharp
void UpdateCardButton(PlayerCardOwnership cardOwn, CardLibraryEntry libraryEntry)
{
    // Get button components
    Button cardButton = GetComponent<Button>();
    Text cardName = transform.Find("CardName").GetComponent<Text>();
    Text cardStatus = transform.Find("CardStatus").GetComponent<Text>();
    Text cardProgress = transform.Find("CardProgress").GetComponent<Text>();
    Image cardImage = GetComponent<Image>();

    // Update display
    cardName.text = cardOwn.cardName;
    cardStatus.text = cardOwn.GetDisplayStatus();  // "Locked" or "Lv.1"
    cardProgress.text = cardOwn.GetNextLevelProgress(libraryEntry.GetCopiesRequiredForLevel(cardOwn.level + 1));
    
    // Visual feedback
    if (cardOwn.owned)
    {
        // Owned: white, interactable
        cardImage.color = Color.white;
        cardButton.interactable = true;
    }
    else
    {
        // Locked: greyed out, disabled
        cardImage.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        cardButton.interactable = false;
    }
}
```

### Card Collection View

```csharp
void PopulateCardCollection()
{
    var collection = PlayFabBackendManager.Instance.GetCardCollection();
    var library = PlayFabBackendManager.Instance.GetCardLibrary();

    foreach (var card in collection.cards)
    {
        var libraryEntry = library.GetCard(card.cardId);
        
        // Display card details
        Debug.Log($"{card.cardName}:");
        Debug.Log($"  Status: {card.GetDisplayStatus()}");
        Debug.Log($"  Copies: {card.quantity}");
        Debug.Log($"  Progress to next level: {card.GetNextLevelProgress(libraryEntry.GetCopiesRequiredForLevel(card.level + 1))}");
    }
}
```

### Leveling Up UI

```csharp
void OnLevelUpButtonClicked(string cardId)
{
    var collection = PlayFabBackendManager.Instance.GetCardCollection();
    var card = collection.cards.Find(c => c.cardId == cardId);
    var libraryEntry = PlayFabBackendManager.Instance.GetCardLibrary().GetCard(cardId);

    if (card.level >= libraryEntry.maxLevel)
    {
        ShowMessage("Card is already max level!");
        return;
    }

    int copiesNeeded = libraryEntry.GetCopiesRequiredForLevel(card.level + 1);

    if (card.quantity < copiesNeeded)
    {
        ShowMessage($"Need {copiesNeeded} copies, only have {card.quantity}");
        return;
    }

    // Confirm and level up
    if (ShowConfirmDialog($"Spend {copiesNeeded} copies to level up?"))
    {
        PlayFabBackendManager.Instance.LevelUpCard(
            cardId,
            copiesNeeded,
            (success) => {
                if (success)
                {
                    ShowMessage($"Card leveled up to Lv.{card.level + 1}!");
                    RefreshCardDisplay();
                }
            }
        );
    }
}
```

---

## DeckSelectionUI Integration

### Update Card Selection Logic

```csharp
public class DeckSelectionUIPlayFab : MonoBehaviour
{
    void ToggleCard(CardData cardData, PlayerCardOwnership cardOwn)
    {
        // Only allow selecting owned cards
        if (!cardOwn.owned)
        {
            Debug.Log($"Card {cardData.cardName} is locked. Need level 1+");
            return;
        }

        // Check if card is already selected
        var selectedCard = selectedCards.Find(c => c.cardName == cardData.cardName);

        if (selectedCard != null)
        {
            selectedCards.Remove(selectedCard);
        }
        else if (selectedCards.Count < 5)
        {
            selectedCards.Add(cardData);
        }
        else
        {
            Debug.Log("Deck is full (5 cards max)");
        }

        RefreshCardButtonStates();
    }

    void RefreshCardButtonStates()
    {
        // Update visual state of all card buttons
        foreach (Transform child in cardListContainer)
        {
            Button button = child.GetComponent<Button>();
            Text statusText = child.Find("Status").GetComponent<Text>();

            // Find corresponding card ownership
            var cardOwn = /* find card ownership */;

            // Update state
            if (cardOwn.owned)
            {
                statusText.text = cardOwn.GetDisplayStatus();
                button.interactable = true;
            }
            else
            {
                statusText.text = "Locked";
                button.interactable = false;
            }

            // Highlight if selected
            if (selectedCards.Exists(c => c.cardName == cardOwn.cardName))
            {
                child.GetComponent<Image>().color = new Color(0.7f, 0.9f, 0.7f); // Green highlight
            }
        }
    }
}
```

---

## Sample Game Flow

```
1. Player logs in
   ↓
2. LoadPlayerCardCollection() loads cards with quantities
   ↓
3. PlayerCardCollection auto-levels cards based on quantities:
   - Has 1 copy → Level 1
   - Has 3 copies → Level 2 (auto-unlocked)
   - Has 6 copies → Level 3 (auto-unlocked)
   - Has 10 copies → Level 4 (auto-unlocked)
   - Has 15 copies → Level 5 (auto-unlocked)
   ↓
4. UI displays each card's status and progress:
   - Locked cards (quantity 0): Greyed out, uninteractable
   - Level 1 cards: White, selectable
   - Level 2+ cards: White, selectable with level indicator
   ↓
5. Server/IAP awards cards when player:
   - Completes campaigns
   - Wins matches
   - Opens card packs
   - Makes purchases
   ↓
6. AwardCardToPlayer() is called
   - Adds copies to quantity
   - Auto-levels if threshold reached
   - Saves to PlayFab
   ↓
7. UI automatically refreshes to show new level
   ↓
8. Player can now optionally:
   - Use manual LevelUpCard() for controlled leveling
   - Or let auto-leveling handle it
```

---

## Configuration Examples

### Default Leveling (Used if CardLevelingConfig is null)
```
Level 1: 1 copy
Level 2: 2 MORE copies (3 total)
Level 3: 3 MORE copies (6 total)
Level 4: 4 MORE copies (10 total)
Level 5: 5 MORE copies (15 total)
```

### Custom Leveling (Rare Cards - More Expensive)
```csharp
var rareConfig = new CardLevelingConfig {
    cardId = "card_legendary_dragon",
    maxLevel = 3,  // Can only level to 3
    copiesForLevel1 = 1,
    copiesForLevel2 = 5,  // Need 5 MORE copies for level 2
    copiesForLevel3 = 10   // Need 10 MORE copies for level 3
};
// Total: 1 → 6 → 16
```

### Custom Leveling (Common Cards - Cheaper)
```csharp
var commonConfig = new CardLevelingConfig {
    cardId = "card_common_unit",
    maxLevel = 5,
    copiesForLevel1 = 1,
    copiesForLevel2 = 1,  // Only need 1 MORE
    copiesForLevel3 = 1,  // Only need 1 MORE
    copiesForLevel4 = 1,  // Only need 1 MORE
    copiesForLevel5 = 1   // Only need 1 MORE
};
// Total: 1 → 2 → 3 → 4 → 5 (same as rarity)
```

---

## Anti-Cheat Protection

The quantity-based leveling system is protected via PlayFab:

✅ **Immutable Record**: Card quantities stored in PlayFab Player Data
✅ **Server Validation**: Server compares client-sent deck against PlayFab record
✅ **Exploit Prevention**: Attempting to send cards with higher levels than saved → Disconnected
✅ **Audit Trail**: All card acquisitions logged with timestamps

See `PLAYFAB_DECK_SECURITY_ANTICHEAT.md` for server-side validation logic.

---

## Testing Scenarios

```csharp
// Test 1: Award card copies and verify auto-leveling
PlayFabBackendManager.Instance.AwardCardToPlayer("card_test", "Test Card", 1);
// Expected: Level 1

PlayFabBackendManager.Instance.AwardCardToPlayer("card_test", "Test Card", 2);
// Expected: Level 2 (1 + 2 = 3 total copies)

PlayFabBackendManager.Instance.AwardCardToPlayer("card_test", "Test Card", 3);
// Expected: Level 3 (1 + 2 + 3 = 6 total copies)

// Test 2: Manual level up with insufficient copies
PlayFabBackendManager.Instance.LevelUpCard("card_test", 5, (success) => {
    Debug.Log(success ? "Leveled up" : "Not enough copies");
    // Expected: false (need 4 copies for level 4, but only have 1)
});

// Test 3: Deck ownership validation
bool owns = PlayFabBackendManager.Instance.GetCardCollection().OwnsCard("card_test");
Debug.Log(owns); // Expected: true (level >= 1)

// Test 4: Locked vs owned
var card = PlayFabBackendManager.Instance.GetCardCollection().cards.Find(c => c.cardId == "card_locked");
Debug.Log(card.GetDisplayStatus()); // Expected: "Locked" if level == 0
```

---

## Performance Notes

- **Auto-Leveling**: O(maxLevel) calculation per card award (fast, ~1ms)
- **Storage**: Each card entry ~100 bytes in PlayFab
- **Sync**: Entire collection saved/loaded per change (efficient for typical player size)
- **Scaling**: Supports thousands of cards, no performance impact

---

## Future Enhancements

1. **Card Fusion**: Spend copies to fuse cards into better versions
2. **Prestige System**: Reset level, gain special rewards
3. **Card Trading**: Allow players to trade duplicate copies
4. **Mastery Tracking**: Track lifetime cards received, display prestige badges
5. **Dynamic Costs**: Adjust copy requirements based on card rarity/power level
