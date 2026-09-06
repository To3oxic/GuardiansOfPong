# How to Add a New Card to the Backend

## Quick Overview

Adding a card is a **3-step process**:
1. Add card definition to `seed_cards.py`
2. Run the seed command
3. (Optional) Add to test player in `seed_sample_player.py`

---

## Step 1: Add Card Definition to seed_cards.py

**File:** `C:\Users\ahmad\GuardiansBackend\players\management\commands\seed_cards.py`

**Find this section** (around line 10):
```python
cards_data = [
    # Ball Abilities (Spell Cards)
    {
        'id': 'toxin_ball',
        'name': 'Toxin Ball',
        # ...existing cards...
    },
```

**Add your new card BEFORE the closing bracket** `]`:
```python
            # Your new card here
            {
                'id': 'card_id_here',                    # lowercase with underscores
                'name': 'Display Name',                   # What players see
                'description': 'What it does',            # Flavor text
                'card_type': 'spell',                     # spell|unit|guardian|legend
                'rarity': 'epic',                         # common|rare|epic|legendary
                'max_level': 3                            # 1-5
            },
        ]  # ← closing bracket
```

### Example - Adding "Fire Wizard" Card

**Before:**
```python
            {
                'id': 'tree',
                'name': 'Tree',
                'description': 'Ancient tree with support abilities',
                'card_type': 'unit',
                'rarity': 'rare',
                'max_level': 3
            },
        ]
```

**After:**
```python
            {
                'id': 'tree',
                'name': 'Tree',
                'description': 'Ancient tree with support abilities',
                'card_type': 'unit',
                'rarity': 'rare',
                'max_level': 3
            },
            {
                'id': 'fire_wizard',
                'name': 'Fire Wizard',
                'description': 'Mage with powerful fire spells',
                'card_type': 'unit',
                'rarity': 'epic',
                'max_level': 5
            },
        ]
```

---

## Step 2: Run Seed Command

Open a terminal in the backend folder:

```bash
cd C:\Users\ahmad\GuardiansBackend
python manage.py seed_cards
```

**Expected output:**
```
 Created card: Fire Wizard
 Seeding complete! 1 new cards added.
```

**Or if it already exists:**
```
 Card already exists: Fire Wizard
 Seeding complete! 0 new cards added.
```

---

## Step 3: (Optional) Add to Test Player

If you want the test player to have this card, edit `seed_sample_player.py`

**File:** `C:\Users\ahmad\GuardiansBackend\players\management\commands\seed_sample_player.py`

**Find this section** (around line 30):
```python
        cards_to_add = [
            ('toxin_ball', 1, 3),
            ('vanish_ball', 1, 2),
            # ...more cards...
            ('tree', 2, 1),
        ]
```

**Add your card to the list:**
```python
        cards_to_add = [
            ('toxin_ball', 1, 3),
            ('vanish_ball', 1, 2),
            # ...more cards...
            ('tree', 2, 1),
            ('fire_wizard', 3, 2),          # ← Add this line
        ]
```

**Format:** `('card_id', level, copies)`
- `card_id`: Must match the 'id' from seed_cards.py
- `level`: Starting level (1 to max_level)
- `copies`: How many copies player gets

**Then run:**
```bash
python manage.py seed_sample_player
```

---

## Card Fields Explained

### 'id' (Identifier)
- Lowercase letters and underscores only
- No spaces, no special characters
- Used in database and code
- Examples: `fire_wizard`, `ice_tower`, `holy_knight`

### 'name' (Display Name)
- What shows in the UI
- Can have spaces and capitals
- Examples: `Fire Wizard`, `Ice Tower`, `Holy Knight`

### 'description' (Flavor Text)
- Brief explanation of what card does
- 100 characters max recommended
- Examples:
  - `Mage with powerful fire spells`
  - `Freezes enemies in place`
  - `Divine warrior with healing powers`

### 'card_type' (Category)
| Type | Purpose | Max Per Deck |
|------|---------|-------------|
| `spell` | Instant effects on ball | 8 slots |
| `unit` | Creatures that spawn on board | 8 slots |
| `guardian` | Player's main defender | 1 slot (special) |
| `legend` | Powerful stone ability | 1 slot (special) |

### 'rarity' (Progression Tier)
| Rarity | Color | Typical Level |
|--------|-------|---------------|
| `common` | Gray | 1-2 |
| `rare` | Blue | 2-3 |
| `epic` | Purple/Magenta | 3-4 |
| `legendary` | Gold/Yellow | 4-5 |

### 'max_level' (Level Cap)
- Minimum: 1
- Maximum: 5
- Common cards: 2-3
- Epic cards: 4-5
- Legendary cards: 3-5

---

## Complete Example: Adding "Frost Nova" Spell

### Step 1: Add to seed_cards.py

Find the spells section:
```python
            # Ball Abilities (Spell Cards)
            {
                'id': 'toxin_ball',
                'name': 'Toxin Ball',
                'description': 'Spawns a ball that applies damage ticks',
                'card_type': 'spell',
                'rarity': 'rare',
                'max_level': 3
            },
            {
                'id': 'vanish_ball',
                'name': 'Vanish Ball',
                'description': 'Temporarily removes ball visibility',
                'card_type': 'spell',
                'rarity': 'rare',
                'max_level': 2
            },
            {
                'id': 'lightning_ball',
                'name': 'Lightning Ball',
                'description': 'Zigzag movement pattern with instant kill',
                'card_type': 'spell',
                'rarity': 'epic',
                'max_level': 3
            },
            # ADD HERE:
            {
                'id': 'frost_nova',
                'name': 'Frost Nova',
                'description': 'Freezes ball in place for 3 seconds',
                'card_type': 'spell',
                'rarity': 'epic',
                'max_level': 3
            },
```

### Step 2: Run command
```bash
python manage.py seed_cards
```

### Step 3: Verify it works
Go to: `http://localhost:8000/api/players/my_cards/`

You should see `frost_nova` in the response (if logged in as testplayer)

---

## Adding Multiple Cards at Once

You can add multiple cards in one go:

```python
            # New batch of cards
            {
                'id': 'ice_tower',
                'name': 'Ice Tower',
                'description': 'Stationary turret that shoots ice',
                'card_type': 'guardian',
                'rarity': 'epic',
                'max_level': 4
            },
            {
                'id': 'phoenix',
                'name': 'Phoenix',
                'description': 'Legendary bird that resurrects',
                'card_type': 'unit',
                'rarity': 'legendary',
                'max_level': 5
            },
            {
                'id': 'shadow_clone',
                'name': 'Shadow Clone',
                'description': 'Creates a duplicate of the ball',
                'card_type': 'spell',
                'rarity': 'rare',
                'max_level': 2
            },
```

Then run:
```bash
python manage.py seed_cards
```

All 3 cards are created in one command!

---

## What Happens Behind the Scenes

When you run `python manage.py seed_cards`:

```
1. Django reads the cards_data list
   ↓
2. For each card definition:
   ├─ Check if card with that 'id' already exists in database
   ├─ If NOT exists: Create new Card record
   ├─ If EXISTS: Skip (idempotent - safe to run multiple times)
   └─ Print message showing what was created
   ↓
3. Card is now in database and can be:
   ├─ Assigned to players
   ├─ Added to decks
   ├─ Retrieved via API
   └─ Used in game
```

---

## Database Query (Behind the Scenes)

What Django does:

```python
# Equivalent to SQL:
# INSERT INTO cards (id, name, description, card_type, rarity, max_level)
# VALUES ('frost_nova', 'Frost Nova', '...', 'spell', 'epic', 3)

Card.objects.get_or_create(
    id='frost_nova',
    defaults={
        'name': 'Frost Nova',
        'description': 'Freezes ball in place for 3 seconds',
        'card_type': 'spell',
        'rarity': 'epic',
        'max_level': 3,
    }
)
```

---

## Testing Your New Card

### Method 1: Via API

Login first:
```bash
curl -X POST http://localhost:8000/api/players/login/ \
  -H "Content-Type: application/json" \
  -d '{"username":"testplayer","password":"testpass123"}'
```

Then check cards:
```bash
curl -X GET http://localhost:8000/api/players/my_cards/ \
  -H "Cookie: sessionid=<session_id>"
```

Look for your new card in the response!

### Method 2: Django Admin

1. Go to: `http://localhost:8000/admin/`
2. Login with admin credentials (or create one with `python manage.py createsuperuser`)
3. Click "Cards" in the left panel
4. Your new card should be listed

### Method 3: Django Shell

```bash
python manage.py shell
```

Then in Python:
```python
from players.models import Card

# Check if card exists
frost_nova = Card.objects.get(id='frost_nova')
print(frost_nova.name)  # Output: Frost Nova
print(frost_nova.rarity)  # Output: epic
```

---

## Common Mistakes

### ❌ Mistake 1: Using spaces in 'id'
```python
'id': 'frost nova',  # WRONG - spaces not allowed
```
**Fix:**
```python
'id': 'frost_nova',  # CORRECT - underscores only
```

### ❌ Mistake 2: Wrong card_type
```python
'card_type': 'hero',  # WRONG - only spell|unit|guardian|legend
```
**Fix:**
```python
'card_type': 'unit',  # CORRECT
```

### ❌ Mistake 3: max_level out of range
```python
'max_level': 10,  # WRONG - should be 1-5
```
**Fix:**
```python
'max_level': 4,  # CORRECT
```

### ❌ Mistake 4: Not saving the file
After editing seed_cards.py, must save it before running command!

### ❌ Mistake 5: Using same 'id' twice
```python
{
    'id': 'frost_nova',
    'name': 'Frost Nova',
    # ...
},
{
    'id': 'frost_nova',  # WRONG - duplicate!
    'name': 'Frost Nova 2',
    # ...
},
```
**Fix:** Use unique ids

---

## Checklist: Adding a New Card

- [ ] Decide on card properties (name, type, rarity, level)
- [ ] Open `seed_cards.py`
- [ ] Find where to add it (with similar cards)
- [ ] Create card definition with:
  - [ ] Unique lowercase 'id' with underscores
  - [ ] Pretty 'name' with capitals
  - [ ] Brief 'description'
  - [ ] Valid 'card_type' (spell/unit/guardian/legend)
  - [ ] Valid 'rarity' (common/rare/epic/legendary)
  - [ ] 'max_level' between 1-5
- [ ] Save the file
- [ ] Run: `python manage.py seed_cards`
- [ ] Check output shows card created
- [ ] (Optional) Add to test player in `seed_sample_player.py`
- [ ] (Optional) Run: `python manage.py seed_sample_player`
- [ ] Test via API or admin panel

---

## Quick Reference

**Card Definition Template:**
```python
{
    'id': 'card_id_lowercase',
    'name': 'Card Name Display',
    'description': 'What it does',
    'card_type': 'spell|unit|guardian|legend',
    'rarity': 'common|rare|epic|legendary',
    'max_level': 1-5
},
```

**Where to add:**
- Spells: After `lightning_ball`
- Units: After `ancient_spirit`
- Guardians: After `xod_guardian`
- Legends: After `stone_legend`

**Commands:**
```bash
# Seed cards
python manage.py seed_cards

# Seed test player with cards
python manage.py seed_sample_player

# Django admin
python manage.py createsuperuser
http://localhost:8000/admin/

# Django shell
python manage.py shell
```

---

## Next: How the Card Is Used

Once added to database:

1. **UI displays it**
   - Shows in player's collection
   - Shows in deck builder
   - Color-coded by rarity

2. **Game mechanics use it**
   - Player can drag it to activate
   - Game checks card_type for effect
   - Applies card effects (damage, spawn, etc)
   - Updates player stats on game end

3. **Database tracks it**
   - Records player's level of card
   - Records how many copies they have
   - Saves in their decks
   - Updates on progression

---

That's it! Adding cards is simple: define → seed → done! 🎮
