# Backend Fix: Set-Cookie Header Format

## Problem
The Set-Cookie header is being malformed when multiple cookies are set. The csrftoken value is including the sessionid, making it invalid.

**Current (broken) output:**
```
Cookies: {
  'sessionid': '3y91bp06uhdd3nnz9hph8jjrqbij5xdv',
  'csrftoken': 'd00ECMXuBVTot3imoMrFyY3jbXxEMcIA,sessionid=3y91bp06uhdd3nnz9hph8jjrqbij5xdv'
}
```

Notice the csrftoken value contains a comma and the sessionid - this is malformed!

## Root Cause
The login endpoint is likely manually constructing or modifying the Set-Cookie header instead of letting Django's middleware handle it automatically.

## Solution
**DO NOT manually set cookies.** Let Django handle it automatically:

### File to Modify
`C:\Users\ahmad\GuardiansBackend\players\views.py`

### Current (Broken) Login Implementation
```python
@csrf_exempt
@action(detail=False, methods=['post', 'get'], permission_classes=[AllowAny])
def login(self, request):
    if request.method == 'GET':
        return Response({"message": "CSRF token endpoint"}, status=200)
    
    # ... authentication logic ...
    
    # WRONG: Don't manually add or modify cookies!
    response = Response({...})
    response['Set-Cookie'] = ...  # ❌ BAD
    return response
```

### Correct Implementation
```python
from django.contrib.auth import authenticate, login as django_login

@csrf_exempt
@action(detail=False, methods=['post'], permission_classes=[AllowAny])
def login(self, request):
    """
    Public login endpoint - CSRF exempt
    
    Django automatically handles ALL cookies:
    - CSRF middleware sets csrftoken cookie
    - django_login() sets sessionid cookie
    - Both are set in response headers automatically
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
        # THIS LINE IS KEY: django_login() sets the sessionid cookie
        django_login(request, user)
        
        # IMPORTANT: DO NOT modify or set cookies manually
        # Django middleware will automatically add:
        # - csrftoken cookie (from CsrfViewMiddleware)
        # - sessionid cookie (from django_login)
        
        serializer = self.get_serializer(user)
        return Response({
            "success": True,
            "message": "Login successful",
            "user": serializer.data
        }, status=200)
        
        # The response will automatically include proper Set-Cookie headers:
        # Set-Cookie: csrftoken=xxx; Path=/; SameSite=Lax
        # Set-Cookie: sessionid=yyy; Path=/; HttpOnly; SameSite=Lax
        
    else:
        return Response({
            "success": False,
            "message": "Invalid credentials"
        }, status=401)
```

## Key Points

1. **Never manually construct Set-Cookie headers** - Django middleware handles this
2. **Call `django_login(request, user)`** - This triggers the session framework to set sessionid
3. **CSRF middleware automatically sets csrftoken** - No manual intervention needed
4. **Return a normal Response** - Django will add the proper headers automatically
5. **Don't modify `response['Set-Cookie']`** - This breaks the automatic handling

## How Django Automatically Handles Cookies

When you return a Response from a view:

1. **CsrfViewMiddleware** (in middleware stack):
   - Checks if view has @csrf_exempt (login does)
   - Even with @csrf_exempt, it may set csrftoken cookie for the client
   - Adds to response: `Set-Cookie: csrftoken=...`

2. **SessionMiddleware** (in middleware stack):
   - After django_login() is called, sets sessionid
   - Adds to response: `Set-Cookie: sessionid=...`

3. **Response** is returned with both cookies in separate Set-Cookie headers

## Expected Output After Fix

```
Set-Cookie: csrftoken=d00ECMXuBVTot3imoMrFyY3jbXxEMcIA; Path=/; SameSite=Lax
Set-Cookie: sessionid=3y91bp06uhdd3nnz9hph8jjrqbij5xdv; Path=/; HttpOnly; SameSite=Lax
```

These are **separate headers**, not concatenated in one value!

## Testing

After fix, test with curl:
```bash
curl -X POST http://localhost:8000/api/players/login/ \
  -H "Content-Type: application/json" \
  -d '{"username":"testplayer","password":"testpass123"}' \
  -v 2>&1 | grep -i "set-cookie"
```

**Expected output:**
```
Set-Cookie: csrftoken=...; Path=/; SameSite=Lax
Set-Cookie: sessionid=...; Path=/; HttpOnly; SameSite=Lax
```

NOT:
```
Set-Cookie: csrftoken=...,sessionid=...
```

---

## Summary

Remove any manual cookie handling from the login endpoint. Let Django's middleware do it automatically. The only line needed for cookies is: `django_login(request, user)`
