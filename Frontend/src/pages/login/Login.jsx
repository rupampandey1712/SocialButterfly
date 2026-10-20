import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { ArrowBackIosNewRounded, LockResetRounded, SmsRounded } from "@mui/icons-material";
import AuthShell from "../auth/AuthShell";
import {
  AuthError,
  DevCodeHint,
  FloatingField,
  OtpInput,
  PasswordMeter,
  PasswordToggle,
  useCountdown,
} from "../auth/AuthFields";
import { apiError, confirmCode, isEmail, isPhone, login, sendCode } from "../../utils/account";
import { honeypotProps } from "../../utils/botCheck";
import { startSession } from "../../utils/session";

const resendSeconds = 30;

/**
 * Sign in with phone/username/email + password, a one-time code (passwordless),
 * or reset a forgotten password. Codes are delivered by the Azure Function.
 */
export default function Login() {
  const [params] = useSearchParams();
  const sessionExpired = params.get("expired") === "1";
  const [mode, setMode] = useState("password");
  const [direction, setDirection] = useState("forward");

  const switchMode = (next, back = false) => {
    setDirection(back ? "back" : "forward");
    setMode(next);
  };

  const finish = (session) => {
    startSession(session);
    // Full navigation so App re-reads the stored role (admins get the dashboard layout).
    window.location.assign("/");
  };

  return (
    <AuthShell
      footer={
        <>
          Don't have an account? <Link to="/register">Sign up</Link>
        </>
      }
    >
      {sessionExpired && mode === "password" && (
        <p className="authNotice">Your session expired. Please log in again.</p>
      )}
      <div key={mode} className={`authStep ${direction === "back" ? "isBack" : ""}`}>
        {mode === "password" && (
          <PasswordLogin
            onSuccess={finish}
            onUseCode={() => switchMode("code")}
            onForgot={() => switchMode("reset")}
          />
        )}
        {mode === "code" && (
          <CodeFlow purpose="Login" onSuccess={finish} onBack={() => switchMode("password", true)} />
        )}
        {mode === "reset" && (
          <CodeFlow purpose="Reset" onSuccess={finish} onBack={() => switchMode("password", true)} />
        )}
      </div>
    </AuthShell>
  );
}

function PasswordLogin({ onSuccess, onUseCode, onForgot }) {
  const [identifier, setIdentifier] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");
  const [website, setWebsite] = useState("");
  const canSubmit = identifier.trim().length > 0 && password.length >= 6;

  const submit = async (event) => {
    event.preventDefault();
    if (!canSubmit || isLoading) return;
    setIsLoading(true);
    setError("");
    try {
      onSuccess(await login(identifier.trim(), password, website));
    } catch (err) {
      setError(apiError(err, "Sorry, your password was incorrect. Please double-check your password."));
      setIsLoading(false);
    }
  };

  return (
    <form className="authForm" onSubmit={submit} noValidate>
      <input {...honeypotProps} value={website} onChange={(e) => setWebsite(e.target.value)} />
      <FloatingField
        label="Phone number, username, or email"
        value={identifier}
        onChange={setIdentifier}
        autoComplete="username"
        autoFocus
      />
      <FloatingField
        label="Password"
        type={showPassword ? "text" : "password"}
        value={password}
        onChange={setPassword}
        autoComplete="current-password"
        action={password && <PasswordToggle visible={showPassword} onToggle={() => setShowPassword((v) => !v)} />}
      />
      <button type="submit" className={`authPrimary ${isLoading ? "isLoading" : ""}`} disabled={!canSubmit || isLoading}>
        <span>Log in</span>
      </button>

      <div className="authDivider">OR</div>

      <button type="button" className="authAlt" onClick={onUseCode}>
        <SmsRounded fontSize="small" /> Log in with a one-time code
      </button>

      <AuthError message={error} />

      <div className="authLinkRow">
        <button type="button" className="authLink" onClick={onForgot}>Forgot password?</button>
      </div>
    </form>
  );
}

/** Shared by passwordless login ("Login") and password reset ("Reset"). */
function CodeFlow({ purpose, onSuccess, onBack }) {
  const isReset = purpose === "Reset";
  const [target, setTarget] = useState("");
  const [sent, setSent] = useState(null);
  const [code, setCode] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");
  const [codeError, setCodeError] = useState(false);
  const [website, setWebsite] = useState("");
  const [resendIn, startResendTimer] = useCountdown(resendSeconds);

  const trimmed = target.trim();
  const targetValid = isEmail(trimmed) || isPhone(trimmed);
  const targetStatus = !trimmed ? undefined : targetValid ? "valid" : trimmed.length > 5 ? "invalid" : undefined;

  const requestCode = async (event) => {
    event?.preventDefault();
    if (!targetValid || isLoading) return;
    setIsLoading(true);
    setError("");
    try {
      setSent(await sendCode(trimmed, purpose, website));
      setCode("");
      startResendTimer();
    } catch (err) {
      setError(apiError(err, "We couldn't send a code right now."));
    } finally {
      setIsLoading(false);
    }
  };

  const verify = async (value = code) => {
    if (value.length !== 6 || isLoading) return;
    if (isReset && newPassword.length < 8) {
      setError("Create a password at least 8 characters long.");
      return;
    }
    setIsLoading(true);
    setError("");
    setCodeError(false);
    try {
      onSuccess(await confirmCode(trimmed, purpose, value, isReset ? newPassword : undefined, website));
    } catch (err) {
      setCodeError(true);
      setError(apiError(err, "That code isn't valid."));
      setIsLoading(false);
    }
  };

  return (
    <div className="authForm">
      <button type="button" className="authBack" onClick={sent ? () => setSent(null) : onBack}>
        <ArrowBackIosNewRounded style={{ fontSize: 14 }} /> Back
      </button>

      {!sent ? (
        <form onSubmit={requestCode} className="authForm authStep" noValidate>
          <input {...honeypotProps} value={website} onChange={(e) => setWebsite(e.target.value)} />
          <div className="authWelcome" style={{ paddingTop: 0 }}>
            <div className="authWelcomeIcon" style={{ width: 72, height: 72 }}>
              {isReset ? <LockResetRounded /> : <SmsRounded />}
            </div>
            <h1 className="authTitle">{isReset ? "Trouble logging in?" : "Log in with a code"}</h1>
            <p className="authSubtitle">
              Enter your email or mobile number and we'll send you a 6-digit code
              {isReset ? " to get back into your account." : " to log in — no password needed."}
            </p>
          </div>
          <FloatingField
            label="Email or mobile number (with country code)"
            value={target}
            onChange={setTarget}
            status={targetStatus}
            hint={targetStatus === "invalid" ? "Use a valid email, or a number like +91 98765 43210." : undefined}
            autoComplete="username"
            autoFocus
          />
          <button type="submit" className={`authPrimary ${isLoading ? "isLoading" : ""}`} disabled={!targetValid || isLoading}>
            <span>Send code</span>
          </button>
        </form>
      ) : (
        <form className="authForm authStep" onSubmit={(e) => { e.preventDefault(); verify(); }} noValidate>
          <h1 className="authTitle">Enter confirmation code</h1>
          <p className="authSubtitle">
            Enter the 6-digit code we sent {sent.channel === "Sms" ? "by SMS" : "by email"} to <strong>{sent.destination}</strong>.
          </p>
          <OtpInput
            value={code}
            onChange={(value) => { setCode(value); setCodeError(false); }}
            onComplete={isReset ? undefined : verify}
            disabled={isLoading}
            hasError={codeError}
          />
          {isReset && (
            <>
              <FloatingField
                label="New password"
                type={showPassword ? "text" : "password"}
                value={newPassword}
                onChange={setNewPassword}
                autoComplete="new-password"
                action={newPassword && <PasswordToggle visible={showPassword} onToggle={() => setShowPassword((v) => !v)} />}
              />
              <PasswordMeter password={newPassword} />
            </>
          )}
          <div className="otpMeta">
            <span>Code expires in 10 minutes</span>
            {resendIn > 0 ? (
              <span>Resend in 0:{String(resendIn).padStart(2, "0")}</span>
            ) : (
              <button type="button" className="igLinkButton" onClick={requestCode}>Resend code</button>
            )}
          </div>
          <button
            type="submit"
            className={`authPrimary ${isLoading ? "isLoading" : ""}`}
            disabled={code.length !== 6 || isLoading || (isReset && newPassword.length < 8)}
          >
            <span>{isReset ? "Reset password & log in" : "Log in"}</span>
          </button>
          <DevCodeHint
            code={sent.devCode}
            onFill={(value) => { setCode(value); if (!isReset) verify(value); }}
          />
        </form>
      )}

      <AuthError message={error} />
    </div>
  );
}
