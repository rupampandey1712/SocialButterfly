import importlib


def test_fastapi_declares_every_dotnet_controller_route(tmp_path, monkeypatch):
    monkeypatch.setenv("DATABASE_URL", f"sqlite:///{tmp_path / 'route-inventory.db'}")
    monkeypatch.setenv("APP_ENV", "Development")
    monkeypatch.setenv("JWT_SECRET", "route-inventory-test-secret-at-least-32-chars")
    monkeypatch.setenv("ADMIN_EMAIL", "")
    import app.main as backend
    backend = importlib.reload(backend)

    expected = {
        ("GET", "/WeatherForecast"),
        ("GET", "/api/Account/session"), ("POST", "/api/Account/logout"),
        ("POST", "/api/Account/register"), ("POST", "/api/Account/login"),
        ("GET", "/api/Account/availability"), ("POST", "/api/Account/code/send"),
        ("POST", "/api/Account/code/confirm"), ("GET", "/api/Account/me/settings"),
        ("PUT", "/api/Account/me/settings"), ("PUT", "/api/Account/me/password"),
        ("PUT", "/api/Account"), ("GET", "/api/Account/{user_id}"),
        ("GET", "/api/Account/{user_id}/profile"), ("GET", "/api/Account/Get"),
        ("GET", "/api/Account/Search"),
        ("POST", "/api/Post"), ("GET", "/api/Post"), ("POST", "/api/Post/Comments"),
        ("GET", "/api/Post/UserPost"), ("GET", "/api/Post/feed"),
        ("GET", "/api/Post/user/{user_id}"), ("GET", "/api/Post/{user_id}"),
        ("GET", "/api/Post/details/{post_id}"), ("DELETE", "/api/Post/{post_id}"),
        ("POST", "/api/Likes"), ("POST", "/api/Likes/toggle"),
        ("POST", "/api/Comment"), ("GET", "/api/Comment/{post_id}"),
        ("POST", "/api/Friends"), ("POST", "/api/Friends/{sender_id}"),
        ("POST", "/api/Friends/{sender_id}/{receiver_id}"),
        ("DELETE", "/api/Friends/{sender_id}/{receiver_id}"),
        ("GET", "/api/Friends/status/{other_id}"), ("GET", "/api/Friends/me/requests"),
        ("GET", "/api/Friends/me/requestuser"), ("GET", "/api/Friends/{user_id}/friends"),
        ("GET", "/api/Friends/{user_id}/myfriends"),
        ("GET", "/api/Friends/{user_id}/friendscount"),
        ("POST", "/api/Follows/{target_id}/toggle"),
        ("GET", "/api/Follows/{user_id}/followers"),
        ("GET", "/api/Follows/{user_id}/following"),
        ("GET", "/api/Follows/people"), ("GET", "/api/Follows/suggestions"),
        ("POST", "/api/Chat"), ("GET", "/api/Chat/with/{other_id}"),
        ("PUT", "/api/Chat/with/{other_id}/read"),
        ("GET", "/api/Notifications/me"), ("GET", "/api/Notifications/me/unread-count"),
        ("POST", "/api/Notifications/me/read"),
        ("POST", "/api/Stories"), ("GET", "/api/Stories/feed"),
        ("GET", "/api/Stories/user/{user_id}"), ("POST", "/api/Stories/{story_id}/view"),
        ("GET", "/api/Stories/{story_id}/viewers"), ("DELETE", "/api/Stories/{story_id}"),
        ("GET", "/api/Admin/GetUserDetail"), ("GET", "/api/Admin/PostByDate"),
        ("PUT", "/api/Admin"), ("GET", "/api/Images/status"),
        ("POST", "/api/Images"), ("GET", "/api/Images/mine"),
        ("GET", "/api/Images/{job_id}"), ("POST", "/api/Images/{job_id}/share"),
        ("POST", "/api/GenAi/generate-text"),
    }
    # Routers are mounted with include_router, so read the routes from the OpenAPI schema
    # (newer FastAPI versions don't flatten included routes into app.routes).
    actual = {
        (method.upper(), path)
        for path, operations in backend.app.openapi()["paths"].items()
        for method in operations
        if method.upper() in {"GET", "POST", "PUT", "DELETE"}
    }
    assert expected <= actual, f"Missing controller routes: {sorted(expected - actual)}"
