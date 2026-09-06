# PlayFab Integration Plan

## Current Status
- ✅ PlayFab SDK v2.228.251219 already installed in `Assets/PlayFabSDK/`
- ✅ All modules present: Client API, Data API, Server API, CloudScript, Authentication
- ✅ Card level system fully implemented and functional
- ✅ Backend using Django REST API (http://185.252.233.117:8000)

## Architecture Overview

### Current Stack (Django)
```
DeckSelectionUI → BackendManager → UnityWebRequest → Django API
                    ↓
            (manual CSRF token handling)
            (session ID management)
            (player data caching)
```

### Target Stack (PlayFab)
```
DeckSelectionUI → PlayFabBackendManager → PlayFabClientInstanceAPI → PlayFab Services
                         ↓
                 (automatic auth context)
                 (entity token + session ticket)
                 (player data via Data API)
```

---

## Implementation Phases

### Phase 1: PlayFab Authentication Setup (CRITICAL PATH)
**Objective:** Replace Django login with PlayFab authentication

#### Step 1.1: Create PlayFabBackendManager.cs
Location: `Assets/Scripts/Backend/PlayFabBackendManager.cs`

```csharp
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.SharedModels;
using UnityEngine;
using System;
using System.Collections.Generic;

public class PlayFabBackendManager : MonoBehaviour
{
    [SerializeField] private string playfabTitleId = "YOUR_TITLE_ID"; // Set in Inspector or via PlayerPrefs
    
    private PlayFabClientInstanceAPI clientApi;
    private PlayFabAuthenticationContext authContext;
    
    // Events
    public event Action OnLoginSuccess;
    public event Action<string> OnLoginError;
    
    // Player Data Cache
    public struct PlayerData
    {
        public string DisplayName;
        public int Gold;
        public int Level;
    }
    
    private PlayerData cachedPlayerData;
    
    void Awake()
    {
        // Initialize PlayFab settings
        PlayFabSettings.staticSettings.TitleId = playfabTitleId;
        
        // Create instance API with auth context
        authContext = new PlayFabAuthenticationContext();
        clientApi = new PlayFabClientInstanceAPI(PlayFabSettings.staticSettings, authContext);
    }
    
    /// <summary>
    /// Login with email and password
    /// </summary>
    public void LoginWithEmailAndPassword(string email, string password, Action<LoginResult> callback = null)
    {
        var request = new LoginWithEmailAddressRequest
        {
            Email = email,
            Password = password,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true,
                GetPlayerStatistics = true,
                GetUserData = true
            }
        };
        
        clientApi.LoginWithEmailAddress(request, 
            result => 
            {
                Debug.Log($"PlayFab Login Success: {result.PlayFabId}");
                
                // Store auth tokens
                authContext.ClientSessionTicket = result.SessionTicket;
                authContext.EntityToken = result.EntityToken?.EntityToken;
                authContext.EntityId = result.EntityToken?.Entity?.Id;
                authContext.EntityType = result.EntityToken?.Entity?.Type;
                
                // Cache player data
                if (result.InfoResultPayload?.PlayerProfile != null)
                {
                    cachedPlayerData.DisplayName = result.InfoResultPayload.PlayerProfile.DisplayName ?? "Player";
                }
                
                OnLoginSuccess?.Invoke();
                callback?.Invoke(result);
            },
            error =>
            {
                Debug.LogError($"PlayFab Login Failed: {error.ErrorMessage}");
                OnLoginError?.Invoke(error.ErrorMessage);
            }
        );
    }
    
    /// <summary>
    /// Register new account
    /// </summary>
    public void RegisterWithEmailAndPassword(string email, string password, string displayName, Action<RegisterPlayFabUserResult> callback = null)
    {
        var request = new RegisterPlayFabUserRequest
        {
            Email = email,
            Password = password,
            DisplayName = displayName,
            RequireBothEmailAndUsernameForLogin = false
        };
        
        clientApi.RegisterPlayFabUser(request,
            result =>
            {
                Debug.Log($"PlayFab Registration Success: {result.PlayFabId}");
                
                // Auto-login after registration
                LoginWithEmailAndPassword(email, password, null);
                callback?.Invoke(result);
            },
            error =>
            {
                Debug.LogError($"PlayFab Registration Failed: {error.ErrorMessage}");
                OnLoginError?.Invoke(error.ErrorMessage);
            }
        );
    }
    
    /// <summary>
    /// Check if user is currently logged in
    /// </summary>
    public bool IsLoggedIn()
    {
        return authContext?.IsClientLoggedIn() ?? false;
    }
    
    /// <summary>
    /// Get cached player data (must call LoginWithEmailAndPassword first)
    /// </summary>
    public PlayerData GetPlayerData()
    {
        return cachedPlayerData;
    }
    
    public string GetPlayFabId()
    {
        return authContext?.PlayFabId;
    }
    
    public string GetSessionTicket()
    {
        return authContext?.ClientSessionTicket;
    }
    
    public string GetEntityToken()
    {
        return authContext?.EntityToken;
    }
}
```

#### Step 1.2: Update PlayFabSettings.cs with Title ID
- Get your PlayFab Title ID from PlayFab developer dashboard
- Set in `PlayFabSettings.staticSettings.TitleId` in code OR via PlayFabSharedSettings ScriptableObject

#### Step 1.3: Modify MainMenuController.cs
Replace Django login calls with PlayFab:

```csharp
// OLD (Django)
// BackendManager.Instance.LoginWithCredentials(email, password);

// NEW (PlayFab)
PlayFabBackendManager.Instance.LoginWithEmailAndPassword(email, password, (result) =>
{
    // Handle successful login
    StartCoroutine(LoadGameScene());
});
```

#### Step 1.4: Test Phase 1
- [ ] Create test PlayFab account in dashboard
- [ ] Test LoginWithEmailAndPassword flow in MainMenu
- [ ] Verify authContext has SessionTicket and EntityToken
- [ ] Verify OnLoginSuccess event fires

---

### Phase 2: Player Data Migration (Replace Django GetAllCards/SaveSelectedDeck)
**Objective:** Move deck and card level data from Django to PlayFab Data API

#### Step 2.1: Extend PlayFabBackendManager with Data API calls

```csharp
public class PlayFabBackendManager : MonoBehaviour
{
    // Add these to Phase 1 class:
    
    private PlayFabDataInstanceAPI dataApi;
    
    void Awake()
    {
        // ... existing code ...
        
        // Initialize Data API
        dataApi = new PlayFabDataInstanceAPI(PlayFabSettings.staticSettings, authContext);
    }
    
    /// <summary>
    /// Save selected deck (replaces BackendManager.SaveSelectedDeck)
    /// </summary>
    public void SaveSelectedDeck(string deckName, List<CardDataDto> cards)
    {
        // Serialize deck to JSON
        var deckData = new
        {
            deckName = deckName,
            cards = cards,
            lastModified = DateTime.UtcNow
        };
        
        var deckJson = JsonUtility.ToJson(deckData);
        
        var request = new SetObjectsRequest
        {
            Entity = new PlayFab.DataModels.EntityKey { Id = authContext.EntityId, Type = authContext.EntityType },
            Objects = new Dictionary<string, SetObject>
            {
                {
                    $"deck_{deckName}", 
                    new SetObject 
                    { 
                        DataObject = deckJson,
                        DeleteAfterReadDays = null // Permanent storage
                    }
                }
            }
        };
        
        dataApi.SetObjects(request,
            result =>
            {
                Debug.Log($"Deck '{deckName}' saved to PlayFab");
            },
            error =>
            {
                Debug.LogError($"Failed to save deck: {error.ErrorMessage}");
            }
        );
    }
    
    /// <summary>
    /// Load all decks for current player
    /// </summary>
    public void GetAllDecks(Action<List<SavedDeck>> callback)
    {
        var request = new PlayFab.DataModels.GetObjectsRequest
        {
            Entity = new PlayFab.DataModels.EntityKey { Id = authContext.EntityId, Type = authContext.EntityType }
        };
        
        dataApi.GetObjects(request,
            result =>
            {
                var decks = new List<SavedDeck>();
                
                foreach (var obj in result.Objects)
                {
                    if (obj.Key.StartsWith("deck_"))
                    {
                        // Parse JSON and convert to SavedDeck
                        var deckData = JsonUtility.FromJson<SavedDeck>(obj.Value.DataObject.ToString());
                        decks.Add(deckData);
                    }
                }
                
                callback?.Invoke(decks);
            },
            error =>
            {
                Debug.LogError($"Failed to load decks: {error.ErrorMessage}");
                callback?.Invoke(new List<SavedDeck>());
            }
        );
    }
}
```

#### Step 2.2: Update DeckSelectionState.cs
Replace level=5 hardcoding with PlayFab data:

```csharp
public class DeckSelectionState : MonoBehaviour
{
    // OLD
    // card.level = 5;
    
    // NEW
    void LoadCardLevelsFromPlayFab()
    {
        PlayFabClientInstanceAPI clientApi = new PlayFabClientInstanceAPI();
        // Use Player Statistics to store card levels
        // GetPlayerStatistics RPC call
    }
}
```

---

### Phase 3: CloudScript Integration (Optional but Recommended)
**Objective:** Move deck validation logic to CloudScript for security

#### Step 3.1: Create CloudScript function
In PlayFab CloudScript editor:

```javascript
handlers.validateAndSaveDeck = function(args, context)
{
    // Server-side deck validation
    // Verify card existence, level limits, deck size
    // Save to player data
    
    return {
        success: true,
        savedDeckId: "deck_123"
    };
};
```

#### Step 3.2: Call CloudScript from Unity
```csharp
public void ExecuteCloudScript(string functionName, object params, Action<object> callback)
{
    var request = new ExecuteCloudScriptRequest
    {
        FunctionName = functionName,
        FunctionParameter = new Dictionary<string, object> { { "deckData", params } }
    };
    
    clientApi.ExecuteCloudScript(request,
        result => callback?.Invoke(result.FunctionResult),
        error => Debug.LogError($"CloudScript error: {error.ErrorMessage}")
    );
}
```

---

### Phase 4: Cleanup & Transition
**Objective:** Deprecate Django backend, switch to PlayFab fully

#### Step 4.1: Parallel Running Period (1-2 weeks)
- Both BackendManager (Django) and PlayFabBackendManager running
- Toggle via config: `USE_PLAYFAB = true/false`
- Monitor PlayFab data consistency

#### Step 4.2: Data Migration
- Script to migrate existing Django player accounts to PlayFab
- Export Django user data → Import to PlayFab via admin API

#### Step 4.3: Full Cutover
- Remove Django backend references
- Make PlayFabBackendManager the single source of truth
- Archive Django code

---

## Key PlayFab SDK Components

### 1. **PlayFabClientInstanceAPI**
- `LoginWithEmailAddress()` - Email/password authentication
- `ExecuteCloudScript()` - Server-side logic calls
- `GetPlayerCombinedInfo()` - Player profile + statistics
- Instance-based (supports multiple auth contexts)

### 2. **PlayFabDataInstanceAPI**
- `SetObjects()` - Save JSON player data
- `GetObjects()` - Load JSON player data
- Uses Entity Token for authentication (not SessionTicket)

### 3. **PlayFabAuthenticationContext**
- **ClientSessionTicket** - For Client API calls
- **EntityToken** - For Data/Server API calls
- **PlayFabId** - Unique player identifier
- **EntityId / EntityType** - For entity-based APIs

### 4. **PlayFabSettings**
- `TitleId` - Your PlayFab title identifier
- `LogLevel` - Debug logging configuration
- `ApiUrl` - Endpoint (default: playfabapi.com)

---

## Configuration Checklist

### Before Phase 1 Implementation:
- [ ] Create PlayFab account at dev.playfab.com
- [ ] Create new title (get Title ID)
- [ ] Enable email/password authentication in Settings
- [ ] Create test player account in PlayFab dashboard
- [ ] Get Title ID and update code

### Environment Setup:
```csharp
// In PlayFabBackendManager.cs Awake():
PlayFabSettings.staticSettings.TitleId = "YOUR_PLAYFAB_TITLE_ID"; // e.g., "A1B2C"
PlayFabSettings.LogLevel = PlayFabLogLevel.Debug; // During development
```

---

## Data Schema

### Player Entity (PlayFab Default)
- **PlayFabId** - Unique player ID
- **Email** - Login credential
- **DisplayName** - Player username
- **Created** - Account creation timestamp

### Player Data Objects (Data API)
```json
{
  "deck_MainDeck": {
    "deckName": "MainDeck",
    "cards": [
      {
        "id": "1",
        "name": "Brave Knight",
        "level": 3,
        "cardEffect": "BraveKnightEffect"
      }
    ],
    "lastModified": "2024-01-15T10:30:00Z"
  }
}
```

### Player Statistics (Optional)
- `card_level_1` - Brave Knight level
- `card_level_2` - Tree level
- etc.

---

## Testing Strategy

### Phase 1 Tests
1. **Registration Flow**
   - Register new account
   - Verify email confirmation (if enabled)
   - Auto-login after registration

2. **Login Flow**
   - Login with valid credentials → SessionTicket issued
   - Login with invalid credentials → Error handling
   - Verify authContext populated correctly

3. **Persistence**
   - Close app, reopen
   - Try to use old SessionTicket → Should fail
   - Force re-login

### Phase 2 Tests
1. **Deck Saving**
   - Save deck with 3 cards
   - Verify PlayFab Data API shows saved deck
   - Edit deck and save again → Update works

2. **Deck Loading**
   - Load decks after login
   - Verify card levels match what was saved

---

## Migration Rollback Plan

If PlayFab integration fails:
1. Keep Django backend running in parallel
2. Set `USE_PLAYFAB = false` in config
3. Revert to BackendManager for all calls
4. Investigate and retry Phase 1

---

## Estimated Timeline

| Phase | Tasks | Duration |
|-------|-------|----------|
| **1** | Auth setup + testing | 3-4 days |
| **2** | Data API integration | 2-3 days |
| **3** | CloudScript (optional) | 2-3 days |
| **4** | Transition + cleanup | 1-2 days |
| **Total** | | ~1-2 weeks |

---

## Next Steps

1. **Immediate (Today)**
   - Get PlayFab Title ID
   - Read PlayFabSettings.cs to understand config
   - Create PlayFabBackendManager.cs (Phase 1)

2. **Short Term (This Week)**
   - Implement LoginWithEmailAndPassword
   - Update MainMenuController to use PlayFab
   - Test authentication flow end-to-end

3. **Medium Term (Next Week)**
   - Implement Data API for deck management
   - Migrate DeckSelectionState
   - Complete parallel testing with Django

---

## Resources

- **PlayFab Documentation:** https://learn.microsoft.com/en-us/gaming/playfab/
- **SDK Reference:** Assets/PlayFabSDK/Shared/Public/PlayFabSettings.cs
- **Client API Methods:** Assets/PlayFabSDK/Client/PlayFabClientInstanceAPI.cs
- **Data API Methods:** Assets/PlayFabSDK/Data/PlayFabDataInstanceAPI.cs
