# Backend API Integration Guide

## ✅ Backend Status: LIVE & READY

**Database:** SQLite3 with 4 tables (players, cards, player_cards, saved_decks)  
**Server:** Django REST Framework running at `http://localhost:8000`  
**Test Credentials:** `testplayer` / `testpass123`

---

## API Endpoints

### 1. **Login Endpoint** (Public)
```
POST /api/players/login/
```

**Request:**
```json
{
  "username": "testplayer",
  "password": "testpass123"
}
```

**Response Structure:**
```json
{
  "success": true,
  "user": {
    "id": "UUID",
    "username": "testplayer",
    "level": 1,
    "rank": 1,
    "coins": 1000,
    "total_wins": 0,
    "total_losses": 0,
    "skill_rating": 1200,
    "decks": [
      {
        "id": "UUID",
        "deck_name": "My First Deck",
        "is_active": true,
        "card_slot_1": {
          "id": "UUID",
          "level": 1,
          "copies": 3,
          "card": {
            "id": "toxin_ball",
            "name": "Toxin Ball",
            "description": "Spawns a ball that applies damage ticks",
            "card_type": "spell",
            "rarity": "rare",
            "max_level": 3
          }
        },
        "card_slot_2": { /* ... similar structure ... */ },
        "card_slot_3": { /* ... */ },
        "card_slot_4": { /* ... */ },
        "card_slot_5": { /* ... */ },
        "card_slot_6": { /* ... */ },
        "card_slot_7": { /* ... */ },
        "card_slot_8": { /* ... */ },
        "legend_stone": {
          "id": "UUID",
          "level": 1,
          "copies": 1,
          "card": {
            "id": "stone_legend",
            "name": "Stone Legend",
            "description": "Legendary stone with powerful effects",
            "card_type": "legend",
            "rarity": "legendary",
            "max_level": 2
          }
        },
        "guardian_card": {
          "id": "UUID",
          "level": 1,
          "copies": 1,
          "card": {
            "id": "xod_guardian",
            "name": "Xod Guardian",
            "description": "Unique guardian that spawns units every 10 seconds",
            "card_type": "guardian",
            "rarity": "legendary",
            "max_level": 3
          }
        }
      }
    ],
    "all_cards": [
      {
        "id": "UUID",
        "level": 1,
        "copies": 3,
        "card": {
          "id": "toxin_ball",
          "name": "Toxin Ball",
          "description": "Spawns a ball that applies damage ticks",
          "card_type": "spell",
          "rarity": "rare",
          "max_level": 3
        }
      },
      {
        "id": "UUID",
        "level": 1,
        "copies": 2,
        "card": {
          "id": "vanish_ball",
          "name": "Vanish Ball",
          "description": "Temporarily removes ball visibility",
          "card_type": "spell",
          "rarity": "rare",
          "max_level": 2
        }
      }
      // ... 6 more cards ...
    ]
  }
}
```

### 2. **Get Current Player** (Authenticated)
```
GET /api/players/me/
```
**Headers:**
```
Authorization: Cookie (Django session)
```

**Response:** Same as user object from login response

### 3. **Get Player Decks** (Authenticated)
```
GET /api/players/my_decks/
```

**Response:**
```json
[
  {
    "id": "UUID",
    "deck_name": "My First Deck",
    "is_active": true,
    "card_slot_1": { /* PlayerCard with nested Card */ },
    "card_slot_2": { /* ... */ },
    // ... all 10 slots ...
    "created_at": "2025-01-15T10:30:00Z",
    "updated_at": "2025-01-15T10:30:00Z"
  }
]
```

### 4. **Get Player Cards** (Authenticated)
```
GET /api/players/my_cards/
```

**Response:**
```json
[
  {
    "id": "UUID",
    "level": 3,
    "copies": 2,
    "card": {
      "id": "brave_knight",
      "name": "Brave Knight",
      "description": "Mobile unit with melee attack",
      "card_type": "unit",
      "rarity": "epic",
      "max_level": 5
    },
    "acquired_at": "2025-01-15T10:30:00Z"
  },
  // ... more cards ...
]
```

---

## Database Schema

### Table: players (CustomUser)
```
id (UUID) - Primary Key
username (String, Unique)
email (String)
password_hash (String)
level (Integer) - Default: 1
rank (Integer) - Default: 1
coins (Integer) - Default: 1000
total_wins (Integer) - Default: 0
total_losses (Integer) - Default: 0
skill_rating (Float) - Default: 1200
created_at (DateTime)
updated_at (DateTime)
```

### Table: cards (Master Card List)
```
id (String, Primary Key) - e.g., "toxin_ball"
name (String)
description (String)
card_type (String) - Choices: spell, unit, guardian, legend
rarity (String) - Choices: common, rare, epic, legendary
max_level (Integer) - 1-5
```

### Table: player_cards (Card Collection)
```
id (UUID) - Primary Key
player_id (UUID) - FK to players
card_id (String) - FK to cards
level (Integer) - 1 to card.max_level
copies (Integer) - Quantity owned
acquired_at (DateTime)
```

### Table: saved_decks
```
id (UUID) - Primary Key
player_id (UUID) - FK to players
deck_name (String)
is_active (Boolean)
card_slot_1 to card_slot_8 (UUID) - FK to player_cards
legend_stone (UUID) - FK to player_cards
guardian_card (UUID) - FK to player_cards
created_at (DateTime)
updated_at (DateTime)
```

---

## Game Cards in Database

### Spell Cards (3)
| ID | Name | Rarity | Max Level |
|---|---|---|---|
| toxin_ball | Toxin Ball | Rare | 3 |
| vanish_ball | Vanish Ball | Rare | 2 |
| lightning_ball | Lightning Ball | Epic | 3 |

### Unit Cards (2)
| ID | Name | Rarity | Max Level |
|---|---|---|---|
| brave_knight | Brave Knight | Epic | 5 |
| angry_bear | Angry Bear | Epic | 4 |

### Guardian Cards (2)
| ID | Name | Rarity | Max Level |
|---|---|---|---|
| boulder_guardian | Boulder Guardian | Legendary | 3 |
| xod_guardian | Xod Guardian | Legendary | 3 |

### Legend Cards (1)
| ID | Name | Rarity | Max Level |
|---|---|---|---|
| stone_legend | Stone Legend | Legendary | 2 |

---

## Unity C# Integration

### Step 1: Login and Cache Response
```csharp
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

[System.Serializable]
public class LoginRequest
{
    public string username;
    public string password;
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
}

[System.Serializable]
public class PlayerCardWithDeck
{
    public string id;
    public int level;
    public int copies;
    public CardData card;
}

[System.Serializable]
public class PlayerCard
{
    public string id;
    public int level;
    public int copies;
    public CardData card;
}

[System.Serializable]
public class CardData
{
    public string id;
    public string name;
    public string description;
    public string card_type;
    public string rarity;
    public int max_level;
}

public class BackendManager : MonoBehaviour
{
    private string apiUrl = "http://localhost:8000";
    private PlayerData cachedPlayerData;

    public IEnumerator Login(string username, string password)
    {
        var loginRequest = new LoginRequest
        {
            username = username,
            password = password
        };

        string jsonBody = JsonUtility.ToJson(loginRequest);
        
        using (UnityWebRequest request = UnityWebRequest.Post(
            $"{apiUrl}/api/players/login/",
            "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                // Parse JSON response
                var response = JsonUtility.FromJson<LoginResponse>(responseText);
                cachedPlayerData = response.user;
                
                Debug.Log("Login successful!");
                yield return StartCoroutine(PopulateSelectionUI());
            }
            else
            {
                Debug.LogError($"Login failed: {request.error}");
            }
        }
    }

    private IEnumerator PopulateSelectionUI()
    {
        if (cachedPlayerData == null)
            yield break;

        SelectionUI selectionUI = FindObjectOfType<SelectionUI>();
        
        // Populate deck selector
        foreach (var deck in cachedPlayerData.decks)
        {
            selectionUI.AddDeckOption(deck.deck_name, deck);
        }

        // Populate collection view
        foreach (var playerCard in cachedPlayerData.all_cards)
        {
            selectionUI.AddCardToCollection(
                playerCard.card.name,
                playerCard.card.card_type,
                playerCard.card.rarity,
                playerCard.level,
                playerCard.copies
            );
        }

        yield return null;
    }
}

[System.Serializable]
public class LoginResponse
{
    public bool success;
    public PlayerData user;
}
```

### Step 2: SelectionUI Updates
```csharp
public class SelectionUI : MonoBehaviour
{
    // ... existing code ...

    public void DisplayDeck(SavedDeck deck)
    {
        Debug.Log($"Displaying deck: {deck.deck_name}");
        
        // Clear existing cards
        cardDisplayPanel.Clear();
        
        // Add all 10 card slots
        var slots = new[]
        {
            deck.card_slot_1, deck.card_slot_2, deck.card_slot_3,
            deck.card_slot_4, deck.card_slot_5, deck.card_slot_6,
            deck.card_slot_7, deck.card_slot_8,
            deck.legend_stone, deck.guardian_card
        };

        foreach (var slot in slots)
        {
            if (slot != null && slot.card != null)
            {
                DisplayCardSlot(slot);
            }
        }
    }

    private void DisplayCardSlot(PlayerCardWithDeck playerCard)
    {
        GameObject cardUI = Instantiate(cardUIPrefab, cardDisplayPanel);
        
        // Populate card UI with data
        cardUI.GetComponentInChildren<Text>().text = 
            $"{playerCard.card.name} Lv{playerCard.level}";
        
        cardUI.GetComponent<Image>().color = GetRarityColor(playerCard.card.rarity);
        
        Debug.Log($"Card added: {playerCard.card.name} ({playerCard.card.rarity})");
    }

    private Color GetRarityColor(string rarity)
    {
        return rarity switch
        {
            "common" => Color.gray,
            "rare" => Color.blue,
            "epic" => Color.magenta,
            "legendary" => Color.yellow,
            _ => Color.white
        };
    }
}
```

---

## Testing the API

### Using Postman/cURL:

**1. Login:**
```bash
curl -X POST http://localhost:8000/api/players/login/ \
  -H "Content-Type: application/json" \
  -d '{"username":"testplayer","password":"testpass123"}'
```

**2. Get My Cards (requires session cookie from login):**
```bash
curl -X GET http://localhost:8000/api/players/my_cards/ \
  -H "Cookie: sessionid=<session_id_from_login>"
```

**3. Get My Decks:**
```bash
curl -X GET http://localhost:8000/api/players/my_decks/ \
  -H "Cookie: sessionid=<session_id_from_login>"
```

---

## Next Steps

1. ✅ Backend API is live and tested
2. ✅ Database populated with 8 game cards
3. ✅ Test player created with full deck
4. ⏳ Create Unity login UI that calls `/api/players/login/`
5. ⏳ Parse response and cache PlayerData
6. ⏳ Populate SelectionUI with decks and cards
7. ⏳ Implement deck selection and ready-up flow
8. ⏳ Create match queue endpoint for matchmaking
9. ⏳ Spawn headless servers and assign matches

---

## Server Status
- **Address:** `http://localhost:8000`
- **Database:** `C:\Users\ahmad\GuardiansBackend\db.sqlite3`
- **Project:** `C:\Users\ahmad\GuardiansBackend\`

**To restart the server:**
```bash
cd C:\Users\ahmad\GuardiansBackend
python manage.py runserver
```
