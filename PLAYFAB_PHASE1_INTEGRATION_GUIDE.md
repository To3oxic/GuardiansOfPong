# PlayFab Phase 1 Integration Guide

## Quick Start

### 1. Get Your PlayFab Title ID
1. Go to https://dev.playfab.com
2. Sign in or create account
3. Create a new Studio (if not exists)
4. Create a new Title
5. Copy the **Title ID** (e.g., "A1B2C")

### 2. Configure PlayFab in Unity
1. Open `Assets/Scripts/Backend/PlayFabBackendManager.cs`
2. In the Inspector, set `Playfab Title Id` to your Title ID
   - **OR** set via code: `PlayFabBackendManager.Instance.SetTitleId("YOUR_TITLE_ID")`

### 3. Enable Email/Password Authentication
1. Go to PlayFab Dashboard → Your Title
2. Navigate to **Settings** → **Authentication Methods**
3. Enable **Email and Password**
4. Save settings

---

## How to Use PlayFabBackendManager

### Basic Authentication Flow

```csharp
// In MainMenuController.cs or login UI script
private void OnLoginButtonClicked(string email, string password)
{
    PlayFabBackendManager.Instance.LoginWithEmailAndPassword(
        email, 
        password,
        successCallback: (result) =>
        {
            Debug.Log($"Login successful: {result.PlayFabId}");
            // Proceed to deck selection
        }
    );
}

// Listen to login events
PlayFabBackendManager.Instance.OnLoginSuccess += () =>
{
    Debug.Log("Player logged in!");
};

PlayFabBackendManager.Instance.OnLoginError += (error) =>
{
    Debug.LogError($"Login failed: {error}");
};
```

### Registration Flow

```csharp
private void OnRegisterButtonClicked(string email, string password, string displayName)
{
    PlayFabBackendManager.Instance.RegisterWithEmailAndPassword(
        email,
        password,
        displayName,
        callback: (result) =>
        {
            Debug.Log($"Registration successful: {result.PlayFabId}");
            // Auto-logs in after registration
        }
    );
}
```

### Check Login Status

```csharp
if (PlayFabBackendManager.Instance.IsLoggedIn())
{
    // Player is logged in
    string playfabId = PlayFabBackendManager.Instance.GetPlayFabId();
    string displayName = PlayFabBackendManager.Instance.GetPlayerData().DisplayName;
}
else
{
    // Show login screen
}
```

### Save Deck to PlayFab

```csharp
// In DeckSelectionState.cs or NetworkUI.cs
public void SaveCurrentDeck(string deckName, List<CardDataDto> cards)
{
    PlayFabBackendManager.Instance.SaveSelectedDeck(
        deckName,
        cards,
        callback: (savedName) =>
        {
            Debug.Log($"Deck saved: {savedName}");
        }
    );
}
```

### Load Decks from PlayFab

```csharp
public void LoadDecks()
{
    PlayFabBackendManager.Instance.LoadAllDecks(callback: (deckList) =>
    {
        foreach (var deck in deckList)
        {
            Debug.Log($"Deck: {deck.DeckName} with {deck.Cards.Count} cards");
        }
    });
}

// Listen to load completion
PlayFabBackendManager.Instance.OnDecksLoaded += () =>
{
    Debug.Log("Decks finished loading");
};
```

### Get Single Deck

```csharp
var deck = PlayFabBackendManager.Instance.GetDeckByName("MainDeck");
if (deck.HasValue)
{
    Debug.Log($"Found deck with {deck.Value.Cards.Count} cards");
}
```

---

## Integration Points

### 1. MainMenuController.cs
**Replace Django login with PlayFab:**

```csharp
// OLD CODE (Django)
// BackendManager.Instance.LoginWithCredentials(email, password);

// NEW CODE (PlayFab)
PlayFabBackendManager.Instance.LoginWithEmailAndPassword(email, password, (result) =>
{
    if (result != null)
    {
        StartCoroutine(LoadGameScene());
    }
});
```

### 2. DeckSelectionUI.cs
**No major changes needed** - UI already uses abstracted BackendManager interface.
Just ensure it works with PlayFab responses.

### 3. DeckSelectionState.cs
**Update BuildPayloadFromCurrent() to use PlayFab card levels:**

```csharp
// CURRENT (hardcoded level 5)
// card.level = 5;

// FUTURE (from PlayFab)
// card.level = PlayFabBackendManager.Instance.GetCardLevel(card.cardId);
```

For now, keep `level = 5` as default while Phase 2 is in development.

### 4. NetworkUI.cs
**Update deck submission to use PlayFab:**

```csharp
// When user clicks "Start Game"
var currentDecks = PlayFabBackendManager.Instance.GetAllCachedDecks();
// OR save new deck
PlayFabBackendManager.Instance.SaveSelectedDeck("CurrentDeck", deckPayload.Cards);
```

---

## Parallel Running (Django + PlayFab)

To safely transition from Django to PlayFab:

### Option 1: Config Flag
```csharp
public bool USE_PLAYFAB = true; // Set in Inspector or PlayerPrefs

if (USE_PLAYFAB)
{
    PlayFabBackendManager.Instance.LoginWithEmailAndPassword(...);
}
else
{
    BackendManager.Instance.LoginWithCredentials(...);
}
```

### Option 2: Both Systems Active
Run both simultaneously for 1-2 weeks:
- PlayFab handles authentication
- Django handles backup/verification
- Log all operations to compare results

---

## Testing Checklist

### ✓ Phase 1 Tests

- [ ] **Initialize**
  - Confirm PlayFabBackendManager singleton created
  - Confirm Title ID loaded correctly
  - Check console for: "[PlayFab] Initialized with Title ID: YOUR_ID"

- [ ] **Register New Account**
  - Enter new email + password + displayName
  - Register button clicked
  - Check PlayFab Dashboard: Player should appear in "Players" list
  - Confirm auto-login occurs after registration
  - Check console for: "[PlayFab] Registration successful"

- [ ] **Login with Email/Password**
  - Login with existing account
  - Confirm OnLoginSuccess event fires
  - Check cachedPlayerData has DisplayName populated
  - Verify SessionTicket and EntityToken set

- [ ] **Login Error Handling**
  - Login with wrong password
  - Confirm OnLoginError event fires
  - Check error message is user-friendly

- [ ] **Check Login Status**
  - After login: IsLoggedIn() returns true
  - After logout: IsLoggedIn() returns false
  - PlayFabId matches dashboard

- [ ] **Logout**
  - Call Logout()
  - Verify SessionTicket cleared
  - Verify IsLoggedIn() returns false

---

## Common Issues & Solutions

### Issue: "PlayFab not initialized"
**Solution:** Ensure Title ID is set before calling login methods
```csharp
PlayFabBackendManager.Instance.SetTitleId("YOUR_TITLE_ID");
PlayFabBackendManager.Instance.LoginWithEmailAndPassword(...);
```

### Issue: "EntityToken missing" when saving decks
**Solution:** EntityToken is populated by LoginWithEmailAddress. Make sure login completed.
```csharp
if (PlayFabBackendManager.Instance.IsLoggedIn())
{
    PlayFabBackendManager.Instance.SaveSelectedDeck(...);
}
```

### Issue: Decks won't load
**Solution:** EntityToken must be valid. Refresh login if decks were saved in different session.
```csharp
// Force re-login to refresh EntityToken
PlayFabBackendManager.Instance.LoginWithEmailAndPassword(email, password);
```

### Issue: JSON serialization error in decks
**Solution:** Ensure CardDataDto has [System.Serializable] attribute
```csharp
[System.Serializable]
public class CardDataDto
{
    public string id;
    public string name;
    public int level;
    // ... other fields
}
```

---

## Debugging Tips

### Enable Debug Logging
```csharp
// In PlayFabBackendManager.cs Awake()
PlayFabSettings.LogLevel = PlayFabLogLevel.Debug; // Development only
```

### Monitor Network Calls
Enable PlayFab SDK logging:
- PlayFab Console: Dashboard → Title → Game Manager → Logs
- Unity Console: Filter by "[PlayFab]" tag

### Check Auth Context
```csharp
Debug.Log($"PlayFabId: {PlayFabBackendManager.Instance.GetPlayFabId()}");
Debug.Log($"EntityId: {PlayFabBackendManager.Instance.GetEntityId()}");
Debug.Log($"EntityToken: {PlayFabBackendManager.Instance.GetEntityToken()}");
Debug.Log($"SessionTicket: {PlayFabBackendManager.Instance.GetSessionTicket()}");
```

---

## Next Steps (Phase 2)

After Phase 1 is validated:

1. **Implement Deck Storage**
   - Extend LoadAllDecks() usage
   - Implement DeleteDeck() UI
   - Replace DeckSelectionState.BuildPayloadFromCurrent() with PlayFab data

2. **Player Statistics**
   - Implement GetCardLevel() with Player Statistics
   - Replace hardcoded level=5 in DeckSelectionState
   - Call UpdateCardLevel() when cards level up

3. **CloudScript**
   - Move deck validation to CloudScript
   - Prevent client-side deck manipulation
   - Execute server-side card balance checks

---

## Reference

### PlayFabBackendManager Public Methods

| Method | Purpose |
|--------|---------|
| `LoginWithEmailAndPassword()` | Email/password authentication |
| `RegisterWithEmailAndPassword()` | Create new account |
| `IsLoggedIn()` | Check current login status |
| `Logout()` | Clear auth context |
| `SaveSelectedDeck()` | Save deck to PlayFab Data API |
| `LoadAllDecks()` | Load all decks from PlayFab |
| `GetDeckByName()` | Get specific deck from cache |
| `DeleteDeck()` | Delete deck from PlayFab |
| `UpdateCardLevel()` | Update player statistics |
| `ExecuteCloudScript()` | Call server-side functions |
| `SetTitleId()` | Set/change Title ID at runtime |

### Events

| Event | When Fired |
|-------|-----------|
| `OnLoginSuccess` | After successful authentication |
| `OnLoginError` | When login fails |
| `OnDecksLoaded` | After LoadAllDecks completes |
| `OnDeckSaved` | After SaveSelectedDeck completes |

---

## Resources

- **PlayFab Documentation:** https://learn.microsoft.com/en-us/gaming/playfab/
- **Client API Reference:** https://learn.microsoft.com/en-us/gaming/playfab/features/authentication/
- **Data API Guide:** https://learn.microsoft.com/en-us/gaming/playfab/features/data/entities/
- **CloudScript Intro:** https://learn.microsoft.com/en-us/gaming/playfab/features/automation/cloudscript/
