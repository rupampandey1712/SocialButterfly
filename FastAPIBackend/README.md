# FastAPI backend

This is a separate Python implementation of the SocialMediaWeb API. The .NET backend and frontend are unchanged. The API covers account registration, login and verification, profile/settings and password changes, search, posts and media, likes, comments, friends, follows and suggestions, chat, notifications, stories and viewers, admin actions, ComfyUI image generation, NVIDIA NIM text generation, and the weather sample.

## Run locally

```powershell
cd FastAPIBackend
py -m venv .venv
.venv\Scripts\Activate.ps1
pip install -r requirements.txt
$env:JWT_SECRET = "replace-with-a-private-secret-of-at-least-32-characters"
uvicorn app.main:app --reload --host 0.0.0.0 --port 8080
```

The default database is SQLite at `FastAPIBackend/socialmedia.db`. Uploaded files use `FastAPIBackend/media` and are served from `/Images`, `/posts`, `/stories`, `/music`, and `/ai-images`. Set `DATABASE_URL` to another SQLAlchemy-supported database. The app applies its Alembic migrations on startup (see below); it does not connect to the .NET SQL Server schema. Set `VITE_API_ORIGIN=http://localhost:8080` when launching the existing Vite frontend to point its proxy at this API; no frontend files need edits.

## Integrations

Set environment variables through the shell or deployment platform; the app does not load `.env` itself. Configure `JWT_SECRET`, issuer/audience, `CORS_ORIGINS`, and `APP_ENV` before deployment. `ADMIN_EMAIL` and `ADMIN_PASSWORD` can seed an administrator.

Verification supports SMTP and Twilio. Development returns the code as `devCode`. Uploads use local storage by default or Azure Blob Storage when `AZURE_STORAGE_CONNECTION_STRING` is set. `BLOB_PUBLIC_BASE_URL` can map blob URLs to a public host. With `AZURE_SERVICEBUS_CONNECTION_STRING`, the API publishes .NET-shaped messages to `chat-messages`, `verification-codes`, `image-generation`, `post-notifications`, and `friend-requests`; Python background workers consume them, persist messages, deliver codes, process notifications and run image jobs. The workers also remove expired stories every 15 minutes. `SERVICEBUS_WORKER_MODE=off` disables Python queue consumers. If this backend shares a Service Bus namespace with the .NET Functions, set `SERVICEBUS_QUEUE_PREFIX` (for example `py-`) and create the prefixed queues, otherwise each stack consumes the other's messages. Image-generation messages are processed one at a time with their lock renewed for up to 15 minutes, and a job already running is never started twice. Use a single consumer deployment per queue, and point all consumers to compatible database schemas; Python's database schema is separate from .NET's SQL Server schema.

Image generation requires ComfyUI (`COMFYUI_URL`) and uses the included workflow. Text generation requires NVIDIA NIM settings (`NIM_BASE_URL`, `NVIDIA_API_KEY`, `NIM_MODEL`). The backend schema and worker configuration are independent from .NET, so SQL Server data, secrets, and external service settings are not migrated automatically.

## Project layout

| Path | Contents |
| --- | --- |
| `app/main.py` | `create_app()` factory: migrations, middleware, static media, routers, admin seeding, background workers |
| `app/config.py` | `Settings`, read from environment variables once per app instance |
| `app/models.py`, `app/db.py` | SQLAlchemy models; timestamps are stored as naive UTC (`utcnow()`) |
| `app/migrations/` | Alembic environment and revisions |
| `app/schemas.py` | Pydantic request bodies; JSON keys bind case-insensitively, like the .NET DTOs |
| `app/routers/` | One module per controller: account, posts (likes/comments), friends, follows, chat, notifications, stories, images (and GenAI), admin |
| `app/views.py` | Response builders; list endpoints load related rows in a fixed number of queries |
| `app/security/` | Cookie/CSRF/auth middleware, PyJWT tokens, bcrypt passwords, rate limiter, bot defense |
| `app/services/` | Blob storage, Service Bus, notifications, image jobs, background workers, media and identity rules |

Malformed request bodies return 400 with an ASP.NET-style `errors` object. Profile photos must be real JPG, PNG, GIF, WebP or AVIF files (extension, declared type and file signature are all checked).

## Database migrations

Schema changes are Alembic revisions in `app/migrations/versions`. The app upgrades the database to the latest revision when it starts, and a database created by an earlier version of this backend (before migrations) is adopted automatically. Revision `0002` adds lookup indexes and makes email addresses unique; if an existing database has two accounts with the same email, startup stops and names the address so you can fix it first.

After changing `app/models.py`, create and review a revision from this directory:

```powershell
alembic revision --autogenerate -m "describe the change"
alembic upgrade head
alembic check   # confirms the models and migrations agree
```

The CLI uses `DATABASE_URL` (default: `socialmedia.db` here). When running several API processes against one database, run `alembic upgrade head` once before starting them.

## Checks

The Python behavior checks are in `tests/` and can be run with `pytest -q` from this directory after installing `requirements-dev.txt`. Tests use a temporary database and media folder (see `tests/conftest.py`), so they don't touch `socialmedia.db` or `media/`. The existing .NET regression suite can be run from the repository root with `dotnet test Backend/tests/SocialMediaWeb.Tests/SocialMediaWeb.Tests.csproj`.

