# Backend Fix: Disable CSRF for Login Endpoint

## Problem
The `/api/players/login/` endpoint should be PUBLIC and NOT require CSRF protection. Currently it's rejecting requests with "CSRF token missing" errors.

## Solution: Exempt Login from CSRF Protection

The login endpoint needs to be decorated with `@csrf_exempt` to disable CSRF protection for that specific view.

### File to Modify
`C:\Users\ahmad\GuardiansBackend\players\views.py`

### Implementation

Add the import at the top of the file:
```python
from django.views.decorators.csrf import csrf_exempt
```

Then decorate the login action method:
```python
@csrf_exempt  # ADD THIS LINE - Disable CSRF for public login endpoint
@action(detail=False, methods=['post', 'get'], permission_classes=[AllowAny])
def login(self, request):
    """
    Public login endpoint - no CSRF required
    Accepts GET (to fetch CSRF token) and POST (to submit credentials)
    """
    
    # For GET requests, just return empty response (client will get CSRF cookie)
    if request.method == 'GET':
        return Response({
            "message": "CSRF token endpoint - GET to fetch token"
        }, status=200)
    
    # For POST requests, authenticate and login
    if request.method == 'POST':
        username = request.data.get('username')
        password = request.data.get('password')
        
        if not username or not password:
            return Response({
                "success": False,
                "message": "Username and password required"
            }, status=400)
        
        # Authenticate user
        from django.contrib.auth import authenticate, login as django_login
        user = authenticate(request, username=username, password=password)
        
        if user is not None:
            # Call django_login to set the session
            django_login(request, user)
            
            # Get serializer and return user data
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

### Why This Works

1. **`@csrf_exempt`** decorator tells Django to skip CSRF protection for this view
2. **Login is public** - no authentication required, so CSRF protection is unnecessary
3. **GET request** fetches the CSRF token (which is still set in Set-Cookie)
4. **POST request** uses that token for subsequent authenticated requests
5. **All other endpoints** still have CSRF protection for security

### Important Notes

- The `@csrf_exempt` decorator should be placed **BEFORE** the `@action` decorator
- Make sure you have this import: `from django.views.decorators.csrf import csrf_exempt`
- This only exempts the login endpoint - all other POST endpoints will still require CSRF
- The CSRF token obtained from the GET request will be used for authenticated endpoints like `/api/players/save-deck/`

### Testing After Fix

```bash
# GET request (no CSRF needed)
curl -X GET http://localhost:8000/api/players/login/ -v

# Should see Set-Cookie with csrftoken in response

# POST request (CSRF exempted for login)
curl -X POST http://localhost:8000/api/players/login/ \
  -H "Content-Type: application/json" \
  -d '{"username":"testplayer","password":"testpass123"}' \
  -v

# Should see Set-Cookie with sessionid in response
```

### Client-Side Update

Once this is fixed, the Unity client will:
1. GET `/api/players/login/` → receive csrftoken
2. POST `/api/players/login/` → authenticate and receive sessionid
3. Use both tokens for authenticated requests to `/api/players/save-deck/` etc.

---

## Summary

Add `@csrf_exempt` decorator to the login endpoint to make it public and not require CSRF protection.
