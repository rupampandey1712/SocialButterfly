"""Point every test run at throwaway storage before anything imports app.main.

Importing app.main builds an application (and its database and media folder) from the environment;
without this, a test run would create socialmedia.db and media/ inside the project folder.
"""

import os
import tempfile

_scratch = tempfile.mkdtemp(prefix="socialmedia-tests-")
os.environ.setdefault("DATABASE_URL", f"sqlite:///{os.path.join(_scratch, 'default.db')}")
os.environ.setdefault("MEDIA_ROOT", os.path.join(_scratch, "media"))
os.environ.setdefault("APP_ENV", "Development")
os.environ.setdefault("JWT_SECRET", "test-secret-at-least-thirty-two-characters-long")
os.environ["ADMIN_EMAIL"] = ""
os.environ.pop("AZURE_SERVICEBUS_CONNECTION_STRING", None)
os.environ.pop("AZURE_STORAGE_CONNECTION_STRING", None)
