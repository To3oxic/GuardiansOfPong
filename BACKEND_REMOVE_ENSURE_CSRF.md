# Backend Fix: Remove @ensure_csrf_cookie from Login

## Problem
The `@ensure_csrf_cookie` decorator on the login endpoint is causing Django to force-set the CSRF cookie, which when combined with the sessionid cookie, creates malformed Set-Cookie headers that the client can't parse properly.

**Current code (broken):**
```python
@ensure_csrf_cookie  # ❌ THIS IS THE PROBLEM
@csrf_exempt
@require_http_methods(["POST"])
def public_login(request):
```

The `@ensure_csrf_cookie` decorator tells Django "make sure a CSRF cookie is set", which adds extra Set-Cookie handling on top of what the session already does, causing concatenation issues.

## Solution
Simply **remove the `@ensure_csrf_cookie` decorator**. Session-based authentication doesn't need CSRF tokens for subsequent requests since it uses httpOnly cookies.

### File to Modify
`C:\Users\ahmad\GuardiansBackend\players\views.py`

### Change Required
**Find this:**
```python
@ensure_csrf_cookie
@csrf_exempt
@require_http_methods(["POST"])
def public_login(request):
```

**Change to this:**
```python
@csrf_exempt
@require_http_methods(["POST"])
def public_login(request):
```

That's it! Just remove the `@ensure_csrf_cookie` line.

## Why This Works

1. **`@csrf_exempt`** - Tells Django this endpoint doesn't require CSRF protection (correct for public login)
2. **`django_login(request, user)`** - Sets the sessionid cookie (httpOnly, secure)
3. **No `@ensure_csrf_cookie`** - Don't force extra CSRF cookie handling

The client will receive a clean single Set-Cookie header with just the sessionid:
```
Set-Cookie: sessionid=3y91bp06uhdd3nnz9hph8jjrqbij5xdv; Path=/; HttpOnly; SameSite=Lax
```

## Session-Based Authentication is Secure

You mentioned the user is authenticated, so using session-based auth is perfectly fine:
- sessionid cookie is httpOnly (can't be accessed by JavaScript)
- sessionid cookie is automatically validated by Django's SessionMiddleware
- No need for CSRF tokens with session auth (CSRF is mainly for cookie-based CSRF attacks, but httpOnly cookies can't be read by other sites' JavaScript)

## After Change

Test with curl:
```bash
curl -X POST http://localhost:8000/api/players/login/ \
  -H "Content-Type: application/json" \
  -d '{"username":"testplayer","password":"testpass123"}' \
  -v 2>&1 | grep -i "set-cookie"
```

**Expected output** (single, clean header):
```
Set-Cookie: sessionid=3y91bp06uhdd3nnz9hph8jjrqbij5xdv; Path=/; HttpOnly; SameSite=Lax
```

NOT multiple confusing headers.

## Summary

Remove the line: `@ensure_csrf_cookie`

The login endpoint only needs:
- `@csrf_exempt` (it's public)
- `@require_http_methods(["POST"])` (only POST allowed)
- `django_login(request, user)` inside (sets sessionid)
- Return Response with user data

That's all you need!
