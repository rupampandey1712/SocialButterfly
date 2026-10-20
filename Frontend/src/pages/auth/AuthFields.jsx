import { useEffect, useRef, useState } from "react";
import { CancelRounded, CheckCircleRounded } from "@mui/icons-material";
import { passwordStrength } from "../../utils/account";

/**
 * Instagram-style input: the placeholder shrinks into a floating label once
 * there is a value. `status` is "valid" | "invalid" | "checking" | undefined.
 */
export function FloatingField({ label, value, onChange, type = "text", status, hint, action, inputRef, ...props }) {
  const [focused, setFocused] = useState(false);
  const isRaised = focused || Boolean(value);

  return (
    <div className={`authField ${isRaised ? "isRaised" : ""} ${focused ? "isFocused" : ""} ${status ? `is-${status}` : ""}`}>
      <label>
        <span className="authFieldLabel">{label}</span>
        <input
          ref={inputRef}
          type={type}
          value={value}
          onChange={(event) => onChange(event.target.value)}
          onFocus={() => setFocused(true)}
          onBlur={() => setFocused(false)}
          aria-invalid={status === "invalid"}
          {...props}
        />
      </label>
      <span className="authFieldAdornments">
        {status === "checking" && <span className="authFieldSpinner" aria-label="Checking" />}
        {status === "valid" && <CheckCircleRounded className="authFieldOk" aria-label="Looks good" />}
        {status === "invalid" && <CancelRounded className="authFieldBad" aria-label="Invalid" />}
        {action}
      </span>
      {hint && <p className={`authFieldHint ${status === "invalid" ? "isError" : ""}`}>{hint}</p>}
    </div>
  );
}

export function PasswordToggle({ visible, onToggle }) {
  return (
    <button type="button" className="authShowPassword" onClick={onToggle}>
      {visible ? "Hide" : "Show"}
    </button>
  );
}

const strengthLabels = ["Too short", "Weak", "Fair", "Good", "Strong"];

export function PasswordMeter({ password }) {
  if (!password) return null;
  const score = passwordStrength(password);
  return (
    <div className={`passwordMeter score${score}`} aria-live="polite">
      <div className="passwordMeterBars">
        {[1, 2, 3, 4].map((step) => (
          <span key={step} className={step <= score ? "isOn" : ""} />
        ))}
      </div>
      <span className="passwordMeterLabel">{strengthLabels[score]}</span>
    </div>
  );
}

/** Six single-digit boxes with auto-advance, backspace and paste support. */
export function OtpInput({ value, onChange, onComplete, disabled, hasError }) {
  const inputs = useRef([]);
  const digits = Array.from({ length: 6 }, (_, i) => value[i] || "");

  useEffect(() => {
    inputs.current[0]?.focus();
  }, []);

  const setDigits = (next) => {
    const joined = next.join("").slice(0, 6);
    onChange(joined);
    if (joined.length === 6) onComplete?.(joined);
  };

  const handleChange = (index, raw) => {
    const clean = raw.replace(/\D/g, "");
    if (!clean) return;
    const next = [...digits];
    // Typing or pasting several digits fills the following boxes too.
    clean.split("").forEach((digit, offset) => {
      if (index + offset < 6) next[index + offset] = digit;
    });
    setDigits(next);
    inputs.current[Math.min(index + clean.length, 5)]?.focus();
  };

  const handleKeyDown = (index, event) => {
    if (event.key === "Backspace") {
      event.preventDefault();
      const next = [...digits];
      if (next[index]) {
        next[index] = "";
      } else if (index > 0) {
        next[index - 1] = "";
        inputs.current[index - 1]?.focus();
      }
      onChange(next.join(""));
    } else if (event.key === "ArrowLeft" && index > 0) {
      inputs.current[index - 1]?.focus();
    } else if (event.key === "ArrowRight" && index < 5) {
      inputs.current[index + 1]?.focus();
    }
  };

  return (
    <div className={`otpInput ${hasError ? "hasError" : ""}`} role="group" aria-label="Confirmation code">
      {digits.map((digit, index) => (
        <input
          key={index}
          ref={(el) => { inputs.current[index] = el; }}
          inputMode="numeric"
          autoComplete={index === 0 ? "one-time-code" : "off"}
          maxLength={6}
          value={digit}
          disabled={disabled}
          className={digit ? "isFilled" : ""}
          style={{ animationDelay: `${index * 40}ms` }}
          onChange={(event) => handleChange(index, event.target.value)}
          onKeyDown={(event) => handleKeyDown(index, event)}
          onFocus={(event) => event.target.select()}
          aria-label={`Digit ${index + 1}`}
        />
      ))}
    </div>
  );
}

/** Countdown used for "Resend code in 0:27". */
export function useCountdown(seconds) {
  const [remaining, setRemaining] = useState(0);

  useEffect(() => {
    if (remaining <= 0) return undefined;
    const timer = setTimeout(() => setRemaining((value) => value - 1), 1000);
    return () => clearTimeout(timer);
  }, [remaining]);

  return [remaining, () => setRemaining(seconds)];
}

export function DevCodeHint({ code, onFill }) {
  if (!code) return null;
  return (
    <div className="devCodeHint">
      <span>
        <strong>Dev mode</strong> · your code is <code>{code}</code>. It's also delivered by the Azure Function
        (see <code>docker compose logs chat-functions</code>).
      </span>
      {onFill && (
        <button type="button" className="igLinkButton" onClick={() => onFill(code)}>Fill</button>
      )}
    </div>
  );
}

export function AuthError({ message }) {
  if (!message) return null;
  return (
    <p key={message} className="authError" role="alert">
      {message}
    </p>
  );
}
