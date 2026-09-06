# Backend Save Deck Endpoint Implementation

## Task
Implement the `/api/players/save-deck/` endpoint in the Django backend to allow authenticated players to save their selected deck.

## Location
File: `C:\Users\ahmad\GuardiansBackend\players\views.py`
Add this method to the `PlayerViewSet` class

## Implementation Requirements

### Method Signature
```python
@action(detail=False, methods=['post'], permission_classes=[IsAuthenticated])
def save_deck(self, request):
    """Save player's selected deck - POST /api/players/save-deck/"""
    # Implementation goes here
```

### Request Format (what Unity sends)
```json
POST /api/players/save-deck/

{
    "deck_name": "Selected Deck",
    "is_active": true,
    "card_ids": ["toxin_ball", "brave_knight", "angry_bear", "vanish_ball", "lightning_ball", "angry_bear", "brave_knight", "lightning_ball"],
    "guardian_id": "boulder_guardian",
    "legend_stone_id": "stone_legend"
}
```

**Request Parameters:**
- `deck_name` (string, required): Name of the deck
- `is_active` (boolean, required): Whether this is the active deck
- `card_ids` (array of 8 strings, required): List of card IDs from player's collection (registry_name field)
- `guardian_id` (string, required): Single guardian card ID from player's collection
- `legend_stone_id` (string, required): Single legend stone card ID from player's collection

### Response Format (Success - 200/201)
```json
{
    "success": true,
    "message": "Deck saved successfully",
    "deck": {
        "id": "uuid-string",
        "deck_name": "Selected Deck",
        "is_active": true,
        "card_slot_1": {
            "id": "player-card-uuid",
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
        "card_slot_2": { /* same structure */ },
        "card_slot_3": { /* same structure */ },
        "card_slot_4": { /* same structure */ },
        "card_slot_5": { /* same structure */ },
        "card_slot_6": { /* same structure */ },
        "card_slot_7": { /* same structure */ },
        "card_slot_8": { /* same structure */ },
        "legend_stone": { /* same structure as card slots */ },
        "guardian_card": { /* same structure as card slots */ },
        "created_at": "2025-11-28T12:00:00Z",
        "updated_at": "2025-11-28T12:00:00Z"
    }
}
```

### Error Response Format (400/403/404)
```json
{
    "success": false,
    "message": "Error description here"
}
```

## Implementation Logic

1. **Extract request data:**
   - Get `deck_name`, `is_active`, `card_ids`, `guardian_id`, `legend_stone_id` from request.data
   - Validate all required fields are present (return 400 if missing)

2. **Validate card count:**
   - `card_ids` must contain exactly 8 cards (return 400 if not)

3. **Validate cards belong to player:**
   - For each card in `card_ids`: look up PlayerCard where `player=request.user` and `card_id` matches
   - If ANY card is not in player's collection, return 400 with message: `"Card '{card_id}' not found in your collection"`

4. **Validate guardian card:**
   - Look up PlayerCard where `player=request.user`, `card_id=guardian_id`
   - If not found, return 400 with message: `"Guardian '{guardian_id}' not found in your collection"`
   - Verify it's actually a guardian type card

5. **Validate legend stone card:**
   - Look up PlayerCard where `player=request.user`, `card_id=legend_stone_id`
   - If not found, return 400 with message: `"Legend stone '{legend_stone_id}' not found in your collection"`
   - Verify it's actually a legend stone type card

6. **Create or Update SavedDeck:**
   - Check if deck with same `deck_name` exists for this player
   - If exists: update it; if not: create new
   - Set fields:
     - `player` = request.user
     - `deck_name` = from request
     - `is_active` = from request
     - `card_slot_1` through `card_slot_8` = the 8 PlayerCard objects
     - `guardian_card` = the guardian PlayerCard object
     - `legend_stone` = the legend stone PlayerCard object

7. **Return Success Response:**
   - Serialize the saved deck using `SavedDeckSerializer`
   - Include all nested card data
   - Return status 201 (Created) or 200 (Updated)

## Key Points

- **Authentication**: Use `permission_classes=[IsAuthenticated]` - Django will handle 403 if user not authenticated
- **Database Lookups**: Use `PlayerCard.objects.filter(player=request.user, card__id=card_id).first()` to verify card ownership
- **Data Validation**: Ensure cards exist AND belong to the authenticated user before saving
- **Serialization**: Use the existing `SavedDeckSerializer` to return the response with all nested relationships
- **Error Handling**: Catch exceptions and return appropriate error messages (400 for validation errors)

## Related Models/Serializers to Use

- Model: `SavedDeck` (already exists in models.py)
- Model: `PlayerCard` (already exists in models.py)
- Model: `Card` (already exists in models.py)
- Serializer: `SavedDeckSerializer` (already exists in serializers.py)

## Testing After Implementation

Test with curl:
```bash
curl -X POST http://localhost:8000/api/players/save-deck/ \
  -H "Content-Type: application/json" \
  -H "Cookie: sessionid=<session_from_login>" \
  -d '{
    "deck_name": "Test Deck",
    "is_active": true,
    "card_ids": ["toxin_ball", "brave_knight", "angry_bear", "vanish_ball", "lightning_ball", "angry_bear", "brave_knight", "lightning_ball"],
    "guardian_id": "boulder_guardian",
    "legend_stone_id": "stone_legend"
  }'
```

Expected: 201 response with full deck structure
