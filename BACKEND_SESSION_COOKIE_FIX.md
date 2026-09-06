# Backend Session Cookie Fix - URGENT

## Problem
The `/api/players/login/` endpoint is NOT sending the `Set-Cookie` header in the response, so the client cannot authenticate subsequent requests.

**Current Behavior:**
- Client logs in successfully with credentials
- Server returns player data with `"success": true`
- **BUT** server does NOT send `Set-Cookie` header
- Client has no way to authenticate for subsequent requests (e.g., `/api/players/save-deck/`)
- Result: All authenticated endpoints return 403 "Authentication credentials were not provided"

**Error from Client:**
```
[BackendManager LOGIN] ✗ No Set-Cookie header in login response!
```

---

## Root Cause
The login view is likely using `rest_framework.response.Response()` which doesn't automatically set session cookies. Django REST Framework's session authentication requires:
1. The view must explicitly set session cookies
2. The response must include proper headers

---

## Solution: Fix the Login Endpoint

### File to Modify
`C:\Users\ahmad\GuardiansBackend\players\views.py`

### Current Problem Code
Your current login endpoint probably looks like:
```python
@action(detail=False, methods=['post'], permission_classes=[AllowAny])
def login(self, request):
    # ... login logic ...
    return Response({
        "success": True,
        "user": serializer.data
    })
```

This does NOT set session cookies properly for Django REST Framework.

### CORRECT Implementation Required

```python
from django.contrib.auth import authenticate, login as django_login
from rest_framework.response import Response
from rest_framework.decorators import action, permission_classes
from rest_framework.permissions import AllowAny

@action(detail=False, methods=['post'], permission_classes=[AllowAny])
def login(self, request):
    """
    Login endpoint that properly sets Django session cookies
    POST /api/players/login/
    """
    username = request.data.get('username')
    password = request.data.get('password')
    
    if not username or not password:
        return Response({
            "success": False,
            "message": "Username and password required"
        }, status=400)
    
    # Authenticate user
    user = authenticate(request, username=username, password=password)
    
    if user is not None:
        # IMPORTANT: Call django_login to set the session
        django_login(request, user)
        
        # After django_login, the session cookie will be set in the response
        serializer = self.get_serializer(user)
        return Response({
            "success": True,
            "message": "Login successful",
            "user": serializer.data
        }, status=200)
    else:
        return Response({
            "success": False,
            "message": "Invalid username or password"
        }, status=401)
```

### Key Changes:
1. **Import** `authenticate` and `django_login` from `django.contrib.auth`
2. **Call** `django_login(request, user)` - **THIS IS CRITICAL**
   - This is what sets the session cookie
   - Without this, no Set-Cookie header is sent
3. **Return** proper success/error responses

### Why This Works:
- `django_login()` internally calls Django's session framework
- Session framework automatically sets the `Set-Cookie` header in the response
- The cookie contains the `sessionid` that clients need for authenticated requests
- Subsequent requests with this cookie will be authenticated

---

## Verification Steps

### 1. Test with cURL (after implementing fix)
```bash
# Save cookies from login
curl -X POST http://localhost:8000/api/players/login/ \
  -H "Content-Type: application/json" \
  -d '{"username":"testplayer","password":"testpass123"}' \
  -v
```

**Expected output** should show:
```
< Set-Cookie: sessionid=abc123def456; Path=/; HttpOnly; SameSite=Lax
```

### 2. Test authenticated endpoint
```bash
# Use the sessionid from above
curl -X GET http://localhost:8000/api/players/me/ \
  -H "Cookie: sessionid=abc123def456"
```

**Expected:** 200 OK with player data

### 3. Test save-deck endpoint
```bash
curl -X POST http://localhost:8000/api/players/save-deck/ \
  -H "Content-Type: application/json" \
  -H "Cookie: sessionid=abc123def456" \
  -d '{
    "deck_name": "Test Deck",
    "is_active": true,
    "card_ids": ["toxin_ball", "brave_knight", "angry_bear", "vanish_ball", "lightning_ball", "angry_bear", "brave_knight", "lightning_ball"],
    "guardian_id": "boulder_guardian",
    "legend_stone_id": "stone_legend"
  }'
```

**Expected:** 201 Created with deck data

---

## Important Notes

1. **Must call `django_login(request, user)`** - this is what sets the cookie
2. **Session must be enabled in settings.py** (should already be, check):
   ```python
   MIDDLEWARE = [
       # ...
       'django.contrib.sessions.middleware.SessionMiddleware',  # MUST be present
       'django.middleware.csrf.CsrfViewMiddleware',
       # ...
   ]
   
   INSTALLED_APPS = [
       # ...
       'django.contrib.sessions',  # MUST be present
       # ...
   ]
   ```

3. **If CORS is enabled**, verify CORS settings allow credentials:
   ```python
   CORS_ALLOW_CREDENTIALS = True
   ```

4. **Test immediately after changes** - don't wait, verify the Set-Cookie header is present

---

## After Implementation

Once you've made this change:
1. Restart Django server
2. Run the cURL test above to verify Set-Cookie header
3. Share the output so we can confirm it's working
4. The Unity client will then be able to authenticate successfully

---

## Summary

**What to do:**
1. Open `C:\Users\ahmad\GuardiansBackend\players\views.py`
2. Find the `login` method in `PlayerViewSet`
3. Import: `from django.contrib.auth import authenticate, login as django_login`
4. Update login method to call `django_login(request, user)` after authentication
5. Restart Django server
6. Test with cURL to verify `Set-Cookie` header is present

**Result:** Once Set-Cookie header is sent, the Unity client will automatically capture the sessionid and use it for authenticated requests.
