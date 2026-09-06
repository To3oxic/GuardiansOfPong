# Add Missing Cards to Database - Instructions for Copilot

## Task: Add 4 Missing Unit Cards to seed_cards.py

**Missing Cards:**
1. Forest Monster
2. Ancient Spirit
3. Stone Sentinel
4. Tree

---

## Step 1: Update seed_cards.py

**File Location:** `C:\Users\ahmad\GuardiansBackend\players\management\commands\seed_cards.py`

**What to do:**
Find this section in the file (around line 50):
```python
            # Legend Stone (Special Card - only 1 per deck)
            {
                'id': 'stone_legend',
                'name': 'Stone Legend',
                'description': 'Legendary stone with powerful effects',
                'card_type': 'legend',
                'rarity': 'legendary',
                'max_level': 2
            },
        ]
```

**Replace it with** (add the 4 new cards BEFORE the closing bracket):
```python
            # Legend Stone (Special Card - only 1 per deck)
            {
                'id': 'stone_legend',
                'name': 'Stone Legend',
                'description': 'Legendary stone with powerful effects',
                'card_type': 'legend',
                'rarity': 'legendary',
                'max_level': 2
            },

            # Additional Unit Cards
            {
                'id': 'forest_monster',
                'name': 'Forest Monster',
                'description': 'Creature from the forest with nature powers',
                'card_type': 'unit',
                'rarity': 'epic',
                'max_level': 4
            },
            {
                'id': 'ancient_spirit',
                'name': 'Ancient Spirit',
                'description': 'Ethereal being with magical abilities',
                'card_type': 'unit',
                'rarity': 'epic',
                'max_level': 5
            },
            {
                'id': 'stone_sentinel',
                'name': 'Stone Sentinel',
                'description': 'Stone guardian with defensive stance',
                'card_type': 'unit',
                'rarity': 'rare',
                'max_level': 3
            },
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

---

## Step 2: Run Seed Command

**In the GuardiansBackend terminal (where server is running):**

Open a NEW terminal window (don't stop the server) and run:

```bash
cd C:\Users\ahmad\GuardiansBackend
python manage.py seed_cards
```

**Expected Output:**
```
 Created card: Forest Monster
 Created card: Ancient Spirit
 Created card: Stone Sentinel
 Created card: Tree

 Seeding complete! 4 new cards added.
```

---

## Step 3: Update Test Player with New Cards

**File Location:** `C:\Users\ahmad\GuardiansBackend\players\management\commands\seed_sample_player.py`

**Find this section** (around line 30):
```python
        # Add cards to player
        cards_to_add = [
            ('toxin_ball', 1, 3),
            ('vanish_ball', 1, 2),
            ('lightning_ball', 2, 1),
            ('brave_knight', 3, 2),
            ('angry_bear', 2, 1),
            ('boulder_guardian', 1, 1),
            ('xod_guardian', 1, 1),
            ('stone_legend', 1, 1),
        ]
```

**Replace with** (add the 4 new cards):
```python
        # Add cards to player
        cards_to_add = [
            ('toxin_ball', 1, 3),
            ('vanish_ball', 1, 2),
            ('lightning_ball', 2, 1),
            ('brave_knight', 3, 2),
            ('angry_bear', 2, 1),
            ('boulder_guardian', 1, 1),
            ('xod_guardian', 1, 1),
            ('stone_legend', 1, 1),
            ('forest_monster', 2, 2),
            ('ancient_spirit', 3, 1),
            ('stone_sentinel', 1, 2),
            ('tree', 2, 1),
        ]
```

---

## Step 4: Reset Test Player (Optional but Recommended)

**If you want the new cards in testplayer's collection:**

Delete and recreate the test player:

```bash
cd C:\Users\ahmad\GuardiansBackend

# Delete old data (optional - only if needed)
# python manage.py flush --noinput

# Recreate test player with new cards
python manage.py seed_sample_player
```

---

## Verification

**Check cards are in database:**

Visit in browser: `http://localhost:8000/api/players/my_cards/`

You should see 12 cards total:
- 3 Spell cards (toxin_ball, vanish_ball, lightning_ball)
- 6 Unit cards (brave_knight, angry_bear, forest_monster, ancient_spirit, stone_sentinel, tree)
- 2 Guardian cards (boulder_guardian, xod_guardian)
- 1 Legend card (stone_legend)

---

## Summary

| Step | Command | Expected Output |
|------|---------|-----------------|
| 1 | Edit seed_cards.py | Add 4 card definitions |
| 2 | `python manage.py seed_cards` | 4 new cards created |
| 3 | Edit seed_sample_player.py | Add new cards to list |
| 4 | `python manage.py seed_sample_player` | Test player updated with 12 cards |

**Total cards after:** 12 (was 8)

---

## Card Details Reference

| Card ID | Name | Type | Rarity | Max Level |
|---------|------|------|--------|-----------|
| forest_monster | Forest Monster | Unit | Epic | 4 |
| ancient_spirit | Ancient Spirit | Unit | Epic | 5 |
| stone_sentinel | Stone Sentinel | Unit | Rare | 3 |
| tree | Tree | Unit | Rare | 3 |

---

## Quick Copy-Paste for Step 1

If you need the exact code to replace, here's the complete cards_data list replacement:

```python
        # All cards in the game with their properties
        cards_data = [
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

            # Unit Cards
            {
                'id': 'brave_knight',
                'name': 'Brave Knight',
                'description': 'Mobile unit with melee attack',
                'card_type': 'unit',
                'rarity': 'epic',
                'max_level': 5
            },
            {
                'id': 'angry_bear',
                'name': 'Angry Bear',
                'description': 'Tanky unit with high HP',
                'card_type': 'unit',
                'rarity': 'epic',
                'max_level': 4
            },
            {
                'id': 'forest_monster',
                'name': 'Forest Monster',
                'description': 'Creature from the forest with nature powers',
                'card_type': 'unit',
                'rarity': 'epic',
                'max_level': 4
            },
            {
                'id': 'ancient_spirit',
                'name': 'Ancient Spirit',
                'description': 'Ethereal being with magical abilities',
                'card_type': 'unit',
                'rarity': 'epic',
                'max_level': 5
            },

            # Guardian Cards
            {
                'id': 'boulder_guardian',
                'name': 'Boulder Guardian',
                'description': 'Stationary guardian that fires projectiles',
                'card_type': 'guardian',
                'rarity': 'legendary',
                'max_level': 3
            },
            {
                'id': 'xod_guardian',
                'name': 'Xod Guardian',
                'description': 'Unique guardian that spawns units every 10 seconds',
                'card_type': 'guardian',
                'rarity': 'legendary',
                'max_level': 3
            },

            # Additional Unit Cards (Rare Tier)
            {
                'id': 'stone_sentinel',
                'name': 'Stone Sentinel',
                'description': 'Stone guardian with defensive stance',
                'card_type': 'unit',
                'rarity': 'rare',
                'max_level': 3
            },
            {
                'id': 'tree',
                'name': 'Tree',
                'description': 'Ancient tree with support abilities',
                'card_type': 'unit',
                'rarity': 'rare',
                'max_level': 3
            },

            # Legend Stone (Special Card - only 1 per deck)
            {
                'id': 'stone_legend',
                'name': 'Stone Legend',
                'description': 'Legendary stone with powerful effects',
                'card_type': 'legend',
                'rarity': 'legendary',
                'max_level': 2
            },
        ]
```

---

## Done!

After these steps, the database will have **12 total cards** and the test player will have all of them in their collection.
