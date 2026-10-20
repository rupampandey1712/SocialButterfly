from datetime import datetime, timezone

from app.security.bot_defense import BotDefense, ProofOfWork
from app.services import service_bus
from app.services.identity_rules import clean_media_title, normalize_email, normalize_phone, normalize_username, username_seed
import importlib
from fastapi.testclient import TestClient


def test_bot_defense_challenge_is_single_use_and_scope_bound():
    pow = ProofOfWork("test-secret-that-is-long-enough")
    challenge = pow.issue(1, "login")
    nonce = 0
    while ProofOfWork.leading_zero_bits(__import__("hashlib").sha256(f"{challenge}:{nonce}".encode()).digest()) < 1:
        nonce += 1
    proof = f"{challenge}:{nonce}"
    assert pow.verify(proof, 1, "login")
    assert not pow.verify(proof, 1, "login")


def test_bot_defense_escalates_after_repeated_failures():
    defense = BotDefense()
    assert defense.required_difficulty("127.0.0.1", "user@example.com") == 0
    for _ in range(5):
        defense.record_failure("127.0.0.1", "user@example.com")
    assert defense.required_difficulty("127.0.0.1", "user@example.com") > 0


def test_notification_queue_selection_matches_dotnet_contract():
    assert service_bus.notification_queue("PostLiked") == "post-notifications"
    assert service_bus.notification_queue("PostCreated") == "post-notifications"
    assert service_bus.notification_queue("FriendRequestSent") == "friend-requests"
    assert service_bus.notification_queue("UserFollowed") == "friend-requests"


def test_notification_event_uses_dotnet_pascal_case_payload(monkeypatch):
    captured = {}

    def fake_publish(queue, payload, message_id=None, subject=None):
        captured.update(queue=queue, payload=payload, message_id=message_id, subject=subject)
        return True

    monkeypatch.setattr(service_bus, "publish", fake_publish)
    assert service_bus.publish_notification("PostCommented", 3, post_id=8, text="hello")
    assert captured["queue"] == "post-notifications"
    assert set(captured["payload"]) == {"EventId", "Type", "ActorId", "RecipientId", "PostId", "Text", "OccurredAt"}
    assert captured["payload"]["ActorId"] == 3
    assert captured["payload"]["PostId"] == 8
    assert captured["subject"] == "NotificationEvent"
    assert datetime.fromisoformat(captured["payload"]["OccurredAt"].replace("Z", "+00:00")).tzinfo == timezone.utc


def test_identity_and_media_rules_match_dotnet_normalization():
    assert normalize_email("A@B.C") == "a@b.c"
    assert normalize_email("bad@@example.com") is None
    assert normalize_phone("+91 (98765) 43210") == "+919876543210"
    assert normalize_phone("+91 " + " " * 20 + "9876543210") is None
    assert normalize_username("@Ava.Martin") == "ava.martin"
    assert username_seed("Ava Martin", None) == "ava.martin"
    assert clean_media_title("", "summer_track.mp3") == "summer track"
    assert clean_media_title("x" * 130) == "x" * 120


def test_account_registration_and_verification_flow(tmp_path, monkeypatch):
    monkeypatch.setenv("DATABASE_URL", f"sqlite:///{tmp_path / 'api-test.db'}")
    monkeypatch.setenv("APP_ENV", "Development")
    monkeypatch.setenv("JWT_SECRET", "test-secret-at-least-thirty-two-characters-long")
    monkeypatch.setenv("ADMIN_EMAIL", "")
    monkeypatch.delenv("AZURE_SERVICEBUS_CONNECTION_STRING", raising=False)
    import app.main as backend
    backend = importlib.reload(backend)

    with TestClient(backend.app) as client:
        session = client.get("/api/Account/session")
        assert session.status_code == 200
        csrf = client.cookies.get("XSRF-TOKEN")
        created = client.post("/api/Account/register", data={
            "name": "Ava Martin", "email": "ava@example.com", "password": "safe-password-123",
        }, headers={"X-XSRF-TOKEN": csrf})
        assert created.status_code == 200
        assert created.json()["username"] == "ava.martin"
        sent = client.post("/api/Account/code/send", json={"target": "ava@example.com", "purpose": "Verify"},
                           headers={"X-XSRF-TOKEN": csrf})
        assert sent.status_code == 200
        assert len(sent.json()["devCode"]) == 6
        confirmed = client.post("/api/Account/code/confirm", json={
            "target": "ava@example.com", "purpose": "Verify", "code": sent.json()["devCode"],
        }, headers={"X-XSRF-TOKEN": csrf})
        assert confirmed.status_code == 200
        assert confirmed.json() == {"verified": True}
        unknown_reset = client.post("/api/Account/code/send", json={"Target":"nobody@example.com","Purpose":"Reset"},
                                    headers={"X-XSRF-TOKEN":csrf})
        assert unknown_reset.status_code == 200 and unknown_reset.json()["devCode"] is None
        reset = client.post("/api/Account/code/send", json={"Target":"ava@example.com","Purpose":"Reset"},
                            headers={"X-XSRF-TOKEN":csrf})
        assert reset.status_code == 200 and reset.json()["destination"] == "av***@example.com"
        reset_confirmed = client.post("/api/Account/code/confirm", json={
            "Target":"ava@example.com","Purpose":"Reset","Code":reset.json()["devCode"],
            "NewPassword":"new-safe-password-456",
        }, headers={"X-XSRF-TOKEN":csrf})
        assert reset_confirmed.status_code == 200 and reset_confirmed.json()["id"] == created.json()["id"]


def test_authenticated_post_social_and_chat_flows(tmp_path, monkeypatch):
    monkeypatch.setenv("DATABASE_URL", f"sqlite:///{tmp_path / 'social-test.db'}")
    monkeypatch.setenv("APP_ENV", "Development")
    monkeypatch.setenv("JWT_SECRET", "test-secret-at-least-thirty-two-characters-long")
    monkeypatch.setenv("ADMIN_EMAIL", "")
    monkeypatch.delenv("AZURE_SERVICEBUS_CONNECTION_STRING", raising=False)
    import app.main as backend
    backend = importlib.reload(backend)

    def create_member(name, email):
        client = TestClient(backend.app)
        client.get("/api/Account/session")
        csrf = client.cookies.get("XSRF-TOKEN")
        created = client.post("/api/Account/register", data={
            "name": name, "email": email, "password": "safe-password-123",
        }, headers={"X-XSRF-TOKEN": csrf})
        assert created.status_code == 200, created.text
        login = client.post("/api/Account/login", data={
            "identifier": email, "password": "safe-password-123",
        }, headers={"X-XSRF-TOKEN": csrf})
        assert login.status_code == 200, login.text
        return client, created.json()["id"], csrf

    with TestClient(backend.app) as lifecycle:
        # Keep the application lifespan active while independent users exercise cookie auth.
        anonymous = TestClient(backend.app)
        assert anonymous.get("/api/Post/feed").status_code == 401
        alice, alice_id, alice_csrf = create_member("Alice Example", "alice@example.com")
        bob, bob_id, bob_csrf = create_member("Bob Example", "bob@example.com")
        assert alice.get("/api/Admin/GetUserDetail").status_code == 403
        assert alice.get("/WeatherForecast").status_code == 403
        settings = alice.get("/api/Account/me/settings")
        assert settings.status_code == 200 and "role" not in settings.json()
        changed = alice.put("/api/Account/me/settings", data={"theme":"dark","notifyLikes":"false"},
                            headers={"X-XSRF-TOKEN":alice_csrf})
        assert changed.status_code == 200 and changed.json()["theme"] == "dark" and changed.json()["notifyLikes"] is False
        assert alice.get("/api/Account/Get").status_code == 200
        assert "value" not in alice.get("/api/Account/Get").json()[0]
        assert next(row for row in alice.get("/api/Account/Get").json() if row["id"]==alice_id)["dob"] == "0001-01-01T00:00:00"
        assert any(row["id"] == bob_id for row in alice.get("/api/Account/Search?Name=Bob").json())
        assert alice.get("/api/Account/availability?username=alice.example").json()["usernameAvailable"] is False
        assert alice.get(f"/api/Account/{alice_id}/profile").json()["friendshipStatus"] == "Self"
        assert alice.get(f"/api/Account/{alice_id}").status_code == 200
        legacy_profile = alice.put("/api/Account", data={"id":alice_id,"name":"Alice Updated"},
                                   headers={"X-XSRF-TOKEN":alice_csrf})
        assert legacy_profile.status_code == 200 and legacy_profile.json()["name"] == "Alice Updated"
        people = alice.get("/api/Follows/people?q=Bob")
        assert people.status_code == 200 and any(row["id"] == bob_id for row in people.json())
        assert next(row for row in alice.get("/api/Follows/people").json() if row["id"]==alice_id)["isSelf"] is True
        assert any(row["id"] == bob_id for row in alice.get("/api/Follows/suggestions").json())

        class FakeMediaStorage:
            def save_upload(self, upload, folder): return f"/{folder}/{upload.filename}"
            def save_bytes(self, data, filename, folder): return f"/{folder}/{filename}"
            def delete(self, path, folder): pass
            def public_url(self, path): return path

        backend.app.state.ctx.storage = FakeMediaStorage()
        story = alice.post("/api/Stories", data={"caption": "hello"},
                           files={"media": ("story.jpg", b"\xff\xd8\xff\xe0image-data", "image/jpeg")},
                           headers={"X-XSRF-TOKEN": alice_csrf})
        assert story.status_code == 200 and story.json()["seen"] is True, story.text

        post = alice.post("/api/Post", data={"description": "A parity smoke test"}, headers={"X-XSRF-TOKEN": alice_csrf})
        assert post.status_code == 200, post.text
        post_id = post.json()["id"]
        assert post.json()["userId"] == alice_id
        assert any(row["id"] == post_id for row in bob.get("/api/Post/feed").json())
        assert any(row["id"] == post_id for row in alice.get("/api/Post/UserPost").json())
        assert any(row["id"] == post_id for row in alice.get(f"/api/Post/user/{alice_id}").json())
        assert any(row["id"] == post_id for row in alice.get(f"/api/Post/{alice_id}").json())
        assert alice.get(f"/api/Post/details/{post_id}").json()["id"] == post_id
        assert alice.get("/api/Post").status_code == 204

        comment = bob.post("/api/Comment", json={"PostId": post_id, "Text": "Looks good"}, headers={"X-XSRF-TOKEN": bob_csrf})
        assert comment.status_code == 200, comment.text
        assert bob.get(f"/api/Comment/{post_id}").json()[0]["text"] == "Looks good"
        legacy_comment = bob.post("/api/Post/Comments", json={"PostId":post_id,"Text":"Legacy comment"},
                                  headers={"X-XSRF-TOKEN":bob_csrf})
        assert legacy_comment.status_code == 200 and legacy_comment.json()["comment"]["text"] == "Legacy comment"
        assert alice.get("/api/Post").json()[0]["userId"] == bob_id
        like = bob.post(f"/api/Likes/toggle?postId={post_id}", headers={"X-XSRF-TOKEN": bob_csrf})
        assert like.status_code == 200 and like.json()["isLiked"] is True
        # The legacy .NET endpoint adds a like once and returns the integer count.
        assert alice.post(f"/api/Likes?postId={post_id}", headers={"X-XSRF-TOKEN": alice_csrf}).json() == 2

        request = alice.post("/api/Friends", json={"ReceiverId": bob_id}, headers={"X-XSRF-TOKEN": alice_csrf})
        assert request.status_code == 200 and request.json()["isAccepted"] is False
        assert alice.get(f"/api/Friends/status/{bob_id}").json()["status"] == "RequestSent"
        assert bob.get("/api/Friends/me/requestuser").json()["value"][0]["id"] == alice_id
        accepted = bob.post(f"/api/Friends/{alice_id}", headers={"X-XSRF-TOKEN": bob_csrf})
        assert accepted.status_code == 200 and accepted.json()["isAccepted"] is True
        assert bob.get("/api/Friends/me/requests").json()["value"] == 0
        assert bob.get(f"/api/Friends/status/{alice_id}").json()["status"] == "Friends"
        assert alice.get(f"/api/Friends/{alice_id}/friendscount").json()["value"] == 1
        assert alice.get(f"/api/Friends/{alice_id}/friends").json()
        assert alice.get(f"/api/Friends/{alice_id}/myfriends").json()["value"][0]["id"] == bob_id

        bob_story_feed = bob.get("/api/Stories/feed").json()
        assert any(group["user"]["id"] == alice_id for group in bob_story_feed)
        assert alice.get(f"/api/Stories/user/{alice_id}").json()[0]["isOwn"] is True
        viewed = bob.post(f"/api/Stories/{story.json()['id']}/view", headers={"X-XSRF-TOKEN": bob_csrf})
        assert viewed.status_code == 204
        viewers = alice.get(f"/api/Stories/{story.json()['id']}/viewers")
        assert viewers.status_code == 200 and viewers.json()[0]["user"]["id"] == bob_id

        followed = alice.post(f"/api/Follows/{bob_id}/toggle", headers={"X-XSRF-TOKEN": alice_csrf})
        assert followed.status_code == 200 and followed.json()["isFollowing"] is True
        assert alice.get(f"/api/Follows/{alice_id}/following").json()[0]["id"] == bob_id
        assert bob.get(f"/api/Follows/{bob_id}/followers").json()[0]["id"] == alice_id
        assert any(row["type"] == "UserFollowed" for row in bob.get("/api/Notifications/me").json())
        assert bob.get("/api/Notifications/me/unread-count").json()["count"] >= 1
        read_notifications = bob.post("/api/Notifications/me/read", headers={"X-XSRF-TOKEN": bob_csrf})
        assert read_notifications.status_code == 204

        sent = alice.post("/api/Chat", json={"ReceiverId": bob_id, "Content": "Hello"}, headers={"X-XSRF-TOKEN": alice_csrf})
        assert sent.status_code == 202, sent.text
        messages = bob.get(f"/api/Chat/with/{alice_id}").json()
        assert len(messages) == 1 and messages[0]["content"] == "Hello"
        assert bob.put(f"/api/Chat/with/{alice_id}/read", headers={"X-XSRF-TOKEN": bob_csrf}).json()["markedRead"] == 1
        assert bob.get("/api/Notifications/me").status_code == 200

        assert alice.delete(f"/api/Stories/{story.json()['id']}", headers={"X-XSRF-TOKEN": alice_csrf}).status_code == 204
        assert alice.delete(f"/api/Friends/{alice_id}/{bob_id}", headers={"X-XSRF-TOKEN": alice_csrf}).json() == {"success":True}
        second_request = alice.post("/api/Friends", json={"receiverId":bob_id}, headers={"X-XSRF-TOKEN":alice_csrf})
        assert second_request.status_code == 200
        old_accept_route = bob.post(f"/api/Friends/{alice_id}/{bob_id}", headers={"X-XSRF-TOKEN":bob_csrf})
        assert old_accept_route.status_code == 200 and old_accept_route.json()["isAccepted"] is True
        assert alice.delete(f"/api/Friends/{alice_id}/{bob_id}", headers={"X-XSRF-TOKEN":alice_csrf}).json() == {"success":True}
        unfollowed = alice.post(f"/api/Follows/{bob_id}/toggle", headers={"X-XSRF-TOKEN":alice_csrf})
        assert unfollowed.status_code == 200 and unfollowed.json()["isFollowing"] is False
        assert alice.delete(f"/api/Post/{post_id}", headers={"X-XSRF-TOKEN": alice_csrf}).status_code == 204
        changed_password = alice.put("/api/Account/me/password", json={"CurrentPassword":"safe-password-123","NewPassword":"another-safe-password-456"},
                                     headers={"X-XSRF-TOKEN":alice_csrf})
        assert changed_password.status_code == 204

        alice.close()
        bob.close()
        anonymous.close()


def test_admin_image_and_genai_controller_routes(tmp_path, monkeypatch):
    monkeypatch.setenv("DATABASE_URL", f"sqlite:///{tmp_path / 'admin-ai-test.db'}")
    monkeypatch.setenv("APP_ENV", "Development")
    monkeypatch.setenv("JWT_SECRET", "test-secret-at-least-thirty-two-characters-long")
    monkeypatch.setenv("ADMIN_EMAIL", "")
    monkeypatch.setenv("NIM_BASE_URL", "https://nim.invalid/v1")
    monkeypatch.setenv("NVIDIA_API_KEY", "test-key")
    monkeypatch.setenv("NIM_MODEL", "test-model")
    monkeypatch.delenv("AZURE_SERVICEBUS_CONNECTION_STRING", raising=False)
    import app.main as backend
    backend = importlib.reload(backend)

    client = TestClient(backend.app)
    client.get("/api/Account/session")
    csrf = client.cookies.get("XSRF-TOKEN")
    registered = client.post("/api/Account/register", data={
        "name":"Admin Tester","email":"admin@example.com","password":"safe-password-123",
    }, headers={"X-XSRF-TOKEN":csrf})
    assert registered.status_code == 200
    admin_id = registered.json()["id"]
    with backend.SessionLocal() as session:
        session.get(backend.User, admin_id).role = "Admin"
        session.commit()
    assert client.post("/api/Account/login", data={"identifier":"admin@example.com","password":"safe-password-123"},
                       headers={"X-XSRF-TOKEN":csrf}).status_code == 200
    assert len(client.get("/WeatherForecast").json()) == 5
    assert client.get("/api/Admin/GetUserDetail").status_code == 200
    assert client.get("/api/Admin/PostByDate").json() == []
    assert client.put(f"/api/Admin?id={admin_id}&IsDeleted=false", headers={"X-XSRF-TOKEN":csrf}).status_code == 200

    async def no_image_work(_job_id):
        return None

    monkeypatch.setattr(backend.app.state.ctx, "process_image", no_image_work)

    class FakeAsyncClient:
        async def __aenter__(self): return self
        async def __aexit__(self, *_args): return None
        async def get(self, *_args, **_kwargs):
            return type("Response", (), {"is_success":True})()

    import httpx
    monkeypatch.setattr(httpx, "AsyncClient", FakeAsyncClient)
    status = client.get("/api/Images/status")
    assert status.status_code == 200 and isinstance(status.json()["online"], bool)
    assert status.json()["remainingToday"] == 25 and "none" in status.json()["styles"]
    queued = client.post("/api/Images", json={"Prompt":"A quiet mountain lake","Aspect":"portrait","Style":"anime"},
                         headers={"X-XSRF-TOKEN":csrf})
    assert queued.status_code == 200, queued.text
    job_id = queued.json()["id"]
    assert client.get("/api/Images/mine").json()[0]["id"] == job_id
    assert client.get(f"/api/Images/{job_id}").status_code == 200
    assert client.post(f"/api/Images/{job_id}/share", json={}, headers={"X-XSRF-TOKEN":csrf}).status_code == 400
    with backend.SessionLocal() as session:
        job=session.get(backend.GeneratedImage,job_id)
        job.status="Succeeded"
        job.image_path="/ai-images/generated.png"
        session.commit()
    shared = client.post(f"/api/Images/{job_id}/share", json={"Caption":"A generated lake"}, headers={"X-XSRF-TOKEN":csrf})
    assert shared.status_code == 200 and shared.json()["postId"] > 0

    class FakeCompletions:
        def create(self, **_kwargs):
            return type("Result", (), {"choices":[type("Choice", (), {
                "message":type("Message", (), {"content":"A generated response"})()
            })()]})()

    class FakeOpenAIClient:
        def __init__(self, **_kwargs):
            self.chat=type("Chat", (), {"completions":FakeCompletions()})()

    import openai
    monkeypatch.setattr(openai, "OpenAI", FakeOpenAIClient)
    generated = client.post("/api/GenAi/generate-text", json={"Prompt":"Summarize a mountain lake"},
                            headers={"X-XSRF-TOKEN":csrf})
    assert generated.status_code == 200 and generated.json()["text"] == "A generated response"
    client.close()


