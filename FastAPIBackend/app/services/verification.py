"""Optional SMTP and Twilio delivery adapters for one-time account codes."""

import os
import smtplib
import ssl
from email.message import EmailMessage

import httpx


def deliver_code(channel: str, target: str, purpose: str, code: str, expires_at) -> None:
    if channel == "Email":
        host = os.getenv("SMTP_HOST")
        sender = os.getenv("SMTP_FROM")
        if not host or not sender:
            raise RuntimeError("Email code delivery is not configured (SMTP_HOST and SMTP_FROM).")
        message = EmailMessage()
        message["Subject"] = "Your SocialMediaWeb verification code"
        message["From"] = sender
        message["To"] = target
        message.set_content(f"Your {purpose.lower()} code is {code}. It expires at {expires_at.isoformat()} UTC.")
        port = int(os.getenv("SMTP_PORT", "587"))
        with smtplib.SMTP(host, port, timeout=15) as server:
            if os.getenv("SMTP_STARTTLS", "1") == "1":
                server.starttls(context=ssl.create_default_context())
            username = os.getenv("SMTP_USERNAME")
            if username:
                server.login(username, os.getenv("SMTP_PASSWORD", ""))
            server.send_message(message)
        return

    sid, token, sender = (os.getenv(name, "") for name in ("TWILIO_ACCOUNT_SID", "TWILIO_AUTH_TOKEN", "TWILIO_FROM"))
    if not sid or not token or not sender:
        raise RuntimeError("SMS code delivery is not configured (Twilio credentials and TWILIO_FROM).")
    result = httpx.post(
        f"https://api.twilio.com/2010-04-01/Accounts/{sid}/Messages.json",
        data={"To": target, "From": sender, "Body": f"Your SocialMediaWeb {purpose.lower()} code is {code}."},
        auth=(sid, token), timeout=15,
    )
    result.raise_for_status()
