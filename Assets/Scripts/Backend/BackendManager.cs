using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Manages all backend API communication and player data caching
/// Place this script on a GameObject that persists between scenes
/// </summary>
public class BackendManager : MonoBehaviour
{
    public static BackendManager Instance { get; private set; }

    [SerializeField] private string backendUrl = "http://185.252.233.117:8000";

    private PlayerData cachedPlayerData;
    private string sessionId;   // Store sessionid from login
    private string csrfToken;   // Store CSRF token from login
    private string csrfCookie;  // Store csrftoken cookie value from login

    // Events for UI updates
    public delegate void LoginSuccessEvent(PlayerData playerData);
    public delegate void LoginFailedEvent(string error);
    public delegate void DataLoadedEvent(PlayerData playerData);

    public static event LoginSuccessEvent OnLoginSuccess;
    public static event LoginFailedEvent OnLoginFailed;
    public static event DataLoadedEvent OnDataLoaded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Login with username and password
    /// </summary>
    public void Login(string username, string password)
    {
        StartCoroutine(LoginCoroutine(username, password));
    }

    private IEnumerator LoginCoroutine(string username, string password)
    {
        var loginRequest = new LoginRequest
        {
            username = username,
            password = password
        };

        string jsonBody = JsonUtility.ToJson(loginRequest);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(
            $"{backendUrl}/api/players/login/",
            UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Debug.Log("Login Response: " + responseText);

                Debug.Log($"[BackendManager LOGIN] ========== PARSING RESPONSE ==========");

                // Parse response
                try
                {
                    var response = JsonUtility.FromJson<LoginResponse>(responseText);
                    if (response.success && response.user != null)
                    {
                        cachedPlayerData = response.user;

                        // Extract tokens from response body if present
                        if (!string.IsNullOrEmpty(response.session_id))
                        {
                            sessionId = response.session_id;
                            Debug.Log($"[BackendManager LOGIN] ✓ Session ID from response: {sessionId}");
                        }

                        if (!string.IsNullOrEmpty(response.csrf_token))
                        {
                            csrfToken = response.csrf_token;
                            Debug.Log($"[BackendManager LOGIN] ✓ CSRF Token from response: {csrfToken}");
                        }

                        // If tokens not in response, try to extract from Set-Cookie headers
                        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(csrfToken))
                        {
                            Debug.Log($"[BackendManager LOGIN] Tokens missing from response, trying headers...");

                            string setCookieDirect = request.GetResponseHeader("Set-Cookie");
                            string setCookieLower = request.GetResponseHeader("set-cookie");
                            string setCookieHeader = setCookieDirect ?? setCookieLower;

                            if (!string.IsNullOrEmpty(setCookieHeader))
                            {
                                Debug.Log($"[BackendManager LOGIN] Set-Cookie header: {setCookieHeader}");

                                // Split by comma to separate multiple Set-Cookie values
                                string[] cookieEntries = setCookieHeader.Split(',');

                                foreach (string entry in cookieEntries)
                                {
                                    string trimmedEntry = entry.Trim();

                                    // Extract the name=value part (before first semicolon)
                                    int semiIndex = trimmedEntry.IndexOf(';');
                                    string nameValuePart = semiIndex > 0 ? trimmedEntry.Substring(0, semiIndex) : trimmedEntry;

                                    // Split name=value
                                    int eqIndex = nameValuePart.IndexOf('=');
                                    if (eqIndex > 0)
                                    {
                                        string name = nameValuePart.Substring(0, eqIndex).Trim();
                                        string value = nameValuePart.Substring(eqIndex + 1).Trim();

                                        // Handle malformed values with commas
                                        if (name.ToLower() == "csrftoken" && value.Contains(","))
                                        {
                                            string[] parts = value.Split(',');
                                            value = parts[0].Trim();
                                        }

                                        if (name.ToLower() == "sessionid" && string.IsNullOrEmpty(sessionId))
                                        {
                                            sessionId = value;
                                            Debug.Log($"[BackendManager LOGIN] ✓ Session ID from header: {sessionId}");
                                        }
                                        else if (name.ToLower() == "csrftoken" && string.IsNullOrEmpty(csrfToken))
                                        {
                                            csrfToken = value;
                                            csrfCookie = value;  // Also store as cookie
                                            Debug.Log($"[BackendManager LOGIN] ✓ CSRF Token from header: {csrfToken}");
                                            Debug.Log($"[BackendManager LOGIN] ✓ CSRF Cookie from header: {csrfCookie}");
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // If we got tokens from JSON response, also extract csrftoken cookie if available
                            string setCookieDirect = request.GetResponseHeader("Set-Cookie");
                            string setCookieLower = request.GetResponseHeader("set-cookie");
                            string setCookieHeader = setCookieDirect ?? setCookieLower;

                            if (!string.IsNullOrEmpty(setCookieHeader))
                            {
                                string[] cookieEntries = setCookieHeader.Split(',');
                                foreach (string entry in cookieEntries)
                                {
                                    string trimmedEntry = entry.Trim();
                                    int semiIndex = trimmedEntry.IndexOf(';');
                                    string nameValuePart = semiIndex > 0 ? trimmedEntry.Substring(0, semiIndex) : trimmedEntry;
                                    int eqIndex = nameValuePart.IndexOf('=');

                                    if (eqIndex > 0)
                                    {
                                        string name = nameValuePart.Substring(0, eqIndex).Trim();
                                        string value = nameValuePart.Substring(eqIndex + 1).Trim();

                                        if (name.ToLower() == "csrftoken")
                                        {
                                            if (value.Contains(","))
                                            {
                                                value = value.Split(',')[0].Trim();
                                            }
                                            csrfCookie = value;
                                            Debug.Log($"[BackendManager LOGIN] ✓ CSRF Cookie from Set-Cookie: {csrfCookie}");
                                        }
                                    }
                                }
                            }
                        }

                        Debug.Log($"[BackendManager LOGIN] ✓ Login successful for player: {response.user.username}");
                        Debug.Log($"[BackendManager LOGIN] Session ID: {sessionId}");
                        Debug.Log($"[BackendManager LOGIN] CSRF Token: {csrfToken}");
                        Debug.Log($"[BackendManager LOGIN] ========== END PARSING ==========");

                        OnLoginSuccess?.Invoke(cachedPlayerData);
                        OnDataLoaded?.Invoke(cachedPlayerData);
                    }
                    else
                    {
                        Debug.LogError("[BackendManager] Login response success=false");
                        OnLoginFailed?.Invoke("Login response indicates failure");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[BackendManager] Failed to parse login response: {e.Message}");
                    OnLoginFailed?.Invoke($"Parse error: {e.Message}");
                }
            }
            else
            {
                string errorMsg = $"Login failed: {request.error}";
                if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                {
                    errorMsg += " - " + request.downloadHandler.text;
                }
                Debug.LogError(errorMsg);
                OnLoginFailed?.Invoke(errorMsg);
            }
        }
    }

    /// <summary>
    /// Get current cached player data (call after successful login)
    /// </summary>
    public PlayerData GetCachedPlayerData()
    {
        return cachedPlayerData;
    }

    /// <summary>
    /// Get all player cards from cache
    /// </summary>
    public List<PlayerCard> GetAllCards()
    {
        if (cachedPlayerData?.all_cards == null)
            return new List<PlayerCard>();

        return new List<PlayerCard>(cachedPlayerData.all_cards);
    }

    /// <summary>
    /// Get all player decks from cache
    /// </summary>
    public List<SavedDeck> GetAllDecks()
    {
        if (cachedPlayerData?.decks == null)
            return new List<SavedDeck>();

        return new List<SavedDeck>(cachedPlayerData.decks);
    }

    /// <summary>
    /// Get a specific deck by name
    /// </summary>
    public SavedDeck GetDeckByName(string deckName)
    {
        if (cachedPlayerData?.decks == null)
            return null;

        foreach (var deck in cachedPlayerData.decks)
        {
            if (deck.deck_name == deckName)
                return deck;
        }
        return null;
    }

    /// <summary>
    /// Get all cards in a specific deck (including legend and guardian)
    /// </summary>
    public List<PlayerCardWithDeck> GetDeckCards(SavedDeck deck)
    {
        var deckCards = new List<PlayerCardWithDeck>();

        if (deck == null) return deckCards;

        // Regular card slots
        if (deck.card_slot_1 != null) deckCards.Add(deck.card_slot_1);
        if (deck.card_slot_2 != null) deckCards.Add(deck.card_slot_2);
        if (deck.card_slot_3 != null) deckCards.Add(deck.card_slot_3);
        if (deck.card_slot_4 != null) deckCards.Add(deck.card_slot_4);
        if (deck.card_slot_5 != null) deckCards.Add(deck.card_slot_5);
        if (deck.card_slot_6 != null) deckCards.Add(deck.card_slot_6);
        if (deck.card_slot_7 != null) deckCards.Add(deck.card_slot_7);
        if (deck.card_slot_8 != null) deckCards.Add(deck.card_slot_8);

        // Special slots
        if (deck.legend_stone != null) deckCards.Add(deck.legend_stone);
        if (deck.guardian_card != null) deckCards.Add(deck.guardian_card);

        return deckCards;
    }

    /// <summary>
    /// Check if player is logged in
    /// </summary>
    public bool IsLoggedIn()
    {
        return cachedPlayerData != null;
    }

    /// <summary>
    /// Get session ID for authentication
    /// </summary>
    public string GetSessionId()
    {
        return sessionId;
    }

    /// <summary>
    /// Get CSRF token for authentication
    /// </summary>
    public string GetCsrfToken()
    {
        return csrfToken;
    }

    /// <summary>
    /// Get CSRF cookie for authentication
    /// </summary>
    public string GetCsrfCookie()
    {
        return csrfCookie;
    }

    public void Logout()
    {
        cachedPlayerData = null;
        sessionId = null;
        csrfToken = null;
        csrfCookie = null;
    }

    /// <summary>
    /// Test login with hardcoded credentials
    /// Attach this script to a GameObject and press Play to test
    /// </summary>
    public void TestLogin()
    {
        Debug.Log("=== Backend Test Started ===");
        Login("testplayer2", "testpass123");
    }

    /// <summary>
    /// Login as Player 1 (hardcoded credentials)
    /// </summary>
    public void LoginAsPlayer1()
    {
        Debug.Log("=== Logging in as Player 1 ===");
        Login("testplayer", "testpass123");
    }

    /// <summary>
    /// Login as Player 2 (hardcoded credentials)
    /// </summary>
    public void LoginAsPlayer2()
    {
        Debug.Log("=== Logging in as Player 2 ===");
        Login("testplayer2", "testpass123");
    }

    /// <summary>
    /// Save the selected deck for the player
    /// </summary>
    public void SaveSelectedDeck(List<string> cardIds, string guardianId, string legendStoneId)
    {
        StartCoroutine(SaveSelectedDeckCoroutine(cardIds, guardianId, legendStoneId));
    }

    private IEnumerator SaveSelectedDeckCoroutine(List<string> cardIds, string guardianId, string legendStoneId)
    {
        var deckData = new SaveDeckRequest
        {
            deck_name = "Updated Deck",
            is_active = true,
            card_ids = cardIds.ToArray(),
            guardian_id = guardianId,
            legend_stone_id = legendStoneId
        };

        string jsonBody = JsonUtility.ToJson(deckData);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(
            $"{backendUrl}/api/players/save-deck/",
            UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            // Add CSRF token as header (required for POST requests with Django CSRF protection)
            if (!string.IsNullOrEmpty(csrfToken))
            {
                request.SetRequestHeader("X-CSRFToken", csrfToken);
            }
            else
            {
                Debug.LogWarning($"[SAVE-DECK] ✗ No CSRF token available!");
            }

            // Build cookie header with BOTH sessionid AND csrftoken cookies
            // Django validates that the CSRF token in the header matches the CSRF cookie
            List<string> cookies = new List<string>();

            if (!string.IsNullOrEmpty(sessionId))
            {
                cookies.Add($"sessionid={sessionId}");
                Debug.Log($"[SAVE-DECK] ✓ Adding sessionid cookie");
            }
            else
            {
                Debug.LogWarning($"[SAVE-DECK] ✗ No session ID available!");
            }

            if (!string.IsNullOrEmpty(csrfCookie))
            {
                cookies.Add($"csrftoken={csrfCookie}");
                Debug.Log($"[SAVE-DECK] ✓ Adding csrftoken cookie: {csrfCookie}");
            }
            else
            {
                Debug.LogWarning($"[SAVE-DECK] ⚠ No CSRF cookie available!");
            }

            if (cookies.Count > 0)
            {
                string cookieHeader = string.Join("; ", cookies);
                request.SetRequestHeader("Cookie", cookieHeader);
                Debug.Log($"[SAVE-DECK] ✓ Cookie header: {cookieHeader}");
            }

            Debug.Log($"[SAVE-DECK] Saving deck with {cardIds.Count} cards, guardian={guardianId}, legend_stone={legendStoneId}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Debug.Log($"[SAVE-DECK] ✓ Status: {request.responseCode}");
                Debug.Log($"[SAVE-DECK] Response: {responseText}");

                try
                {
                    var response = JsonUtility.FromJson<SaveDeckResponse>(responseText);
                    if (response.success)
                    {
                        Debug.Log("[SAVE-DECK] ✅ SUCCESS! Deck saved!");
                        Debug.Log($"[SAVE-DECK] Deck ID: {response.deck.id}");
                        Debug.Log($"[SAVE-DECK] Deck Name: {response.deck.deck_name}");
                    }
                    else
                    {
                        Debug.LogError($"[SAVE-DECK] ❌ Failed: {response.message}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[SAVE-DECK] ❌ Parse error: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[SAVE-DECK] ❌ Request failed: {request.error} (Status: {request.responseCode})");
                if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                {
                    Debug.LogError($"[SAVE-DECK] Response: {request.downloadHandler.text}");
                }
            }
        }
    }
}

// ==================== Data Classes ====================

[System.Serializable]
public class LoginRequest
{
    public string username;
    public string password;
}

[System.Serializable]
public class LoginResponse
{
    public bool success;
    public string message;
    public PlayerData user;
    public string session_id;
    public string csrf_token;
}

[System.Serializable]
public class PlayerData
{
    public string id;
    public string username;
    public int level;
    public int rank;
    public int coins;
    public int total_wins;
    public int total_losses;
    public float skill_rating;
    public PlayerCard[] all_cards;
    public SavedDeck[] decks;
}

[System.Serializable]
public class SavedDeck
{
    public string id;
    public string deck_name;
    public bool is_active;
    public PlayerCardWithDeck card_slot_1;
    public PlayerCardWithDeck card_slot_2;
    public PlayerCardWithDeck card_slot_3;
    public PlayerCardWithDeck card_slot_4;
    public PlayerCardWithDeck card_slot_5;
    public PlayerCardWithDeck card_slot_6;
    public PlayerCardWithDeck card_slot_7;
    public PlayerCardWithDeck card_slot_8;
    public PlayerCardWithDeck legend_stone;
    public PlayerCardWithDeck guardian_card;
    public string created_at;
    public string updated_at;
}

[System.Serializable]
public class PlayerCardWithDeck
{
    public string id;
    public int level;
    public int copies;
    public BackendCardData card;
}

[System.Serializable]
public class PlayerCard
{
    public string id;
    public int level;
    public int copies;
    public BackendCardData card;
    public string acquired_at;
}

[System.Serializable]
public class BackendCardData
{
    public string id;
    public string name;
    public string description;
    public string registry_name;  // Use this to find CardData/LegendStoneData/CastingLegendData
    public string card_type;  // spell, unit, guardian, legend
    public string rarity;     // common, rare, epic, legendary
    public int max_level;
}

[System.Serializable]
public class SaveDeckRequest
{
    public string deck_name;
    public bool is_active;
    public string[] card_ids;
    public string guardian_id;
    public string legend_stone_id;
}

[System.Serializable]
public class SaveDeckResponse
{
    public bool success;
    public string message;
    public SavedDeck deck;
}
