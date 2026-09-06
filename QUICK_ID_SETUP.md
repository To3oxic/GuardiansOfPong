# Copy-Paste ID Values - Quick Setup

Simply copy the ID value and paste it into the `id` field in the Inspector.

## Cards (Abilities/Spells)

Copy each ID below and paste into the corresponding CardData asset's `id` field.

```
Card Name: ToxinBall
ID to copy: c1

Card Name: BraveKnight
ID to copy: c2

Card Name: Vanish
ID to copy: c3

Card Name: LightningBall
ID to copy: c4

Card Name: AncientSpirit
ID to copy: c5

Card Name: AngryBear
ID to copy: c6

Card Name: ForestMonsters
ID to copy: c7

Card Name: StoneSentinel
ID to copy: c8

Card Name: Tree
ID to copy: c9
```

## Legends (Guardians)

Copy each ID below and paste into the corresponding CastingLegendData asset's `id` field.

```
Legend Name: Xod
ID to copy: l1

Legend Name: Boulder
ID to copy: l2

Legend Name: Pyro
ID to copy: l3
```

## Legend Stones (Passive Abilities)

Copy each ID below and paste into the corresponding LegendStoneData asset's `id` field.

```
Stone Name: ForestSpirit
ID to copy: s1
```

---

## Simple Step-by-Step Process

1. **Find asset** (e.g., ToxinBall.asset)
2. **Click asset** to open Inspector
3. **Find "ID" field** in Inspector
4. **Copy ID value** from above (e.g., "c1")
5. **Paste into ID field**
6. **Repeat for all items**

## No Spaces or Quotes Needed

When pasting, just paste the ID like this:
- ✅ Correct: `c1` (no quotes)
- ❌ Wrong: `"c1"` (has quotes)
- ❌ Wrong: ` c1 ` (has spaces)

## Total Count

- **9 Cards** to set IDs on (c1-c9)
- **3 Legends** to set IDs on (l1-l3)  
- **1+ Stones** to set IDs on (s1+)

**Total: ~13 items** to quickly update

All values are simple, short, and easy to copy-paste!

---

## PlayFab CARD_DB (For Reference)

This is already set up in GameFunctions.js. You don't need to do anything with this - just verify your Unity IDs match:

```javascript
var CARD_DB = {
    "c1": { name: "ToxinBall", rarity: "RARE" },
    "c2": { name: "BraveKnight", rarity: "COMMON" },
    "c3": { name: "Vanish", rarity: "COMMON" },
    "c4": { name: "LightningBall", rarity: "EPIC" },
    "c5": { name: "AncientSpirit", rarity: "EPIC" },
    "c6": { name: "AngryBear", rarity: "RARE" },
    "c7": { name: "ForestMonsters", rarity: "COMMON" },
    "c8": { name: "StoneSentinel", rarity: "RARE" },
    "c9": { name: "Tree", rarity: "COMMON" },
    
    "l1": { name: "Xod", rarity: "EPIC" },
    "l2": { name: "Boulder", rarity: "COMMON" },
    "l3": { name: "Pyro", rarity: "RARE" },
    
    "s1": { name: "ForestSpirit", rarity: "COMMON" },
};
```

**Your Unity IDs should match exactly!**

---

## Quick Check After Setting All IDs

After you set all the IDs, verify by running this in-game:

1. Open the game
2. Open Loot Box
3. Open Level 1 loot box
4. Should see rewards display
5. Check inventory for new items

If all works → **You're done! The ID system is live!**

If something breaks → Check [ID_SYSTEM_GUIDE.md](ID_SYSTEM_GUIDE.md) troubleshooting section
