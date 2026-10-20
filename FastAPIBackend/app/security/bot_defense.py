"""In-memory bot risk scoring and stateless single-use proof-of-work challenges."""

from __future__ import annotations

import base64
import hashlib
import hmac
import json
import secrets
import threading
import time
from collections import deque
from typing import Callable


class BotDefense:
    WINDOW = 15 * 60
    ATTACK_WINDOW = 5 * 60
    FREE_IP_FAILURES = 3
    FREE_ACCOUNT_FAILURES = 3
    DISTINCT_IDENTIFIERS = 5
    GLOBAL_ATTACK_FAILURES = 100
    BASE_DIFFICULTY = 16
    MAX_DIFFICULTY = 22
    # Memory bounds: scoring only needs "more than DISTINCT_IDENTIFIERS" names per IP (the .NET
    # tracker keeps at most 50), and a hard cap per table evicts the least recently seen entries.
    MAX_IDENTIFIERS = 50
    MAX_TRACKERS = 100_000

    def __init__(self, clock: Callable[[], float] = time.time) -> None:
        self._clock = clock
        self._lock = threading.RLock()
        # Insertion order doubles as recency order: _tracker moves a key to the end on every touch.
        self._ip: dict[str, dict] = {}
        self._account: dict[str, dict] = {}
        self._recent: deque[float] = deque(maxlen=self.GLOBAL_ATTACK_FAILURES)

    @staticmethod
    def account_key(identifier: str | None) -> str | None:
        if not identifier or not identifier.strip(): return None
        return identifier.strip().lstrip("@").lower()[:256]

    def _tracker(self, store: dict, key: str, now: float) -> dict:
        value=store.pop(key,None)
        if not value or now-value["at"]>self.WINDOW:
            value={"failures":0,"identifiers":set(),"flagged":False,"at":now}
        else: value["at"]=now
        store[key]=value
        while len(store)>self.MAX_TRACKERS: store.pop(next(iter(store)))
        return value

    def _live(self, store: dict, key: str | None, now: float) -> dict | None:
        value=store.get(key) if key else None
        return value if value and now-value["at"]<=self.WINDOW else None

    def required_difficulty(self, ip: str, account: str | None) -> int:
        now=self._clock()
        with self._lock:
            self._trim(now); ipdata=self._live(self._ip,ip,now); accountdata=self._live(self._account,account,now)
            score=0
            if ipdata:
                score+=max(ipdata["failures"]-self.FREE_IP_FAILURES,0)
                if len(ipdata["identifiers"])>self.DISTINCT_IDENTIFIERS: score+=4
                if ipdata["flagged"]: score+=10
            if accountdata: score+=max(accountdata["failures"]-self.FREE_ACCOUNT_FAILURES,0)
            if score: return min(self.BASE_DIFFICULTY+score,self.MAX_DIFFICULTY)
            return self.BASE_DIFFICULTY if len(self._recent)>=self.GLOBAL_ATTACK_FAILURES else 0

    def record_failure(self, ip: str, account: str | None) -> None:
        now=self._clock()
        with self._lock:
            tracker=self._tracker(self._ip,ip,now); tracker["failures"]+=1
            if account:
                if len(tracker["identifiers"])<self.MAX_IDENTIFIERS: tracker["identifiers"].add(account)
                self._tracker(self._account,account,now)["failures"]+=1
            self._recent.append(now); self._trim(now)

    def record_success(self, ip: str, account: str | None) -> None:
        now=self._clock()
        with self._lock:
            if account: self._account.pop(account,None)
            tracker=self._ip.get(ip)
            if tracker and not tracker["flagged"]: self._ip.pop(ip,None)
            self._trim(now)

    def flag_bot(self, ip: str) -> None:
        with self._lock: self._tracker(self._ip,ip,self._clock())["flagged"]=True

    def tracked(self) -> tuple[int, int]:
        """(IP trackers, account trackers) currently held in memory."""
        with self._lock: return len(self._ip),len(self._account)

    def _trim(self, now: float) -> None:
        while self._recent and now-self._recent[0]>self.ATTACK_WINDOW: self._recent.popleft()
        # Trackers are in least-recently-touched order, so expired ones are all at the front.
        for store in (self._ip,self._account):
            while store:
                key=next(iter(store))
                if now-store[key]["at"]<=self.WINDOW: break
                del store[key]


class ProofOfWork:
    LIFETIME=300

    def __init__(self, secret: str) -> None:
        derived=hmac.new(secret.encode(),b"socialbutterfly-pow-v1",hashlib.sha256).digest()
        self._key=derived; self._used: dict[str,float]={}; self._lock=threading.Lock()

    @staticmethod
    def _b64(value: bytes) -> str: return base64.urlsafe_b64encode(value).rstrip(b"=").decode()

    def _sign(self, body: str) -> str: return self._b64(hmac.new(self._key,body.encode("ascii"),hashlib.sha256).digest())

    def issue(self, difficulty: int, scope: str) -> str:
        payload={"S":secrets.token_hex(16).upper(),"D":difficulty,"E":int(time.time())+self.LIFETIME,"Sc":scope}
        body=self._b64(json.dumps(payload,separators=(",",":" )).encode())
        return body+"."+self._sign(body)

    @staticmethod
    def leading_zero_bits(digest: bytes) -> int:
        bits=0
        for byte in digest:
            if byte==0: bits+=8; continue
            bits+=8-byte.bit_length(); break
        return bits

    def verify(self, proof: str | None, required: int, scope: str) -> bool:
        if not proof or len(proof)>512 or ":" not in proof: return False
        challenge,nonce=proof.rsplit(":",1)
        if not nonce.isascii() or not nonce.isdigit() or not 1<=len(nonce)<=20: return False
        try: body,signature=challenge.split(".",1)
        except ValueError: return False
        if not hmac.compare_digest(signature,self._sign(body)): return False
        try:
            payload=json.loads(base64.urlsafe_b64decode(body+"="*((4-len(body)%4)%4)))
            difficulty=int(payload["D"]); expiry=int(payload["E"])
        except (ValueError,KeyError,TypeError,json.JSONDecodeError): return False
        if payload.get("Sc")!=scope or difficulty<required or expiry<int(time.time()): return False
        if self.leading_zero_bits(hashlib.sha256(f"{challenge}:{nonce}".encode()).digest())<difficulty: return False
        now=time.time()
        with self._lock:
            self._used={key:until for key,until in self._used.items() if until>now}
            if signature in self._used: return False
            self._used[signature]=now+self.LIFETIME
        return True
