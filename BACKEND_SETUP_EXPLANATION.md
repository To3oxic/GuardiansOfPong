# Django Backend Setup - Detailed Breakdown

## 🏗️ Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Django Application                        │
│                                                               │
│  ┌──────────────────────┐      ┌──────────────────────┐     │
│  │  URL Routing         │      │  Middleware Stack    │     │
│  │  (guardians_backend  │      │  (CORS, Sessions,    │     │
│  │   /urls.py)          │      │   Auth, Security)    │     │
│  └──────────────────────┘      └──────────────────────┘     │
│           │                              │                   │
│           └──────────────────┬───────────┘                   │
│                              │                               │
│           ┌──────────────────▼─────────────┐                │
│           │  ViewSets & Serializers        │                │
│           │  (players/views.py)            │                │
│           │  (players/serializers.py)      │                │
│           └──────────────────┬─────────────┘                │
│                              │                               │
│           ┌──────────────────▼─────────────┐                │
│           │  Django ORM Models             │                │
│           │  (players/models.py)           │                │
│           │  - CustomUser                  │                │
│           │  - Card                        │                │
│           │  - PlayerCard                  │                │
│           │  - SavedDeck                   │                │
│           └──────────────────┬─────────────┘                │
│                              │                               │
│           ┌──────────────────▼─────────────┐                │
│           │  SQLite3 Database              │                │
│           │  (db.sqlite3)                  │                │
│           │  4 tables                      │                │
│           └────────────────────────────────┘                │
└─────────────────────────────────────────────────────────────┘
```

---

## 📁 Project Structure

```
C:\Users\ahmad\GuardiansBackend\
│
├── manage.py                           # Django CLI interface
├── db.sqlite3                          # SQLite database file
├── requirements.txt                    # Python dependencies
│
├── guardians_backend/                  # Main Django project config
│   ├── __init__.py
│   ├── settings.py                     # ⭐ KEY CONFIG FILE
│   ├── urls.py                         # ⭐ API ROUTING
│   ├── asgi.py                         # Async app server
│   └── wsgi.py                         # Production app server
│
├── players/                            # Main Django app
│   ├── models.py                       # ⭐ DATABASE MODELS
│   ├── serializers.py                  # ⭐ JSON SERIALIZERS
│   ├── views.py                        # ⭐ API ENDPOINTS
│   ├── admin.py                        # Django admin panel
│   ├── apps.py                         # App config
│   ├── tests.py
│   ├── __init__.py
│   └── management/
│       ├── __init__.py
│       └── commands/
│           ├── seed_cards.py           # ⭐ POPULATE CARDS
│           ├── seed_sample_player.py   # ⭐ CREATE TEST USER
│           └── __init__.py
│
└── cards/                              # Secondary app (not used yet)
    ├── models.py
    ├── views.py
    ├── admin.py
    └── apps.py
```

---

## ⚙️ Django Configuration (settings.py)

### 1. **Basic Settings**
```python
DEBUG = True                              # Development mode
SECRET_KEY = '...'                        # Django secret (auto-generated)
ALLOWED_HOSTS = ['127.0.0.1', 'localhost']
```

### 2. **Installed Apps**
```python
INSTALLED_APPS = [
    'django.contrib.admin',               # Admin panel
    'django.contrib.auth',                # Authentication
    'django.contrib.contenttypes',        # Content type framework
    'django.contrib.sessions',            # Session handling
    'django.contrib.messages',            # Messaging
    'django.contrib.staticfiles',         # Static files
    'rest_framework',                     # ← Django REST Framework
    'corsheaders',                        # ← CORS headers
    'players',                            # ← Our app
    'cards',                              # ← Cards app
]
```

### 3. **Middleware Stack** (Request processing order)
```python
MIDDLEWARE = [
    'django.middleware.security.SecurityMiddleware',
    'django.contrib.sessions.middleware.SessionMiddleware',
    'corsheaders.middleware.CorsMiddleware',           # CORS support
    'django.middleware.common.CommonMiddleware',
    'django.middleware.csrf.CsrfViewMiddleware',
    'django.contrib.auth.middleware.AuthenticationMiddleware',
    'django.contrib.messages.middleware.MessageMiddleware',
    'django.middleware.clickjacking.XFrameOptionsMiddleware',
]
```
**How it works:**
1. Request comes in
2. SecurityMiddleware adds security headers
3. SessionMiddleware creates session object
4. CorsMiddleware allows cross-origin requests
5. Django's auth checks user identity
6. Response goes back

### 4. **Database Configuration**
```python
DATABASES = {
    'default': {
        'ENGINE': 'django.db.backends.sqlite3',
        'NAME': BASE_DIR / 'db.sqlite3',
    }
}
```
**What this means:**
- Using SQLite3 (file-based, no server needed)
- File location: `C:\Users\ahmad\GuardiansBackend\db.sqlite3`
- Perfect for development
- Can switch to PostgreSQL for production

### 5. **Custom User Model**
```python
AUTH_USER_MODEL = 'players.CustomUser'
```
**Why:**
- Default Django user doesn't have game fields
- CustomUser extends AbstractUser with: level, rank, coins, wins, losses, rating

### 6. **REST Framework Configuration**
```python
REST_FRAMEWORK = {
    'DEFAULT_AUTHENTICATION_CLASSES': [
        'rest_framework.authentication.SessionAuthentication',
    ],
    'DEFAULT_PERMISSION_CLASSES': [
        'rest_framework.permissions.IsAuthenticated',
    ],
    'DEFAULT_PAGINATION_CLASS':
        'rest_framework.pagination.PageNumberPagination',
    'PAGE_SIZE': 10,
}
```
**What this does:**
- SessionAuthentication: Uses Django sessions (cookies)
- IsAuthenticated: Most endpoints require login
- Pagination: Return 10 items per page

### 7. **CORS Configuration**
```python
CORS_ALLOWED_ORIGINS = [
    'http://localhost:3000',
    'http://127.0.0.1:3000',
]
CORS_ALLOW_CREDENTIALS = True
```
**Why:**
- Allows Unity client to call API from different port/domain
- Credentials allow session cookies to be sent

---

## 🔄 URL Routing (urls.py)

```python
from rest_framework.routers import DefaultRouter
from players.views import PlayerViewSet

router = DefaultRouter()
router.register(r'players', PlayerViewSet)

urlpatterns = [
    path('admin/', admin.site.urls),           # Django admin
    path('api/', include(router.urls)),        # API routes
]
```

**Generated endpoints:**
```
POST   /api/players/login/          (custom action)
GET    /api/players/me/             (custom action)
GET    /api/players/my_decks/       (custom action)
GET    /api/players/my_cards/       (custom action)
GET    /api/players/                (list all - admin only)
POST   /api/players/                (create - admin only)
```

---

## 📊 Database Models (models.py)

### Model 1: CustomUser (extends AbstractUser)
```python
class CustomUser(AbstractUser):
    id = models.UUIDField(primary_key=True)
    level = models.IntegerField(default=1)
    rank = models.IntegerField(default=1)
    coins = models.IntegerField(default=1000)
    total_wins = models.IntegerField(default=0)
    total_losses = models.IntegerField(default=0)
    skill_rating = models.FloatField(default=1200)
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)
    
    class Meta:
        db_table = 'players'
```

### Model 2: Card (Master card definitions)
```python
class Card(models.Model):
    id = models.CharField(max_length=50, primary_key=True)
    name = models.CharField(max_length=100)
    description = models.TextField()
    card_type = models.CharField(
        max_length=20,
        choices=[('spell','Spell'), ('unit','Unit'), ...]
    )
    rarity = models.CharField(
        max_length=20,
        choices=[('common','Common'), ('rare','Rare'), ...]
    )
    max_level = models.IntegerField()
    
    class Meta:
        db_table = 'cards'
```

### Model 3: PlayerCard (User's card collection)
```python
class PlayerCard(models.Model):
    id = models.UUIDField(primary_key=True)
    player = models.ForeignKey(CustomUser, on_delete=models.CASCADE)
    card = models.ForeignKey(Card, on_delete=models.CASCADE)
    level = models.IntegerField()
    copies = models.IntegerField()
    acquired_at = models.DateTimeField(auto_now_add=True)
    
    class Meta:
        db_table = 'player_cards'
        unique_together = ['player', 'card']  # One row per player+card combo
```

### Model 4: SavedDeck (Player decks)
```python
class SavedDeck(models.Model):
    id = models.UUIDField(primary_key=True)
    player = models.ForeignKey(CustomUser, on_delete=models.CASCADE)
    deck_name = models.CharField(max_length=100)
    is_active = models.BooleanField(default=False)
    
    # 8 regular card slots
    card_slot_1 = models.ForeignKey(PlayerCard, on_delete=...)
    card_slot_2 = models.ForeignKey(PlayerCard, on_delete=...)
    # ... card_slot_3 through card_slot_8
    
    # 2 special slots
    legend_stone = models.ForeignKey(PlayerCard, on_delete=...)
    guardian_card = models.ForeignKey(PlayerCard, on_delete=...)
    
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)
    
    class Meta:
        db_table = 'saved_decks'
```

---

## 📤 Serializers (serializers.py)

**Purpose:** Convert Python objects ↔ JSON

### Serializer 1: CardSerializer
```python
class CardSerializer(serializers.ModelSerializer):
    class Meta:
        model = Card
        fields = ['id', 'name', 'description', 'card_type', 'rarity', 'max_level']
```
**Output:**
```json
{
  "id": "toxin_ball",
  "name": "Toxin Ball",
  "description": "Spawns a ball that applies damage ticks",
  "card_type": "spell",
  "rarity": "rare",
  "max_level": 3
}
```

### Serializer 2: PlayerCardSerializer
```python
class PlayerCardSerializer(serializers.ModelSerializer):
    card = CardSerializer(read_only=True)  # Nested Card data
    
    class Meta:
        model = PlayerCard
        fields = ['id', 'level', 'copies', 'card', 'acquired_at']
```
**Output:**
```json
{
  "id": "uuid-123",
  "level": 2,
  "copies": 3,
  "card": { /* full card data above */ },
  "acquired_at": "2025-01-15T10:30:00Z"
}
```

### Serializer 3: SavedDeckSerializer
```python
class SavedDeckSerializer(serializers.ModelSerializer):
    card_slot_1 = PlayerCardSerializer(read_only=True)
    card_slot_2 = PlayerCardSerializer(read_only=True)
    # ... all 10 slots with nested data
    
    class Meta:
        model = SavedDeck
        fields = [
            'id', 'deck_name', 'is_active',
            'card_slot_1', ..., 'card_slot_8',
            'legend_stone', 'guardian_card',
            'created_at', 'updated_at'
        ]
```

### Serializer 4: PlayerSerializer
```python
class PlayerSerializer(serializers.ModelSerializer):
    class Meta:
        model = CustomUser
        fields = [
            'id', 'username', 'level', 'rank', 'coins',
            'total_wins', 'total_losses', 'skill_rating'
        ]
```

### Serializer 5: PlayerDetailSerializer
```python
class PlayerDetailSerializer(serializers.ModelSerializer):
    decks = SavedDeckSerializer(
        source='saveddeck_set', many=True, read_only=True
    )
    all_cards = serializers.SerializerMethodField()
    
    def get_all_cards(self, obj):
        player_cards = obj.playercard_set.all()
        return PlayerCardSerializer(player_cards, many=True).data
    
    class Meta:
        model = CustomUser
        fields = [
            'id', 'username', 'level', 'rank', 'coins',
            'total_wins', 'total_losses', 'skill_rating',
            'decks', 'all_cards'
        ]
```

---

## 🔌 API Endpoints (views.py)

```python
class PlayerViewSet(viewsets.ModelViewSet):
    queryset = CustomUser.objects.all()
    serializer_class = PlayerSerializer
    permission_classes = [IsAuthenticated]
    
    @action(detail=False, methods=['post'], permission_classes=[AllowAny])
    def login(self, request):
        """POST /api/players/login/"""
        username = request.data.get('username')
        password = request.data.get('password')
        user = authenticate(username=username, password=password)
        
        if user:
            serializer = PlayerDetailSerializer(user)
            return Response({'success': True, 'user': serializer.data})
        return Response({'error': 'Invalid credentials'}, status=401)
    
    @action(detail=False, methods=['get'])
    def me(self, request):
        """GET /api/players/me/"""
        serializer = PlayerDetailSerializer(request.user)
        return Response(serializer.data)
    
    @action(detail=False, methods=['get'])
    def my_decks(self, request):
        """GET /api/players/my_decks/"""
        decks = SavedDeck.objects.filter(player=request.user)
        serializer = SavedDeckSerializer(decks, many=True)
        return Response(serializer.data)
    
    @action(detail=False, methods=['get'])
    def my_cards(self, request):
        """GET /api/players/my_cards/"""
        cards = PlayerCard.objects.filter(player=request.user)
        serializer = PlayerCardSerializer(cards, many=True)
        return Response(serializer.data)
```

---

## 🌱 Management Commands

### seed_cards.py
```python
# Creates 12 game cards in database
python manage.py seed_cards

# Output:
# Created card: Toxin Ball
# Created card: Vanish Ball
# ...
# Seeding complete! 12 new cards added.
```

### seed_sample_player.py
```python
# Creates test user with full deck and collection
python manage.py seed_sample_player

# Creates:
# - User: testplayer / testpass123
# - Deck: "My First Deck" with 10 slots
# - All 12 cards in collection with varied levels/copies
```

---

## 🔐 Authentication Flow

```
1. Client sends POST /api/players/login/
   └─ Body: {username: "testplayer", password: "testpass123"}

2. Django receives request
   └─ Middleware processes (CORS, Security, etc)

3. PlayerViewSet.login() method executes
   └─ authenticate() hashes password and checks against DB
   └─ If valid, creates session cookie

4. Response sent to client
   └─ JSON: {success: true, user: {id, username, decks, cards, ...}}
   └─ Header: Set-Cookie: sessionid=...

5. Client stores session cookie
   └─ Subsequent requests include: Cookie: sessionid=...

6. On protected endpoints (me, my_decks, my_cards)
   └─ Django SessionAuthentication validates cookie
   └─ request.user is set to authenticated user
   └─ Endpoint executes with user's data
```

---

## 📊 Data Relationships

```
CustomUser (1)
    │
    ├─→ PlayerCard (Many) ─→ Card (Single)
    │       │
    │       └─ has: level, copies
    │
    └─→ SavedDeck (Many)
        └─ contains 10 PlayerCard references:
            - card_slot_1 through card_slot_8
            - legend_stone
            - guardian_card
```

**Example:**
- testplayer (CustomUser)
  - Has 12 PlayerCards (one per card)
    - toxin_ball (level 1, copies 3)
    - vanish_ball (level 1, copies 2)
    - ...
  - Has 1 SavedDeck
    - Slot 1: toxin_ball PlayerCard
    - Slot 2: brave_knight PlayerCard
    - ...
    - Legend: stone_legend PlayerCard
    - Guardian: xod_guardian PlayerCard

---

## 🚀 Startup Process

```
1. Start command:
   python manage.py runserver

2. Django initializes:
   ├─ Load settings.py
   ├─ Initialize all INSTALLED_APPS
   ├─ Apply migrations (create tables if needed)
   ├─ Load middleware stack
   └─ Register URL routes

3. Server listens on:
   http://localhost:8000

4. Routes become available:
   ├─ http://localhost:8000/admin/ (Django admin)
   ├─ http://localhost:8000/api/ (API root)
   ├─ http://localhost:8000/api/players/ (endpoints)
   └─ http://localhost:8000/api/players/login/ (login)
```

---

## 📝 File Relationships

```
urls.py
├─ routes to PlayerViewSet in views.py
└─ uses DefaultRouter for auto-routing

views.py
├─ imports models from models.py
├─ uses serializers from serializers.py
└─ defines endpoints (login, me, my_decks, my_cards)

serializers.py
├─ imports models from models.py
└─ defines how to convert model → JSON

models.py
├─ defines database table structure
├─ defines relationships between tables
└─ uses Django ORM for database queries

settings.py
├─ configures INSTALLED_APPS
├─ configures MIDDLEWARE
├─ configures REST_FRAMEWORK
├─ configures DATABASES
└─ configures CORS

management/commands/
├─ seed_cards.py
│  └─ creates Card objects in database
└─ seed_sample_player.py
   ├─ creates CustomUser object
   ├─ creates PlayerCard objects
   └─ creates SavedDeck object
```

---

## 🔍 Key Concepts

### Django ORM
- Instead of writing SQL, write Python
- Models define tables automatically
- ForeignKey = relationships between tables
- Queries like: `User.objects.filter(username='test')`

### Serializers
- Convert model instances → JSON (for API responses)
- Convert JSON → model instances (for API requests)
- Handle nested relationships
- Validate incoming data

### ViewSets
- Combine list, create, retrieve, update, delete operations
- DefaultRouter auto-generates URLs
- @action decorator creates custom endpoints

### Middleware
- Processed in order for requests
- Processed in reverse order for responses
- Each middleware can modify request/response

### Sessions
- Django creates unique session ID for each user
- Stored as cookie in browser
- Server stores session data (user ID, etc)
- More secure than storing user info in cookie

---

## 📌 Summary

**Backend is a REST API built with:**
- Django: Web framework
- Django REST Framework: API toolkit
- SQLite: Database
- CORS headers: Cross-origin requests
- Sessions: User authentication
- Management commands: Data seeding

**Flow:**
1. Client → HTTP Request → Django Middleware
2. URL Router → ViewSet → Serializer
3. Database ORM → Model queries
4. Response JSON → Client

**Security:**
- Passwords hashed by Django
- Sessions prevent token exposure
- CORS restricts origins
- Middleware adds security headers
- Admin panel for manual data management
