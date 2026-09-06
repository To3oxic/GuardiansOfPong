# Card Leveling System - Visual UI Example

## Card Collection UI Layout

```
┌─────────────────────────────────────────────────────────────────┐
│                     MY CARD COLLECTION                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐            │
│  │ [Card Image] │  │ [Card Image] │  │ [Card Image] │  ...      │
│  │              │  │              │  │              │            │
│  │  Fireball    │  │   Heal       │  │  Ice Shard   │            │
│  │   Lv.3       │  │   Locked     │  │   Lv.1       │            │
│  │              │  │              │  │              │            │
│  │  3 copies    │  │  0 copies    │  │  1 copy      │            │
│  │              │  │              │  │              │            │
│  │ ░░░░░░░░░░   │  │ ░░░░░░░░░░   │  │ ░░░░░░░░░░   │            │
│  │  3/6         │  │  0/1         │  │  1/2         │            │
│  │ [SELECT]     │  │ [LOCKED]     │  │ [SELECT]     │            │
│  └──────────────┘  └──────────────┘  └──────────────┘            │
│                                                                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐            │
│  │ [Card Image] │  │ [Card Image] │  │ [Card Image] │  ...      │
│  │              │  │              │  │              │            │
│  │  Meteor      │  │  Poison      │  │  Shield      │            │
│  │   Lv.2       │  │   Lv.5       │  │   Lv.4       │            │
│  │              │  │              │  │              │            │
│  │  3 copies    │  │  15 copies   │  │  10 copies   │            │
│  │              │  │              │  │              │            │
│  │ ░░░░░░░░░░   │  │ ░░░░░░░░░░   │  │ ░░░░░░░░░░   │            │
│  │  3/3 (Max)   │  │  15/15(Max)  │  │  10/5 (Max)  │            │
│  │ [SELECT]     │  │ [SELECT]     │  │ [SELECT]     │            │
│  └──────────────┘  └──────────────┘  └──────────────┘            │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Card Button States

### Locked Card
```
┌─────────────────┐
│  [Dark Image]   │
│  Spell Name     │
│   Locked        │
│                 │
│  0 copies       │
│  ▭▭▭▭▭▭▭▭▭▭    │
│   0/1           │
│ [LOCKED BTN]    │  ← Greyed out, uninteractable
│ (Gray overlay)  │
└─────────────────┘
```

### Level 1 Card
```
┌─────────────────┐
│ [Bright Image]  │
│  Spell Name     │
│   Lv.1          │
│                 │
│  1 copy         │
│  ▪▭▭▭▭▭▭▭▭▭   │
│   1/2           │
│ [SELECT CARD]   │  ← White, interactable
│ (Normal color)  │
└─────────────────┘
```

### Level 5 Card (Max)
```
┌─────────────────┐
│ [Bright Image]  │
│  Spell Name     │
│   Lv.5          │
│                 │
│  15 copies      │
│  ▪▪▪▪▪▪▪▪▪▪   │  ← Full bar
│ 15/15 (MAX)     │
│ [SELECT CARD]   │  ← White, interactable
│ (Normal color)  │
└─────────────────┘
```

---

## Progress Bar Examples

### Locked
```
Progress to unlock: 0/1
▭ ▭ ▭ ▭ ▭ ▭ ▭ ▭ ▭ ▭  (empty)
```

### Level 1 → Level 2 (halfway)
```
Progress to Lv.2: 1/2
▪ ▭ ▭ ▭ ▭ ▭ ▭ ▭ ▭ ▭  (1 of 2)
```

### Level 2 → Level 3 (halfway)
```
Progress to Lv.3: 3/3 (unlock next level)
▪ ▪ ▪ ▭ ▭ ▭ ▭ ▭ ▭ ▭  (3 of 3)
```

### Level 2 → Level 3 (halfway through collecting)
```
Progress to Lv.3: 1/3
▪ ▭ ▭ ▭ ▭ ▭ ▭ ▭ ▭ ▭  (1 of 3 needed)
```

### Level 5 (Maxed)
```
Max Level Reached
▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪  (full)
```

---

## Deck Selection UI

### Before Unlocking Cards
```
┌────────────────────────────────┐
│      SELECT YOUR DECK          │
├────────────────────────────────┤
│                                 │
│ Select 5 Cards + Legend + Stone │
│                                 │
│ Cards Selected: 0/5             │
│                                 │
│ ┌──────────┐  ┌──────────┐      │
│ │Fireball  │  │  Heal    │      │  Locked - can't select
│ │  Lv.1    │  │ Locked   │      │
│ │ [SELECT] │  │[LOCKED]  │      │
│ └──────────┘  └──────────┘      │
│                                 │
│ ┌──────────┐  ┌──────────┐      │
│ │ Meteor   │  │ Poison   │      │  Can select
│ │  Lv.2    │  │ Locked   │      │
│ │ [SELECT] │  │[LOCKED]  │      │
│ └──────────┘  └──────────┘      │
│                                 │
│         [START GAME]            │  ← Disabled until 5 selected
│                                 │
└────────────────────────────────┘
```

### After Selecting Cards
```
┌────────────────────────────────┐
│      SELECT YOUR DECK          │
├────────────────────────────────┤
│                                 │
│ Select 5 Cards + Legend + Stone │
│                                 │
│ Cards Selected: 3/5             │
│                                 │
│ ┌──────────┐  ┌──────────┐      │
│ │Fireball  │  │  Heal    │      │
│ │  Lv.1    │  │ Locked   │      │
│ │[✓SELECTED]  │[LOCKED]  │      │  ← Selected gets highlight
│ └──────────┘  └──────────┘      │
│                                 │
│ ┌──────────┐  ┌──────────┐      │
│ │ Meteor   │  │ Poison   │      │
│ │  Lv.2    │  │ Locked   │      │
│ │[✓SELECTED] │[LOCKED]  │      │
│ └──────────┘  └──────────┘      │
│                                 │
│  Legend: Fire Dragon (Lv.2)    │
│  Stone: Red Gem (Lv.1)         │
│                                 │
│         [START GAME]            │  ← Enabled when 5+Legend+Stone
│                                 │
└────────────────────────────────┘
```

---

## Card Award Notification

### Pack Opening Animation
```
┌──────────────────────────┐
│   OPENING CARD PACK      │
├──────────────────────────┤
│                          │
│   ✨ You got: ✨        │
│                          │
│   ┌────────────┐        │
│   │  Fireball  │  ← New card
│   │  Lv.1      │        │
│   │  1 copy    │        │
│   └────────────┘        │
│                          │
│   ┌────────────┐        │
│   │   Heal     │  ← Duplicate
│   │  Lv.2 → ?  │        │
│   │  2 → 3 cp  │        │
│   └────────────┘        │
│                          │
│   ┌────────────┐        │
│   │   Meteor   │  ← Duplicate
│   │  Lv.2 → 3! │  ← Leveled up!
│   │  3 → 6 cp  │        │
│   └────────────┘        │
│                          │
│  [CONTINUE]             │
└──────────────────────────┘
```

---

## Collection Growth Example

### Day 1: New Player
```
Card         | Status  | Quantity | Locked?
─────────────────────────────────────────
Fireball     | Lv.1    | 1        | ✗ (can use)
Heal         | Locked  | 0        | ✓ (locked)
Meteor       | Locked  | 0        | ✓ (locked)
Poison       | Locked  | 0        | ✓ (locked)
```

### Day 3: Won 2 Matches, Earned Packs
```
Card         | Status  | Quantity | Locked?
─────────────────────────────────────────
Fireball     | Lv.2    | 3        | ✗ (leveled up!)
Heal         | Lv.1    | 1        | ✗ (unlocked!)
Meteor       | Locked  | 0        | ✓ (locked)
Poison       | Locked  | 0        | ✓ (locked)
```

### Day 5: More Matches, Daily Rewards
```
Card         | Status  | Quantity | Locked?
─────────────────────────────────────────
Fireball     | Lv.3    | 6        | ✗ (leveled up!)
Heal         | Lv.2    | 3        | ✗ (leveled up!)
Meteor       | Lv.1    | 1        | ✗ (unlocked!)
Poison       | Locked  | 0        | ✓ (locked)
```

### Day 10: Consistent Play
```
Card         | Status  | Quantity | Locked?
─────────────────────────────────────────
Fireball     | Lv.4    | 10       | ✗ (maxing out!)
Heal         | Lv.3    | 6        | ✗
Meteor       | Lv.2    | 3        | ✗
Poison       | Lv.1    | 1        | ✗ (finally!)
```

---

## Card Button HTML Sketch

```html
<div class="card-button">
  <!-- Card Image -->
  <img class="card-image" src="fireball.png">
  
  <!-- Card Name -->
  <div class="card-name">Fireball</div>
  
  <!-- Status Badge -->
  <div class="card-status">Lv.2</div>
  
  <!-- Quantity Display -->
  <div class="card-quantity">3 copies</div>
  
  <!-- Progress Bar -->
  <div class="progress-container">
    <div class="progress-bar">
      <div class="progress-fill" style="width: 50%"></div>
    </div>
    <div class="progress-text">3/6</div>
  </div>
  
  <!-- Select Button -->
  <button class="select-button">SELECT</button>
</div>
```

### CSS Styling

```css
.card-button {
  display: flex;
  flex-direction: column;
  width: 150px;
  padding: 10px;
  border-radius: 8px;
  background: white;
  transition: all 0.2s;
}

.card-button.locked {
  background: rgba(128, 128, 128, 0.3);
}

.card-button.locked .card-image {
  opacity: 0.6;
  filter: grayscale(100%);
}

.card-button.locked .select-button {
  background: gray;
  cursor: not-allowed;
}

.card-button.selected {
  background: rgba(144, 238, 144, 0.4);
  border: 3px solid green;
}

.card-status {
  font-weight: bold;
  color: #2196F3;
  font-size: 12px;
}

.card-status.locked {
  color: #999;
}

.progress-fill {
  height: 100%;
  background: linear-gradient(90deg, #4CAF50, #8BC34A);
  border-radius: 3px;
  transition: width 0.3s;
}

.select-button {
  background: white;
  color: #2196F3;
  border: 2px solid #2196F3;
  padding: 5px;
  border-radius: 4px;
  cursor: pointer;
  font-weight: bold;
  transition: all 0.2s;
}

.select-button:hover:not(:disabled) {
  background: #2196F3;
  color: white;
}

.select-button:disabled {
  background: #ddd;
  color: #999;
  border-color: #999;
  cursor: not-allowed;
}
```

---

## Mobile Responsive Layout

### Phone (Portrait)
```
┌──────────────────────────┐
│   CARD COLLECTION        │
├──────────────────────────┤
│                          │
│  ┌─────────┐ ┌─────────┐ │
│  │ Fire    │ │ Heal    │ │
│  │ Lv.3    │ │Locked   │ │
│  │[SELECT] │ │[LOCKED] │ │
│  └─────────┘ └─────────┘ │
│                          │
│  ┌─────────┐ ┌─────────┐ │
│  │Meteor   │ │Poison   │ │
│  │ Lv.2    │ │Locked   │ │
│  │[SELECT] │ │[LOCKED] │ │
│  └─────────┘ └─────────┘ │
│                          │
│  ┌─────────────────────┐ │
│  │  Shield             │ │
│  │  Lv.1               │ │
│  │  [SELECT CARD]      │ │
│  └─────────────────────┘ │
│                          │
└──────────────────────────┘
```

### Tablet (Landscape)
```
┌──────────────────────────────────────────────┐
│            CARD COLLECTION                    │
├──────────────────────────────────────────────┤
│                                               │
│ ┌──────────┐ ┌──────────┐ ┌──────────┐     │
│ │ Fire     │ │ Heal     │ │ Meteor   │     │
│ │ Lv.3     │ │ Locked   │ │ Lv.2     │     │
│ │[SELECT]  │ │[LOCKED]  │ │[SELECT]  │     │
│ └──────────┘ └──────────┘ └──────────┘     │
│                                               │
│ ┌──────────┐ ┌──────────┐ ┌──────────┐     │
│ │ Poison   │ │ Shield   │ │ Lightning│     │
│ │ Locked   │ │ Lv.1     │ │ Locked   │     │
│ │[LOCKED]  │ │[SELECT]  │ │[LOCKED]  │     │
│ └──────────┘ └──────────┘ └──────────┘     │
│                                               │
└──────────────────────────────────────────────┘
```

---

## Summary

**Key UI Elements:**
- ✅ Card Image (greyed if locked)
- ✅ Card Name
- ✅ Level Badge (Lv.1-5 or "Locked")
- ✅ Quantity Display (e.g., "3 copies")
- ✅ Progress Bar (visual + text "3/6")
- ✅ Select Button (enabled/disabled based on ownership)
- ✅ Color coding (white=owned, grey=locked)
- ✅ Visual feedback (highlight when selected)

**Responsive Design:**
- Mobile: 2-column grid
- Tablet: 3-column grid
- Desktop: 4-5 column grid

**Accessibility:**
- Disabled buttons for locked cards
- Clear labels ("Locked", "Lv.X", "X copies")
- Progress text as backup for color-blind users
- Sufficient contrast between locked/owned states
