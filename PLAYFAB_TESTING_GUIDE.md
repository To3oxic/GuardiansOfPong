# PlayFab Card System - Testing Guide

## Phase 1: Setup

### 1. Create PlayFab Title (if not already done)
1. Go to [PlayFab.com](https://playfab.com)
2. Create new title (e.g., "My Game Test")
3. Copy Title ID into `PlayFabBackendManager` Inspector field

### 2. Deploy Cloud Functions
1. Go to PlayFab Portal → Automation → Cloud Functions
2. Create two functions:

**Function 1: LevelUpCard**
- Name: `LevelUpCard`
- Paste content from `PlayFabCloudFunctions/LevelUpCard.js`
- Save & Publish

**Function 2: SaveDeck**
- Name: `SaveDeck`
- Paste content from `PlayFabCloudFunctions/SaveDeck.js`
- Save & Publish

### 3. Setup Currency (Optional but recommended)
1. Go to PlayFab Portal → Currencies
2. Create currency: Code `GD`, Display Name `Game Diamonds`
3. This is used by LevelUpCard cloud function

### 4. Add UI to MainMenu
Add these to your MainMenu scene:
- **LoginManager** script (new UI controller)
- **CardDisplayManager** script (card list display)
- Input fields: emailInput, passwordInput
- Buttons: loginButton, registerButton, awardTestCardButton, levelUpTestCardButton
- Text displays: statusText, cardsTitle

---

## Phase 2: Testing Workflow

### Step 1: Create Test User in PlayFab Portal
1. Go to PlayFab Portal → Data
2. Click "New Player"
3. Create user with:
   - Username: `testuser@gmail.com`
   - Password: `Test123!`
   - Display Name: `TestPlayer`

**OR** click "Register" button in your game (it will auto-create)

### Step 2: Run Game & Login
1. Press Play in Unity Editor
2. Enter test user credentials:
   - Email: `testuser@gmail.com`
   - Password: `Test123!`
3. Click "Login"

**Expected Result:**
- ✅ Login successful message
- ✅ Main menu appears
- ✅ Card list loads (initially empty)
- ✅ Console shows: `[LoginManager] Loaded 0 cards`

### Step 3: Award Test Card
1. Click "Award Test Card" button
2. Button calls `AwardCardToPlayer("card_boulder", "Boulder Guardian", 1)`

**Expected Result:**
- ✅ Console shows: `[PlayFab] Awarded 1 copy/copies of Boulder Guardian...`
- ✅ Card appears in list: "Boulder Guardian - Lv.1 (x1)"
- ✅ PlayFab Player Data updated with "player_cards" object

### Step 4: Award More Copies
1. Click "Award Test Card" multiple times
2. Each click adds 1 more copy

**Expected Result:**
- ✅ Card quantity increases: "Boulder Guardian - Lv.1 (x2)", "Boulder Guardian - Lv.1 (x3)"
- ✅ Level stays at 1 (auto-leveling disabled)

### Step 5: Test Level Up
1. Click "Level Up Test Card" button
2. Button calls `LevelUpCard("card_boulder", 100)` (costs 100 diamonds)

**Expected Flow:**
1. Cloud Function validates:
   - ✅ Card exists in player collection
   - ✅ Card not at max level (5)
   - ✅ Player has 100+ diamonds (FAILS if no currency)

**If currency check fails:**
1. Go to PlayFab Portal → Players → Find your player
2. Click on their data
3. Go to Currencies → Add 1000 diamonds
4. Try level up again

**Expected Result:**
- ✅ Console shows: `[PlayFab] Card card_boulder leveled up successfully`
- ✅ Card displays: "Boulder Guardian - Lv.2 (x3)"
- ✅ Currency deducted (900 remaining)
- ✅ PlayFab Player Data "player_cards" updated

---

## Phase 3: Verify Data in PlayFab

### Check Player Card Collection
1. Go to PlayFab Portal → Players
2. Click on your test player
3. View Player Data → "player_cards"

**Should see JSON like:**
```json
{
  "cards": [
    {
      "cardId": "card_boulder",
      "cardName": "Boulder Guardian",
      "level": 2,
      "owned": true,
      "quantity": 3,
      "timesAcquired": 3,
      "acquiredDate": "2026-01-06T..."
    }
  ]
}
```

### Check Player Currency
1. Go to PlayFab Portal → Players → Your player
2. Click Currencies
3. Verify diamonds deducted (should be 900 if you had 1000)

### Check PlayStream Events
1. Go to PlayFab Portal → Players → Your player
2. Click PlayStream
3. Verify you see:
   - `ExecuteCloudScript` events (LevelUpCard calls)
   - `SubtractUserVirtualCurrency` (currency deduction)

---

## Phase 4: Test Deck Saving

### Step 1: Save Deck
1. Create method in CardDisplayManager:
```csharp
private void OnSaveDeckPressed()
{
    List<string> cardNames = new List<string> { "Boulder Guardian" };
    backendManager.SaveDeck("My First Deck", cardNames, (success) =>
    {
        if (success)
            Debug.Log("[CardDisplayManager] Deck saved!");
        else
            Debug.LogError("[CardDisplayManager] Failed to save deck");
    });
}
```

2. Add button to UI and hook it

### Step 2: Verify in PlayFab
1. Go to PlayFab Portal → Players → Your player → Player Data
2. Check "selected_deck" object

**Should see:**
```json
{
  "DeckName": "My First Deck",
  "CardIds": ["card_boulder"],
  "CardCount": 1,
  "CreatedDate": "2026-01-06T...",
  "LastModified": "2026-01-06T..."
}
```

---

## Phase 5: Security Testing (Prevent Cheating)

### Test 1: Insufficient Cards
1. Try to save deck with card you don't own:
```csharp
List<string> cardNames = new List<string> { "Nonexistent Card" };
backendManager.SaveDeck("Cheat Deck", cardNames, callback);
```

**Expected Result:**
- ❌ Error: "Card not found in player collection"
- ✅ Deck NOT saved to PlayFab

### Test 2: Locked Cards
1. Create new player with no cards
2. Try to save deck with any card:
```csharp
backendManager.SaveDeck("Cheat", new List<string> { "Boulder Guardian" }, callback);
```

**Expected Result:**
- ❌ Error: "Card not found in player collection"
- ✅ Deck NOT saved

### Test 3: Insufficient Currency
1. Edit PlayFab currency to 0 diamonds
2. Try to level up card
```csharp
backendManager.LevelUpCard("card_boulder", 100, callback);
```

**Expected Result:**
- ❌ Error: "Insufficient currency. Need 100 but have 0"
- ✅ Card level NOT increased
- ✅ Currency NOT deducted

### Test 4: Max Level
1. Level up card 5 times to reach Level 5
2. Try to level up again

**Expected Result:**
- ❌ Error: "Card is already at maximum level 5"
- ❌ Card level NOT increased beyond 5

---

## Files Created/Modified

### New Files
- `Assets/Scripts/UI/LoginManager.cs` - Handles login and initialization
- `Assets/Scripts/UI/CardDisplayManager.cs` - Displays card collection
- `PlayFabCloudFunctions/LevelUpCard.js` - Server-side level up logic
- `PlayFabCloudFunctions/SaveDeck.js` - Server-side deck saving logic

### Modified Files
- `Assets/Scripts/Backend/PlayFabBackendManager.cs`
  - Added `LevelUpCard()` - calls cloud function
  - Added `SaveDeck()` - calls cloud function
- `Assets/Scripts/Backend/CardOwnershipModels.cs`
  - Simplified for manual-only leveling
  - Removed auto-leveling logic

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "PlayFabBackendManager not found" | Add PlayFabBackendManager prefab to MainMenu scene |
| "Title ID invalid" | Check PlayFab Portal, copy correct Title ID |
| "Cloud function not found" | Verify functions deployed in PlayFab → Automation → Cloud Functions |
| "Insufficient currency" error | Add currency in PlayFab Player data → Currencies |
| "Card not found" | Make sure card was awarded first with AwardCardToPlayer() |
| Login fails with "Invalid user" | Create player in PlayFab Portal or register via game UI |

---

## Success Criteria

- ✅ User can register new account
- ✅ User can login with email/password
- ✅ Card collection loads and displays
- ✅ Test card can be awarded
- ✅ Card level increases via cloud function
- ✅ Currency is deducted on level up
- ✅ All changes persist in PlayFab
- ✅ Cheating attempts are blocked by server validation
