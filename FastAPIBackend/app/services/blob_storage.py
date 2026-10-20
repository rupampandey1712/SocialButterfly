"""Azure Blob Storage adapter with the same local paths used by development mode."""

from __future__ import annotations

import mimetypes
import os
import re
import uuid
from pathlib import Path
from urllib.parse import unquote, urlparse

from fastapi import UploadFile


class BlobStorage:
    CONTAINERS={"Images":"profile-images","posts":"post-images","stories":"stories","music":"music","ai-images":"ai-images"}

    def __init__(self, local_root:Path):
        self.local_root=local_root
        self.connection=os.getenv("AZURE_STORAGE_CONNECTION_STRING")
        self.public_base=os.getenv("BLOB_PUBLIC_BASE_URL","").rstrip("/")
        self._client=None
        if self.connection:
            from azure.storage.blob import BlobServiceClient
            self._client=BlobServiceClient.from_connection_string(self.connection)

    @staticmethod
    def safe_name(original:str|None)->str:
        filename=Path(original or "").name
        ext=Path(filename).suffix.lower()
        if len(ext)>6 or (ext and not re.fullmatch(r"\.[a-z0-9]+",ext)): ext=""
        stem=Path(filename).stem
        stem="".join(c if c.isascii() and c.isalnum() or c in "-_" else "_" for c in stem).strip("_")[:40] or "file"
        return f"{stem}_{uuid.uuid4().hex}{ext}"

    def save_upload(self,upload:UploadFile,folder:str)->str:
        container=self.CONTAINERS[folder]; name=self.safe_name(upload.filename)
        content_type=mimetypes.guess_type(name)[0] or "application/octet-stream"
        if self._client:
            blob=self._client.get_container_client(container)
            try: blob.create_container(public_access="blob")
            except Exception as exc:
                if getattr(exc,"status_code",None)!=409: raise
            client=blob.get_blob_client(name)
            upload.file.seek(0)
            from azure.storage.blob import ContentSettings
            client.upload_blob(upload.file,overwrite=True,content_settings=ContentSettings(content_type=content_type,cache_control="public, max-age=31536000, immutable"))
            return self.public_url(client.url)
        directory=self.local_root/folder; directory.mkdir(parents=True,exist_ok=True)
        upload.file.seek(0)
        with (directory/name).open("wb") as target:
            import shutil
            shutil.copyfileobj(upload.file,target)
        return f"/{folder}/{name}"

    def save_bytes(self,data:bytes,filename:str,folder:str)->str:
        container=self.CONTAINERS[folder]; name=self.safe_name(filename)
        content_type=mimetypes.guess_type(name)[0] or "application/octet-stream"
        if self._client:
            blob=self._client.get_container_client(container)
            try: blob.create_container(public_access="blob")
            except Exception as exc:
                if getattr(exc,"status_code",None)!=409: raise
            client=blob.get_blob_client(name)
            from azure.storage.blob import ContentSettings
            client.upload_blob(data,overwrite=True,content_settings=ContentSettings(content_type=content_type,cache_control="public, max-age=31536000, immutable"))
            return self.public_url(client.url)
        directory=self.local_root/folder; directory.mkdir(parents=True,exist_ok=True); (directory/name).write_bytes(data)
        return f"/{folder}/{name}"

    def delete(self,url:str|None,folder:str)->None:
        if not url:return
        name=unquote(Path(urlparse(url).path).name)
        if not name:return
        if self._client:
            from azure.core.exceptions import ResourceNotFoundError
            try: self._client.get_container_client(self.CONTAINERS[folder]).delete_blob(name,delete_snapshots="include")
            except ResourceNotFoundError: pass
        else:
            path=self.local_root/folder/name
            if path.is_file(): path.unlink()

    def public_url(self,value:str|None)->str|None:
        if not value:return value
        parsed=urlparse(value)
        if not parsed.scheme:return value
        if self.public_base:
            base=urlparse(self.public_base)
            return parsed._replace(scheme=base.scheme,netloc=base.netloc).geturl()
        if parsed.hostname=="azurite": return parsed._replace(netloc=(parsed.netloc.replace("azurite","localhost",1))).geturl()
        return value
