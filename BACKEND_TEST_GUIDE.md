# Backend Test - Quick Setup

## What This Does

A simple C# script that:
1. Logs in with hardcoded credentials (testplayer/testpass123)
2. Receives player data from backend
3. Displays everything in Debug logs

**Expected Output:**
```
✓ Login successful!
=== PLAYER DATA ===
Username: testplayer
ID: 550e8400-e29b-41d4-a716-446655440000
Level: 1
Rank: 1
Coins: 1000
Total Wins: 0
Total Losses: 0
Skill Rating: 1200

=== DECKS (1) ===
Deck 1: My First Deck (Active: True)

=== CARD COLLECTION (12 cards) ===
1. Toxin Ball (Lv1, x3) - rare spell
2. Vanish Ball (Lv1, x2) - rare spell
3. Lightning Ball (Lv2, x1) - epic spell
4. Brave Knight (Lv3, x2) - epic unit
5. Angry Bear (Lv2, x1) - epic unit
6. Forest Monster (Lv2, x2) - epic unit
7. Ancient Spirit (Lv3, x1) - epic unit
8. Stone Sentinel (Lv1, x2) - rare unit
9. Tree (Lv2, x1) - rare unit
10. Boulder Guardian (Lv1, x1) - legendary guardian
11. Xod Guardian (Lv1, x1) - legendary guardian
12. Stone Legend (Lv1, x1) - legendary legend

=== Backend Test Complete ===
```

---

## Step-by-Step Setup

### 1. Start Backend Server

Open terminal in backend folder:
```bash
cd C:\Users\ahmad\GuardiansBackend
python manage.py runserver
```

Wait for: `Starting development server at http://0.0.0.0:8000/`

### 2. Add Script to Unity

The script is already created at: `Assets/Scripts/BackendTest.cs`

### 3. Create Test GameObject

In your Unity scene:
1. Right-click in Hierarchy → Create Empty
2. Name it: "BackendTester"
3. Drag `BackendTest.cs` onto this GameObject
4. Save scene

### 4. Run Game

Press Play in Unity and check Console:
- Go to Window → General → Console (if not visible)
- You should see all the debug logs

---

## What Each Debug Log Shows

| Log | Meaning |
|-----|---------|
| `✓ Login successful!` | Connection worked, credentials valid |
| `Response received:` | Raw JSON from server |
| `Username: testplayer` | Player info loaded |
| `=== DECKS (1) ===` | Shows how many decks player has |
| `Deck 1: My First Deck` | Deck name and active status |
| `=== CARD COLLECTION (12 cards) ===` | All cards in player's collection |
| `1. Toxin Ball (Lv1, x3)...` | Card name, level, copies owned |

---

## Troubleshooting

### Error: "Unable to connect to the remote server"
```
Problem: Backend server not running
Solution: 
  1. Start server: python manage.py runserver
  2. Check it's running at http://localhost:8000
  3. Wait 5 seconds for startup
```

### Error: "Invalid credentials"
```
Problem: Test user doesn't exist
Solution:
  1. Run: python manage.py seed_sample_player
  2. Restart server
  3. Try again
```

### Error: "Failed to parse response"
```
Problem: Response format is wrong
Solution:
  1. Check browser: http://localhost:8000/api/players/login/
  2. Server might be down
  3. Check backend console for errors
```

### Shows: "No cards found!" or "No decks found!"
```
Problem: Test player has no data
Solution:
  1. Run: python manage.py seed_cards
  2. Run: python manage.py seed_sample_player
  3. Restart server
  4. Try again
```

---

## How to Read the Logs

### Card Collection Example
```
2. Vanish Ball (Lv1, x2) - rare spell
   ↑             ↑    ↑          ↑ ↑
   |             |    |          | └─ Card type
   |             |    |          └──── Rarity
   |             |    └───────────── How many copies
   |             └────────────────── Current level
   └──────────────────────────────── Card name + number
```

### Deck Info Example
```
Deck 1: My First Deck (Active: True)
        ↑             ↑            ↑
        |             |            └── Is this the selected deck?
        |             └───────────── Deck name
        └────────────────────────── Deck number
```

---

## Making It Your Own

Want to modify the test? Here are common changes:

### Change Server URL
```csharp
private string serverUrl = "http://localhost:8000";
// Change 8000 to different port if needed
```

### Change Credentials
```csharp
private string username = "testplayer";
private string password = "testpass123";
// Create another user and test with those credentials
```

### Add More Logging
Add this inside `DisplayPlayerData()`:
```csharp
// Show first deck's cards
if (player.decks != null && player.decks.Length > 0)
{
    Debug.Log("\n=== FIRST DECK CARDS ===");
    var firstDeck = player.decks[0];
    Debug.Log($"Deck: {firstDeck.deck_name}");
    // Note: Deck cards need additional API call to get slot details
}
```

### Test with Different User
Create new test user:
```bash
python manage.py createsuperuser
# OR manually in admin: http://localhost:8000/admin/
```

Then change credentials in script:
```csharp
private string username = "newusername";
private string password = "newpassword";
```

---

## What's Being Tested

✓ Network connectivity to backend
✓ Login endpoint working
✓ Authentication (session creation)
✓ JSON parsing
✓ Player data structure
✓ Deck storage
✓ Card collection

---

## Next Steps After Success

If all logs show correctly:
1. ✅ Backend is working
2. ✅ Database has data
3. ✅ API returns correct format
4. ✅ Unity networking works

Now you can:
- Integrate with MainMenu UI
- Create real login UI
- Build SelectionUI population
- Implement game flow

---

## Quick Reference

**Backend Commands:**
```bash
# Start server
python manage.py runserver

# Seed cards
python manage.py seed_cards

# Seed test player
python manage.py seed_sample_player

# Check admin panel
http://localhost:8000/admin/

# Check API directly
http://localhost:8000/api/players/
```

**Script Location:**
- `Assets/Scripts/BackendTest.cs`

**Test Credentials:**
- Username: `testplayer`
- Password: `testpass123`

**Success Indicators:**
- Console shows "✓ Login successful!"
- Shows 12 cards in collection
- Shows 1 deck named "My First Deck"
- Player stats show (Level 1, 1000 coins, etc)
