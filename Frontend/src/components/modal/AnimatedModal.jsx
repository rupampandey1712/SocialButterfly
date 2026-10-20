import { useCallback, useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { CloseRounded } from "@mui/icons-material";
import "./animatedModal.css";

const exitDurationMs = 180;

/**
 * Portal modal with enter/exit animations. `onClose` fires after the exit
 * animation finishes, so callers can simply unmount on close.
 */
export default function AnimatedModal({ onClose, children, className = "", showClose = true, label }) {
  const [isClosing, setIsClosing] = useState(false);

  const requestClose = useCallback(() => {
    setIsClosing(true);
    setTimeout(onClose, exitDurationMs);
  }, [onClose]);

  useEffect(() => {
    const onKeyDown = (event) => {
      if (event.key === "Escape") requestClose();
    };
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [requestClose]);

  return createPortal(
    <div
      className={`igModalBackdrop ${isClosing ? "isClosing" : ""}`}
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) requestClose();
      }}
    >
      {showClose && (
        <button type="button" className="igModalClose" onClick={requestClose} aria-label="Close">
          <CloseRounded />
        </button>
      )}
      <div className={`igModalPanel ${className}`} role="dialog" aria-modal="true" aria-label={label}>
        {typeof children === "function" ? children(requestClose) : children}
      </div>
    </div>,
    document.body
  );
}
