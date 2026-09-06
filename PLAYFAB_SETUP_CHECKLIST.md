# PlayFab Testing Setup - Quick Checklist

## ✓ Code Changes (Already Done)
- [x] Created `LoginManager.cs` - handles email/password login
- [x] Created `CardDisplayManager.cs` - shows card collection
- [x] Updated `PlayFabBackendManager.cs` - calls cloud functions
- [x] Cloud functions deployed to PlayFab (LevelUpCard.js, SaveDeck.js)

## ⚡ Quick Setup (30 minutes)

### 1. Add PlayFabBackendManager to Scene
- Open MainMenu scene
- Add new GameObject: `PlayFabBackendManager`
- Attach script: `PlayFabBackendManager`
- In Inspector, set **PlayFab Title ID** (from PlayFab portal)

### 2. Add LoginManager to Scene
- Add new GameObject: `LoginManager`
- Attach script: `LoginManager.cs`
- Create a Canvas with:
  - Panel: `LoginPanel` (contains login UI)
  - InputField: `emailInput`
  - InputField: `passwordInput`
  - Button: `loginButton`
  - Button: `registerButton`
  - TextMeshProUGUI: `statusText`
  - GameObject: `mainMenuPanel` (hidden by default)
- Assign all references in LoginManager inspector

### 3. Add CardDisplayManager to Scene
- Add new GameObject: `CardDisplayManager`
- Attach script: `CardDisplayManager.cs`
- Create a Panel with:
  - TextMeshProUGUI: `cardsTitle`
  - ScrollView or Container: `cardListContainer`
  - Button: `awardTestCardButton`
  - Button: `levelUpTestCardButton`
- Assign all references in CardDisplayManager inspector

### 4. Setup PlayFab Currency (Optional)
- Go to PlayFab Portal → Currencies
- Create: Code `GD`, Display Name `Game Diamonds`
- (Needed for LevelUpCard cost validation)

### 5. Create Test User
**Option A: Via Game UI**
1. Run game
2. Enter email: `test@example.com`
3. Enter password: `Test123!`
4. Click "Register"

**Option B: Via PlayFab Portal**
1. Go to PlayFab Portal → Data
2. Click "New Player"
3. Create user credentials

### 6. Test Flow
1. Run game
2. Login with test credentials
3. Click "Award Test Card" → Card appears
4. Click "Level Up Test Card" → Card levels up
5. Check PlayFab Portal → Player Data to verify

---

## 🔧 What Each Script Does

### LoginManager.cs
```
User enters email/password
        ↓
Click Login
        ↓
PlayFabBackendManager.LoginWithEmailAndPassword()
        ↓
On Success: Hide login panel, show main menu
        ↓
Load player's cards automatically
```

### CardDisplayManager.cs
```
On Start: Load player cards
        ↓
Display each card: "CardName - Lv.X (xQty)"
        ↓
Award Test Card button → AwardCardToPlayer()
        ↓
Level Up Test Card button → LevelUpCard() [cloud function]
        ↓
Refresh display with updated data
```

### Cloud Functions
```
Client calls LevelUpCard("card_id", 100)
        ↓
Server validates:
  - Card exists in player collection
  - Card not at max level
  - Player has 100+ currency
        ↓
If valid:
  - Deduct 100 currency
  - Increment card level
  - Save to PlayFab
  - Return success
        ↓
If invalid:
  - Return error message
  - NO changes made
```

---

## 📊 What to Verify in PlayFab

### Player Data Object "player_cards"
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
      "acquiredDate": "2026-01-06T10:30:00Z"
    }
  ]
}
```

### Player Currencies
- GD (Game Diamonds): Should decrease when leveling cards

### PlayStream Events
- ExecuteCloudScript → Shows cloud function calls
- SubtractUserVirtualCurrency → Shows currency deductions

---

## 🚀 Testing Sequence

1. **Login Test**
   - Register new user ✓
   - Login succeeds ✓
   - Main menu shows ✓

2. **Card Award Test**
   - Click "Award Test Card" ✓
   - Card appears in list ✓
   - PlayFab data updated ✓

3. **Quantity Test**
   - Award same card 3 times ✓
   - Quantity shows x3 ✓
   - Level stays 1 (no auto-level) ✓

4. **Level Up Test**
   - Click "Level Up Test Card" ✓
   - Card levels to 2 ✓
   - Currency deducted (100) ✓
   - PlayFab data updated ✓

5. **Max Level Test**
   - Level up card 5 times ✓
   - 6th level up fails ✓
   - Error: "already at maximum level" ✓

6. **Security Test**
   - Try to level up locked card ✗
   - Try to save deck with locked card ✗
   - Try to level with no currency ✗
   - All blocked by server ✓

---

## ⚠️ Common Issues

| Error | Fix |
|-------|-----|
| PlayFabBackendManager not found | Add to scene, assign Title ID |
| Title ID invalid | Copy from PlayFab Portal |
| Cloud function not found | Deploy via PlayFab Portal → Automation |
| Insufficient currency | Add currency in PlayFab Player Data |
| "Card not found" | Award card first with button |
| Login fails | Register user first (via game or PlayFab Portal) |

---

## 📝 Next Steps After Testing

1. ✅ Verify all basic flows work
2. ✅ Test security (cheating attempts blocked)
3. ✅ Check PlayFab data persistence
4. ⚠️ Then:
   - Add deck saving UI
   - Add currency earning mechanics
   - Add card shop/gacha system
   - Add matchmaking integration
   - Add card stat application to gameplay
