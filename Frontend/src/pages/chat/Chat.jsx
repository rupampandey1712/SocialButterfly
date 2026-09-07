import axios from "axios";
import { ForumOutlined, SendRounded } from "@mui/icons-material";
import { useCallback, useEffect, useMemo, useState } from "react";
import Topbar from "../../components/topbar/Topbar";
import "./chat.css";

const apiOrigin = (import.meta.env.VITE_API_ORIGIN ?? "https://localhost:7151").replace(/\/$/, "");
const pollIntervalMs = 3000;

const createMessageId = () => (
  globalThis.crypto?.randomUUID?.()
    ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`
);

const formatTime = (value) => new Intl.DateTimeFormat(undefined, {
  hour: "numeric",
  minute: "2-digit",
}).format(new Date(value));

export default function Chat() {
  const currentUserId = Number(sessionStorage.getItem("Id"));
  const token = sessionStorage.getItem("tokens");
  const [contacts, setContacts] = useState([]);
  const [selectedContact, setSelectedContact] = useState(null);
  const [messages, setMessages] = useState([]);
  const [pendingMessages, setPendingMessages] = useState([]);
  const [draft, setDraft] = useState("");
  const [isSending, setIsSending] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");

  const authConfig = useMemo(() => ({
    headers: { Authorization: `Bearer ${token}` },
  }), [token]);

  useEffect(() => {
    if (!Number.isInteger(currentUserId) || currentUserId <= 0) {
      return;
    }

    axios.get(`${apiOrigin}/api/Account/Get`, authConfig)
      .then((response) => {
        setContacts((response.data ?? []).filter((user) => user.id !== currentUserId));
      })
      .catch(() => setError("Unable to load people to chat with."));
  }, [authConfig, currentUserId]);

  const loadConversation = useCallback(async () => {
    if (!selectedContact) {
      return;
    }

    try {
      setIsLoading(true);
      const response = await axios.get(
        `${apiOrigin}/api/Chat/with/${selectedContact.id}?limit=100`,
        authConfig,
      );
      const deliveredMessages = response.data ?? [];
      setMessages(deliveredMessages);
      setPendingMessages((pending) => pending.filter(
        (pendingMessage) => !deliveredMessages.some(
          (message) => message.messageId === pendingMessage.messageId,
        ),
      ));

      if (deliveredMessages.some(
        (message) => message.receiverId === currentUserId && !message.isRead,
      )) {
        await axios.put(
          `${apiOrigin}/api/Chat/with/${selectedContact.id}/read`,
          {},
          authConfig,
        );
      }
      setError("");
    } catch {
      setError("Conversation updates are temporarily unavailable. Retrying…");
    } finally {
      setIsLoading(false);
    }
  }, [authConfig, currentUserId, selectedContact]);

  useEffect(() => {
    loadConversation();
    if (!selectedContact) {
      return undefined;
    }

    const timer = window.setInterval(loadConversation, pollIntervalMs);
    return () => window.clearInterval(timer);
  }, [loadConversation, selectedContact]);

  const visibleMessages = useMemo(() => {
    const uniqueMessages = new Map();
    [...messages, ...pendingMessages].forEach((message) => {
      uniqueMessages.set(message.messageId, message);
    });

    return [...uniqueMessages.values()].sort(
      (left, right) => new Date(left.sentAt) - new Date(right.sentAt),
    );
  }, [messages, pendingMessages]);

  const selectContact = (contact) => {
    setSelectedContact(contact);
    setMessages([]);
    setPendingMessages([]);
    setError("");
  };

  const sendMessage = async (event) => {
    event.preventDefault();
    const content = draft.trim();
    if (!content || !selectedContact || isSending) {
      return;
    }

    const messageId = createMessageId();
    const optimisticMessage = {
      messageId,
      senderId: currentUserId,
      receiverId: selectedContact.id,
      content,
      sentAt: new Date().toISOString(),
      isPending: true,
    };

    setPendingMessages((pending) => [...pending, optimisticMessage]);
    setDraft("");
    setIsSending(true);

    try {
      await axios.post(`${apiOrigin}/api/Chat`, {
        receiverId: selectedContact.id,
        content,
        clientMessageId: messageId,
      }, authConfig);
      await loadConversation();
    } catch {
      setPendingMessages((pending) => pending.filter((message) => message.messageId !== messageId));
      setDraft(content);
      setError("Your message could not be queued. Please try again.");
    } finally {
      setIsSending(false);
    }
  };

  return (
    <div className="chatPage">
      <Topbar />
      <main className="chatShell">
        <aside className="chatContacts" aria-label="Chat contacts">
          <div className="chatContactsHeader">
            <ForumOutlined />
            <div>
              <p>Messages</p>
              <span>Choose someone to start a conversation</span>
            </div>
          </div>
          <div className="chatContactList">
            {contacts.map((contact) => (
              <button
                key={contact.id}
                type="button"
                className={`chatContact ${selectedContact?.id === contact.id ? "isSelected" : ""}`}
                onClick={() => selectContact(contact)}
              >
                <img src={contact.imagePath || "/assets/person/1.jpeg"} alt="" />
                <span>{contact.name || contact.email || "SocialButterfly member"}</span>
              </button>
            ))}
            {contacts.length === 0 && <p className="chatEmptyContacts">No other members are available yet.</p>}
          </div>
        </aside>

        <section className="chatThread" aria-live="polite">
          {!selectedContact ? (
            <div className="chatWelcome">
              <ForumOutlined />
              <h1>Your conversations live here</h1>
              <p>Select a member on the left. Messages are delivered reliably in the background.</p>
            </div>
          ) : (
            <>
              <header className="chatThreadHeader">
                <img src={selectedContact.imagePath || "/assets/person/1.jpeg"} alt="" />
                <div>
                  <h1>{selectedContact.name || selectedContact.email}</h1>
                  <p>{isLoading ? "Syncing conversation…" : "Messages are saved securely"}</p>
                </div>
              </header>

              <div className="chatMessages">
                {visibleMessages.length === 0 && (
                  <p className="chatThreadEmpty">No messages yet. Say hello.</p>
                )}
                {visibleMessages.map((message) => {
                  const isMine = message.senderId === currentUserId;
                  return (
                    <article
                      className={`chatBubble ${isMine ? "isMine" : ""}`}
                      key={message.messageId}
                    >
                      <p>{message.content}</p>
                      <span>
                        {formatTime(message.sentAt)}
                        {message.isPending ? " · Queued" : isMine && message.isRead ? " · Read" : ""}
                      </span>
                    </article>
                  );
                })}
              </div>

              <form className="chatComposer" onSubmit={sendMessage}>
                <label className="visually-hidden" htmlFor="chat-message">Message</label>
                <textarea
                  id="chat-message"
                  value={draft}
                  maxLength={2000}
                  rows="1"
                  placeholder={`Message ${selectedContact.name || "member"}`}
                  onChange={(event) => setDraft(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter" && !event.shiftKey) {
                      event.preventDefault();
                      sendMessage(event);
                    }
                  }}
                />
                <button type="submit" disabled={!draft.trim() || isSending} aria-label="Send message">
                  <SendRounded />
                </button>
              </form>
            </>
          )}
          {error && <p className="chatError" role="status">{error}</p>}
        </section>
      </main>
    </div>
  );
}
