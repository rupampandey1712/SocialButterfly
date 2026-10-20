"""Regression tests for the backend review: response shapes, data exposure, upload validation,
account integrity, query counts, memory bounds, background work and schema migrations."""

import asyncio
import importlib
import threading
from contextlib import contextmanager
from datetime import timedelta

import jwt
import pytest
from fastapi.testclient import TestClient
from sqlalchemy import create_engine, event, inspect, text
from sqlalchemy.exc import IntegrityError

PASSWORD = "safe-password-123"
PNG = b"\x89PNG\r\n\x1a\n" + b"\x00" * 32
JPEG = b"\xff\xd8\xff\xe0" + b"\x00" * 32


@pytest.fixture
def backend(tmp_path, monkeypatch):
    monkeypatch.setenv("DATABASE_URL", f"sqlite:///{tmp_path / 'review.db'}")
    monkeypatch.setenv("MEDIA_ROOT", str(tmp_path / "media"))
    import app.main as module
    return importlib.reload(module)


def member(backend, name, email):
    client = TestClient(backend.app)
    client.get("/api/Account/session")
    csrf = client.cookies.get("XSRF-TOKEN")
    client.headers["X-XSRF-TOKEN"] = csrf
    created = client.post("/api/Account/register", data={"name": name, "email": email, "password": PASSWORD})
    assert created.status_code == 200, created.text
    assert client.post("/api/Account/login", data={"identifier": email, "password": PASSWORD}).status_code == 200
    return client, created.json()["id"]


class FakeStorage:
    def __init__(self):
        self.saved_on_threads = []

    def save_upload(self, upload, folder): return f"/{folder}/{upload.filename}"

    def save_bytes(self, data, filename, folder):
        self.saved_on_threads.append(threading.get_ident())
        return f"/{folder}/{filename}"

    def delete(self, path, folder): pass

    def public_url(self, path): return path


@contextmanager
def counted_queries(engine):
    counter = {"n": 0}

    def count(*_args, **_kwargs): counter["n"] += 1

    event.listen(engine, "before_cursor_execute", count)
    try:
        yield counter
    finally:
        event.remove(engine, "before_cursor_execute", count)


# --- 1. Response shapes the frontend reads -------------------------------------------------------

def test_friend_endpoints_wrap_values_like_dotnet(backend):
    ava, ava_id = member(backend, "Ava", "ava@example.com")
    leo, leo_id = member(backend, "Leo", "leo@example.com")
    assert leo.post("/api/Friends", json={"receiverId": ava_id}).status_code == 200
    assert ava.get("/api/Friends/me/requests").json() == {"result": None, "value": 1}
    requests = ava.get("/api/Friends/me/requestuser").json()["value"]
    assert [r["id"] for r in requests] == [leo_id] and set(requests[0]) == {"id", "name", "dob", "imagePath"}
    assert ava.post(f"/api/Friends/{leo_id}").status_code == 200
    assert [f["id"] for f in ava.get(f"/api/Friends/{ava_id}/myfriends").json()["value"]] == [leo_id]
    assert ava.get(f"/api/Friends/{ava_id}/friendscount").json() == {"result": None, "value": 1}


# --- 2. Data exposure ----------------------------------------------------------------------------

def test_friends_list_contains_only_public_fields(backend):
    ava, ava_id = member(backend, "Ava", "ava@example.com")
    leo, leo_id = member(backend, "Leo", "leo@example.com")
    leo.put("/api/Account/me/settings", data={"phoneNumber": "+14155550123"})
    leo.post("/api/Friends", json={"receiverId": ava_id}); ava.post(f"/api/Friends/{leo_id}")
    stranger, _ = member(backend, "Sam", "sam@example.com")
    response = stranger.get(f"/api/Friends/{ava_id}/friends")
    assert response.status_code == 200
    assert response.json() == [{"id": leo_id, "name": "Leo", "imagePath": None}]
    for secret in ("leo@example.com", "+14155550123", "$2b$", "User"):
        assert secret not in response.text


# --- 3. Upload validation ------------------------------------------------------------------------

BAD_PHOTOS = [
    ("evil.html", b"<script>alert(document.domain)</script>", "text/html"),
    ("photo.html", b"<h1>phish</h1>", "image/png"),
    ("logo.svg", b"<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>", "image/svg+xml"),
    ("disguised.png", b"<html><script>alert(1)</script></html>", "image/png"),
]


@pytest.mark.parametrize("photo", BAD_PHOTOS, ids=[p[0] for p in BAD_PHOTOS])
def test_register_rejects_non_image_profile_photo(backend, photo):
    client = TestClient(backend.app); client.get("/api/Account/session")
    response = client.post("/api/Account/register", headers={"X-XSRF-TOKEN": client.cookies.get("XSRF-TOKEN")},
                           data={"name": "Mallory", "email": "m@example.com", "password": PASSWORD}, files={"imageFile": photo})
    assert response.status_code == 400 and "Profile photos must be images" in response.text
    with backend.SessionLocal() as db:
        assert db.query(backend.User).filter_by(email="m@example.com").count() == 0


@pytest.mark.parametrize("photo", BAD_PHOTOS, ids=[p[0] for p in BAD_PHOTOS])
def test_settings_and_legacy_update_reject_non_image_profile_photo(backend, photo):
    ava, ava_id = member(backend, "Ava", "ava@example.com")
    assert ava.put("/api/Account/me/settings", files={"imageFile": photo}).status_code == 400
    assert ava.put("/api/Account", data={"id": str(ava_id)}, files={"imageFile": photo}).status_code == 400
    assert ava.get("/api/Account/me/settings").json()["imagePath"] is None


def test_real_images_are_accepted_as_profile_photos(backend):
    backend.app.state.ctx.storage = FakeStorage()
    ava, ava_id = member(backend, "Ava", "ava@example.com")
    assert ava.put("/api/Account/me/settings", files={"imageFile": ("me.png", PNG, "image/png")}).json()["imagePath"] == "/Images/me.png"
    assert ava.put("/api/Account", data={"id": str(ava_id)}, files={"imageFile": ("me.jpg", JPEG, "image/jpeg")}).json()["imagePath"] == "/Images/me.jpg"


# --- 4. Account integrity ------------------------------------------------------------------------

def test_legacy_update_cannot_take_another_members_email(backend):
    member(backend, "Ava", "ava@example.com")
    leo, leo_id = member(backend, "Leo", "leo@example.com")
    for taken in ("ava@example.com", " AVA@Example.com "):
        response = leo.put("/api/Account", data={"id": str(leo_id), "email": taken})
        assert response.status_code == 400 and "Another account" in response.text
    with backend.SessionLocal() as db:
        assert db.query(backend.User).filter_by(email="ava@example.com").count() == 1


def test_legacy_update_validates_and_normalizes_email(backend):
    leo, leo_id = member(backend, "Leo", "leo@example.com")
    sent = leo.post("/api/Account/code/send", json={"target": "leo@example.com", "purpose": "Verify"}).json()
    leo.post("/api/Account/code/confirm", json={"target": "leo@example.com", "purpose": "Verify", "code": sent["devCode"]})
    assert leo.get("/api/Account/me/settings").json()["emailConfirmed"] is True
    assert leo.put("/api/Account", data={"id": str(leo_id), "email": "not-an-email"}).status_code == 400
    changed = leo.put("/api/Account", data={"id": str(leo_id), "email": "  Leo.Park@Example.COM "}).json()
    assert changed["email"] == "leo.park@example.com" and changed["emailConfirmed"] is False


def test_database_enforces_one_account_per_email(backend):
    with backend.SessionLocal() as db:
        db.add_all([backend.User(email="dup@example.com", password_hash="x", username="dup.one"),
                    backend.User(email="dup@example.com", password_hash="x", username="dup.two")])
        with pytest.raises(IntegrityError):
            db.commit()
        db.rollback()
        db.add_all([backend.User(phone_number="+14155550100", password_hash="x", username="phone.one"),
                    backend.User(phone_number="+14155550101", password_hash="x", username="phone.two")])
        db.commit()  # phone-only accounts (no email) don't collide


def test_password_change_is_rate_limited(backend):
    ava, _ = member(backend, "Ava", "ava@example.com")
    statuses = [ava.put("/api/Account/me/password", json={"currentPassword": f"guess-{i}", "newPassword": "new-password-123"}).status_code
                for i in range(11)]
    assert statuses == [400] * 10 + [429]


# --- 5. Query efficiency -------------------------------------------------------------------------

def test_feed_runs_a_fixed_number_of_queries(backend):
    ava, _ = member(backend, "Ava", "ava@example.com")
    leo, _ = member(backend, "Leo", "leo@example.com")
    ids = [ava.post("/api/Post", data={"description": f"post {i}"}).json()["id"] for i in range(20)]
    leo.post(f"/api/Likes/toggle?postId={ids[0]}")
    leo.post("/api/Comment", json={"postId": ids[0], "text": "nice"})
    with counted_queries(backend.engine) as queries:
        feed = leo.get("/api/Post/feed?take=20").json()
    assert len(feed) == 20 and queries["n"] <= 8, f"{queries['n']} queries for 20 posts"
    first = next(p for p in feed if p["id"] == ids[0])
    assert (first["likeCount"], first["commentCount"], first["isLiked"], first["user"]["name"]) == (1, 1, True, "Ava")
    assert all(p["likeCount"] == 0 and not p["isLiked"] for p in feed if p["id"] != ids[0])


class Unlimited:
    def allow(self, key, limit): return True


def test_list_endpoints_do_not_query_per_item(backend):
    backend.app.state.ctx.storage = FakeStorage()
    backend.app.state.ctx.rate_limiter = Unlimited()  # this test signs up nine members from one address
    viewer, viewer_id = member(backend, "Viewer", "viewer@example.com")
    newcomer, _ = member(backend, "Newcomer", "newcomer@example.com")
    endpoints = ["/api/Post/feed", "/api/Notifications/me", "/api/Stories/feed", f"/api/Follows/{viewer_id}/followers",
                 f"/api/Follows/{viewer_id}/following", "/api/Follows/people", f"/api/Friends/{viewer_id}/friends"]
    created = 0

    def add_members(count):
        nonlocal created
        for _ in range(count):
            created += 1
            client, other_id = member(backend, f"Member {created}", f"member{created}@example.com")
            client.post(f"/api/Follows/{viewer_id}/toggle"); viewer.post(f"/api/Follows/{other_id}/toggle")
            client.post("/api/Friends", json={"receiverId": viewer_id}); viewer.post(f"/api/Friends/{other_id}")
            client.post("/api/Stories", files={"media": ("s.jpg", JPEG, "image/jpeg")})
            post_id = client.post("/api/Post", data={"description": "hello"}).json()["id"]
            viewer.post(f"/api/Likes/toggle?postId={post_id}")

    def measure():
        counts = {}
        for path in endpoints + ["suggestions"]:
            client = newcomer if path == "suggestions" else viewer
            url = "/api/Follows/suggestions" if path == "suggestions" else path
            with counted_queries(backend.engine) as queries:
                assert client.get(url).status_code == 200
            counts[path] = queries["n"]
        return counts

    add_members(2)
    small = measure()
    add_members(5)
    assert measure() == small


# --- 6. Bounded memory ---------------------------------------------------------------------------

def test_bot_defense_caps_identifiers_and_forgets_idle_trackers():
    from app.security.bot_defense import BotDefense
    now = [1000.0]
    defense = BotDefense(clock=lambda: now[0])
    for i in range(500):
        defense.record_failure("203.0.113.9", f"user{i}@example.com")
    assert len(defense._ip["203.0.113.9"]["identifiers"]) == BotDefense.MAX_IDENTIFIERS
    assert defense.required_difficulty("203.0.113.9", None) == BotDefense.MAX_DIFFICULTY
    assert defense.tracked() == (1, 500)
    now[0] += BotDefense.WINDOW + 1
    assert defense.required_difficulty("198.51.100.1", None) == 0
    assert defense.tracked() == (0, 0)


def test_bot_defense_evicts_least_recent_trackers_beyond_the_cap(monkeypatch):
    from app.security.bot_defense import BotDefense
    monkeypatch.setattr(BotDefense, "MAX_TRACKERS", 10)
    defense = BotDefense()
    defense.flag_bot("10.0.0.0")
    for i in range(1, 50):
        defense.record_failure(f"10.0.0.{i}", None)
    defense.flag_bot("10.0.0.49")
    assert defense.tracked()[0] == 10
    assert defense.required_difficulty("10.0.0.0", None) == 0   # evicted
    assert defense.required_difficulty("10.0.0.49", None) > 0   # recent, kept


def test_rate_limiter_keeps_only_the_current_window():
    from app.security.rate_limit import RateLimiter
    now = [0.0]
    limiter = RateLimiter(clock=lambda: now[0])
    assert all(limiter.allow(f"10.0.{i // 256}.{i % 256}:auth", 10) for i in range(5000))
    assert [limiter.allow("1.2.3.4:auth", 2) for _ in range(3)] == [True, True, False]
    now[0] += 60
    assert limiter.allow("1.2.3.4:auth", 2) and len(limiter) == 1


def test_rate_limiter_is_thread_safe():
    from app.security.rate_limit import RateLimiter
    limiter = RateLimiter()
    allowed = []
    threads = [threading.Thread(target=lambda: allowed.extend(limiter.allow("shared", 100) for _ in range(50))) for _ in range(8)]
    for t in threads: t.start()
    for t in threads: t.join()
    assert sum(allowed) == 100


# --- 7. Background work: claiming, threads and Service Bus locks ----------------------------------

def queue_job(backend, user_id, status="Queued", started_at=None):
    with backend.SessionLocal() as db:
        job = backend.GeneratedImage(user_id=user_id, prompt="a lake", style="none", width=1024, height=1024, seed=7,
                                     status=status, started_at=started_at)
        db.add(job); db.commit()
        return job.id


def test_image_jobs_are_claimed_once_unless_abandoned(backend):
    from app.db import utcnow
    from app.services.images import STALE_AFTER, claim_job
    _, ava_id = member(backend, "Ava", "ava@example.com")
    ctx = backend.app.state.ctx
    job_id = queue_job(backend, ava_id)
    assert claim_job(ctx, job_id)[1:] == (1024, 1024, 7)
    assert claim_job(ctx, job_id) is None  # a redelivered message must not start it again
    abandoned = queue_job(backend, ava_id, "Running", utcnow() - STALE_AFTER - timedelta(minutes=1))
    assert claim_job(ctx, abandoned) is not None


def test_image_processing_saves_off_the_event_loop_and_runs_once(backend, monkeypatch):
    import httpx
    from app.services import images
    _, ava_id = member(backend, "Ava", "ava@example.com")
    ctx = backend.app.state.ctx
    ctx.storage = FakeStorage()
    real_sleep = asyncio.sleep
    monkeypatch.setattr(images.asyncio, "sleep", lambda _seconds: real_sleep(0))  # skip ComfyUI polling delays

    class Reply:
        def __init__(self, body=None, content=b""): self.body, self.content = body, content
        def raise_for_status(self): pass
        def json(self): return self.body

    class FakeComfy:
        def __init__(self, **_kwargs): pass
        async def __aenter__(self): return self
        async def __aexit__(self, *_args): return None
        async def post(self, url, **_kwargs): return Reply({"prompt_id": "p1"})
        async def get(self, url, **_kwargs):
            if "/history/" in url: return Reply({"p1": {"outputs": {"9": {"images": [{"filename": "out.png"}]}}}})
            return Reply(content=PNG)

    monkeypatch.setattr(httpx, "AsyncClient", FakeComfy)
    job_id = queue_job(backend, ava_id)

    async def run_twice():
        await ctx.process_image(job_id)
        await ctx.process_image(job_id)
        return threading.get_ident()

    loop_thread = asyncio.run(run_twice())
    assert len(ctx.storage.saved_on_threads) == 1 and ctx.storage.saved_on_threads[0] != loop_thread
    with backend.SessionLocal() as db:
        job = db.get(backend.GeneratedImage, job_id)
        assert (job.status, job.image_path) == ("Succeeded", "/ai-images/generated.png")


def test_service_bus_worker_renews_image_locks_and_uses_prefixed_queues(backend, monkeypatch):
    import azure.servicebus.aio as sb
    from app.services import workers
    monkeypatch.setenv("AZURE_SERVICEBUS_CONNECTION_STRING", "Endpoint=sb://fake/;SharedAccessKeyName=k;SharedAccessKey=v")
    monkeypatch.setenv("SERVICEBUS_QUEUE_PREFIX", "py-")
    ctx = backend.app.state.ctx
    seen = {"jobs": [], "registered": [], "completed": [], "queue": None, "batch": None}

    async def fake_process(job_id): seen["jobs"].append(job_id)
    monkeypatch.setattr(ctx, "process_image", fake_process)

    class Message:
        body = b'{"JobId": 42}'

    class Receiver:
        def __init__(self): self.calls = 0
        async def __aenter__(self): return self
        async def __aexit__(self, *_args): return None
        async def receive_messages(self, max_message_count, max_wait_time):
            self.calls += 1; seen["batch"] = max_message_count
            if self.calls > 1: raise asyncio.CancelledError
            return [Message()]
        async def complete_message(self, message): seen["completed"].append(message)
        async def abandon_message(self, message): raise AssertionError("message should complete")

    class Client:
        async def __aenter__(self): return self
        async def __aexit__(self, *_args): return None
        def get_queue_receiver(self, queue_name, max_wait_time):
            seen["queue"] = queue_name
            return Receiver()

    class Renewer:
        async def __aenter__(self): return self
        async def __aexit__(self, *_args): return None
        def register(self, receiver, message, max_lock_renewal_duration=None):
            seen["registered"].append(max_lock_renewal_duration)

    monkeypatch.setattr(sb.ServiceBusClient, "from_connection_string", classmethod(lambda cls, _c: Client()))
    monkeypatch.setattr(sb, "AutoLockRenewer", Renewer)
    with pytest.raises(asyncio.CancelledError):
        asyncio.run(workers.service_bus_worker(ctx, "image-generation"))
    assert seen["queue"] == "py-image-generation" and seen["batch"] == 1
    assert seen["registered"] == [15 * 60] and seen["jobs"] == [42] and len(seen["completed"]) == 1


def test_queue_prefix_applies_to_published_messages(monkeypatch):
    import azure.servicebus as sb
    from app.services import service_bus
    monkeypatch.setenv("AZURE_SERVICEBUS_CONNECTION_STRING", "Endpoint=sb://fake/")
    monkeypatch.setenv("SERVICEBUS_QUEUE_PREFIX", "py-")
    sent = {}

    class Sender:
        def __enter__(self): return self
        def __exit__(self, *_args): return None
        def send_messages(self, message): sent["message"] = message

    class Client:
        def __enter__(self): return self
        def __exit__(self, *_args): return None
        def get_queue_sender(self, name): sent["queue"] = name; return Sender()

    monkeypatch.setattr(sb.ServiceBusClient, "from_connection_string", classmethod(lambda cls, _c: Client()))
    assert service_bus.publish("chat-messages", {"MessageId": "x"}, "id1", "ChatQueueMessage")
    assert sent["queue"] == "py-chat-messages"


def test_worker_handlers_run_database_work_on_threads(backend, monkeypatch):
    from app.services import workers
    _, ava_id = member(backend, "Ava", "ava@example.com")
    _, leo_id = member(backend, "Leo", "leo@example.com")
    ctx = backend.app.state.ctx
    threads = []
    original = workers.persist_chat_message
    monkeypatch.setattr(workers, "persist_chat_message", lambda *a: (threads.append(threading.get_ident()), original(*a)))
    event = {"MessageId": "6f1c1c1e-8c1a-4a57-9a4e-0a9bfc2f6d10", "SenderId": ava_id, "ReceiverId": leo_id,
             "Content": "hi", "SentAt": "2026-10-18T10:00:00+00:00"}

    async def deliver_twice():
        await workers.handle_message(ctx, "chat-messages", event)
        await workers.handle_message(ctx, "chat-messages", event)  # redelivery is idempotent
        return threading.get_ident()

    loop_thread = asyncio.run(deliver_twice())
    assert threads and loop_thread not in threads
    with backend.SessionLocal() as db:
        assert db.query(backend.ChatMessage).count() == 1


# --- 8. Request models ---------------------------------------------------------------------------

def test_json_bodies_bind_case_insensitively_and_reject_bad_types(backend):
    ava, _ = member(backend, "Ava", "ava@example.com")
    post_id = ava.post("/api/Post", data={"description": "hello"}).json()["id"]
    for body in ({"postId": post_id, "text": "camel"}, {"PostId": post_id, "Text": "pascal"}, {"POSTID": post_id, "tExT": "odd"}):
        assert ava.post("/api/Comment", json=body).status_code == 200
    bad = ava.post("/api/Comment", json={"postId": "not-a-number", "text": "x"})
    assert bad.status_code == 400 and "postId" in bad.json()["errors"]
    assert ava.post("/api/Chat", json={"receiverId": 1, "content": "x", "clientMessageId": "nope"}).status_code == 400


# --- 9. Tokens -----------------------------------------------------------------------------------

def test_session_tokens_are_verified_by_pyjwt(backend):
    ctx = backend.app.state.ctx
    tokens = ctx.tokens
    assert tokens.user_id(tokens.issue(5, "User")) == 5
    good = tokens.issue(5, "User")
    assert tokens.user_id(good[:-2] + ("AA" if not good.endswith("AA") else "BB")) is None
    base = {"sub": "5", "role": "Admin", "iss": tokens.issuer, "aud": tokens.audience}
    secret = ctx.settings.jwt_secret
    from datetime import datetime, timezone
    future = datetime.now(timezone.utc) + timedelta(minutes=5)
    assert tokens.user_id(jwt.encode({**base, "exp": datetime.now(timezone.utc) - timedelta(minutes=1)}, secret, "HS256")) is None
    assert tokens.user_id(jwt.encode({**base, "aud": "https://elsewhere", "exp": future}, secret, "HS256")) is None
    assert tokens.user_id(jwt.encode({**base, "iss": "https://elsewhere", "exp": future}, secret, "HS256")) is None
    assert tokens.user_id(jwt.encode({**base, "exp": future}, "a-different-secret-of-sufficient-length", "HS256")) is None
    assert tokens.user_id(jwt.encode(base, secret, "HS256")) is None  # no expiry
    unsigned = jwt.encode({**base, "exp": future}, None, algorithm="none")
    assert tokens.user_id(unsigned) is None


# --- 10. Migrations ------------------------------------------------------------------------------

def test_migrations_produce_exactly_the_model_schema(tmp_path):
    from alembic.autogenerate import compare_metadata
    from alembic.migration import MigrationContext
    from app.db import Base
    from app.migrate import upgrade_database
    engine = create_engine(f"sqlite:///{tmp_path / 'fresh.db'}")
    upgrade_database(engine)
    with engine.connect() as connection:
        diff = compare_metadata(MigrationContext.configure(connection), Base.metadata)
    assert diff == []


def test_create_all_database_from_before_migrations_is_adopted(tmp_path):
    from app.migrate import upgrade_database
    engine = create_engine(f"sqlite:///{tmp_path / 'legacy.db'}")
    upgrade_database(engine, "0001")
    with engine.begin() as connection:
        connection.execute(text("DROP TABLE alembic_version"))
        connection.execute(text("ALTER TABLE users DROP COLUMN mute_new_post_notifications"))
        connection.execute(text("INSERT INTO users (dob, email, password_hash, role, is_deleted, username, theme, email_confirmed, "
                                "phone_confirmed, mute_like_notifications, mute_comment_notifications, mute_follow_notifications) "
                                "VALUES ('x', ' Mixed@Case.com', 'h', 'User', 0, 'mixed', 'system', 0, 0, 0, 0, 0)"))
    upgrade_database(engine)
    with engine.connect() as connection:
        assert connection.execute(text("SELECT version_num FROM alembic_version")).scalar() == "0002"
        assert connection.execute(text("SELECT email FROM users")).scalar() == "mixed@case.com"
        columns = {c["name"] for c in inspect(connection).get_columns("users")}
        assert "mute_new_post_notifications" in columns
        assert "ux_users_email" in {i["name"] for i in inspect(connection).get_indexes("users")}


def test_unique_email_migration_names_duplicate_accounts(tmp_path):
    from app.migrate import upgrade_database
    engine = create_engine(f"sqlite:///{tmp_path / 'dupes.db'}")
    upgrade_database(engine, "0001")
    with engine.begin() as connection:
        for username, email in (("one", "Ava@Example.com"), ("two", "ava@example.com")):
            connection.execute(text("INSERT INTO users (dob, email, password_hash, role, is_deleted, username, theme, email_confirmed, "
                                    "phone_confirmed, mute_like_notifications, mute_comment_notifications, mute_follow_notifications, "
                                    "mute_new_post_notifications) VALUES ('x', :e, 'h', 'User', 0, :u, 'system', 0, 0, 0, 0, 0, 0)"),
                               {"e": email, "u": username})
    with pytest.raises(RuntimeError, match="ava@example.com"):
        upgrade_database(engine)
