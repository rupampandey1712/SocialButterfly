import { useEffect, useState } from "react";
import "./toaster.css";

export function showToast(message) {
  window.dispatchEvent(new CustomEvent("app:toast", { detail: message }));
}

export default function Toaster() {
  const [toasts, setToasts] = useState([]);

  useEffect(() => {
    const onToast = (event) => {
      const id = Date.now() + Math.random();
      setToasts((prev) => [...prev, { id, message: event.detail }]);
      setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== id)), 2800);
    };
    window.addEventListener("app:toast", onToast);
    return () => window.removeEventListener("app:toast", onToast);
  }, []);

  return (
    <div className="toaster" aria-live="polite">
      {toasts.map((toast) => (
        <div key={toast.id} className="toast">{toast.message}</div>
      ))}
    </div>
  );
}
