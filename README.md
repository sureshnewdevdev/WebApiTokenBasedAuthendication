# Refresh Token Flow – How to Test

This project exposes a JWT authentication endpoint and a refresh-token endpoint.
Use this guide to verify the refresh token functionality end to end.

## Prerequisites

- .NET SDK (same major version used by the project)
- `curl` (or Postman/Insomnia)

## Run the API

From the repository root:

```bash
dotnet run --project WebApiTokenBasedAuthendication/WebApiTokenBasedAuthendication.csproj
```

By default, the app is configured with:

- Issuer: `https://localhost:7110/`
- Audience: `WebApiTokenBasedAuthendication`

Swagger UI is enabled in Development.

## Endpoints used in this test

- `POST /api/Authendication/authendicate`
  - Returns `accessToken`, `refreshToken`, and expiry timestamps.
- `POST /api/Authendication/refresh`
  - Accepts `refreshToken` and returns a **new** access token + refresh token pair.
- `GET /api/People`
  - Protected endpoint. Requires a valid bearer access token.

> Note: The sample credential validation currently accepts any username/password and maps to a demo user.

---

## Step-by-step test (using curl)

### 1) Authenticate and get initial tokens

```bash
curl -k -X POST "https://localhost:7110/api/Authendication/authendicate" \
  -H "Content-Type: application/json" \
  -d '{"userName":"demo","password":"demo"}'
```

Expected response shape:

```json
{
  "accessToken": "<jwt>",
  "refreshToken": "<refresh-token>",
  "accessTokenExpiryUtc": "<utc-date>",
  "refreshTokenExpiryUtc": "<utc-date>"
}
```

Copy both `accessToken` and `refreshToken`.

### 2) Call protected API with access token

```bash
curl -k "https://localhost:7110/api/People" \
  -H "Authorization: Bearer <accessToken>"
```

Expected: HTTP `200 OK` with people text.

### 3) Use refresh token to get new token pair

```bash
curl -k -X POST "https://localhost:7110/api/Authendication/refresh" \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<refreshToken>"}'
```

Expected:

- HTTP `200 OK`
- New `accessToken`
- New `refreshToken`

### 4) Confirm refresh token rotation (single-use token)

Call the refresh endpoint **again** with the same old refresh token:

```bash
curl -k -X POST "https://localhost:7110/api/Authendication/refresh" \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<oldRefreshToken>"}'
```

Expected: HTTP `401 Unauthorized` with `Invalid refresh token.`

This confirms the old refresh token was invalidated when used.

### 5) Call protected endpoint with the new access token

```bash
curl -k "https://localhost:7110/api/People" \
  -H "Authorization: Bearer <newAccessToken>"
```

Expected: HTTP `200 OK`.

---

## Negative test cases

1. **Missing refresh token in request body**

```bash
curl -k -X POST "https://localhost:7110/api/Authendication/refresh" \
  -H "Content-Type: application/json" \
  -d '{}'
```

Expected: HTTP `400 Bad Request` with `Refresh token is required.`

2. **Invalid refresh token value**

```bash
curl -k -X POST "https://localhost:7110/api/Authendication/refresh" \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"not-a-valid-token"}'
```

Expected: HTTP `401 Unauthorized` with `Invalid refresh token.`

## Notes about current implementation

- Access token lifetime: **15 minutes**.
- Refresh token lifetime: **7 days**.
- Refresh tokens are stored in-memory (`ConcurrentDictionary`), so they are lost when the app restarts.

