# Fixes & Setup Notes

## 1. Docker vs Docker Compose (Root Cause of the Original Issue)

### The problem
The app container was started manually with `docker run ramp1712/socialmediaweb:latest`. Every action from the frontend then failed with:

```
System.Net.Sockets.SocketException: Name or service not known
SqlException ... Error Locating Server/Instance Specified
```

### Why
`docker run` starts **one isolated container** on the default Docker network:

- The SQL Server, Azurite, and Service Bus containers were **not running at all**.
- Even if they had been, the default Docker network has **no DNS name resolution** between containers — the app container could not resolve the host name `sqlserver` in the connection string (`Server=sqlserver,1433;...`).

`docker compose up -d` reads `docker-compose.yml` and starts the **whole stack as one unit**:

- Creates a private network (`socialmediaweb_default`)
- Starts all 5 containers (app, sqlserver, azurite, servicebus, chat-functions) on it
- Each service is reachable by its service name via built-in DNS, so `Server=sqlserver` resolves correctly

### Rule of thumb
If a project contains a `docker-compose.yml`, always run it with `docker compose up -d`.
`docker run` is only appropriate for a truly standalone single container.

### Common commands

```powershell
docker compose up -d                 # start the whole stack
docker compose ps                    # check status
docker compose logs app --tail 50    # view backend logs
docker compose up -d --build app     # rebuild backend after code changes
docker compose down                  # stop everything
docker compose down -v               # stop + wipe database volumes (fresh start)
```

## 2. Fix: Service Bus Emulator Config (infra/servicebus/config.json)

The Service Bus emulator crashed on startup with:

```
Expected time to be less than or equal to 5m because Max Duplicate detection
window supported 5m, but found 10m.
```

**Change:** `DuplicateDetectionHistoryTimeWindow` was `PT10M` (10 minutes) on all three
queues (`chat-messages`, `post-notifications`, `friend-requests`) — the emulator supports a
maximum of 5 minutes.

**Fix applied:**

```diff
- "DuplicateDetectionHistoryTimeWindow": "PT10M",
+ "DuplicateDetectionHistoryTimeWindow": "PT5M",
```

After the edit the container was restarted with `docker compose restart servicebus`.

## 3. Fix: Frontend API Origin (Frontend/.env.local)

The frontend code hardcodes `https://localhost:7151` (the local non-Docker backend URL).
`Frontend/vite.config.mjs` rewrites this value with the `VITE_API_ORIGIN` environment
variable at dev-server startup.

When `VITE_API_ORIGIN` is not set, requests become relative (`127.0.0.1:5173/api/...`),
fall into the Vite dev proxy, which forwards them to `https://localhost:7151` — nothing is
listening there, so the proxy itself returns `500 Internal Server Error`. This 500 never
reaches the backend container.

**Fix applied:** created `Frontend/.env.local` (already covered by `.gitignore`):

```
VITE_API_ORIGIN=http://localhost:8080
```

Requests now go directly from the browser to the Docker backend on port 8080, which is
allowed because the backend enables `AllowAnyOrigin` CORS.

## 4. Fix: Vite config not reading .env.local (Frontend/vite.config.mjs)

`Register.jsx` (and others) hardcode `https://localhost:7151`; the `configure-api-origin`
plugin in `vite.config.mjs` rewrites it with `apiOrigin`, which was read only from
`process.env.VITE_API_ORIGIN`. Vite does **not** load `.env` files into `process.env`,
so `.env.local` was ignored and requests fell through to the proxy → `ECONNREFUSED`.

**Fixes applied:**

- Read the value via `loadEnv("development", process.cwd(), "")` with `||` fallbacks
  (an empty shell env var no longer wins over the file).
- The dev-proxy targets for `/api`, `/Images`, `/posts` were changed from the hardcoded
  `https://localhost:7151` to `apiOrigin || "http://localhost:8080"` as a safety net.

## 5. Fix: Root Dockerfile restore was incomplete (Dockerfile)

The backend project gained a reference to `SocialMediaWeb.ServiceDefaults`, but the root
`Dockerfile` only copied `Backend/SocialMediaWeb.csproj` before `dotnet restore`, so the
image build failed with `error NETSDK1004: project.assets.json not found`.

**Fix applied:** copy the ServiceDefaults csproj before restore (the ChatFunctions
Dockerfile already did this):

```dockerfile
COPY Backend/SocialMediaWeb.ServiceDefaults/SocialMediaWeb.ServiceDefaults.csproj Backend/SocialMediaWeb.ServiceDefaults/
```

After this, `docker compose up -d --build app` rebuilt the image from local code — which
also picked up the EF migrations code, so the `SocialMedia` database is now created
automatically on startup (previously the container ran the old prebuilt image and
registration failed with SQL error 4060 *"Cannot open database SocialMedia"*).

## 6. Fix: Azurite connection string rejected by newer Azure SDK (docker-compose.yml)

The old-style Azurite connection string (`DefaultEndpointsProtocol=http;AccountName=...;
AccountKey=...;BlobEndpoint=http://azurite:10000/...`) causes
`System.FormatException: No valid combination of account information found` with the
Azure.Storage SDK version bundled in `Aspire.Azure.Storage.Blobs 9.2.0` — the newer SDK
rejects `BlobEndpoint` + `AccountKey` combinations.

**Fix applied** (`ConnectionStrings__blobs` on the `app` service):

```
UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://azurite
```

Verified: registration endpoint returns HTTP 200 after this change.

## 7. How to run the project (correct flow)

```powershell
# Terminal 1 — from project root (D:\Dot Net\SocialMediaWeb)
docker compose up -d

# Terminal 2 — frontend
cd Frontend
npm start   # or: npm run dev

# Open http://127.0.0.1:5173
```

### Verification checklist

| Check                | How                                  |
| -------------------- | ------------------------------------ |
| All containers up    | `docker compose ps`                  |
| Backend healthy      | `http://localhost:8080/swagger` → 200 |
| Frontend             | `http://127.0.0.1:5173`              |
| Requests hit backend | DevTools → Network shows `localhost:8080/api/...` |

## 8. Fix: Profile and Post Images Blob Storage URL Resolution

### The problem
When registering with an image or uploading post images:
- Images were uploaded to Azurite inside the Docker stack.
- However, because `Blob:PublicBaseUrl` was not set in `docker-compose.yml` or `appsettings`, `BlobStorageService` returned the internal container URL (`http://azurite:10000/...`).
- SQL Server stored `http://azurite:10000/...`.
- The browser on the host machine cannot resolve the internal Docker hostname `azurite`, causing all image requests to fail (`ERR_NAME_NOT_RESOLVED`) and avatars to appear blank or broken across the entire frontend.

### Fixes applied
1. **`BlobStorageService.cs` & `IBlobStorageService.cs`**:
   - `ToPublicUrl` rewritten to map internal `azurite` hosts to `localhost:10000` (or `Blob:PublicBaseUrl` if configured).
   - Added public `ToPublicUrl(string? blobUrl)` method to sanitize stored URLs on retrieval.
2. **`docker-compose.yml`**:
   - Added `Blob__PublicBaseUrl: ${BLOB_PUBLIC_BASE_URL:-http://localhost:10000}` to the `app` container.
   - Added loose mode flags (`-L --loose --skipApiVersionCheck`) to `azurite`.
3. **Database cleanup**:
   - Updated existing user records in SQL Server from `http://azurite:10000` to `http://localhost:10000`.
4. **`Register.jsx`**:
   - Added file input `accept="image/*"`.
   - Added live image preview thumbnail before submitting.
   - Added loading state to prevent double submits.
5. **Frontend Image Helper (`Frontend/src/utils/image.js`)**:
   - Added `getPublicImageUrl` to automatically rewrite any `azurite:10000` hostnames to `localhost:10000` and provide default avatar fallback (`/assets/person/1.jpeg`).
   - Updated `Login.jsx` to store public URLs in both `url` and `imagePath` in `sessionStorage`.
   - Updated `Topbar.jsx`, `Profile.jsx`, `Share.jsx`, `CommentsModal.jsx`, `Comments.jsx`, `Post.jsx`, `allPosts.jsx`, `Requests.jsx`, `Rightbar.jsx`, and `Chat.jsx` to use `getPublicImageUrl` and `onError` image fallbacks.

