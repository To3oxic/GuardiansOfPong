# Scene Setup Guide - PlayFab Testing

## Scene Structure

```
MainMenu (Scene)
├── Canvas
│   ├── LoginPanel (GameObject)
│   │   ├── Panel (UI)
│   │   ├── Text: "Email:"
│   │   ├── InputField: emailInput
│   │   ├── Text: "Password:"
│   │   ├── InputField: passwordInput
│   │   ├── Button: loginButton (text: "Login")
│   │   ├── Button: registerButton (text: "Register")
│   │   └── TextMeshProUGUI: statusText (text: "Ready to login")
│   │
│   └── MainMenuPanel (GameObject) - Hidden initially
│       ├── Text: "Welcome!"
│       ├── CardDisplayUI (Container)
│       │   ├── TextMeshProUGUI: cardsTitle (text: "My Cards")
│       │   ├── ScrollView
│       │   │   └── Content (cardListContainer)
│       │   ├── Button: awardTestCardButton (text: "Award Test Card")
│       │   └── Button: levelUpTestCardButton (text: "Level Up Test Card")
│       │
│       └── MainMenuButtons
│           ├── Button: startGameButton
│           ├── Button: deckButton
│           ├── Button: shopButton
│           └── Button: settingsButton
│
└── Managers (Non-UI GameObjects)
    ├── PlayFabBackendManager (Singleton)
    ├── LoginManager
    └── CardDisplayManager
```

---

## Inspector Setup - PlayFabBackendManager

**GameObject:** PlayFabBackendManager
**Script:** PlayFabBackendManager.cs

```
PlayFab Backend Manager
├─ PlayFab Title Id: "YOUR_TITLE_ID_HERE"
└─ (Rest auto-initialized)
```

**Where to get Title ID:**
1. Go to PlayFab.com
2. Select your title
3. Dashboard shows Title ID: `XXXXX`

---

## Inspector Setup - LoginManager

**GameObject:** LoginManager
**Script:** LoginManager.cs

```
Login Manager (Script)
├─ UI References
│  ├─ Login Panel: Canvas > LoginPanel
│  ├─ Email Input: Canvas > LoginPanel > emailInput (InputField)
│  ├─ Password Input: Canvas > LoginPanel > passwordInput (InputField)
│  ├─ Login Button: Canvas > LoginPanel > loginButton (Button)
│  ├─ Register Button: Canvas > LoginPanel > registerButton (Button)
│  └─ Status Text: Canvas > LoginPanel > statusText (TextMeshProUGUI)
│
└─ Main Menu
   └─ Main Menu Panel: Canvas > MainMenuPanel (GameObject)
```

**Button Setup:**
- **loginButton** OnClick: 
  - GameObject: LoginManager
  - Function: LoginManager.OnLoginPressed()
  
- **registerButton** OnClick:
  - GameObject: LoginManager
  - Function: LoginManager.OnRegisterPressed()

---

## Inspector Setup - CardDisplayManager

**GameObject:** CardDisplayManager
**Script:** CardDisplayManager.cs

```
Card Display Manager (Script)
├─ UI References
│  ├─ Cards Title: Canvas > MainMenuPanel > CardDisplayUI > cardsTitle
│  │   (TextMeshProUGUI, initial text: "My Cards (0)")
│  │
│  └─ Card List Container: Canvas > MainMenuPanel > CardDisplayUI > ScrollView > Content
│      (Transform, this is where cards are instantiated)
│
├─ Card Display Prefab: (Empty for text-only display)
│  (Optional: Create prefab with 3 TextMeshProUGUI fields if needed)
│
└─ Test/Debug
   ├─ Award Test Card Button: Canvas > MainMenuPanel > CardDisplayUI > awardTestCardButton
   │  OnClick Function: CardDisplayManager.OnAwardTestCard()
   │
   └─ Level Up Test Card Button: Canvas > MainMenuPanel > CardDisplayUI > levelUpTestCardButton
      OnClick Function: CardDisplayManager.OnLevelUpTestCard()
```

---

## UI Hierarchy (Detailed)

### LoginPanel (Should be Hidden After Login)
```
LoginPanel
├─ Panel (Image component, for background)
│  └─ LayoutGroup: Vertical Layout Group
│
├─ Text: "Email:"
│  └─ Font Size: 36
│
├─ emailInput (InputField)
│  ├─ Placeholder: "Enter email"
│  ├─ Content Type: Email Address
│  └─ Character Validation: Email
│
├─ Text: "Password:"
│  └─ Font Size: 36
│
├─ passwordInput (InputField)
│  ├─ Placeholder: "Enter password"
│  ├─ Content Type: Password
│  ├─ Line Type: Single Line
│  └─ Character Validation: None
│
├─ loginButton (Button)
│  ├─ Text: "Login"
│  ├─ Font Size: 32
│  └─ OnClick → LoginManager.OnLoginPressed()
│
├─ registerButton (Button)
│  ├─ Text: "Register"
│  ├─ Font Size: 32
│  └─ OnClick → LoginManager.OnRegisterPressed()
│
└─ statusText (TextMeshProUGUI)
   ├─ Text: "Ready to login"
   ├─ Font Size: 28
   ├─ Color: White (initially)
   └─ Alignment: Center
```

### MainMenuPanel (Hidden Initially)
```
MainMenuPanel (Starts: inactive)
│
├─ CardDisplayUI (Container)
│  │
│  ├─ cardsTitle (TextMeshProUGUI)
│  │  ├─ Text: "My Cards"
│  │  ├─ Font Size: 40
│  │  └─ Color: Yellow
│  │
│  ├─ ScrollView
│  │  └─ Viewport
│  │     └─ Content (cardListContainer)
│  │        ├─ Layout Group: Vertical Layout Group
│  │        └─ [Cards instantiated here at runtime]
│  │
│  ├─ awardTestCardButton (Button)
│  │  ├─ Text: "Award Test Card"
│  │  ├─ Font Size: 28
│  │  └─ OnClick → CardDisplayManager.OnAwardTestCard()
│  │
│  └─ levelUpTestCardButton (Button)
│     ├─ Text: "Level Up Test Card"
│     ├─ Font Size: 28
│     └─ OnClick → CardDisplayManager.OnLevelUpTestCard()
│
└─ MainMenuButtons
   ├─ startGameButton (Button)
   │  └─ Text: "Start Game"
   │
   ├─ deckButton (Button)
   │  └─ Text: "My Deck"
   │
   ├─ shopButton (Button)
   │  └─ Text: "Shop"
   │
   └─ settingsButton (Button)
      └─ Text: "Settings"
```

---

## Canvas Settings

```
Canvas
├─ Render Mode: Screen Space - Overlay
├─ Reference Resolution: 1920 x 1080
├─ UI Scale Mode: Scale With Screen Size
└─ Canvas Scaler: UI Scale Mode → Scale With Screen Size
```

---

## Sample Test Run

### Before Login (First Frame)
```
Screen shows:
┌─────────────────────────────────┐
│      LOGIN PANEL                │
│                                 │
│  Email: [testuser@gmail.com   ] │
│  Password: [●●●●●●●●●●       ] │
│                                 │
│  [ Login ]  [ Register ]        │
│                                 │
│  Status: Ready to login         │
└─────────────────────────────────┘
```

### After Login (MainMenuPanel Active)
```
Screen shows:
┌─────────────────────────────────┐
│      MAIN MENU                  │
│                                 │
│  My Cards (1)                   │
│  ┌─────────────────────────────┐│
│  │ Boulder Guardian - Lv.1 (x1)││
│  └─────────────────────────────┘│
│                                 │
│  [ Award Test Card    ]         │
│  [ Level Up Test Card ]         │
│                                 │
│  [ Start Game ] [ Deck ]        │
│  [ Shop ]       [ Settings ]    │
└─────────────────────────────────┘
```

### After Awarding More Cards & Leveling
```
Screen shows:
┌─────────────────────────────────┐
│      MAIN MENU                  │
│                                 │
│  My Cards (1)                   │
│  ┌─────────────────────────────┐│
│  │ Boulder Guardian - Lv.3 (x4)││
│  └─────────────────────────────┘│
│                                 │
│  [ Award Test Card    ]         │
│  [ Level Up Test Card ]         │
│                                 │
│  Status: Card leveled up!       │
│  Currency: 700 / 1000           │
└─────────────────────────────────┘
```

---

## Console Expected Output

### On Startup
```
[PlayFab] Initialized with Title ID: XXXXX
[MainMenuManager] MainMenu initialized
[LoginManager] Login UI ready
```

### On Register
```
[PlayFab] Registration successful. PlayFabId: 12345ABCDE...
[PlayFab] Login successful. PlayFabId: 12345ABCDE..., DisplayName: testuser
[LoginManager] Logged in as testuser
[LoginManager] Loaded 0 cards
```

### On Award Card
```
[PlayFab] Awarded 1 copy/copies of Boulder Guardian. Total owned: 1, Level: 1
[CardDisplayManager] Test card awarded successfully
[CardDisplayManager] Displayed 1 cards
```

### On Level Up
```
[PlayFab] Card card_boulder leveled up successfully
[CardDisplayManager] Card leveled up successfully
[CardDisplayManager] Displayed 1 cards
Console: Boulder Guardian - Lv.2 (x2)
```

---

## Quick Troubleshooting

| Problem | Check |
|---------|-------|
| LoginPanel not showing | Canvas exists, LoginPanel is child of Canvas |
| Buttons not responding | Button component has OnClick events assigned |
| Cards not displaying | cardListContainer is set to Content of ScrollView |
| Cloud functions fail | Title ID correct, functions deployed to PlayFab |
| Status text not updating | statusText field assigned in LoginManager |
| Cards not loading after login | PlayFabBackendManager singleton exists in scene |
