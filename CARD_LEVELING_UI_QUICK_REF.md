# Card Leveling UI Quick Reference

## What Changed

Players now have **quantities** of each card instead of just ownership levels. Cards auto-level based on accumulated copies.

---

## Key Values to Display

### 1. Card Status (Already Owned?)
```csharp
var card = playerCards.GetCardCollection().cards.Find(c => c.cardId == cardId);
bool isOwned = card.level > 0;  // Level 0 = locked, Level 1+ = owned

if (isOwned) {
    cardButton.interactable = true;
    cardImage.color = Color.white;
} else {
    cardButton.interactable = false;
    cardImage.color = new Color(0.5f, 0.5f, 0.5f, 0.6f); // Greyed
}
```

### 2. Card Level Display
```csharp
int level = card.level;  // 0, 1, 2, 3, 4, 5
string levelText = (level == 0) ? "Locked" : $"Lv.{level}";
levelText.text = levelText;
```

### 3. Card Quantity / Progress to Next Level
```csharp
int quantity = card.quantity;  // Total copies owned
string quantityText = $"{quantity} copies";
quantityText.text = quantityText;

// Show progress to next level
if (card.level == 0) {
    progressText.text = "0/1";  // Need 1 copy to unlock
} else {
    int nextRequired = libraryEntry.GetCopiesRequiredForLevel(card.level + 1);
    progressText.text = $"{quantity}/{nextRequired}";
}
```

---

## Simple Card Button Prefab Layout

```
CardButton (Button)
├── CardImage (Image)
├── CardName (Text) → "Fireball"
├── CardLevel (Text) → "Lv.2" or "Locked"
├── CardQuantity (Text) → "3 copies"
└── ProgressBar (Slider or Text)
    └── "3/6" (progress to level 3)
```

---

## Update PopulateCardList() Method

```csharp
void PopulateCardList()
{
    // Clear existing
    foreach (Transform child in cardListContainer)
        Destroy(child.gameObject);

    var allCards = PlayFabBackendManager.Instance.GetAllCardsWithOwnershipStatus();
    var library = PlayFabBackendManager.Instance.GetCardLibrary();

    foreach (var cardOwn in allCards)
    {
        // Skip legends/stones
        if (cardOwn.cardId.StartsWith("legend_") || cardOwn.cardId.StartsWith("stone_"))
            continue;

        var libraryEntry = library.GetCard(cardOwn.cardId);
        if (libraryEntry == null) continue;

        GameObject buttonObj = Instantiate(cardButtonPrefab, cardListContainer);
        Button button = buttonObj.GetComponent<Button>();
        Text nameText = buttonObj.transform.Find("CardName").GetComponent<Text>();
        Text levelText = buttonObj.transform.Find("CardLevel").GetComponent<Text>();
        Text quantityText = buttonObj.transform.Find("CardQuantity").GetComponent<Text>();
        Text progressText = buttonObj.transform.Find("Progress").GetComponent<Text>();
        Image cardImage = buttonObj.GetComponent<Image>();

        // Set display values
        nameText.text = cardOwn.cardName;
        levelText.text = cardOwn.GetDisplayStatus();  // "Locked" or "Lv.1"
        quantityText.text = $"{cardOwn.quantity}";
        
        // Progress to next level
        if (cardOwn.level == 0) {
            progressText.text = "0/1";  // Need 1 to unlock
        } else if (cardOwn.level >= libraryEntry.maxLevel) {
            progressText.text = "Max";
        } else {
            int nextRequired = libraryEntry.GetCopiesRequiredForLevel(cardOwn.level + 1);
            progressText.text = $"{cardOwn.quantity}/{nextRequired}";
        }

        // Visual state
        if (cardOwn.owned)
        {
            button.interactable = true;
            cardImage.color = Color.white;
        }
        else
        {
            button.interactable = false;
            cardImage.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        }

        // Click handler
        button.onClick.AddListener(() => ToggleCard(cardOwn.cardId, cardOwn));
    }
}
```

---

## Example: Award 5 Card Packs to Player

```csharp
void OnCardPackOpened()
{
    // Simulate opening 5 random cards
    string[] cardIds = { "card_fireball", "card_heal", "card_shield", "card_attack", "card_draw" };
    
    foreach (string cardId in cardIds)
    {
        var libraryEntry = PlayFabBackendManager.Instance.GetCardLibrary().GetCard(cardId);
        
        // Award 1 copy with leveling config
        var config = new CardLevelingConfig {
            cardId = cardId,
            maxLevel = 5,
            copiesForLevel1 = 1,
            copiesForLevel2 = 2,
            copiesForLevel3 = 3,
            copiesForLevel4 = 4,
            copiesForLevel5 = 5
        };

        PlayFabBackendManager.Instance.AwardCardToPlayer(
            cardId,
            libraryEntry.name,
            1,  // Award 1 copy
            config,
            (success) => {
                if (success) {
                    var card = PlayFabBackendManager.Instance.GetCardCollection().cards.Find(c => c.cardId == cardId);
                    Debug.Log($"Got {libraryEntry.name}! Level: {card.level}, Total: {card.quantity}");
                }
            }
        );
    }
    
    // Refresh UI after all awards
    Invoke(nameof(RefreshCardDisplay), 0.5f);
}
```

---

## Migration: Update Existing Code

### Before (Old)
```csharp
public void AcquireCard(string cardId, string cardName, int level = 1)
{
    var existing = cards.Find(c => c.cardId == cardId);
    if (existing == null)
    {
        cards.Add(new PlayerCardOwnership {
            cardId = cardId,
            cardName = cardName,
            level = level,
            owned = true
        });
    }
}
```

### After (New)
```csharp
// Automatically handles quantities and leveling
PlayFabBackendManager.Instance.AwardCardToPlayer(
    cardId,
    cardName,
    1,  // 1 copy (or more)
    levelingConfig,  // Auto-level based on quantity
    callback
);
```

---

## Testing Checklist

- [ ] Player logs in → Cards load with quantities
- [ ] New player → Gets starter cards at Level 1
- [ ] Award 2 copies → Locked card unlocks to Level 1
- [ ] Award 2 more (3 total) → Auto-upgrades to Level 2
- [ ] Award 3 more (6 total) → Auto-upgrades to Level 3
- [ ] UI shows "2/3" progress for next level
- [ ] Greyed out cards can't be selected for deck
- [ ] Owned cards show correct level indicator
- [ ] Deck saves with owned cards only
- [ ] Server validates deck (see PLAYFAB_DECK_SECURITY_ANTICHEAT.md)

---

## Common Issues

**Q: Card won't unlock even after awarding copies**
A: Make sure `AwardCardToPlayer()` is called, not manual `AcquireCard()`. The manager method handles auto-leveling.

**Q: Quantity shows 0 but card is owned**
A: Card was manually added before quantity system. Run:
```csharp
card.quantity = Mathf.Max(card.quantity, card.level);  // Sync quantity to level
PlayFabBackendManager.Instance.SaveCardCollectionToPlayFab();
```

**Q: Cards not refreshing in UI after award**
A: Call `RefreshCardDisplay()` after the callback completes:
```csharp
PlayFabBackendManager.Instance.AwardCardToPlayer(id, name, 1, config, (success) => {
    if (success) RefreshCardDisplay();
});
```

**Q: How to let player manually level up?**
A: Use `LevelUpCard()` with confirmation:
```csharp
int copiesNeeded = libraryEntry.GetCopiesRequiredForLevel(card.level + 1);
if (ShowConfirm($"Spend {copiesNeeded} copies?")) {
    PlayFabBackendManager.Instance.LevelUpCard(cardId, copiesNeeded, RefreshCardDisplay);
}
```
