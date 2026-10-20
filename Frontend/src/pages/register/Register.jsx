import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { AddAPhotoOutlined, CelebrationRounded, PhotoCameraRounded } from "@mui/icons-material";
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
import {
  apiError,
  checkAvailability,
  confirmCode,
  isEmail,
  isPhone,
  isUsername,
  login,
  register,
  sendCode,
  updateSettings,
} from "../../utils/account";
import { startSession, updateSessionProfile } from "../../utils/session";
import { honeypotProps } from "../../utils/botCheck";

const minimumAge = 13;
const steps = ["details", "verify", "profile", "done"];

function ageFrom(dob) {
  if (!dob) return null;
  const birth = new Date(dob);
  const now = new Date();
  const age = now.getFullYear() - birth.getFullYear();
  const beforeBirthday = now.getMonth() < birth.getMonth() ||
    (now.getMonth() === birth.getMonth() && now.getDate() < birth.getDate());
  return beforeBirthday ? age - 1 : age;
}

/** Debounced availability lookup; status is "checking" | "valid" | "invalid" | undefined. */
function useAvailability(field, value, isFormatValid) {
  const [state, setState] = useState({ status: undefined, suggestion: null });

  useEffect(() => {
    if (!value) {
      setState({ status: undefined, suggestion: null });
      return undefined;
    }
    if (!isFormatValid) {
      setState({ status: "invalid", suggestion: null });
      return undefined;
    }
    setState((prev) => ({ ...prev, status: "checking" }));
    let isActive = true;
    const timer = setTimeout(async () => {
      try {
        const result = await checkAvailability({ [field]: value });
        const available = result[`${field}Available`];
        if (isActive) setState({ status: available ? "valid" : "invalid", suggestion: result.suggestedUsername });
      } catch {
        if (isActive) setState({ status: undefined, suggestion: null });
      }
    }, 450);
    return () => { isActive = false; clearTimeout(timer); };
  }, [field, value, isFormatValid]);

  return state;
}

/**
 * Instagram-style sign up: details (mobile or email) -> confirmation code ->
 * profile photo & bio -> welcome. The code is delivered through Service Bus and
 * the Azure Function; the photo is stored in Azure Blob Storage.
 */
export default function Register() {
  const [step, setStep] = useState("details");
  const [account, setAccount] = useState(null);
  const stepIndex = steps.indexOf(step);

  return (
    <AuthShell footer={step === "details" ? <>Have an account? <Link to="/login">Log in</Link></> : null}>
      <div className="authProgress" aria-hidden="true">
        {steps.slice(0, 3).map((name, index) => (
          <span key={name} className={index === stepIndex ? "isActive" : index < stepIndex ? "isDone" : ""} />
        ))}
      </div>

      <div key={step} className="authStep">
        {step === "details" && (
          <DetailsStep onCreated={(created) => { setAccount(created); setStep("verify"); }} />
        )}
        {step === "verify" && account && <VerifyStep account={account} onVerified={() => setStep("profile")} />}
        {step === "profile" && account && <ProfileStep account={account} onDone={() => setStep("done")} />}
        {step === "done" && account && <WelcomeStep account={account} />}
      </div>
    </AuthShell>
  );
}

function DetailsStep({ onCreated }) {
  const [method, setMethod] = useState("phone");
  const [contact, setContact] = useState("");
  const [name, setName] = useState("");
  const [username, setUsername] = useState("");
  const [usernameTouched, setUsernameTouched] = useState(false);
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [dob, setDob] = useState("");
  const [website, setWebsite] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");

  const trimmedContact = contact.trim();
  const contactFormatValid = method === "phone" ? isPhone(trimmedContact) : isEmail(trimmedContact);
  const contactCheck = useAvailability(method === "phone" ? "phone" : "email", trimmedContact, contactFormatValid);
  const usernameCheck = useAvailability("username", username, isUsername(username));
  const age = ageFrom(dob);

  // Suggest a username from the full name until the member edits it themselves.
  useEffect(() => {
    if (usernameTouched || name.trim().length < 2) return undefined;
    const timer = setTimeout(async () => {
      try {
        const result = await checkAvailability({ name: name.trim() });
        if (result.suggestedUsername) setUsername(result.suggestedUsername);
      } catch {
        // Suggestions are a convenience.
      }
    }, 500);
    return () => clearTimeout(timer);
  }, [name, usernameTouched]);

  const canSubmit =
    contactFormatValid && contactCheck.status === "valid" &&
    name.trim().length > 0 &&
    usernameCheck.status === "valid" &&
    password.length >= 8 &&
    age !== null && age >= minimumAge;

  const submit = async (event) => {
    event.preventDefault();
    if (!canSubmit || isLoading) return;
    setIsLoading(true);
    setError("");
    try {
      const created = await register({
        Name: name.trim(),
        Username: username,
        [method === "phone" ? "PhoneNumber" : "Email"]: trimmedContact,
        Password: password,
        Dob: new Date(dob).toISOString(),
        Website: website,
      });
      onCreated({ ...created, password, target: trimmedContact, method });
    } catch (err) {
      setError(apiError(err, "We couldn't create your account. Please try again."));
      setIsLoading(false);
    }
  };

  let contactStatus;
  let contactHint;
  if (trimmedContact) {
    if (!contactFormatValid) {
      contactStatus = trimmedContact.length > 6 ? "invalid" : undefined;
      contactHint = method === "phone" ? "Include your country code, e.g. +91 98765 43210." : "Enter a valid email address.";
    } else {
      contactStatus = contactCheck.status;
      if (contactCheck.status === "invalid") {
        contactHint = method === "phone" ? "Another account is using this number." : "Another account is using this email.";
      }
    }
  }

  return (
    <form className="authForm" onSubmit={submit} noValidate>
      <input {...honeypotProps} value={website} onChange={(e) => setWebsite(e.target.value)} />
      <p className="authSubtitle" style={{ fontSize: 16, fontWeight: 600 }}>
        Sign up to see photos and videos from your friends.
      </p>

      <div className={`authSegment ${method === "email" ? "isRight" : ""}`} role="tablist">
        <span className="authSegmentPill" />
        <button type="button" role="tab" aria-selected={method === "phone"} className={method === "phone" ? "isActive" : ""}
          onClick={() => { setMethod("phone"); setContact(""); }}>
          Mobile number
        </button>
        <button type="button" role="tab" aria-selected={method === "email"} className={method === "email" ? "isActive" : ""}
          onClick={() => { setMethod("email"); setContact(""); }}>
          Email
        </button>
      </div>

      <FloatingField
        key={method}
        label={method === "phone" ? "Mobile number (e.g. +91 98765 43210)" : "Email address"}
        type={method === "phone" ? "tel" : "email"}
        inputMode={method === "phone" ? "tel" : "email"}
        autoComplete={method === "phone" ? "tel" : "email"}
        value={contact}
        onChange={setContact}
        status={contactStatus}
        hint={contactHint}
        autoFocus
      />
      <FloatingField label="Full name" value={name} onChange={setName} autoComplete="name" maxLength={60} />
      <FloatingField
        label="Username"
        value={username}
        onChange={(value) => { setUsernameTouched(true); setUsername(value.toLowerCase().replace(/\s/g, "")); }}
        autoComplete="username"
        maxLength={30}
        status={usernameCheck.status}
        hint={
          username && !isUsername(username)
            ? "Use 3-30 letters, numbers, periods or underscores."
            : usernameCheck.status === "invalid" ? "This username isn't available." : undefined
        }
      />
      {usernameCheck.status === "invalid" && usernameCheck.suggestion && (
        <div className="authSuggestions">
          <span className="authFieldHint">Try:</span>
          <button type="button" onClick={() => { setUsernameTouched(true); setUsername(usernameCheck.suggestion); }}>
            {usernameCheck.suggestion}
          </button>
        </div>
      )}
      <FloatingField
        label="Password"
        type={showPassword ? "text" : "password"}
        value={password}
        onChange={setPassword}
        autoComplete="new-password"
        status={password.length >= 8 ? "valid" : undefined}
        hint={password && password.length < 8 ? "Use at least 8 characters." : undefined}
        action={password && <PasswordToggle visible={showPassword} onToggle={() => setShowPassword((v) => !v)} />}
      />
      <PasswordMeter password={password} />
      <FloatingField
        label="Birthday"
        type="date"
        value={dob}
        onChange={setDob}
        max={new Date().toISOString().slice(0, 10)}
        status={dob ? (age >= minimumAge ? "valid" : "invalid") : undefined}
        hint={dob && age < minimumAge ? `You need to be at least ${minimumAge} to sign up.` : "This won't be part of your public profile."}
      />

      <p className="authTerms">
        By signing up, you agree to our Terms, Privacy Policy and Cookies Policy. We'll send a code to confirm
        your {method === "phone" ? "number" : "email"}.
      </p>

      <button type="submit" className={`authPrimary ${isLoading ? "isLoading" : ""}`} disabled={!canSubmit || isLoading}>
        <span>Sign up</span>
      </button>
      <AuthError message={error} />
    </form>
  );
}

function VerifyStep({ account, onVerified }) {
  const [sent, setSent] = useState(null);
  const [code, setCode] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");
  const [codeError, setCodeError] = useState(false);
  const [resendIn, startResendTimer] = useCountdown(30);
  const requested = useRef(false);

  const request = async () => {
    setError("");
    try {
      setSent(await sendCode(account.target, "Verify"));
      startResendTimer();
    } catch (err) {
      setError(apiError(err, "We couldn't send your code."));
    }
  };

  useEffect(() => {
    // StrictMode mounts effects twice in development; send only one code.
    if (requested.current) return;
    requested.current = true;
    request();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const verify = async (value = code) => {
    if (value.length !== 6 || isLoading) return;
    setIsLoading(true);
    setError("");
    setCodeError(false);
    try {
      await confirmCode(account.target, "Verify", value);
      // Sign in with the password they just chose so the flow continues seamlessly.
      startSession(await login(account.username, account.password));
      onVerified();
    } catch (err) {
      setCodeError(true);
      setError(apiError(err, "That code isn't valid."));
      setIsLoading(false);
    }
  };

  const isPhoneTarget = account.method === "phone";

  return (
    <form className="authForm" onSubmit={(e) => { e.preventDefault(); verify(); }} noValidate>
      <div className="authWelcome" style={{ paddingTop: 0 }}>
        <div className="authWelcomeIcon" style={{ width: 72, height: 72, fontSize: 32 }}>
          {isPhoneTarget ? "📱" : "✉️"}
        </div>
        <h1 className="authTitle">{isPhoneTarget ? "Confirm your mobile number" : "Confirm your email"}</h1>
        <p className="authSubtitle">
          Enter the 6-digit code we sent to <strong>{sent?.destination || account.target}</strong>.
        </p>
      </div>

      <OtpInput
        value={code}
        onChange={(value) => { setCode(value); setCodeError(false); }}
        onComplete={verify}
        disabled={isLoading}
        hasError={codeError}
      />

      <div className="otpMeta">
        <span>{sent ? "Code expires in 10 minutes" : "Sending code…"}</span>
        {resendIn > 0 ? (
          <span>Resend in 0:{String(resendIn).padStart(2, "0")}</span>
        ) : (
          <button type="button" className="igLinkButton" onClick={request}>Resend code</button>
        )}
      </div>

      <button type="submit" className={`authPrimary ${isLoading ? "isLoading" : ""}`} disabled={code.length !== 6 || isLoading}>
        <span>Next</span>
      </button>

      <DevCodeHint code={sent?.devCode} onFill={(value) => { setCode(value); verify(value); }} />
      <AuthError message={error} />
    </form>
  );
}

function ProfileStep({ account, onDone }) {
  const [file, setFile] = useState(null);
  const [preview, setPreview] = useState(null);
  const [bio, setBio] = useState("");
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => () => preview && URL.revokeObjectURL(preview), [preview]);

  const choose = (selected) => {
    if (!selected?.type.startsWith("image/")) return;
    setFile(selected);
    setPreview(URL.createObjectURL(selected));
  };

  const save = async () => {
    if (!file && !bio.trim()) {
      onDone();
      return;
    }
    setIsSaving(true);
    setError("");
    try {
      const settings = await updateSettings(account.id, {
        ImageFile: file || undefined,
        Information: bio.trim() || undefined,
      });
      updateSessionProfile({ imagePath: settings.imagePath });
      onDone();
    } catch (err) {
      setError(apiError(err, "We couldn't save your profile."));
      setIsSaving(false);
    }
  };

  return (
    <div className="authForm">
      <h1 className="authTitle">Add a profile photo</h1>
      <p className="authSubtitle">Add a profile photo so your friends know it's you.</p>

      <label className="authAvatarPicker">
        {preview ? (
          <span className="igRing"><img src={preview} alt="Profile preview" /></span>
        ) : (
          <span className="authAvatarPlaceholder"><AddAPhotoOutlined /></span>
        )}
        <span className="authAvatarBadge"><PhotoCameraRounded /></span>
        <input type="file" accept="image/*" hidden onChange={(e) => choose(e.target.files?.[0])} />
      </label>

      <textarea
        className="authTextarea"
        placeholder="Write a short bio (optional)"
        value={bio}
        maxLength={150}
        onChange={(e) => setBio(e.target.value)}
      />
      <span className="authFieldHint" style={{ textAlign: "right" }}>{bio.length}/150</span>

      <button type="button" className={`authPrimary ${isSaving ? "isLoading" : ""}`} onClick={save} disabled={isSaving}>
        <span>{file || bio.trim() ? "Save and continue" : "Skip for now"}</span>
      </button>
      <AuthError message={error} />
    </div>
  );
}

function WelcomeStep({ account }) {
  useEffect(() => {
    const timer = setTimeout(() => window.location.assign("/"), 3000);
    return () => clearTimeout(timer);
  }, []);

  return (
    <div className="authWelcome">
      <div className="authWelcomeIcon"><CelebrationRounded /></div>
      <h1 className="authTitle" style={{ fontSize: 22 }}>Welcome to SocialButterfly</h1>
      <p className="authSubtitle">
        You're all set, <strong>@{account.username}</strong>. Taking you to your feed…
      </p>
      <button type="button" className="authPrimary" onClick={() => window.location.assign("/")}>
        <span>Go to feed</span>
      </button>
      <button type="button" className="authLink" style={{ marginTop: 14 }} onClick={() => window.location.assign("/explore")}>
        Find people to follow
      </button>
    </div>
  );
}
