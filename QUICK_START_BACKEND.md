# Quick Start Guide - Backend API

## 🚀 Start Backend Server

```powershell
cd C:\Users\ahmad\GuardiansBackend
python manage.py runserver
```

Server runs at: `http://localhost:8000`

---

## 🔑 Test Credentials

```
Username: testplayer
Password: testpass123
```

---

## 📋 API Endpoints

### 1. Login (Public)
```
POST /api/players/login/
Content-Type: application/json

{
  "username": "testplayer",
  "password": "testpass123"
}
```

**Returns:** Full player data including decks and all cards

---

### 2. Get My Cards (Authenticated)
```
GET /api/players/my_cards/
```

**Returns:** Array of 8 player cards with levels and copies

---

### 3. Get My Decks (Authenticated)
```
GET /api/players/my_decks/
```

**Returns:** Array of player decks with all 10 card slots populated

---

### 4. Get Current Player (Authenticated)
```
GET /api/players/me/
```

**Returns:** Full player profile

---

## 🎮 Game Cards in Database

| Type | Card Name | Rarity | Max Level |
|------|-----------|--------|-----------|
| Spell | Toxin Ball | Rare | 3 |
| Spell | Vanish Ball | Rare | 2 |
| Spell | Lightning Ball | Epic | 3 |
| Unit | Brave Knight | Epic | 5 |
| Unit | Angry Bear | Epic | 4 |
| Guardian | Boulder Guardian | Legendary | 3 |
| Guardian | Xod Guardian | Legendary | 3 |
| Legend | Stone Legend | Legendary | 2 |

---

## 🔗 Unity Integration Files

### BackendManager.cs
```csharp
// Singleton for all API communication
// Add to a persistent GameObject in MainMenu

// Usage:
BackendManager.Instance.Login("username", "password");
BackendManager.OnLoginSuccess += (playerData) => { /* handle login */ };

// Access data:
PlayerData data = BackendManager.Instance.GetCachedPlayerData();
List<SavedDeck> decks = BackendManager.Instance.GetAllDecks();
List<PlayerCard> cards = BackendManager.Instance.GetAllCards();
```

### SelectionUIBackendIntegration.cs
```csharp
// Attach to SelectionUI Canvas
// Automatically populates from backend data

// Subscribe to login event and UI will auto-populate
// Features:
// - Display player stats
// - Show deck selector
// - Display card collection grouped by type
// - Show selected deck cards
// - Color-code cards by rarity
```

---

## 📁 Backend Project Structure

```
C:\Users\ahmad\GuardiansBackend\
├── manage.py                          # Django command runner
├── db.sqlite3                         # Database (auto-created)
├── requirements.txt                   # Python dependencies
├── guardians_backend/
│   ├── settings.py                    # Django config
│   ├── urls.py                        # API routes
│   └── wsgi.py                        # Production settings
└── players/
    ├── models.py                      # Database models
    ├── serializers.py                 # JSON serializers
    ├── views.py                       # API endpoints
    ├── admin.py                       # Admin panel
    └── management/commands/
        ├── seed_cards.py              # Populate game cards
        └── seed_sample_player.py      # Create test player
```

---

## 🔧 Management Commands

### Seed Game Cards
```bash
python manage.py seed_cards
# Creates 8 game cards in database
```

### Create Test Player
```bash
python manage.py seed_sample_player
# Creates testplayer with full deck and all cards
```

### Run Migrations
```bash
python manage.py migrate --noinput
# Creates all database tables
```

### Create Admin User
```bash
python manage.py createsuperuser
# Then access at http://localhost:8000/admin/
```

---

## 🔌 Database Schema (4 Tables)

### players (CustomUser)
- id (UUID)
- username, email, password
- level, rank, coins
- total_wins, total_losses
- skill_rating (Elo-like system)

### cards
- id (string: "toxin_ball", etc.)
- name, description
- card_type (spell|unit|guardian|legend)
- rarity (common|rare|epic|legendary)
- max_level (1-5)

### player_cards
- id (UUID)
- player_id (FK)
- card_id (FK)
- level (1 to max_level)
- copies (quantity owned)

### saved_decks
- id (UUID)
- player_id (FK)
- deck_name
- card_slot_1 through card_slot_8 (FK to player_cards)
- legend_stone (FK to player_cards)
- guardian_card (FK to player_cards)

---

## 🧪 Test with Browser

1. Open: `http://localhost:8000/api/players/`
   - Shows all available endpoints

2. Visit: `http://localhost:8000/admin/`
   - Admin panel (after creating admin user)

3. Try: `http://localhost:8000/api/players/login/` (from Postman)
   - Send POST with username/password

---

## ⚡ Quick Restart Steps

**If server crashes or won't start:**

```bash
# 1. Go to backend folder
cd C:\Users\ahmad\GuardiansBackend

# 2. Recreate migrations if needed
python manage.py makemigrations players

# 3. Run migrations
python manage.py migrate --noinput

# 4. Reseed data if database lost
python manage.py seed_cards
python manage.py seed_sample_player

# 5. Start server
python manage.py runserver
```

---

## 🐛 Debugging

**Check if port 8000 is available:**
```powershell
Get-NetTCPConnection -LocalPort 8000
```

**View server logs:**
- Terminal where `python manage.py runserver` is running

**Common issues:**
- Server won't start → Check Python/Django installation
- Login fails → Check credentials (testplayer/testpass123)
- No cards → Run `python manage.py seed_cards`
- Database locked → Delete `db.sqlite3` and remigrate

---

## 📱 Production Deployment

**When ready to deploy:**
1. Switch database to PostgreSQL
2. Set `DEBUG=False` in settings.py
3. Configure `ALLOWED_HOSTS`
4. Use production WSGI server (Gunicorn)
5. Set up SSL/HTTPS
6. Deploy to cloud (AWS, Heroku, DigitalOcean, etc.)

---

## 📊 Response Example

**Login Response Structure:**
```json
{
  "success": true,
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "username": "testplayer",
    "level": 1,
    "rank": 1,
    "coins": 1000,
    "total_wins": 0,
    "total_losses": 0,
    "skill_rating": 1200,
    "decks": [
      {
        "id": "uuid",
        "deck_name": "My First Deck",
        "is_active": true,
        "card_slot_1": {
          "id": "uuid",
          "level": 1,
          "copies": 3,
          "card": {
            "id": "toxin_ball",
            "name": "Toxin Ball",
            "card_type": "spell",
            "rarity": "rare"
          }
        }
        // ... 9 more slots ...
      }
    ],
    "all_cards": [
      // ... 8 cards total ...
    ]
  }
}
```

---

## ✅ Backend Status

- **Server:** Running ✅
- **Database:** Connected ✅
- **Cards:** 8 seeded ✅
- **Test Player:** Created ✅
- **API Endpoints:** 4 live ✅
- **CORS:** Enabled ✅

**Ready for Unity integration!**

---

**Questions?** Check `BACKEND_API_INTEGRATION.md` for detailed documentation.
