using PlayFab;
using PlayFab.ClientModels;
using PlayFab.DataModels;
using PlayFab.SharedModels;
using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static DeckPayload;

/// <summary>
/// PlayFab backend manager for authentication and player data management.
/// Replaces the Django-based BackendManager for modern cloud backend infrastructure.
/// System: Manual card leveling with quantity tracking.
/// </summary>
public class PlayFabBackendManager : MonoBehaviour
{
    public static PlayFabBackendManager Instance { get; private set; }

    [SerializeField] private string playfabTitleId = "YOUR_TITLE_ID"; // Override in Inspector

    private PlayFabClientInstanceAPI clientApi;
    private PlayFabDataInstanceAPI dataApi;
    private PlayFabAuthenticationContext authContext;

    private bool isInitialized = false;
    private bool _isLoginComplete = false;  // Flag to indicate entire login sequence is complete

    // ========== EVENTS ==========
    public event Action OnLoginSuccess;
    public event Action<string> OnLoginError;
    public event Action OnDecksLoaded;
    public event Action<string> OnDeckSaved;

    // ========== PLAYER DATA CACHING ==========
    public struct PlayerData
    {
        public string DisplayName;
        public string PlayFabId;
        public DateTime? CreatedTime;
    }

    [System.Serializable]
    public struct SavedDeck
    {
        public string DeckName;
        public List<DeckPayload.CardDataDto> Cards;
        public DateTime LastModified;
    }

    private PlayerData cachedPlayerData;
    private Dictionary<string, SavedDeck> cachedDecks = new Dictionary<string, SavedDeck>();
    private PlayerCardCollection cachedCardCollection = new PlayerCardCollection();
    private CardLibrary cachedCardLibrary = new CardLibrary();

    // ========== LIFECYCLE ==========
    void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializePlayFab();
    }

    private void InitializePlayFab()
    {
        try
        {
            // Get Title ID from environment or config
            string titleId = playfabTitleId;
            if (titleId == "YOUR_TITLE_ID")
            {
                titleId = PlayerPrefs.GetString("PlayFab_TitleId", "YOUR_TITLE_ID");
            }

            PlayFabSettings.staticSettings.TitleId = titleId;

            // Create authentication context
            authContext = new PlayFabAuthenticationContext();

            // Create API instances
            clientApi = new PlayFabClientInstanceAPI(PlayFabSettings.staticSettings, authContext);
            dataApi = new PlayFabDataInstanceAPI(PlayFabSettings.staticSettings, authContext);

            isInitialized = true;
            Debug.Log($"[PlayFab] Initialized with Title ID: {titleId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlayFab] Initialization failed: {ex.Message}");
        }
    }

    // ========== AUTHENTICATION METHODS ==========

    /// <summary>
    /// Login with email and password.
    /// Retrieves player profile and initializes auth context with session ticket + entity token.
    /// </summary>
    public void LoginWithEmailAndPassword(string email, string password, Action<LoginResult> callback = null)
    {
        if (!isInitialized)
        {
            Debug.LogError("[PlayFab] Not initialized. Call InitializePlayFab first.");
            OnLoginError?.Invoke("PlayFab not initialized");
            return;
        }

        var request = new LoginWithEmailAddressRequest
        {
            Email = email,
            Password = password,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true,
                GetPlayerStatistics = true,
                GetUserData = true,
                GetUserReadOnlyData = true
            }
        };

        clientApi.LoginWithEmailAddress(request,
            result =>
            {
                HandleLoginSuccess(result);
                callback?.Invoke(result);
            },
            error =>
            {
                HandleLoginError(error);
            }
        );
    }

    /// <summary>
    /// Register new PlayFab account with email and password.
    /// Auto-logs in after successful registration.
    /// </summary>
    public void RegisterWithEmailAndPassword(string email, string password, string displayName, Action<RegisterPlayFabUserResult> callback = null)
    {
        if (!isInitialized)
        {
            Debug.LogError("[PlayFab] Not initialized.");
            OnLoginError?.Invoke("PlayFab not initialized");
            return;
        }

        var request = new RegisterPlayFabUserRequest
        {
            Email = email,
            Password = password,
            DisplayName = displayName
        };

        clientApi.RegisterPlayFabUser(request,
            result =>
            {
                Debug.Log($"[PlayFab] Registration successful. PlayFabId: {result.PlayFabId}");

                // Auto-login after registration
                LoginWithEmailAndPassword(email, password, null);

                callback?.Invoke(result);
            },
            error =>
            {
                Debug.LogError($"[PlayFab] Registration failed: {error.ErrorMessage}");
                OnLoginError?.Invoke(error.ErrorMessage);
            }
        );
    }

    private void HandleLoginSuccess(LoginResult result)
    {
        // Store authentication tokens
        authContext.ClientSessionTicket = result.SessionTicket;

        if (result.EntityToken != null)
        {
            authContext.EntityToken = result.EntityToken.EntityToken;
            authContext.EntityId = result.EntityToken.Entity.Id;
            authContext.EntityType = result.EntityToken.Entity.Type;
        }

        authContext.PlayFabId = result.PlayFabId;

        // Cache player data
        cachedPlayerData = new PlayerData
        {
            DisplayName = result.InfoResultPayload?.PlayerProfile?.DisplayName ?? "Player",
            PlayFabId = result.PlayFabId,
            CreatedTime = result.InfoResultPayload?.PlayerProfile?.Created
        };

        Debug.Log($"[PlayFab] Login successful. PlayFabId: {result.PlayFabId}, DisplayName: {cachedPlayerData.DisplayName}");

        // Set player profile in PlayerStates
        if (PlayerStates.Instance != null)
        {
            PlayerStates.Instance.SetPlayerProfile(result.PlayFabId, cachedPlayerData.DisplayName, cachedPlayerData.CreatedTime);
        }

        // Reset login complete flag at start of login sequence
        _isLoginComplete = false;

        // Load all player state (cards, decks, inventory) from CloudScript in one call
        LoadPlayerState(() =>
        {
            Debug.Log("[PlayFab] Player state loaded completely");

            // Mark login sequence as complete
            _isLoginComplete = true;
            Debug.Log("[PlayFab] === LOGIN SEQUENCE COMPLETE ===");

            OnLoginSuccess?.Invoke();
        });

    }

    private void HandleLoginError(PlayFabError error)
    {
        Debug.LogError($"[PlayFab] Login failed: Code={error.HttpCode}, Message={error.ErrorMessage}");
        OnLoginError?.Invoke(error.ErrorMessage);
    }

    /// <summary>
    /// Check if player is currently logged in with valid session ticket.
    /// </summary>
    public bool IsLoggedIn()
    {
        return authContext != null && authContext.IsClientLoggedIn();
    }

    /// <summary>
    /// Check if login sequence is completely finished (cards loaded + deck loaded + events fired).
    /// Use this to ensure UI is ready before performing login-dependent operations.
    /// </summary>
    public bool IsLoginComplete()
    {
        return _isLoginComplete;
    }

    /// <summary>
    /// Logout current player (clears auth context).
    /// </summary>
    public void Logout()
    {
        if (authContext != null)
        {
            authContext.ForgetAllCredentials();
        }
        cachedDecks.Clear();
        _isLoginComplete = false;  // Reset login complete flag
        Debug.Log("[PlayFab] Logged out");
    }

    // ========== PLAYER DATA METHODS ==========

    /// <summary>
    /// Get cached player data (populated on login).
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

    public string GetEntityId()
    {
        return authContext?.EntityId;
    }

    public string GetEntityType()
    {
        return authContext?.EntityType;
    }

    // ========== DECK MANAGEMENT (Data API) ==========

    /// <summary>
    /// <summary>
    /// Save player's selected deck via server-side cloud function.
    /// Takes card names and validates them server-side against player's collection.
    /// This prevents client-side tampering with deck composition.
    /// </summary>
    public void SaveDeck(string deckName, List<string> cardIds, Action<bool> callback = null)
    {
        if (!IsLoggedIn())
        {
            Debug.LogError("[PlayFab] Cannot save deck: Not logged in");
            callback?.Invoke(false);
            return;
        }

        // Call cloud function with deck name and card IDs
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "saveDeck",
            FunctionParameter = new { deckName = deckName, cardIds = cardIds },
            GeneratePlayStreamEvent = true
        };

        clientApi.ExecuteCloudScript(request,
            result =>
            {
                // Check if cloud function executed successfully
                if (result.FunctionResult == null)
                {
                    Debug.LogError("[PlayFab] SaveDeck cloud function returned null");
                    callback?.Invoke(false);
                    return;
                }

                // Parse the response
                var functionResult = result.FunctionResult as System.Collections.Generic.Dictionary<string, object>;

                if (functionResult == null || !functionResult.ContainsKey("success"))
                {
                    Debug.LogError("[PlayFab] SaveDeck: Invalid response format");
                    callback?.Invoke(false);
                    return;
                }

                bool success = (bool)functionResult["success"];

                if (success)
                {
                    Debug.Log($"[PlayFab] Deck '{deckName}' saved successfully with {cardIds.Count} cards");
                    OnDeckSaved?.Invoke(deckName);
                    callback?.Invoke(true);
                }
                else
                {
                    string error = functionResult.ContainsKey("error") ? functionResult["error"].ToString() : "Unknown error";
                    Debug.LogError($"[PlayFab] SaveDeck failed: {error}");
                    callback?.Invoke(false);
                }
            },
            error =>
            {
                Debug.LogError($"[PlayFab] SaveDeck cloud function call failed: {error.ErrorMessage}");
                callback?.Invoke(false);
            }
        );
    }

    /// <summary>
    /// Legacy method: Save player's selected deck to PlayFab Data API.
    /// <summary>
    /// Load all decks for current player from PlayFab Data API.
    /// Populates cachedDecks and invokes OnDecksLoaded event.
    /// </summary>
    public void LoadAllDecks(Action<List<SavedDeck>> callback = null)
    {
        if (!IsLoggedIn() || string.IsNullOrEmpty(authContext.EntityToken))
        {
            Debug.LogError("[PlayFab] Cannot load decks: Not logged in or EntityToken missing");
            callback?.Invoke(new List<SavedDeck>());
            return;
        }

        var request = new GetObjectsRequest
        {
            Entity = new PlayFab.DataModels.EntityKey
            {
                Id = authContext.EntityId,
                Type = authContext.EntityType
            }
        };

        dataApi.GetObjects(request,
            result =>
            {
                cachedDecks.Clear();
                var deckList = new List<SavedDeck>();

                if (result.Objects != null)
                {
                    Debug.Log($"[PlayFab] Retrieved {result.Objects.Count} objects from Data API");
                    foreach (var kvp in result.Objects)
                    {
                        Debug.Log($"[PlayFab] Processing object: {kvp.Key}");
                        if (kvp.Key.StartsWith("active_deck"))
                        {
                            try
                            {
                                // Parse DataObject - it could be a string or an object
                                string jsonData = null;

                                if (kvp.Value.DataObject is string stringData)
                                {
                                    jsonData = stringData;
                                }
                                else if (kvp.Value.DataObject != null)
                                {
                                    // If it's an object, serialize it to JSON
                                    jsonData = JsonUtility.ToJson(kvp.Value.DataObject);
                                }
                                else
                                {
                                    Debug.LogWarning($"[PlayFab] DataObject is null for deck {kvp.Key}");
                                    continue;
                                }

                                Debug.Log($"[PlayFab] Parsing deck JSON: {jsonData.Substring(0, Mathf.Min(100, jsonData.Length))}...");
                                SavedDeck deck = JsonUtility.FromJson<SavedDeck>(jsonData);

                                cachedDecks[deck.DeckName] = deck;
                                deckList.Add(deck);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[PlayFab] Failed to parse deck {kvp.Key}: {ex.Message}");
                            }
                        }
                    }
                }

                Debug.Log($"[PlayFab] Loaded {deckList.Count} decks");
                OnDecksLoaded?.Invoke();
                callback?.Invoke(deckList);
            },
            error =>
            {
                Debug.LogError($"[PlayFab] Failed to load decks: {error.ErrorMessage}");
                callback?.Invoke(new List<SavedDeck>());
            }
        );
    }

    /// <summary>
    /// Get a specific deck by name from cache.
    /// Call LoadAllDecks first to populate cache.
    /// </summary>
    public SavedDeck? GetDeckByName(string deckName)
    {
        if (cachedDecks.TryGetValue(deckName, out var deck))
        {
            return deck;
        }
        return null;
    }

    /// <summary>
    /// Get the currently saved "active" deck (the one player selected for matchmaking).
    /// This is retrieved from PlayFab to prevent client-side manipulation.
    /// </summary>
    public void LoadActiveDeck(Action<SavedDeck?> callback = null)
    {
        if (!IsLoggedIn() || string.IsNullOrEmpty(authContext.EntityToken))
        {
            Debug.LogError("[PlayFab] Cannot load active deck: Not logged in");
            callback?.Invoke(null);
            return;
        }

        var request = new GetObjectsRequest
        {
            Entity = new PlayFab.DataModels.EntityKey
            {
                Id = authContext.EntityId,
                Type = authContext.EntityType
            }
        };

        dataApi.GetObjects(request,
            result =>
            {
                SavedDeck? activeDeck = null;

                if (result.Objects != null && result.Objects.ContainsKey("active_deck"))
                {
                    try
                    {
                        // Parse DataObject - it could be a string or an object
                        string jsonData = null;

                        if (result.Objects["active_deck"].DataObject is string stringData)
                        {
                            jsonData = stringData;
                        }
                        else if (result.Objects["active_deck"].DataObject != null)
                        {
                            // If it's an object, serialize it to JSON
                            jsonData = JsonUtility.ToJson(result.Objects["active_deck"].DataObject);
                        }
                        else
                        {
                            Debug.LogWarning("[PlayFab] Active deck DataObject is null");
                        }

                        if (!string.IsNullOrEmpty(jsonData))
                        {
                            Debug.Log($"[PlayFab] Parsing active deck JSON: {jsonData.Substring(0, Mathf.Min(100, jsonData.Length))}...");
                            activeDeck = JsonUtility.FromJson<SavedDeck>(jsonData);
                            Debug.Log($"[PlayFab] Loaded active deck: {activeDeck?.DeckName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[PlayFab] Failed to parse active deck: {ex.Message}");
                    }
                }

                callback?.Invoke(activeDeck);
            },
            error =>
            {
                Debug.LogError($"[PlayFab] Failed to load active deck: {error.ErrorMessage}");
                callback?.Invoke(null);
            }
        );
    }

    /// <summary>
    /// Save the active deck to PlayFab via CloudScript.
    /// CloudScript validates that each card is actually owned by the player.
    /// This is called automatically when player finalizes deck selection.
    /// Server uses this to validate the deck instead of trusting client.
    /// </summary>
    public void SaveActiveDeck(string deckName, List<DeckPayload.CardDataDto> cards, Action<bool> callback = null)
    {
        if (!IsLoggedIn())
        {
            Debug.LogError("[PlayFab] Cannot save active deck: Not logged in");
            callback?.Invoke(false);
            return;
        }

        // Extract card IDs from CardDataDto list
        var cardIds = new List<string>();
        foreach (var card in cards)
        {
            if (!string.IsNullOrEmpty(card.id))
            {
                cardIds.Add(card.id);
            }
        }

        if (cardIds.Count == 0)
        {
            Debug.LogError("[PlayFab] Cannot save active deck: No valid cards");
            callback?.Invoke(false);
            return;
        }

        // Call CloudScript which validates cards server-side
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "saveDeck",
            FunctionParameter = new { deckName = deckName, cardIds = cardIds },
            GeneratePlayStreamEvent = true
        };

        clientApi.ExecuteCloudScript(request,
            result =>
            {
                try
                {
                    // Check if cloud function executed successfully
                    if (result.FunctionResult == null)
                    {
                        Debug.LogError("[PlayFab] SaveActiveDeck cloud function returned null");
                        callback?.Invoke(false);
                        return;
                    }

                    // Parse the response - it might be JsonObject or Dictionary
                    System.Collections.Generic.Dictionary<string, object> functionResult = null;

                    // Try as JsonObject first (PlayFab SDK format)
                    if (result.FunctionResult is PlayFab.Json.JsonObject jsonObj)
                    {
                        Debug.Log("[PlayFab] SaveActiveDeck response is JsonObject, converting...");
                        functionResult = new System.Collections.Generic.Dictionary<string, object>();
                        foreach (var key in jsonObj.Keys)
                        {
                            if (key == null)
                            {
                                Debug.LogWarning("[PlayFab] Skipping null key in SaveActiveDeck response");
                                continue;
                            }
                            functionResult[key] = jsonObj[key];
                        }
                    }
                    // Try as Dictionary
                    else if (result.FunctionResult is System.Collections.Generic.Dictionary<string, object> dict)
                    {
                        functionResult = dict;
                    }

                    if (functionResult == null || !functionResult.ContainsKey("success"))
                    {
                        Debug.LogError("[PlayFab] SaveActiveDeck: Invalid response format");
                        if (functionResult != null)
                        {
                            Debug.LogError($"[PlayFab] Available keys: {string.Join(", ", functionResult.Keys)}");
                        }
                        callback?.Invoke(false);
                        return;
                    }

                    bool success = (bool)functionResult["success"];

                    if (success)
                    {
                        Debug.Log($"[PlayFab] Active deck '{deckName}' saved successfully with {cardIds.Count} items");

                        // Update PlayerStates with active deck so UI is updated immediately
                        if (PlayerStates.Instance != null)
                        {
                            var activeDeck = new SavedDeck
                            {
                                DeckName = deckName,
                                Cards = cards,
                                LastModified = System.DateTime.UtcNow
                            };
                            PlayerStates.Instance.SetActiveDeck(activeDeck);
                            Debug.Log("[PlayFab] Updated PlayerStates with active deck");
                        }

                        OnDeckSaved?.Invoke(deckName);
                        callback?.Invoke(true);
                    }
                    else
                    {
                        string error = functionResult.ContainsKey("error") ? functionResult["error"].ToString() : "Unknown error";
                        Debug.LogError($"[PlayFab] SaveActiveDeck failed: {error}");
                        callback?.Invoke(false);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PlayFab] Error processing SaveActiveDeck response: {ex.Message}");
                    callback?.Invoke(false);
                }
            },
            error =>
            {
                Debug.LogError($"[PlayFab] SaveActiveDeck cloud function call failed: {error.ErrorMessage}");
                callback?.Invoke(false);
            }
        );
    }

    /// <summary>
    /// Get card level for a specific card using Player Statistics.
    /// Requires the statistics to be set via UpdatePlayerStatistics or CloudScript.
    /// </summary>
    public int GetCardLevel(string cardId)
    {
        // This would be implemented using GetPlayerStatistics
        // For now, returns default level 5 (used in DeckSelectionState)
        // TODO: Implement full Player Statistics API integration
        return 5;
    }

    /// <summary>
    /// Update player statistics (e.g., card levels).
    /// Called server-side via CloudScript or client-side with proper validation.
    /// </summary>
    public void UpdateCardLevel(string cardId, int level, Action<bool> callback = null)
    {
        if (!IsLoggedIn())
        {
            Debug.LogError("[PlayFab] Cannot update stats: Not logged in");
            callback?.Invoke(false);
            return;
        }

        var request = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate>
            {
                new StatisticUpdate
                {
                    StatisticName = $"card_level_{cardId}",
                    Value = level
                }
            }
        };

        clientApi.UpdatePlayerStatistics(request,
            result =>
            {
                Debug.Log($"[PlayFab] Card {cardId} level updated to {level}");
                callback?.Invoke(true);
            },
            error =>
            {
                Debug.LogError($"[PlayFab] Failed to update card level: {error.ErrorMessage}");
                callback?.Invoke(false);
            }
        );
    }

    // ========== CLOUDSCRIPT EXECUTION ==========

    /// <summary>
    /// Execute CloudScript function for server-side validation/logic.
    /// Used for deck validation, card level verification, etc.
    /// </summary>
    public void ExecuteCloudScript(string functionName, Dictionary<string, object> functionParameter, Action<object> successCallback, Action<string> errorCallback = null)
    {
        if (!IsLoggedIn())
        {
            Debug.LogError("[PlayFab] Cannot execute CloudScript: Not logged in");
            errorCallback?.Invoke("Not logged in");
            return;
        }

        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = functionName,
            FunctionParameter = functionParameter
        };

        clientApi.ExecuteCloudScript(request,
            result =>
            {
                if (result.FunctionResultTooLarge ?? false)
                {
                    Debug.LogWarning($"[PlayFab] CloudScript result too large for {functionName}");
                }

                Debug.Log($"[PlayFab] CloudScript '{functionName}' executed successfully");
                successCallback?.Invoke(result.FunctionResult);
            },
            error =>
            {
                Debug.LogError($"[PlayFab] CloudScript execution failed: {error.ErrorMessage}");
                errorCallback?.Invoke(error.ErrorMessage);
            }
        );
    }

    // ========== UTILITY METHODS ==========

    /// <summary>
    /// Set PlayFab Title ID at runtime (useful for environment-specific IDs).
    /// </summary>
    public void SetTitleId(string titleId)
    {
        playfabTitleId = titleId;
        PlayerPrefs.SetString("PlayFab_TitleId", titleId);
        InitializePlayFab();
    }

    /// <summary>
    /// Get all cached decks (from last LoadAllDecks call).
    /// </summary>
    public List<SavedDeck> GetAllCachedDecks()
    {
        return cachedDecks.Values.ToList();
    }

    /// <summary>
    /// Clear all cached data (useful on logout).
    /// </summary>
    public void ClearCache()
    {
        cachedDecks.Clear();
        cachedPlayerData = default;
        cachedCardCollection = new PlayerCardCollection();
    }

    // ========== CARD OWNERSHIP MANAGEMENT ==========

    /// <summary>
    /// Load all player state from CloudScript (cards, active deck, inventory, etc.)
    /// CloudScript retrieves from Internal Data (secure server-side storage).
    /// This is the main method called during login to populate all player data.
    /// </summary>
    public void LoadPlayerState(Action onComplete = null)
    {
        if (!IsLoggedIn())
        {
            Debug.LogError("[PlayFab] Cannot load player state: Not logged in");
            onComplete?.Invoke();
            return;
        }

        // Call CloudScript function to retrieve player state from Internal Data
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "getPlayerState",
            FunctionParameter = new { },
            GeneratePlayStreamEvent = true
        };

        clientApi.ExecuteCloudScript(request,
            result =>
            {
                try
                {
                    if (result.FunctionResult == null)
                    {
                        Debug.LogError("[PlayFab] getPlayerState returned null");
                        onComplete?.Invoke();
                        return;
                    }

                    Debug.Log($"[PlayFab] FunctionResult type: {result.FunctionResult.GetType()}");

                    // Parse the response - it might be JsonObject or Dictionary
                    System.Collections.Generic.Dictionary<string, object> functionResult = null;

                    // Try as JsonObject first (PlayFab SDK format)
                    if (result.FunctionResult is PlayFab.Json.JsonObject jsonObj)
                    {
                        Debug.Log("[PlayFab] Response is JsonObject, converting...");
                        functionResult = new System.Collections.Generic.Dictionary<string, object>();
                        foreach (var key in jsonObj.Keys)
                        {
                            // Skip null keys
                            if (key == null)
                            {
                                Debug.LogWarning("[PlayFab] Skipping null key in JsonObject");
                                continue;
                            }
                            functionResult[key] = jsonObj[key];
                        }
                    }
                    // Try as Dictionary
                    else if (result.FunctionResult is System.Collections.Generic.Dictionary<string, object> dict)
                    {
                        functionResult = dict;
                    }

                    if (functionResult == null || !functionResult.ContainsKey("success"))
                    {
                        Debug.LogError("[PlayFab] getPlayerState: Invalid response format - no 'success' key");
                        if (functionResult != null)
                        {
                            Debug.LogError($"[PlayFab] Available keys: {string.Join(", ", functionResult.Keys)}");
                        }
                        onComplete?.Invoke();
                        return;
                    }

                    bool success = (bool)functionResult["success"];
                    if (!success)
                    {
                        Debug.LogError("[PlayFab] getPlayerState failed: " + (functionResult.ContainsKey("error") ? functionResult["error"] : "Unknown error"));
                        onComplete?.Invoke();
                        return;
                    }

                    // ===== EXTRACT CARDS =====
                    cachedCardCollection = new PlayerCardCollection();
                    if (functionResult.ContainsKey("cards") && functionResult["cards"] is System.Collections.Generic.List<object> cardsList)
                    {
                        Debug.Log($"[PlayFab] Found {cardsList.Count} cards in response");

                        foreach (var cardObj in cardsList)
                        {
                            try
                            {
                                // The cardObj might be a JsonObject or Dictionary from PlayFab
                                var card = new PlayerCardOwnership();

                                // Try to extract fields directly from the object
                                if (cardObj is PlayFab.Json.JsonObject jsonCard)
                                {
                                    Debug.Log($"[PlayFab] Card is JsonObject with keys: {string.Join(", ", jsonCard.Keys)}");

                                    card.cardId = SafeGetString(jsonCard, "cardId");
                                    card.cardName = SafeGetString(jsonCard, "cardName");
                                    card.level = SafeGetInt(jsonCard, "level");
                                    card.quantity = SafeGetInt(jsonCard, "quantity");
                                    card.owned = SafeGetBool(jsonCard, "owned");
                                    card.timesAcquired = SafeGetInt(jsonCard, "timesAcquired");

                                    if (jsonCard.ContainsKey("acquiredDate"))
                                    {
                                        string dateStr = SafeGetString(jsonCard, "acquiredDate");
                                        if (System.DateTime.TryParse(dateStr, out var date))
                                            card.acquiredDate = date;
                                    }
                                }
                                else if (cardObj is System.Collections.Generic.Dictionary<string, object> dictCard)
                                {
                                    Debug.Log($"[PlayFab] Card is Dictionary with keys: {string.Join(", ", dictCard.Keys)}");

                                    // Convert Dictionary to JSON string and parse it
                                    string dictStr = JsonUtility.ToJson(dictCard);
                                    var parsedCard = JsonUtility.FromJson<PlayerCardOwnership>(dictStr);
                                    card.cardId = parsedCard.cardId;
                                    card.cardName = parsedCard.cardName;
                                    card.level = parsedCard.level;
                                    card.quantity = parsedCard.quantity;
                                    card.owned = parsedCard.owned;
                                    card.timesAcquired = parsedCard.timesAcquired;
                                }

                                Debug.Log($"[PlayFab] Parsed card - cardId: {card.cardId}, cardName: {card.cardName}, level: {card.level}, qty: {card.quantity}");

                                if (!string.IsNullOrEmpty(card.cardId) && !string.IsNullOrEmpty(card.cardName))
                                {
                                    cachedCardCollection.cards.Add(card);
                                }
                                else
                                {
                                    Debug.LogWarning($"[PlayFab] Card has empty cardId or cardName, skipping!");
                                }
                            }
                            catch (Exception cardEx)
                            {
                                Debug.LogWarning($"[PlayFab] Failed to parse card object: {cardEx.Message}\n{cardEx.StackTrace}");
                            }
                        }
                    }
                    Debug.Log($"[PlayFab] Loaded {cachedCardCollection.cards.Count} cards into collection");

                    // ===== EXTRACT ACTIVE DECK =====
                    SavedDeck? activeDeck = null;
                    if (functionResult.ContainsKey("activeDeck") && functionResult["activeDeck"] != null)
                    {
                        try
                        {
                            object deckObj = functionResult["activeDeck"];
                            Debug.Log($"[PlayFab] deckobj: {deckObj}");

                            // Parse the response to get deck name and card IDs
                            string deckName = null;
                            List<string> cardIds = new List<string>();

                            if (deckObj is PlayFab.Json.JsonObject jsonDeck)
                            {
                                deckName = SafeGetString(jsonDeck, "deckName");

                                // Extract card IDs list
                                if (jsonDeck.ContainsKey("cardIds") && jsonDeck["cardIds"] is System.Collections.Generic.List<object> cardIdsList)
                                {
                                    foreach (var cardIdObj in cardIdsList)
                                    {
                                        if (cardIdObj is string cardIdStr)
                                            cardIds.Add(cardIdStr);
                                    }
                                }
                            }
                            else if (deckObj is System.Collections.Generic.Dictionary<string, object> deckDict)
                            {
                                if (deckDict.ContainsKey("deckName"))
                                    deckName = deckDict["deckName"].ToString();

                                if (deckDict.ContainsKey("cardIds") && deckDict["cardIds"] is System.Collections.Generic.List<object> cardIdsList)
                                {
                                    foreach (var cardIdObj in cardIdsList)
                                    {
                                        if (cardIdObj is string cardIdStr)
                                            cardIds.Add(cardIdStr);
                                    }
                                }
                            }

                            // Build SavedDeck with card IDs for UI to display
                            if (!string.IsNullOrEmpty(deckName))
                            {
                                var deckCards = new List<DeckPayload.CardDataDto>();

                                foreach (var cardId in cardIds)
                                {
                                    // Create CardDataDto with ID for SelectionCardManager lookup
                                    deckCards.Add(new DeckPayload.CardDataDto
                                    {
                                        id = cardId
                                    });
                                }

                                activeDeck = new SavedDeck
                                {
                                    DeckName = deckName,
                                    Cards = deckCards,
                                    LastModified = System.DateTime.UtcNow
                                };

                                Debug.Log($"[PlayFab] Loaded active deck: {activeDeck?.DeckName} with {deckCards.Count} cards for UI selection");
                            }
                        }
                        catch (Exception deckEx)
                        {
                            Debug.LogWarning($"[PlayFab] Failed to parse active deck: {deckEx.Message}");
                        }
                    }

                    // ===== EXTRACT INVENTORY =====
                    int gems = 0, gold = 0;
                    if (functionResult.ContainsKey("inventory"))
                    {
                        try
                        {

                            object invObj = functionResult["inventory"];

                            // Extract gems and gold directly from JsonObject or Dictionary
                            if (invObj is PlayFab.Json.JsonObject jsonInv)
                            {
                                Debug.Log($"[PlayFab] Inventory is JsonObject with keys: {string.Join(", ", jsonInv.Keys)}");
                                string invStr = jsonInv.ToString();
                                Debug.Log($"[PlayFab] Inventory content: {invStr}");
                                var inv = JsonUtility.FromJson<InventoryData>(invStr);
                                gems = inv.gems;
                                gold = inv.gold;
                            }
                            else if (invObj is System.Collections.Generic.Dictionary<string, object> invDict)
                            {
                                gems = SafeGetIntFromDict(invDict, "gems");
                                gold = SafeGetIntFromDict(invDict, "gold");
                            }

                            Debug.Log($"[PlayFab] Loaded inventory: {gems} gems, {gold} gold");
                        }
                        catch (Exception invEx)
                        {
                            Debug.LogWarning($"[PlayFab] Failed to parse inventory: {invEx.Message}");
                        }
                    }

                    // ===== UPDATE PLAYER STATES =====
                    if (PlayerStates.Instance != null)
                    {
                        // Set card collection (marks _cardsLoaded = true)
                        PlayerStates.Instance.SetCardCollection(cachedCardCollection);

                        // Set gems/gold
                        PlayerStates.Instance.Gems = gems;
                        PlayerStates.Instance.Gold = gold;
                        Debug.Log($"[PlayFab] PlayerStates updated: {gems} gems, {gold} gold");

                        // Set active deck (marks _activeDeckLoaded = true)
                        PlayerStates.Instance.SetActiveDeck(activeDeck);

                        Debug.Log("[PlayFab] PlayerStates updated with all player data");
                    }
                    else
                    {
                        Debug.LogError("[PlayFab] PlayerStates.Instance is NULL during LoadPlayerState callback. Scene may not be loaded yet.");
                        Debug.LogError("[PlayFab] Ensure PlayerStates is in the scene BEFORE login, or call login after scene loads.");
                    }

                    Debug.Log("[PlayFab] Player state loaded successfully");
                    onComplete?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PlayFab] Error loading player state: {ex.Message}");
                    onComplete?.Invoke();
                }
            },
            error =>
            {
                Debug.LogError($"[PlayFab] Failed to load player state: {error.ErrorMessage}");
                onComplete?.Invoke();
            }
        );
    }

    /// <summary>
    /// Load player's card collection from PlayFab via CloudScript.
    /// CloudScript retrieves from Internal Data (secure server-side storage).
    /// Call this after login to populate card ownership information.
    /// </summary>
    public void LoadPlayerCardCollection(Action<PlayerCardCollection> callback = null)
    {
        if (!IsLoggedIn())
        {
            Debug.LogError("[PlayFab] Cannot load card collection: Not logged in");
            callback?.Invoke(new PlayerCardCollection());
            return;
        }

        // Call LoadPlayerState and extract just the card collection
        LoadPlayerState(() =>
        {
            callback?.Invoke(cachedCardCollection);
        });
    }
    /// Loaded from PlayFab Title Data and cached locally.
    /// </summary>
    public void LoadCardLibrary(Action<CardLibrary> callback = null)
    {
        if (!isInitialized)
        {
            Debug.LogError("[PlayFab] Cannot load card library: Not initialized");
            callback?.Invoke(new CardLibrary());
            return;
        }

        // In a real scenario, you would fetch this from PlayFab Title Data
        // For now, this loads from local DeckRegistry
        LoadCardLibraryFromDeckRegistry();

        Debug.Log($"[PlayFab] Loaded card library with {cachedCardLibrary.cards.Count} cards");
        callback?.Invoke(cachedCardLibrary);
    }

    /// <summary>
    /// Get player's card collection (cached locally after LoadPlayerCardCollection).
    /// </summary>
    public PlayerCardCollection GetCardCollection()
    {
        return cachedCardCollection;
    }

    /// <summary>
    /// Check if player owns a specific card.
    /// </summary>
    public bool OwnsCard(string cardId)
    {
        return cachedCardCollection.OwnsCard(cardId);
    }

    /// <summary>
    /// Get the level of a card the player owns (local cache).
    /// Returns 0 if player doesn't own the card.
    /// </summary>
    public int GetPlayerCardLevel(string cardId)
    {
        return cachedCardCollection.GetCardLevel(cardId);
    }

    /// <summary>
    /// Award card copies to the player and save to PlayFab.
    /// <summary>
    /// Award card copies to player. 
    /// Automatically unlocks card (sets Level 1) on first copy.
    /// Does NOT auto-level - player must manually upgrade with currency.
    /// </summary>
    public void AwardCardToPlayer(string cardId, string cardName, int copiesToAdd = 1, Action<bool> callback = null)
    {
        if (!IsLoggedIn() || string.IsNullOrEmpty(authContext.EntityToken))
        {
            Debug.LogError("[PlayFab] Cannot award card: Not logged in");
            callback?.Invoke(false);
            return;
        }

        // Update local collection with quantities (auto-unlocks on first copy, no auto-level)
        cachedCardCollection.AcquireCard(cardId, cardName, copiesToAdd);

        // Get the updated card info for logging
        var card = cachedCardCollection.cards.Find(c => c.cardId == cardId);
        if (card != null)
        {
            Debug.Log($"[PlayFab] Awarded {copiesToAdd} copy/copies of {cardName}. Total owned: {card.quantity}, Level: {card.level}");
        }

        // Save to PlayFab
        SaveCardCollectionToPlayFab((success) =>
        {
            callback?.Invoke(success);
        });
    }

    /// <summary>
    /// Manually level up a card (player-initiated with currency cost).
    /// Server-side validation: validates ownership, currency, and max level.
    /// Calls PlayFab Cloud Function to handle all logic.
    /// </summary>
    public void LevelUpCard(string cardId, int currencyCost = 100, Action<bool, string> callback = null)
    {
        if (!IsLoggedIn() || string.IsNullOrEmpty(authContext.EntityToken))
        {
            Debug.LogError("[PlayFab] Cannot level up card: Not logged in");
            callback?.Invoke(false, "Not logged in");
            return;
        }

        // Call cloud function with card ID and currency cost
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "LevelUpCard",
            FunctionParameter = new { cardId = cardId, currencyCost = currencyCost },
            GeneratePlayStreamEvent = true
        };

        clientApi.ExecuteCloudScript(request,
            result =>
            {
                try
                {
                    // Check if cloud function executed successfully
                    if (result.FunctionResult == null)
                    {
                        Debug.LogError("[PlayFab] LevelUpCard cloud function returned null");
                        callback?.Invoke(false, "Cloud function returned null");
                        return;
                    }

                    // Parse the response - it might be JsonObject or Dictionary
                    System.Collections.Generic.Dictionary<string, object> functionResult = null;

                    // Try as JsonObject first (PlayFab SDK format)
                    if (result.FunctionResult is PlayFab.Json.JsonObject jsonObj)
                    {
                        Debug.Log("[PlayFab] LevelUpCard response is JsonObject, converting...");
                        functionResult = new System.Collections.Generic.Dictionary<string, object>();
                        foreach (var key in jsonObj.Keys)
                        {
                            if (key == null)
                            {
                                Debug.LogWarning("[PlayFab] Skipping null key in LevelUpCard response");
                                continue;
                            }
                            functionResult[key] = jsonObj[key];
                        }
                    }
                    // Try as Dictionary
                    else if (result.FunctionResult is System.Collections.Generic.Dictionary<string, object> dict)
                    {
                        functionResult = dict;
                    }

                    if (functionResult == null || !functionResult.ContainsKey("success"))
                    {
                        Debug.LogError("[PlayFab] LevelUpCard: Invalid response format");
                        callback?.Invoke(false, "Invalid response format");
                        return;
                    }

                    bool success = (bool)functionResult["success"];

                    if (success)
                    {
                        Debug.Log($"[PlayFab] Card {cardId} leveled up successfully");

                        // Extract updated card and inventory from response
                        if (functionResult.ContainsKey("card"))
                        {
                            try
                            {
                                string cardJson = JsonUtility.ToJson(functionResult["card"]);
                                var updatedCard = JsonUtility.FromJson<PlayerCardOwnership>(cardJson);

                                // Update local cache
                                var existingCard = cachedCardCollection.cards.Find(c => c.cardId == cardId);
                                if (existingCard != null)
                                {
                                    existingCard.level = updatedCard.level;
                                    existingCard.quantity = updatedCard.quantity;
                                }

                                // Update PlayerStates if available
                                if (PlayerStates.Instance != null)
                                {
                                    var stateCard = PlayerStates.Instance.CardCollection.cards.Find(c => c.cardId == cardId);
                                    if (stateCard != null)
                                    {
                                        stateCard.level = updatedCard.level;
                                        stateCard.quantity = updatedCard.quantity;
                                    }

                                    // Update gems if available
                                    if (functionResult.ContainsKey("inventory"))
                                    {
                                        string inventoryJson = JsonUtility.ToJson(functionResult["inventory"]);
                                        var inventory = JsonUtility.FromJson<InventoryData>(inventoryJson);
                                        PlayerStates.Instance.Gems = inventory.gems;
                                        PlayerStates.Instance.Gold = inventory.gold;
                                    }
                                }

                                Debug.Log($"[PlayFab] Card {cardId} now Level {updatedCard.level}, Quantity: {updatedCard.quantity}");
                            }
                            catch (Exception parseEx)
                            {
                                Debug.LogWarning($"[PlayFab] Failed to parse updated card data: {parseEx.Message}");
                            }
                        }

                        // Reload player state to ensure everything is synced
                        Debug.Log($"[PlayFab] Reloading player state after level up...");
                        LoadPlayerState(() =>
                        {
                            var finalCard = cachedCardCollection.cards.Find(c => c.cardId == cardId);
                            if (finalCard != null)
                            {
                                Debug.Log($"[PlayFab] Level up complete - Card {cardId} final level: {finalCard.level}");
                            }
                            callback?.Invoke(true, "Card leveled up successfully");
                        });
                    }
                    else
                    {
                        string error = functionResult.ContainsKey("error") ? functionResult["error"].ToString() : "Unknown error";
                        Debug.LogError($"[PlayFab] LevelUpCard failed: {error}");
                        callback?.Invoke(false, error);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PlayFab] Error processing LevelUpCard response: {ex.Message}");
                    callback?.Invoke(false, "Error processing response: " + ex.Message);
                }
            },
            error =>
            {
                Debug.LogError($"[PlayFab] LevelUpCard cloud function call failed: {error.ErrorMessage}");
                callback?.Invoke(false, error.ErrorMessage);
            }
        );
    }

    [System.Serializable]
    private class InventoryData
    {
        public int gems;
        public int gold;
    }

    /// <summary>
    /// Get all available cards with ownership status for UI display.
    /// </summary>
    public List<PlayerCardOwnership> GetAllCardsWithOwnershipStatus()
    {
        var result = new List<PlayerCardOwnership>();

        // For each card in the library, create an ownership record
        foreach (var libraryCard in cachedCardLibrary.cards)
        {
            var ownership = new PlayerCardOwnership
            {
                cardId = libraryCard.id,
                cardName = libraryCard.name,
                owned = cachedCardCollection.OwnsCard(libraryCard.id),
                level = cachedCardCollection.GetCardLevel(libraryCard.id)
            };
            result.Add(ownership);
        }

        return result;
    }

    /// <summary>
    /// Initialize a new player with starter cards.
    /// Called on first login or when player has no card collection.
    /// </summary>
    private void InitializeNewPlayerCards()
    {
        cachedCardCollection = new PlayerCardCollection();

        // Load DeckRegistry and give starter cards
        LoadCardLibraryFromDeckRegistry();

        // Award all starter cards to new player
        foreach (var card in cachedCardLibrary.cards)
        {
            if (card.starterCard)
            {
                cachedCardCollection.AcquireCard(card.id, card.name, 1);
            }
        }

        // Save initial collection
        SaveCardCollectionToPlayFab((success) =>
        {
            if (success)
            {
                Debug.Log("[PlayFab] Initialized new player with starter cards");
            }
        });
    }

    /// <summary>
    /// Save player's card collection to PlayFab Player Data.
    /// </summary>
    private void SaveCardCollectionToPlayFab(Action<bool> callback = null)
    {
        if (!IsLoggedIn() || string.IsNullOrEmpty(authContext.EntityToken))
        {
            Debug.LogError("[PlayFab] Cannot save card collection: Not logged in");
            callback?.Invoke(false);
            return;
        }

        string collectionJson = JsonUtility.ToJson(cachedCardCollection);

        var request = new SetObjectsRequest
        {
            Entity = new PlayFab.DataModels.EntityKey
            {
                Id = authContext.EntityId,
                Type = authContext.EntityType
            },
            Objects = new List<SetObject>
            {
                new SetObject
                {
                    ObjectName = "player_cards",
                    DataObject = collectionJson
                }
            }
        };

        dataApi.SetObjects(request,
            result =>
            {
                Debug.Log("[PlayFab] Card collection saved successfully");
                callback?.Invoke(true);
            },
            error =>
            {
                Debug.LogError($"[PlayFab] Failed to save card collection: {error.ErrorMessage}");
                callback?.Invoke(false);
            }
        );
    }

    /// <summary>
    /// Load card library from local DeckRegistry.
    /// In production, you could fetch this from PlayFab Title Data instead.
    /// </summary>
    private void LoadCardLibraryFromDeckRegistry()
    {
        cachedCardLibrary = new CardLibrary();

        // Load DeckRegistry from Resources
        var registry = Resources.Load<DeckRegistry>("DeckRegistry");
        if (registry == null)
        {
            Debug.LogWarning("[PlayFab] DeckRegistry not found in Resources");
            return;
        }

        // Convert CardData to CardLibraryEntry
        foreach (var cardData in registry.allCards)
        {
            var cardName = cardData.cardName ?? "Unknown Card";
            cachedCardLibrary.cards.Add(new CardLibraryEntry
            {
                id = cardName.ToLower().Replace(" ", "_"),
                name = cardName,
                description = "",
                cardType = "unit",
                maxLevel = 5,
                starterCard = false
            });
        }

        // Optionally add legends and stones
        foreach (var legend in registry.allGuardians)
        {
            cachedCardLibrary.cards.Add(new CardLibraryEntry
            {
                id = $"legend_{legend.legendName.ToLower().Replace(" ", "_")}",
                name = legend.legendName,
                description = "",
                cardType = "legend",
                maxLevel = 5,
                starterCard = false
            });
        }

        foreach (var stone in registry.allLegendStones)
        {
            cachedCardLibrary.cards.Add(new CardLibraryEntry
            {
                id = $"stone_{stone.stoneName.ToLower().Replace(" ", "_")}",
                name = stone.stoneName,
                description = "",
                cardType = "stone",
                maxLevel = 5,
                starterCard = false
            });
        }

        Debug.Log($"[PlayFab] Loaded {cachedCardLibrary.cards.Count} cards from DeckRegistry");
    }

    /// <summary>
    /// Initialize player's card collection with all cards from DeckRegistry.
    /// Called after first login to populate PlayFab with all available cards.
    /// </summary>
    public void InitializePlayerCardCollection()
    {
        if (cachedCardLibrary.cards.Count == 0)
        {
            LoadCardLibraryFromDeckRegistry();
        }

        var newCollection = new PlayerCardCollection();

        // Add all cards as locked (level 0, owned=false, quantity=0)
        foreach (var cardEntry in cachedCardLibrary.cards)
        {
            newCollection.cards.Add(new PlayerCardOwnership
            {
                cardId = cardEntry.id,
                cardName = cardEntry.name,
                level = 0,      // Locked
                owned = false,
                quantity = 0,
                timesAcquired = 0,
                acquiredDate = System.DateTime.UtcNow
            });
        }

        cachedCardCollection = newCollection;

        // Save to PlayFab
        SavePlayerCardCollection(() =>
        {
            Debug.Log("[PlayFab] Player card collection initialized with " + newCollection.cards.Count + " cards");
        });
    }

    /// <summary>
    /// Save the entire player card collection to PlayFab.
    /// </summary>
    private void SavePlayerCardCollection(Action onComplete = null)
    {
        if (!IsLoggedIn())
        {
            Debug.LogError("[PlayFab] Not logged in, cannot save card collection");
            return;
        }

        try
        {
            var playerCardsDict = new Dictionary<string, object>();
            foreach (var card in cachedCardCollection.cards)
            {
                playerCardsDict[card.cardId] = new Dictionary<string, object>
                {
                    { "cardId", card.cardId },
                    { "cardName", card.cardName },
                    { "level", card.level },
                    { "owned", card.owned },
                    { "quantity", card.quantity },
                    { "timesAcquired", card.timesAcquired },
                    { "acquiredDate", card.acquiredDate.ToString("o") } // ISO 8601 format
                };
            }

            var request = new SetObjectsRequest
            {
                Objects = new List<SetObject>
                {
                    new SetObject
                    {
                        ObjectName = "player_cards",
                        DataObject = playerCardsDict
                    }
                }
            };

            dataApi.SetObjects(request,
                result =>
                {
                    Debug.Log("[PlayFab] Card collection saved successfully");
                    onComplete?.Invoke();
                },
                error =>
                {
                    Debug.LogError($"[PlayFab] Failed to save card collection: {error.ErrorMessage}");
                }
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlayFab] Error saving card collection: {ex.Message}");
        }
    }

    // ========== HELPER METHODS FOR SAFE TYPE CONVERSION ==========

    /// <summary>
    /// Safe string extraction from JsonObject
    /// </summary>
    private string SafeGetString(PlayFab.Json.JsonObject obj, string key)
    {
        if (!obj.ContainsKey(key) || obj[key] == null)
            return "";

        var val = obj[key];
        if (val is string str)
            return str;

        return val.ToString();
    }

    /// <summary>
    /// Safe int extraction from JsonObject
    /// Handles double, string, and long conversions
    /// </summary>
    private int SafeGetInt(PlayFab.Json.JsonObject obj, string key)
    {
        if (obj[key] == null)
            return 0;

        var val = obj[key];

        // Try different types
        if (val is double d)
            return (int)d;
        if (val is int i)
            return i;
        if (val is long l)
            return (int)l;
        if (val is ulong ul)
            return (int)ul;
        if (val is string s && int.TryParse(s, out int result))
            return result;

        return 0;
    }

    /// <summary>
    /// Safe bool extraction from JsonObject
    /// </summary>
    private bool SafeGetBool(PlayFab.Json.JsonObject obj, string key)
    {
        if (!obj.ContainsKey(key) || obj[key] == null)
            return false;

        var val = obj[key];
        if (val is bool b)
            return b;
        if (val is string s && bool.TryParse(s, out bool result))
            return result;

        return false;
    }

    /// <summary>
    /// Safe string extraction from Dictionary
    /// </summary>
    private string SafeGetStringFromDict(System.Collections.Generic.Dictionary<string, object> dict, string key)
    {
        if (!dict.ContainsKey(key) || dict[key] == null)
            return "";

        var val = dict[key];
        if (val is string str)
            return str;

        return val.ToString();
    }

    /// <summary>
    /// Safe int extraction from Dictionary
    /// </summary>
    private int SafeGetIntFromDict(System.Collections.Generic.Dictionary<string, object> dict, string key)
    {
        if (!dict.ContainsKey(key) || dict[key] == null)
            return 0;

        var val = dict[key];

        if (val is double d)
            return (int)d;
        if (val is int i)
            return i;
        if (val is long l)
            return (int)l;
        if (val is ulong ul)
            return (int)ul;
        if (val is string s && int.TryParse(s, out int result))
            return result;

        return 0;
    }

    /// <summary>
    /// Safe bool extraction from Dictionary
    /// </summary>
    private bool SafeGetBoolFromDict(System.Collections.Generic.Dictionary<string, object> dict, string key)
    {
        if (!dict.ContainsKey(key) || dict[key] == null)
            return false;

        var val = dict[key];
        if (val is bool b)
            return b;
        if (val is string s && bool.TryParse(s, out bool result))
            return result;

        return false;
    }

    // ========== MATCHMAKING ==========

    public class MatchData
    {
        public string MatchId;
        public string OpponentName;
        public string ServerIp;
        public int ServerPort;
        public string Status; // "starting", "ready", "failed"
    }

    private Coroutine matchPollingCoroutine;
    private bool isPollingMatch = false;
    private float matchPollInterval = 1f;

    public event Action OnMatchFound;
    public event Action<string> OnMatchStatusChanged;
    public event Action<string> OnMatchError;

    public MatchData CurrentMatchData { get; private set; }

    /// <summary>
    /// Start matchmaking by calling PlayFab cloud function
    /// </summary>
    public void StartMatchmaking(Action<MatchData> onMatchStarted, Action<string> onError = null)
    {
        if (!IsLoggedIn())
        {
            Debug.LogError("[Matchmaking] Not logged in");
            onError?.Invoke("Not logged in");
            return;
        }

        ExecuteCloudScript("startMatchmaking", new Dictionary<string, object>(),
            result =>
            {
                Debug.Log("[Matchmaking] Matchmaking started");
                var resultDict = result as Dictionary<string, object>;
                if (resultDict != null && resultDict.ContainsKey("matchId"))
                {
                    string matchId = resultDict["matchId"].ToString();
                    Debug.Log($"[Matchmaking] Match ID: {matchId}");
                    onMatchStarted?.Invoke(null);

                    // Start polling for match status
                    StartMatchPolling();
                }
                else
                {
                    Debug.Log("[Matchmaking] Queued, waiting for opponent...");
                    onMatchStarted?.Invoke(null);

                    // Start polling for match status
                    StartMatchPolling();
                }
            },
            errorMsg =>
            {
                Debug.LogError($"[Matchmaking] Failed to start matchmaking: {errorMsg}");
                onError?.Invoke(errorMsg);
            }
        );
    }

    /// <summary>
    /// Start polling for match status
    /// </summary>
    private void StartMatchPolling()
    {
        if (isPollingMatch) return;

        isPollingMatch = true;
        matchPollingCoroutine = StartCoroutine(MatchPollingCoroutine());
    }

    /// <summary>
    /// Stop polling for match status
    /// </summary>
    public void StopMatchPolling()
    {
        if (!isPollingMatch) return;

        isPollingMatch = false;
        if (matchPollingCoroutine != null)
        {
            StopCoroutine(matchPollingCoroutine);
        }
    }

    /// <summary>
    /// Coroutine that polls match status every N seconds
    /// </summary>
    private System.Collections.IEnumerator MatchPollingCoroutine()
    {
        while (isPollingMatch && IsLoggedIn())
        {
            yield return new WaitForSeconds(matchPollInterval);

            ExecuteCloudScript("checkMatchStatus", new Dictionary<string, object>(),
                result =>
                {
                    if (!isPollingMatch) return;

                    // Convert result to string then parse JSON
                    string resultStr = result?.ToString() ?? "";
                    Debug.Log($"[Matchmaking] Raw response: {resultStr}");

                    Dictionary<string, object> resultDict = null;

                    try
                    {
                        // Try to parse as JsonObject first
                        if (result is PlayFab.Json.JsonObject jsonObj)
                        {
                            resultDict = new Dictionary<string, object>();
                            foreach (var key in jsonObj.Keys)
                            {
                                if (key != null)
                                    resultDict[key] = jsonObj[key];
                            }
                        }
                        // Or as Dictionary
                        else if (result is Dictionary<string, object> dict)
                        {
                            resultDict = dict;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[Matchmaking] Failed to parse response: {ex.Message}");
                        return;
                    }

                    if (resultDict == null)
                    {
                        Debug.LogError("[Matchmaking] Could not parse response");
                        return;
                    }

                    Debug.Log($"[Matchmaking] Parsed result with {resultDict.Count} keys");

                    // Check if matched
                    bool matched = SafeGetBoolFromDict(resultDict, "matched");
                    if (!matched)
                    {
                        if (resultDict.ContainsKey("message"))
                        {
                            Debug.Log($"[Matchmaking] {resultDict["message"]}");
                        }
                        return;
                    }

                    // Extract match status
                    string status = resultDict.ContainsKey("status") ? resultDict["status"].ToString() : "starting";
                    OnMatchStatusChanged?.Invoke(status);

                    // Check if ready
                    if (status == "ready")
                    {
                        string matchId = resultDict.ContainsKey("matchId") ? resultDict["matchId"].ToString() : null;
                        string serverIp = resultDict.ContainsKey("serverIp") ? resultDict["serverIp"].ToString() : null;
                        int serverPort = SafeGetIntFromDict(resultDict, "serverPort");
                        string opponent = resultDict.ContainsKey("opponent") ? resultDict["opponent"].ToString() : "Opponent";

                        if (matchId != null && serverIp != null && serverPort > 0)
                        {
                            CurrentMatchData = new MatchData
                            {
                                MatchId = matchId,
                                OpponentName = opponent,
                                ServerIp = serverIp,
                                ServerPort = serverPort,
                                Status = "ready"
                            };

                            Debug.Log($"[Matchmaking] Match ready! Server: {serverIp}:{serverPort}");

                            // Stop polling
                            StopMatchPolling();

                            // Connect to server before notifying (don't fire OnMatchFound until connected)
                            StartCoroutine(ConnectToGameServer(serverIp, serverPort));
                        }
                        else
                        {
                            Debug.LogError("[Matchmaking] Match ready but missing connection details");
                            OnMatchError?.Invoke("Missing server connection details");
                            StopMatchPolling();
                        }
                    }
                    else
                    {
                        Debug.Log($"[Matchmaking] Server status: {status}");
                    }
                },
                errorMsg =>
                {
                    Debug.LogWarning($"[Matchmaking] Error polling match status: {errorMsg}");
                    // Continue polling even on error
                }
            );
        }

        Debug.Log("[Matchmaking] Stopped polling for match status");
    }

    /// <summary>
    /// Cancel matchmaking
    /// </summary>
    public void CancelMatchmaking(Action onCancelled = null)
    {
        StopMatchPolling();

        ExecuteCloudScript("cancelMatchmaking", new Dictionary<string, object>(),
            result =>
            {
                Debug.Log("[Matchmaking] Cancelled matchmaking");
                CurrentMatchData = null;
                onCancelled?.Invoke();
            },
            errorMsg =>
            {
                Debug.LogError($"[Matchmaking] Failed to cancel: {errorMsg}");
            }
        );
    }

    /// <summary>
    /// Connect to game server as client
    /// </summary>
    private System.Collections.IEnumerator ConnectToGameServer(string serverIp, int serverPort)
    {
        Debug.Log($"[Matchmaking] Attempting to connect to {serverIp}:{serverPort}");

        yield return new WaitForSeconds(0.5f);

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[Matchmaking] NetworkManager not found!");
            OnMatchError?.Invoke("NetworkManager not available");
            yield break;
        }

        Debug.Log("[Matchmaking] Delegating to HeadlessServer.ClientConnectionRoutine()");

        // Set the connection config with server details
        HeadlessServer.SetClientConnectionConfig(serverIp, (ushort)serverPort);

        // Start the HeadlessServer client connection
        yield return HeadlessServer.Instance.StartCoroutine(HeadlessServer.Instance.GetClientConnectionRoutine());

        Debug.Log("[Matchmaking] HeadlessServer client connection complete");

        // Only fire OnMatchFound AFTER successful connection
        OnMatchFound?.Invoke();
    }
}

