# Matchmaking UI Setup Guide

## 🎯 Overview
This guide explains how to set up the Matchmaking UI in your Unity scene.

## 📋 Required Components

### 1. Create Matchmaking Manager GameObject

1. **Create a new empty GameObject** in your scene (or prefab scene)
   - Name: `MatchmakingManager`
   - Parent: Root of scene or DontDestroyOnLoad manager

2. **Add the `MatchmakingManager` script**
   - Drag the script to this GameObject

### 2. Create Matchmaking Panel UI

**Hierarchy Structure:**

```
Canvas (Main UI Canvas)
├── MatchmakingPanel (Panel Component)
│   ├── Background (Image Component - Dark overlay)
│   ├── MatchmakingContainer (Vertical Layout Group)
│   │   ├── Title (TextMeshProUGUI)
│   │   ├── StatusText (TextMeshProUGUI)
│   │   ├── QueuePositionText (TextMeshProUGUI)
│   │   ├── Spinner (Animated rotating image)
│   │   └── CancelButton (Button Component)
│   └── (Optional) Icon
```

### 3. Configure Canvas & Panel

**Canvas Settings:**
- Render Mode: Screen Space - Overlay
- Sorting Order: 100 (appear above other UI)

**MatchmakingPanel (RectTransform):**
- Anchors: Full Screen (1,1) to (0,0)
- Offset: 0,0,0,0
- Size: Matches canvas

**MatchmakingPanel (Image Component):**
- Color: Black with transparency (0,0,0,200) for dark overlay

### 4. Add UI Elements

#### StatusText
```
Component: TextMeshProUGUI
Text: "Looking for opponent..."
Font Size: 40
Color: White
Alignment: Center
Raycast Target: False
```

#### QueuePositionText
```
Component: TextMeshProUGUI
Text: (Leave empty)
Font Size: 30
Color: Light Gray
Alignment: Center
```

#### CancelButton
```
Button Component:
├── Text: "Cancel"
├── Interactable: True
Target Graphic: Button Image
Transition: Color Tint (optional)
```

#### Spinner (Optional)
```
Image Component:
├── Source Image: Loading spinner icon
├── Animation: Add rotation animation or use code
```

### 5. Assign References in MatchmakingManager Inspector

```
Matchmaking Panel: [Drag MatchmakingPanel GameObject here]
Status Text: [Drag StatusText here]
Queue Position Text: [Drag QueuePositionText here]
Cancel Button: [Drag CancelButton here]
Panel Canvas Group: [Assign CanvasGroup component]
Game Scene Name: "GameArena" (or your scene name)
Poll Interval: 1.0
```

### 6. Add CanvasGroup Component

**To MatchmakingPanel GameObject:**
1. Click "Add Component"
2. Add `CanvasGroup`
3. Set in MatchmakingManager's `panelCanvasGroup` field

### 7. Wire Button to MatchmakingManager

**On CancelButton (Button Component):**
1. Expand "On Click" event
2. Click "+"
3. Drag MatchmakingManager GameObject to the object field
4. Function: `MatchmakingManager > StartMatchmaking()` 
   - **Actually**: Drag to: `MatchmakingManager > OnCancelButtonClicked()`

### 8. Add Find Match Button to Card Selection

**In your Card Selection UI:**

1. **Create "Find Match" Button** (or rename existing Submit button)
   ```
   Button Component:
   ├── Text: "Find Match"
   ├── Colors: Green theme
   ```

2. **Wire to SelectionCardManager:**
   - On Click → SelectionCardManager > `StartMatchmakingWithSelectedDeck()`

## 📝 Script Integration Summary

### MatchmakingManager Methods

```csharp
// Start matchmaking with a specific deck
public void StartMatchmaking(string deckId)

// Shows the matchmaking UI
private void ShowSearchingUI()

// Updates queue position display
private void UpdateQueuePositionUI(int position)

// Hide the matchmaking UI
private void HideMatchmakingUI()
```

### SelectionCardManager Methods

```csharp
// Start matchmaking with currently selected deck
public void StartMatchmakingWithSelectedDeck()

// Get active deck ID from cached player data
// Calls: MatchmakingManager.Instance.StartMatchmaking(deckId)
```

## 🔌 Backend API Flow

```
1. User clicks "Find Match" button
   ↓
2. SelectionCardManager.StartMatchmakingWithSelectedDeck() called
   ↓
3. MatchmakingManager.StartMatchmaking(deckId) called
   ↓
4. POST /api/matchmaking/join_queue/
   Body: { "deck_id": "uuid" }
   ↓
5. MatchmakingPanel UI shows "Looking for opponent..."
   ↓
6. MatchmakingManager polls GET /api/matchmaking/queue_status/ every 1 second
   ↓
7. When matched:
   - Shows opponent name
   - Receives server IP:port
   ↓
8. Connects NetworkManager to server
   ↓
9. Loads GameArena scene
```

## ⚙️ Configuration in Inspector

### MatchmakingManager Fields

| Field | Value | Notes |
|-------|-------|-------|
| matchmakingPanel | MatchmakingPanel GameObject | Required |
| statusText | StatusText TextMeshProUGUI | Required |
| queuePositionText | QueuePositionText TextMeshProUGUI | Required |
| cancelButton | CancelButton | Required |
| panelCanvasGroup | CanvasGroup | Required |
| gameSceneName | "GameArena" | Scene to load when matched |
| pollInterval | 1.0 | Seconds between status checks |

## 🎨 Optional UI Enhancements

### 1. Spinner Animation
```csharp
// Add to MatchmakingManager if desired
private void RotateSpinner()
{
    if (spinnerImage != null)
    {
        spinnerImage.rectTransform.Rotate(0, 0, -3f); // 3 degrees per frame
    }
}

// Call in Update():
if (isSearching)
{
    RotateSpinner();
}
```

### 2. Opponent Avatar
Add an Image component to show opponent's avatar:
```
├── OpponentAvatar (Image)
│   └── Shows opponent profile picture
```

### 3. Estimated Wait Time
Add another TextMeshProUGUI:
```
│   └── EstimatedWaitText (TextMeshProUGUI)
│       └── "Estimated wait: 30 seconds"
```

## ⚠️ Important Notes

1. **NetworkManager Setup**: Ensure NetworkManager is configured properly
   - Has ConnectionData set
   - Has NetworkConfig with proper transport
   
2. **Scene Loading**: Make sure "GameArena" scene exists and is in Build Settings
   
3. **Backend Connection**: BackendManager must be initialized before starting matchmaking
   
4. **Deck Validation**: Ensure player has selected a complete deck before matchmaking
   
5. **Disconnection Handling**: Client automatically calls `leave_queue` if:
   - User clicks Cancel
   - Connection lost
   - Scene changes

## 🧪 Testing the Matchmaking

### Local Testing (2 Players)

1. **Terminal 1**: Start backend matchmaker
   ```
   python manage.py matchmaker --buildpath "path/to/build.exe"
   ```

2. **Terminal 2**: Start Django server
   ```
   python manage.py runserver
   ```

3. **Unity Client 1**: Play scene, login, select deck, click "Find Match"

4. **Unity Client 2**: Play scene, login, select deck, click "Find Match"

5. **Result**: Both clients should connect to same headless server and load GameArena

### Debug Output

You should see logs like:
```
[MatchmakingManager] Starting matchmaking with deck: abc-123-uuid
[MatchmakingManager] Joining queue with deck: abc-123-uuid
[MatchmakingManager] Successfully joined queue at position 1
[MatchmakingManager] MATCHED! Opponent: player2
[MatchmakingManager] Attempting to connect to 127.0.0.1:7777
[MatchmakingManager] Connected! Loading game scene
```

## 📚 Related Documentation

- `MATCHMAKING_BACKEND_IMPLEMENTATION.md` - Backend setup
- `BackendManager.cs` - API communication
- `SelectionCardManager.cs` - Deck selection
