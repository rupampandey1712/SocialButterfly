import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import {
  CheckCircleRounded,
  DarkModeOutlined,
  ErrorOutlineRounded,
  LightModeOutlined,
  LockOutlined,
  NotificationsNoneOutlined,
  PaletteOutlined,
  PersonOutlineRounded,
  SettingsBrightnessOutlined,
} from "@mui/icons-material";
import Topbar from "../../components/topbar/Topbar";
import AnimatedModal from "../../components/modal/AnimatedModal";
import { showToast } from "../../components/toast/Toaster";
import { DevCodeHint, OtpInput, PasswordMeter, useCountdown } from "../auth/AuthFields";
import {
  apiError,
  changePassword,
  checkAvailability,
  confirmCode,
  fetchSettings,
  isEmail,
  isPhone,
  isUsername,
  sendCode,
  updateSettings,
} from "../../utils/account";
import { getCurrentUser, updateSessionProfile } from "../../utils/session";
import { applyTheme } from "../../utils/theme";
import { DEFAULT_AVATAR, getPublicImageUrl } from "../../utils/image";
import "../auth/auth.css";
import "./settings.css";

const sections = [
  { key: "profile", label: "Edit profile", icon: <PersonOutlineRounded /> },
  { key: "account", label: "Account & security", icon: <LockOutlined /> },
  { key: "notifications", label: "Notifications", icon: <NotificationsNoneOutlined /> },
  { key: "appearance", label: "Appearance", icon: <PaletteOutlined /> },
];

export default function Settings() {
  const user = getCurrentUser();
  const [params, setParams] = useSearchParams();
  const active = sections.some((s) => s.key === params.get("tab")) ? params.get("tab") : "profile";
  const [settings, setSettings] = useState(null);

  useEffect(() => {
    if (!user.id) return;
    fetchSettings(user.id).then(setSettings).catch(() => showToast("Couldn't load your settings"));
  }, [user.id]);

  const save = async (fields, message = "Saved") => {
    const updated = await updateSettings(user.id, fields);
    setSettings(updated);
    updateSessionProfile({ name: updated.name, username: updated.username, imagePath: updated.imagePath });
    showToast(message);
    return updated;
  };

  return (
    <>
      <Topbar />
      <div className="settingsPage">
        <aside className="settingsNav">
          <h1>Settings</h1>
          {sections.map((section) => (
            <button
              key={section.key}
              type="button"
              className={`settingsNavItem ${active === section.key ? "isActive" : ""}`}
              onClick={() => setParams({ tab: section.key })}
            >
              {section.icon}
              <span>{section.label}</span>
            </button>
          ))}
        </aside>

        <main key={active} className="settingsContent">
          {!settings ? (
            <div className="igSpinner" />
          ) : active === "profile" ? (
            <ProfileSection settings={settings} onSave={save} />
          ) : active === "account" ? (
            <AccountSection settings={settings} onSave={save} onSettings={setSettings} userId={user.id} />
          ) : active === "notifications" ? (
            <NotificationsSection settings={settings} onSave={save} />
          ) : (
            <AppearanceSection settings={settings} onSave={save} />
          )}
        </main>
      </div>
    </>
  );
}

function ProfileSection({ settings, onSave }) {
  const [name, setName] = useState(settings.name || "");
  const [username, setUsername] = useState(settings.username || "");
  const [bio, setBio] = useState(settings.information || "");
  const [file, setFile] = useState(null);
  const [preview, setPreview] = useState(null);
  const [usernameState, setUsernameState] = useState("valid");
  const [isSaving, setIsSaving] = useState(false);
  const inputRef = useRef(null);

  useEffect(() => () => preview && URL.revokeObjectURL(preview), [preview]);

  useEffect(() => {
    if (username === settings.username) {
      setUsernameState("valid");
      return undefined;
    }
    if (!isUsername(username)) {
      setUsernameState("invalid");
      return undefined;
    }
    setUsernameState("checking");
    const timer = setTimeout(async () => {
      try {
        const result = await checkAvailability({ username });
        setUsernameState(result.usernameAvailable ? "valid" : "taken");
      } catch {
        setUsernameState("valid");
      }
    }, 400);
    return () => clearTimeout(timer);
  }, [username, settings.username]);

  const dirty = name !== (settings.name || "") || username !== (settings.username || "") ||
    bio !== (settings.information || "") || file;

  const submit = async (event) => {
    event.preventDefault();
    if (!dirty || usernameState !== "valid" || isSaving) return;
    setIsSaving(true);
    try {
      await onSave({
        Name: name,
        Username: username,
        Information: bio,
        ImageFile: file || undefined,
      }, "Profile saved");
      setFile(null);
    } catch (error) {
      showToast(apiError(error, "Couldn't save your profile"));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <form className="settingsSection" onSubmit={submit}>
      <h2>Edit profile</h2>

      <div className="settingsAvatarCard">
        <button type="button" className="settingsAvatar" onClick={() => inputRef.current?.click()} aria-label="Change photo">
          <img
            className="igAvatar"
            src={preview || getPublicImageUrl(settings.imagePath)}
            alt=""
            onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
          />
        </button>
        <div>
          <strong>{settings.username}</strong>
          <span>{settings.name}</span>
        </div>
        <button type="button" className="igButton" onClick={() => inputRef.current?.click()}>Change photo</button>
        <input
          ref={inputRef}
          type="file"
          accept="image/*"
          hidden
          onChange={(e) => {
            const selected = e.target.files?.[0];
            if (!selected?.type.startsWith("image/")) return;
            setFile(selected);
            setPreview(URL.createObjectURL(selected));
          }}
        />
      </div>

      <label className="settingsField">
        <span>Name</span>
        <input value={name} onChange={(e) => setName(e.target.value)} maxLength={60} />
        <small>Help people discover your account by using the name you're known by.</small>
      </label>

      <label className="settingsField">
        <span>Username</span>
        <div className={`settingsInputWrap is-${usernameState}`}>
          <input value={username} maxLength={30} onChange={(e) => setUsername(e.target.value.toLowerCase().replace(/\s/g, ""))} />
          {usernameState === "valid" && username !== settings.username && <CheckCircleRounded className="settingsOk" />}
          {(usernameState === "invalid" || usernameState === "taken") && <ErrorOutlineRounded className="settingsBad" />}
        </div>
        <small className={usernameState === "invalid" || usernameState === "taken" ? "isError" : ""}>
          {usernameState === "taken" ? "This username isn't available." :
            usernameState === "invalid" ? "Use 3-30 letters, numbers, periods or underscores." :
            "People can find you as @" + username}
        </small>
      </label>

      <label className="settingsField">
        <span>Bio</span>
        <textarea value={bio} onChange={(e) => setBio(e.target.value)} maxLength={150} rows={3} />
        <small>{bio.length} / 150</small>
      </label>

      <button type="submit" className={`igButton settingsSubmit ${isSaving ? "isLoading" : ""}`} disabled={!dirty || usernameState !== "valid" || isSaving}>
        {isSaving ? "Saving…" : "Submit"}
      </button>
    </form>
  );
}

function AccountSection({ settings, onSave, onSettings, userId }) {
  const [email, setEmail] = useState(settings.email || "");
  const [phone, setPhone] = useState(settings.phoneNumber || "");
  const [verifying, setVerifying] = useState(null);
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [isSaving, setIsSaving] = useState(false);

  const contactDirty = email !== (settings.email || "") || phone !== (settings.phoneNumber || "");
  const contactValid = (!email || isEmail(email)) && (!phone || isPhone(phone)) && (email || phone);

  const saveContacts = async () => {
    if (!contactDirty || !contactValid) return;
    setIsSaving(true);
    try {
      await onSave({ Email: email, PhoneNumber: phone }, "Contact info updated — please verify it");
    } catch (error) {
      showToast(apiError(error, "Couldn't update your contact info"));
    } finally {
      setIsSaving(false);
    }
  };

  const savePassword = async (event) => {
    event.preventDefault();
    if (!current || next.length < 8) return;
    try {
      await changePassword(userId, current, next);
      setCurrent("");
      setNext("");
      showToast("Password changed");
    } catch (error) {
      showToast(apiError(error, "Couldn't change your password"));
    }
  };

  const ContactStatus = ({ confirmed, value, onVerify }) =>
    !value ? null : confirmed ? (
      <span className="settingsBadge isVerified"><CheckCircleRounded fontSize="small" /> Verified</span>
    ) : (
      <button type="button" className="settingsBadge isPending" onClick={onVerify}>
        <ErrorOutlineRounded fontSize="small" /> Verify now
      </button>
    );

  return (
    <div className="settingsSection">
      <h2>Account & security</h2>
      <p className="settingsLead">
        You can log in with your username, email or mobile number. Verified contacts can also receive one-time login codes.
      </p>

      <label className="settingsField">
        <span>Email</span>
        <div className="settingsInputWrap">
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="you@example.com" />
          {email === settings.email && (
            <ContactStatus confirmed={settings.emailConfirmed} value={settings.email} onVerify={() => setVerifying(settings.email)} />
          )}
        </div>
        {email && !isEmail(email) && <small className="isError">Enter a valid email address.</small>}
      </label>

      <label className="settingsField">
        <span>Mobile number</span>
        <div className="settingsInputWrap">
          <input type="tel" value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="+91 98765 43210" />
          {phone === settings.phoneNumber && (
            <ContactStatus confirmed={settings.phoneConfirmed} value={settings.phoneNumber} onVerify={() => setVerifying(settings.phoneNumber)} />
          )}
        </div>
        {phone && !isPhone(phone) && <small className="isError">Include your country code, e.g. +91 98765 43210.</small>}
      </label>

      <button type="button" className="igButton settingsSubmit" onClick={saveContacts} disabled={!contactDirty || !contactValid || isSaving}>
        Save contact info
      </button>

      <form className="settingsCard" onSubmit={savePassword}>
        <h3>Change password</h3>
        <label className="settingsField">
          <span>Current password</span>
          <input type="password" value={current} onChange={(e) => setCurrent(e.target.value)} autoComplete="current-password" />
        </label>
        <label className="settingsField">
          <span>New password</span>
          <input type="password" value={next} onChange={(e) => setNext(e.target.value)} autoComplete="new-password" />
        </label>
        <PasswordMeter password={next} />
        <button type="submit" className="igButton settingsSubmit" disabled={!current || next.length < 8}>Change password</button>
      </form>

      {verifying && (
        <VerifyContactModal
          target={verifying}
          onClose={() => setVerifying(null)}
          onVerified={() => {
            onSettings((prev) => ({
              ...prev,
              emailConfirmed: prev.emailConfirmed || verifying === prev.email,
              phoneConfirmed: prev.phoneConfirmed || verifying === prev.phoneNumber,
            }));
            showToast("Verified!");
          }}
        />
      )}
    </div>
  );
}

function VerifyContactModal({ target, onClose, onVerified }) {
  const [sent, setSent] = useState(null);
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [hasError, setHasError] = useState(false);
  const [resendIn, startTimer] = useCountdown(30);
  const requested = useRef(false);

  const request = async () => {
    setError("");
    try {
      setSent(await sendCode(target, "Verify"));
      startTimer();
    } catch (err) {
      setError(apiError(err, "Couldn't send a code"));
    }
  };

  useEffect(() => {
    if (requested.current) return;
    requested.current = true;
    request();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <AnimatedModal onClose={onClose} className="verifyModal" label="Verify contact">
      {(close) => {
        const verify = async (value = code) => {
          if (value.length !== 6) return;
          setHasError(false);
          try {
            await confirmCode(target, "Verify", value);
            onVerified();
            close();
          } catch (err) {
            setHasError(true);
            setError(apiError(err, "That code isn't valid."));
          }
        };
        return (
          <div className="verifyModalBody">
            <h2>Enter confirmation code</h2>
            <p>We sent a 6-digit code to <strong>{sent?.destination || target}</strong>.</p>
            <OtpInput value={code} onChange={(v) => { setCode(v); setHasError(false); }} onComplete={verify} hasError={hasError} />
            <div className="otpMeta">
              <span>Expires in 10 minutes</span>
              {resendIn > 0 ? <span>Resend in 0:{String(resendIn).padStart(2, "0")}</span> : (
                <button type="button" className="igLinkButton" onClick={request}>Resend</button>
              )}
            </div>
            {error && <p className="authError">{error}</p>}
            <DevCodeHint code={sent?.devCode} onFill={(value) => { setCode(value); verify(value); }} />
          </div>
        );
      }}
    </AnimatedModal>
  );
}

function Toggle({ checked, onChange, label, description }) {
  return (
    <label className="settingsToggleRow">
      <span>
        <strong>{label}</strong>
        <small>{description}</small>
      </span>
      <span className={`settingsSwitch ${checked ? "isOn" : ""}`}>
        <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} />
        <span className="settingsSwitchThumb" />
      </span>
    </label>
  );
}

function NotificationsSection({ settings, onSave }) {
  const update = async (key, value) => {
    try {
      await onSave({ [key]: value }, value ? "Notifications on" : "Notifications off");
    } catch (error) {
      showToast(apiError(error, "Couldn't update notifications"));
    }
  };

  return (
    <div className="settingsSection">
      <h2>Notifications</h2>
      <p className="settingsLead">Choose what shows up in your activity feed. Delivered by the notifications Azure Function.</p>
      <div className="settingsCard">
        <Toggle checked={settings.notifyLikes} onChange={(v) => update("NotifyLikes", v)}
          label="Likes" description="When someone likes your post." />
        <Toggle checked={settings.notifyComments} onChange={(v) => update("NotifyComments", v)}
          label="Comments" description="When someone comments on your post." />
        <Toggle checked={settings.notifyFollows} onChange={(v) => update("NotifyFollows", v)}
          label="Followers & friend requests" description="New followers, friend requests and accepted requests." />
        <Toggle checked={settings.notifyNewPosts} onChange={(v) => update("NotifyNewPosts", v)}
          label="New posts" description="When people you're connected with share a new post." />
      </div>
    </div>
  );
}

const themes = [
  { key: "light", label: "Light", icon: <LightModeOutlined /> },
  { key: "dark", label: "Dark", icon: <DarkModeOutlined /> },
  { key: "system", label: "Use device setting", icon: <SettingsBrightnessOutlined /> },
];

function AppearanceSection({ settings, onSave }) {
  const choose = async (theme) => {
    applyTheme(theme);
    try {
      await onSave({ Theme: theme }, "Appearance updated");
    } catch (error) {
      showToast(apiError(error, "Couldn't save your appearance"));
    }
  };

  return (
    <div className="settingsSection">
      <h2>Appearance</h2>
      <p className="settingsLead">Your choice is saved to your account and follows you to every device.</p>
      <div className="themeGrid">
        {themes.map((theme, i) => (
          <button
            key={theme.key}
            type="button"
            className={`themeCard igFadeUp ${settings.theme === theme.key ? "isActive" : ""}`}
            style={{ animationDelay: `${i * 60}ms` }}
            onClick={() => choose(theme.key)}
          >
            <span className={`themePreview preview-${theme.key}`}>
              <span className="themePreviewNav" />
              <span className="themePreviewCard">
                <span className="themePreviewAvatar" />
                <span className="themePreviewLine" />
                <span className="themePreviewMedia" />
              </span>
            </span>
            <span className="themeLabel">
              {theme.icon} {theme.label}
              {settings.theme === theme.key && <CheckCircleRounded className="themeCheck" />}
            </span>
          </button>
        ))}
      </div>
    </div>
  );
}
