using UnityEngine;
using System;
using System.Collections.Generic;
using static DeckPayload;

/// <summary>
/// Centralized player state management.
/// Holds all player data retrieved during login: cards, decks, gems, etc.
/// Acts as single source of truth for player information.
/// </summary>
public class PlayerStates : MonoBehaviour
{
    public static PlayerStates Instance { get; private set; }

    // ========== PLAYER PROFILE ==========
    public string PlayFabId { get; set; }
    public string DisplayName { get; set; }
    public DateTime? CreatedTime { get; set; }

    // ========== CARD OWNERSHIP ==========
    public PlayerCardCollection CardCollection { get; set; } = new PlayerCardCollection();

    // ========== DECKS ==========
    public Dictionary<string, PlayFabBackendManager.SavedDeck> AllDecks { get; set; } = new Dictionary<string, PlayFabBackendManager.SavedDeck>();
    public PlayFabBackendManager.SavedDeck? ActiveDeck { get; set; }

    // ========== CURRENCY & RESOURCES ==========
    public int Gems { get; set; } = 0;
    public int Gold { get; set; } = 0;

    // ========== EVENTS ==========
    public event Action OnPlayerDataLoaded;
    public event Action OnCardCollectionLoaded;
    public event Action<PlayFabBackendManager.SavedDeck> OnActiveDeckLoaded;
    public event Action OnDecksLoaded;

    // ========== STATE FLAGS ==========
    private bool _isLoginComplete = false;
    private bool _cardsLoaded = false;
    private bool _decksLoaded = false;
    private bool _activeDeckLoaded = false;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[PlayerStates] Initialized");
    }

    /// <summary>
    /// Check if login sequence is completely finished
    /// </summary>
    public bool IsLoginComplete()
    {
        return _isLoginComplete;
    }

    /// <summary>
    /// Check if specific player data is loaded
    /// </summary>
    public bool AreCardsLoaded() => _cardsLoaded;
    public bool AreDecksLoaded() => _decksLoaded;
    public bool IsActiveDeckLoaded() => _activeDeckLoaded;

    /// <summary>
    /// Called by PlayFabBackendManager after login to initialize player profile
    /// </summary>
    public void SetPlayerProfile(string playfabId, string displayName, DateTime? createdTime)
    {
        PlayFabId = playfabId;
        DisplayName = displayName;
        CreatedTime = createdTime;

        Debug.Log($"[PlayerStates] Profile set: {DisplayName} ({PlayFabId})");
    }

    /// <summary>
    /// Called by PlayFabBackendManager after loading card collection
    /// </summary>
    public void SetCardCollection(PlayerCardCollection collection)
    {
        CardCollection = collection ?? new PlayerCardCollection();
        _cardsLoaded = true;

        Debug.Log($"[PlayerStates] Card collection loaded: {CardCollection.cards.Count} cards");
        OnCardCollectionLoaded?.Invoke();

        CheckLoginComplete();
    }

    /// <summary>
    /// Called by PlayFabBackendManager after loading all decks
    /// </summary>
    public void SetAllDecks(Dictionary<string, PlayFabBackendManager.SavedDeck> decks)
    {
        AllDecks = decks ?? new Dictionary<string, PlayFabBackendManager.SavedDeck>();
        _decksLoaded = true;

        Debug.Log($"[PlayerStates] All decks loaded: {AllDecks.Count} decks");
        OnDecksLoaded?.Invoke();

        CheckLoginComplete();
    }

    /// <summary>
    /// Called by PlayFabBackendManager after loading active deck
    /// </summary>
    public void SetActiveDeck(PlayFabBackendManager.SavedDeck? deck)
    {
        ActiveDeck = deck;
        _activeDeckLoaded = true;

        if (deck != null)
        {
            Debug.Log($"[PlayerStates] Active deck loaded: {deck?.DeckName} with {deck?.Cards.Count} cards");
            OnActiveDeckLoaded?.Invoke(deck.Value);
        }
        else
        {
            Debug.Log("[PlayerStates] No active deck found");
        }

        CheckLoginComplete();
    }

    /// <summary>
    /// Called when active deck is saved (after deck submission)
    /// </summary>
    public void UpdateActiveDeck(string deckName, List<CardDataDto> cards)
    {
        ActiveDeck = new PlayFabBackendManager.SavedDeck
        {
            DeckName = deckName,
            Cards = cards,
            LastModified = DateTime.UtcNow
        };

        Debug.Log($"[PlayerStates] Active deck updated: {deckName} with {cards.Count} cards");
    }

    /// <summary>
    /// Check if all required data is loaded to mark login as complete
    /// </summary>
    private void CheckLoginComplete()
    {
        if (_cardsLoaded && _activeDeckLoaded)
        {
            _isLoginComplete = true;
            Debug.Log("[PlayerStates] === LOGIN COMPLETE ===");
            OnPlayerDataLoaded?.Invoke();
        }
    }

    /// <summary>
    /// Reset all player data on logout
    /// </summary>
    public void ResetPlayerData()
    {
        PlayFabId = null;
        DisplayName = null;
        CreatedTime = null;

        CardCollection = new PlayerCardCollection();
        AllDecks.Clear();
        ActiveDeck = null;

        Gems = 0;
        Gold = 0;

        _isLoginComplete = false;
        _cardsLoaded = false;
        _decksLoaded = false;
        _activeDeckLoaded = false;

        Debug.Log("[PlayerStates] Player data reset");
    }

    /// <summary>
    /// Get a specific card from collection by ID
    /// </summary>
    public PlayerCardOwnership? GetCard(string cardId)
    {
        return CardCollection.cards.Find(c => c.cardId == cardId);
    }

    /// <summary>
    /// Check if player owns a specific card
    /// </summary>
    public bool OwnsCard(string cardId)
    {
        return CardCollection.OwnsCard(cardId);
    }

    /// <summary>
    /// Get the level of a card player owns
    /// </summary>
    public int GetCardLevel(string cardId)
    {
        return CardCollection.GetCardLevel(cardId);
    }

    /// <summary>
    /// Get a specific deck by name
    /// </summary>
    public PlayFabBackendManager.SavedDeck? GetDeck(string deckName)
    {
        if (AllDecks.TryGetValue(deckName, out var deck))
        {
            return deck;
        }
        return null;
    }

    /// <summary>
    /// Get all card IDs from active deck
    /// </summary>
    public List<CardDataDto> GetActiveDeckCards()
    {
        if (ActiveDeck != null)
        {
            return ActiveDeck.Value.Cards;
        }
        return new List<CardDataDto>();
    }
}
