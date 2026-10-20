# Security Guide — SocialButterfly

This document explains every attack class we considered for SocialButterfly, how each one works, and exactly how the application prevents it. Each section points to the code that implements the protection and the tests that prove it.

> **Scope:** ASP.NET Core API (`Backend/`), React frontend (`Frontend/`), Azure Functions worker (`Backend/SocialMediaWeb.ChatFunctions/`), and the local Azure stack (Azurite Blob Storage, Service Bus emulator, SQL Server) run via `docker-compose.yml`.

---

## Contents

1. [Security architecture at a glance](#1-security-architecture-at-a-glance)
2. [Session theft via XSS → HttpOnly cookie sessions](#2-session-theft-via-xss--httponly-cookie-sessions)
3. [Cross-Site Request Forgery (CSRF)](#3-cross-site-request-forgery-csrf)
4. [Cross-origin access (CORS)](#4-cross-origin-access-cors)
5. [Cross-Site Scripting (XSS)](#5-cross-site-scripting-xss)
6. [SQL injection](#6-sql-injection)
7. [Broken access control / IDOR](#7-broken-access-control--idor)
8. [Privilege escalation (self-registering as Admin)](#8-privilege-escalation-self-registering-as-admin)
9. [Sensitive data exposure](#9-sensitive-data-exposure)
10. [Brute force and credential stuffing](#10-brute-force-and-credential-stuffing)
11. [Account enumeration](#11-account-enumeration)
12. [One-time code (OTP) attacks](#12-one-time-code-otp-attacks)
13. [Malicious file uploads](#13-malicious-file-uploads)
14. [Clickjacking](#14-clickjacking)
15. [Token forgery and weak secrets](#15-token-forgery-and-weak-secrets)
16. [Information leakage through errors](#16-information-leakage-through-errors)
17. [Search abuse (LIKE wildcard injection)](#17-search-abuse-like-wildcard-injection)
18. [Message queue abuse (Service Bus / Functions)](#18-message-queue-abuse-service-bus--functions)
19. [Secrets and data at rest](#19-secrets-and-data-at-rest)
20. [How to verify everything](#20-how-to-verify-everything)
21. [Known limitations and production checklist](#21-known-limitations-and-production-checklist)

---

## 1. Security architecture at a glance

Every request to the API goes through these layers, in this order (`Backend/Program.cs`):

```
Browser ──► Exception handler        generic errors only, no stack traces
         ──► Security headers         CSP, frame blocking, nosniff, …
         ──► CORS                     only our frontend origins
         ──► CSRF protection          Origin check + double-submit token
         ──► Authentication           JWT read from the HttpOnly cookie
         ──► Authorization            deny-by-default; Admin role for admin APIs
         ──► Rate limiting            login / code endpoints
         ──► Controller               identity taken from the token only
         ──► Service / EF Core        parameterized queries, validation
```

Core principles:

| Principle | What it means here |
|---|---|
| **Deny by default** | Every endpoint requires login unless it is explicitly marked `[AllowAnonymous]`. |
| **Identity from the token only** | The server never trusts a `userId` sent by the browser to decide *who* is acting. |
| **Least exposure** | Responses are DTOs; password hashes, emails, and tokens never reach the browser. |
| **Defence in depth** | For example, CSRF is stopped by the `SameSite` cookie *and* an Origin check *and* a CSRF token. |

---

## 2. Session theft via XSS → HttpOnly cookie sessions

### The attack
If a login token is stored where JavaScript can read it (`localStorage`, `sessionStorage`, a normal cookie), then any script that runs on the page can steal it. That includes an injected script (XSS), a compromised npm package, or a malicious browser extension. With the token, the attacker becomes the user until it expires.

### How we prevent it
The JWT now lives **only** in an `HttpOnly` cookie, which JavaScript cannot read at all.

| Cookie attribute | Value | Why |
|---|---|---|
| `HttpOnly` | yes | Page scripts cannot read the session token. |
| `SameSite` | `Strict` | The browser never attaches it to requests started by other sites. |
| `Secure` | yes outside local HTTP development | Sent only over HTTPS in production. |
| `Path` | `/` | — |
| `Expires` | the token's own expiry | The cookie and the token expire together. |

- **Login** (`POST /api/Account/login`) and **code login** (`POST /api/Account/code/confirm`) set the cookie and **remove the token from the JSON body**.
- **`GET /api/Account/session`** tells the app who is signed in, based on the cookie. The frontend calls it before the first render (`Frontend/src/index.js` → `bootstrapSession()`).
- **`POST /api/Account/logout`** clears the cookie on the server. JavaScript can't clear an `HttpOnly` cookie.
- The API reads the token from the cookie in `JwtBearerEvents.OnMessageReceived` (`Program.cs`). A `Bearer` header is still accepted for API tools such as Swagger.
- `sessionStorage` now holds only **non-secret display data** (id, name, avatar, role) for rendering. Editing those values grants nothing, because the server re-checks the cookie on every request. Old builds stored the JWT in `sessionStorage` as `tokens`; startup deletes that key.

**Code:** `Backend/Security/AuthCookies.cs`, `Backend/Controllers/AccountController.cs` (`SignIn`, `Session`, `Logout`), `Frontend/src/utils/session.js`, `Frontend/src/utils/api.js`

> **Important:** the browser must reach the API on the **same origin** as the page (`/api/...`). In development the Vite proxy does this; in Docker the app (or nginx) serves both. Calling the API cross-origin would break the `SameSite=Strict` cookie, by design.

---

## 3. Cross-Site Request Forgery (CSRF)

### The attack
Browsers attach cookies automatically. If you are logged in and visit `evil.example`, that page can submit a hidden form or `fetch()` to our API, and your browser would send your session cookie with it. Without protection, the attacker could make you like, post, follow, delete posts, or change settings.

Moving sessions into a cookie (section 2) is exactly what makes CSRF possible, so it needs its own layered defence.

### How we prevent it (three independent layers)

**Layer 1: `SameSite=Strict` session cookie.**
Modern browsers do not send the session cookie on requests that start from another site.

**Layer 2: Origin / Referer check.**
For every state-changing request (`POST`, `PUT`, `DELETE`, `PATCH`), `CsrfProtectionMiddleware` reads the `Origin` header (or the `Referer`). It rejects the request with `403` unless the origin is ours: either the same origin as the API or one listed in `Security:AllowedOrigins`. `Origin: null` (sandboxed iframes, `file://` pages) is rejected too.

**Layer 3: Double-submit CSRF token.**
- The API issues a random 256-bit `XSRF-TOKEN` cookie on the first API call. It is readable by our own page and is `SameSite=Strict`.
- axios copies it into the `X-XSRF-TOKEN` header on every request (`Frontend/src/utils/api.js`).
- The middleware compares the cookie and the header with a **constant-time** comparison and rejects any mismatch.
- Another site cannot read our cookies, so it cannot produce the matching header.

This also blocks **login CSRF**, where an attacker silently logs the victim into the attacker's account: the anonymous login, register and code endpoints require the token too.

Requests that authenticate with an explicit `Authorization: Bearer` header skip only the token check, because browsers never add that header automatically. They still go through the Origin check.

**Code:** `Backend/Security/CsrfProtectionMiddleware.cs`
**Tests:** `CsrfProtectionTests` in `Backend/tests/SocialMediaWeb.Tests/SecurityHardeningTests.cs`

| Scenario | Result |
|---|---|
| POST with session cookie but no CSRF header | `403` |
| POST with a guessed or wrong CSRF header | `403` |
| POST from `https://evil.example`, even with a valid token | `403` |
| Hidden HTML form from another site | `403` |
| POST with `Origin: null` | `403` |
| Normal request from our frontend | `200` |

---

## 4. Cross-origin access (CORS)

### The attack
With a permissive CORS policy (the old `AllowAnyOrigin()`), any website could call the API from a visitor's browser and read the responses.

### How we prevent it
- CORS allows **only the configured frontend origins** (`Security:AllowedOrigins` in `Backend/appsettings.json`), with credentials.
  - Default list: `http://localhost:8080`, `http://127.0.0.1:8080`, `http://localhost:5173`, `http://127.0.0.1:5173`.
  - Override per environment with `Security__AllowedOrigins__0=https://your-domain`, etc.
- Only the methods and headers we use are allowed (`GET POST PUT DELETE`; `Content-Type`, `X-XSRF-TOKEN`).
- A preflight from an unknown origin gets **no** `Access-Control-Allow-Origin` header, so the browser blocks the call.

> **Honest limitation:** CORS is enforced by **browsers**. Nothing can stop someone calling the API with `curl` or a script. Those clients still need a valid session, and every check in sections 6–13 still applies to them. CORS stops *other websites* from using a visitor's browser against us.

**Code:** `Backend/Program.cs` (CORS policy `ReactJsDomain`)

---

## 5. Cross-Site Scripting (XSS)

### The attack
An attacker stores something like `<img src=x onerror=alert(document.cookie)>` in a comment, caption, bio or username. If the app inserts it into the page as HTML, the script runs in every viewer's browser. It can steal sessions, act as the user, or deface the page.

### How we prevent it

| Layer | Protection |
|---|---|
| **Output encoding** | Every piece of user content is rendered through React, which escapes text automatically. The codebase has **no** `dangerouslySetInnerHTML`, `innerHTML`, `eval` or `new Function` (audited). |
| **API responses are data** | The API returns `application/json`, never HTML built from user input. The stored payload above comes back as inert text. |
| **Content-Security-Policy** | Pages are served with a strict CSP (see below). Even if markup were injected, inline scripts and scripts from other domains would not run. |
| **No token to steal** | The session cookie is `HttpOnly` (section 2), so `document.cookie` cannot reveal it. |
| **Upload type control** | HTML and SVG cannot be uploaded as media, and the server sets each file's content type from its validated extension (section 13). Uploads therefore cannot become script-bearing pages. |
| **`nosniff`** | `X-Content-Type-Options: nosniff` stops browsers guessing a file is HTML or script. |

**Content-Security-Policy for app pages** (`Backend/Security/SecurityHeadersMiddleware.cs` and `Frontend/nginx.conf`):

```
default-src 'self';
script-src 'self' https://maps.googleapis.com;
style-src 'self' 'unsafe-inline' https://fonts.googleapis.com;
font-src 'self' data: https://fonts.gstatic.com;
img-src 'self' data: blob: <blob-storage-origin> https://*.googleapis.com https://*.gstatic.com;
media-src 'self' blob: <blob-storage-origin>;
connect-src 'self' https://api.iconify.design https://api.simplesvg.com https://api.unisvg.com https://maps.googleapis.com;
object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'
```

API responses get the strictest possible policy: `default-src 'none'; frame-ancestors 'none'`.

> `'unsafe-inline'` is allowed for **styles only**, because animations and components set inline styles. Scripts stay `'self'`-only.
> The Vite **dev** server sends the other hardening headers but not the CSP, because hot reload needs inline scripts. The CSP applies when the app is served by the API or nginx.

---

## 6. SQL injection

### The attack
If user input is pasted into SQL text, input like `' OR 1=1--` or `'; DROP TABLE Users;--` changes the query itself. That can dump data, bypass login or destroy tables.

### How we prevent it
- **All database access goes through Entity Framework Core LINQ**, which always sends values as **parameters**. The audit found no `FromSqlRaw`, `ExecuteSqlRaw` or string-built SQL in the codebase.
- The only raw SQL is a constant statement in a migration (`UPDATE [Users] SET [Username] = CONCAT('user', [Id]) …`) with no user input.
- Input lengths are bounded by DTO and model limits, and search terms are capped at 100 characters (section 17).

**Live test results** (`GET /api/Follows/people?q=…`):

| Payload | Result |
|---|---|
| `' OR 1=1--` | Treated as literal text → 0 results |
| `'; DROP TABLE Users;--` | Literal text → 0 results; `Users` table intact |
| `%' UNION SELECT PasswordHash FROM Users--` | Literal text → 0 results; no hash in the response |

**Tests:** `InputHardeningTests.SearchText_EscapesLikeWildcards` (includes the `DROP TABLE` payload)

---

## 7. Broken access control / IDOR

### The attack
*Insecure Direct Object Reference*: the attacker changes an ID in a request, e.g. `?userId=2002` or `PUT /api/Account` with `Id=2002`, to act as or modify another user.

Previously the API trusted these client-supplied IDs everywhere. Anyone could like, post, follow, comment, delete or edit settings **as someone else**.

### How we prevent it
- **Deny by default:** a global `FallbackPolicy` requires an authenticated user on every endpoint (`Program.cs`). Only these are anonymous: `login`, `register`, `availability`, `code/send`, `code/confirm`, `session`, `logout`.
- **Identity comes from the token:** controllers call `User.RequireUserId()` (`Backend/Services/Classes/CurrentUser.cs`) and ignore IDs in the URL or body for *who is acting*. Examples:
  - Likes, follows, stories and notifications act on the signed-in user (`/api/Notifications/me`, `/api/Account/me/settings`, …).
  - New posts, comments and friend requests set `UserId` / `SenderId` from the token, whatever the request body says.
- **Ownership checks:**
  - Only the author can delete a post or a story.
  - Only the author can see a story's viewers.
  - Only the receiver can accept a friend request.
  - Only one side of a friendship can remove it.
  - The legacy `PUT /api/Account` requires `Id == current user` or Admin.
- **Admin APIs** require the `Admin` role: `[Authorize(Roles = "Admin")]` on `AdminController`.

**Tests:** in `SecurityTests.cs`:
- `OnlySignInEndpoints_AllowAnonymousAccess` fails if anyone adds a new anonymous endpoint by accident.
- `NoAction_AcceptsCallerIdentityFromTheClient` fails if an action accepts `viewerId`, `followerId`, `currentUserId`, or `userId` from the query string.
- `AdminController_RequiresAdminRole`

---

## 8. Privilege escalation (self-registering as Admin)

### The attack
Registration used to honour a `Role` field from the request, so anyone could sign up with `Role=Admin` and take over the admin dashboard.

### How we prevent it
- Self-registration **always** creates `Role = "User"`; any requested role is ignored (`UserService.RegisterWithResultAsync`).
- Admins are provisioned from server configuration only (`Backend/Services/Classes/AdminSeeder.cs`):
  - Set `ADMIN_EMAIL` and `ADMIN_PASSWORD` in `.env`.
  - On startup the account is created, or promoted if it already exists.

**Tests:** `Register_IgnoresRequestedAdminRole`, `AdminSeeder_CreatesAdmin_AndPromotesExistingAccount`, `AdminSeeder_WithoutConfiguration_DoesNothing`

---

## 9. Sensitive data exposure

### The attack
Several endpoints serialized whole database entities, sending password hashes and emails to the browser.

The affected endpoints were `GET /api/Account/{id}`, the legacy `PUT /api/Account`, `GET /api/Post/UserPost`, `GET /api/Post/{id}`, and comments with an included user. Leaked hashes can be cracked offline; leaked emails enable spam and phishing.

### How we prevent it
- All responses use **DTOs** that contain only display-safe fields: `PostFeedDto`, `PostAuthorDto`, `CommentResponseDto`, `UserProfileDto`, `UserListItemDto`, `UserSettingsDto`.
- **Emails and birthdays** are removed from `GET /api/Account/Get` and from search results for everyone except Admins.
- **Your own** email and phone number are only returned by `/api/Account/me/settings`.
- **Tokens never appear in JSON** (section 2).
- Verification codes are stored only as **SHA-256 hashes** (`VerificationCode.CodeHash`), and passwords as **BCrypt** hashes.

**Live check:** ten endpoints were called with a real session. None contained `PasswordHash`, and none contained an email.

---

## 10. Brute force and credential stuffing

### The attack
Automated guessing of passwords or 6-digit codes, or replaying leaked email/password pairs from other sites.

### How we prevent it
- **Rate limiting per client IP** (`Program.cs`, ASP.NET Core rate limiter):

  | Policy | Endpoints | Limit |
  |---|---|---|
  | `auth` | `login`, `register`, `me/password` | 10 requests / minute |
  | `codes` | `code/send`, `code/confirm` | 8 requests / minute |

  Excess requests get `429 Too Many Requests`. Live test: the 9th wrong password in a minute was refused.
- **Strong password minimum** of 8 characters, enforced by the server (`IdentityRules.MinimumPasswordLength`), with a strength meter in the sign-up UI.
- **BCrypt** password hashing with per-user salts.
- Login failures return the same message whether the account exists or not.

### Distributed bots (botnets) without hurting real users

Per-IP limits alone don't stop a botnet: thousands of IPs can each stay under 10 tries a minute. So sign-in endpoints (`login`, `register`, `code/send`, `code/confirm`) also go through a **risk-based bot gate**.

**Code:** `Backend/Security/BotDefense.cs`, `BotGate.cs`, `ProofOfWork.cs`; `Frontend/src/utils/powWorker.js`, `botCheck.js`

**Signals tracked over a 15-minute sliding window:**

| Signal | Why it matters |
|---|---|
| Failures per IP (only failures count) | One machine guessing |
| Failures per **account**, from any IP | A botnet focusing on one victim from many IPs |
| Distinct usernames tried from one IP (more than 5) | Credential stuffing with leaked lists |
| Honeypot hit (hidden `website` field filled in) | Only form-filling bots see the field |
| **Global** failures across the site (100 or more in 5 minutes) | A large distributed attack: "attack mode" |

**Response: a proof-of-work challenge, not a lockout.** When the risk is above zero, the API answers `428` with a signed challenge. The browser's Web Worker finds a nonce so that `SHA-256(challenge:nonce)` starts with *N* zero bits, then retries automatically while showing "Checking you're human…". The server checks that the proof:
- carries a valid HMAC signature;
- has not expired (5 minutes);
- belongs to the right endpoint;
- meets the required difficulty;
- has not been used before (single use).

| Situation | Difficulty | Cost per attempt (browser) |
|---|---|---|
| Normal user, including the first 3 typos | 0: no challenge | 0 |
| Attack mode (site-wide surge) | 16 bits | ~0.05–0.2 s |
| Repeated failures on an IP or account | 17 → 22 bits, growing with each failure | up to ~2–4 s |
| Honeypot-flagged IP | 22 bits | ~2–4 s |

**Why this protects genuine users:**
- **No hard lockout.** Locking an account after N failures would let an attacker lock the real owner out. Here the owner can always sign in; it just costs their browser a moment of computation.
- **Only failures raise the score,** and a successful sign-in resets the history for that account and IP.
- **Invisible to people:** no CAPTCHA, no third-party scripts, no tracking.
- **Expensive for bots at scale:** each guess costs CPU time, and the cost rises the more a bot fails.

**Real client IPs behind proxies:** set `Security:TrustedProxies` to your load balancer or proxy IPs. Only then is `X-Forwarded-For` honoured, so bots can't spoof their IP and every user behind the proxy doesn't share one IP.

**Tests:** `BotDefenseTests` and `ProofOfWorkTests` in `Backend/tests/SocialMediaWeb.Tests/BotDefenseTests.cs`. They cover typos never challenging, escalation, distributed attacks on one account, success resets, credential stuffing, honeypot, attack-mode expiry, and replayed, tampered, cross-scope, too-easy and foreign-key proofs.

> **Scaling note:** bot scores are kept in memory, so they are per API instance. With several instances, back `BotDefense` with a distributed cache (e.g. Azure Cache for Redis). For very large attacks, add an edge WAF / Azure Front Door bot protection in front.

---

## 11. Account enumeration

### The attack
Responses that differ between "no such account" and "account exists" let attackers find out which emails or phone numbers are registered. That list then feeds phishing or credential stuffing.

### How we prevent it
- **Password login** returns one generic message for a wrong password and for an unknown user.
- **Passwordless login and password reset** (`code/send` with purpose `Login`/`Reset`) answer **exactly as if a code was sent** when the account doesn't exist, with the same response shape and no code issued (`VerificationService.SendCodeAsync`).
- These endpoints are also rate-limited (section 10).

**Tests:** `LoginCode_ForUnknownAccount_LooksLikeSuccess_AndSendsNothing`

> Residual by design: `GET /api/Account/availability` tells sign-up whether a username, email or phone is free. Every social network needs this to work. It is rate-limited at the network level and returns only a yes/no answer.

---

## 12. One-time code (OTP) attacks

### The attack
Guessing 6-digit codes, reusing an old code, or timing side channels during code comparison.

### How we prevent it (`Backend/Services/Classes/VerificationService.cs`)

| Control | Value |
|---|---|
| Code generation | Cryptographically secure (`RandomNumberGenerator`) |
| Storage | SHA-256 hash of target + code; the plain code is never stored |
| Lifetime | 10 minutes |
| Attempts | Maximum 5 wrong tries, then the code is burned |
| Single use | A code is marked consumed on success; reuse fails |
| Superseding | A new code invalidates any previous active code |
| Resend cooldown | 30 seconds |
| Hourly cap | 5 codes per target per hour |
| Comparison | `CryptographicOperations.FixedTimeEquals` (constant time) |
| Rate limit | 8 requests / minute / IP (section 10) |

Codes are delivered asynchronously: API → Service Bus queue `verification-codes` → Azure Function `SendVerificationCode` → sender. The Function drops codes that have already expired in the queue.

> **Development only:** the API includes `devCode` in the response, and the local `LoggingVerificationSender` writes codes to the Functions log, so the flow can be tested without an email/SMS provider. `devCode` is returned only when `ASPNETCORE_ENVIRONMENT=Development` (and can be disabled with `Verification:ExposeDevCodes=false`). In production, replace the logging sender with Azure Communication Services.

**Tests:** in `AccountFeatureTests.cs`:
- `VerificationCode_ConfirmsEmail_AndRejectsWrongCode`
- `VerificationCode_LoginPurpose_ReturnsSession_AndCodeIsSingleUse`
- `VerificationCode_ResendTooSoon_Throws`
- `ResetCode_ChangesPassword`

---

## 13. Malicious file uploads

### The attack
Upload attacks include:
- **Disguised files:** HTML, SVG or an executable labelled `image/jpeg`. If served back with an attacker-chosen content type, the file can run script (stored XSS) or trick users into downloading malware.
- **Path traversal names:** names like `../../web.config` or right-to-left override tricks (`photo.jpg‮gnp.exe`).
- **Oversized files:** to exhaust storage or memory.

### How we prevent it
- **Allowlist by MIME type *and* extension** (`Backend/Services/Classes/MediaRules.cs`). Both must agree:
  - Images: `.jpg .jpeg .png .gif .webp .avif`
  - Videos: `.mp4 .webm .mov .m4v .ogv`
  - Audio: `.mp3 .m4a .aac .wav .ogg .oga .opus .flac .weba`
  - **SVG and HTML are not accepted.**
- **Server-chosen blob names** (`BlobStorageService.SafeBlobName`): a sanitized ASCII hint of at most 40 characters, plus a random GUID, plus a validated extension. No path separators, `..`, markup or Unicode tricks survive.
- **Server-chosen content type** (`BlobStorageService.ContentTypeFor`): derived from the validated extension, never taken from the client.
- **Size limits:**

  | Upload | Limit |
  |---|---|
  | Post media | 80 MB |
  | Story media | 50 MB |
  | Music | 25 MB |
  | Request body | Kestrel cap |
- Music can only be attached to a photo or video post, and every music file is validated separately.

**Live test results:** HTML labelled `image/jpeg` → `400`; SVG with `onload` → `400`; `.jpg` sent as `text/html` → `400`; video sent as `audio/mpeg` music → `400`.
**Tests:** in `MediaTests.cs`: `MismatchedOrUnsupportedFiles_AreRejected` and `Story_WithNonAudioMusic_IsRejected`. In `SecurityHardeningTests.cs`: `SafeBlobName_HasNoPathOrMarkupCharacters` and `ContentType_ComesFromExtension`.

---

## 14. Clickjacking

### The attack
The attacker loads our app in an invisible `<iframe>` on their site and tricks the user into clicking buttons in it, such as "Follow", "Delete" or "Confirm".

### How we prevent it
- `X-Frame-Options: DENY`
- `Content-Security-Policy: frame-ancestors 'none'`
- `Cross-Origin-Opener-Policy: same-origin`

Sent by the API (`SecurityHeadersMiddleware`), by nginx (`Frontend/nginx.conf`), and, except for CSP, by the Vite dev server (`Frontend/vite.config.mjs`).

---

## 15. Token forgery and weak secrets

### The attack
JWTs are signed with `JWT:Secret`. If the secret is weak or public, attackers can sign their own tokens and become any user, including Admin. The sample secret is published in `docker-compose.yml`, `.env.example` and `appsettings.json`.

### How we prevent it
- **Startup guard** (`Program.cs`): outside Development, the API **refuses to start** if `JWT:Secret` is shorter than 32 characters or equals the published sample value.
- Full JWT validation: signature, issuer, audience and lifetime, with `ClockSkew = 0`.
- Tokens expire after `JWT:TokenValidityInMinutes`, and the cookie expires with the token.

**Live test:** a forged token in the session cookie → `401`.

---

## 16. Information leakage through errors

### The attack
Detailed exception pages and stack traces reveal frameworks, file paths, SQL and code structure, which help attackers plan the next step. The Docker stack runs in `Development`, where ASP.NET would normally show the full developer exception page.

### How we prevent it
- A global `UseExceptionHandler` (`Program.cs`) returns a generic `500` problem response: `"Something went wrong. Please try again."`. It never includes exception details.
- Validation failures return `400` with user-friendly messages only.

**Live test:** malformed JSON → `400` with no stack trace.

---

## 17. Search abuse (LIKE wildcard injection)

### The attack
Even with parameterized queries, a search value goes into a SQL `LIKE` pattern, where `%`, `_` and `[` are wildcards. An attacker can send patterns like `%%%%`, `[a-z]%` or very long strings. That forces expensive scans or returns results the user shouldn't be able to pattern-match.

### How we prevent it
- `SearchText.Contains()` (`Backend/Security/SearchText.cs`) escapes `[`, `%` and `_` with SQL Server brackets, wraps the term as `%term%`, and caps it at 100 characters.
- Used by every search: user search (`UserService.Search`) and Explore / people (`FollowService.GetPeopleAsync`).

**Tests:** `SearchText_EscapesLikeWildcards`, `SearchText_CapsLength`

---

## 18. Message queue abuse (Service Bus / Functions)

> The AI image studio (`ImagesController` → `image-generation` queue → `GenerateImage` Function → ComfyUI) follows the same rules, plus:
> - **Owner-only jobs:** you can only read or share your own images.
> - **Quotas** stop anyone exhausting the GPU:
>   - 2 images in progress at once and 25 per day per member;
>   - 6 requests per minute (`ai` rate-limit policy).
> - **Server-side style presets:** the allowlisted styles are added on the server, so clients can't change the workflow template.
> - **Prompt cleanup:** control characters are stripped and prompts are capped at 800 characters, plus a minimal screen for prohibited content.
> - **Safe results:** generated PNGs go through the same safe blob naming and content-type rules as uploads.
> - **Not exposed:** ComfyUI is never reachable from the browser; only the Functions worker (and the API's health check) call it.
> - **Production:** add a real moderation service (e.g. Azure AI Content Safety) on both the prompt and the generated image.

### The attack
Replayed or duplicated queue messages create duplicate notifications. Malformed messages crash consumers. A queue outage could break user actions.

### How we prevent it
- **Idempotency:**
  - Notifications have a unique `(EventId, RecipientId)` index.
  - Chat messages have a unique `MessageId`.
  - Duplicate deliveries are detected and ignored (`NotificationStore`, `ChatMessageStore`).
- **Validation:** consumers reject events with missing IDs or unknown types. A failing message is retried up to `MaxDeliveryCount`, then dead-lettered.
- **Consistency checks:** a `PostLiked` notification is only created if the like still exists; recipients who muted that type are skipped.
- **Isolation from user actions:** publishing is fire-and-forget with logging (`NotificationPublisher`). A Service Bus outage never fails a like, comment or post.
- **Stale codes:** the verification Function drops codes that expired while queued.
- Queues are only reachable from inside the Docker network or with the connection string. Producers are the API's services only; there is no public endpoint that writes to a queue directly.

---

## 19. Secrets and data at rest

| Item | Protection |
|---|---|
| Passwords | BCrypt with per-user salt |
| One-time codes | SHA-256 hash; 10-minute lifetime |
| Session token | HttpOnly cookie only; never in JSON, storage or logs |
| `.env` | Git-ignored (root `.gitignore`) |
| Uploaded media | Azurite data in `storage/azurite/`: git-ignored, excluded from Docker build contexts (`.dockerignore`) |
| Test credentials | `TEST_ACCOUNTS.local.md`: git-ignored and excluded from Docker builds |
| Admin credentials | Supplied via `ADMIN_EMAIL` / `ADMIN_PASSWORD` environment variables only |

---

## 20. How to verify everything

### Automated tests
```bash
cd Backend/tests/SocialMediaWeb.Tests
dotnet test
```
90 tests, including the security suites:

| File | Covers |
|---|---|
| `SecurityTests.cs` | Anonymous endpoint allowlist, admin role, no client-supplied identity, admin seeding |
| `SecurityHardeningTests.cs` | CSRF middleware, LIKE escaping, safe blob names, content types, enumeration |
| `MediaTests.cs` | Upload type, extension and size validation |
| `AccountFeatureTests.cs` | Identity rules, OTP lifecycle, follows, stories, muted notifications |

### Manual checks against the running stack
With `docker compose up -d` running, each of these should be refused:

```bash
# Anonymous access is refused
curl -i http://localhost:8080/api/Post/feed                      # 401

# A cross-site POST is refused
curl -i -X POST -H "Origin: https://evil.example" \
     http://localhost:8080/api/Likes/toggle?postId=1              # 403

# Preflight from an unknown origin gets no CORS grant
curl -i -X OPTIONS -H "Origin: https://evil.example" \
     -H "Access-Control-Request-Method: POST" \
     http://localhost:8080/api/Post                               # no Access-Control-Allow-Origin

# Security headers on the app
curl -I http://localhost:8080/                                    # CSP, X-Frame-Options, nosniff, …
```

In the browser's DevTools → Application → Cookies, `sb_session` shows **HttpOnly ✓** and **SameSite Strict**. `document.cookie` in the console shows only `XSRF-TOKEN`.

---

## 21. Known limitations and production checklist

These are deliberate trade-offs or items that need deployment-specific setup:

| Item | Status | Action for production |
|---|---|---|
| **JWT secret** | Sample value used for local dev | Set a random `JWT_SECRET` (≥ 32 chars) in `.env`; startup enforces this outside Development |
| **Admin account** | Created from `ADMIN_EMAIL` / `ADMIN_PASSWORD` | Set strong values in `.env` |
| **HTTPS** | Local stack is HTTP | Terminate TLS in front of the app; cookies become `Secure` and HSTS is enabled automatically outside Development |
| **Allowed origins** | Localhost defaults | Set `Security__AllowedOrigins__0=https://your-domain` |
| **Media URLs are public** | Anyone with a blob link can open the file | For private content, switch containers to private access and serve short-lived SAS URLs |
| **Email/SMS delivery** | Logged locally; `devCode` shown in Development | Use Azure Communication Services and run with `ASPNETCORE_ENVIRONMENT=Production` |
| **Rate limits are per IP and in memory** | Fine for one instance | Behind a load balancer, configure forwarded headers and use a distributed limiter or a WAF / Azure Front Door |
| **Service Bus / SQL / Azurite credentials** | Emulator defaults | Use Azure-managed services with Managed Identity or Key Vault secrets |
| **Admin "Sign out" link** | Pre-existing dead link in the admin sidebar | Wire it to `POST /api/Account/logout` |
| **Non-browser clients** | CORS cannot restrict them | They still require a valid session; consider API gateway policies if needed |

---

*Last reviewed: 2026-10-10. All protections above were verified against the running local stack (31/31 live attack checks) and by the automated test suite (90/90 passing).*
