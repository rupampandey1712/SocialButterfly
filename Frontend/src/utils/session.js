import axios from "axios";
import { getApiUrl, SESSION_KEYS } from "./api";
import { getPublicImageUrl } from "./image";
import { applyTheme } from "./theme";

/**
 * The real credential is the API's HttpOnly cookie. sessionStorage only caches
 * non-secret profile details (id, name, avatar, role) for rendering; the server
 * re-checks the cookie on every request, so editing these values grants nothing.
 */
export function getCurrentUser() {
  const id = Number(sessionStorage.getItem("Id")) || null;
  return {
    id,
    name: sessionStorage.getItem("name") || "",
    username: sessionStorage.getItem("username") || "",
    image: getPublicImageUrl(sessionStorage.getItem("url") || sessionStorage.getItem("imagePath")),
  };
}

export function isLoggedIn() {
  return sessionStorage.getItem("isLoggedIn") === "true" && Boolean(sessionStorage.getItem("Id"));
}

/** Caches the signed-in profile from /login, /code/confirm or /session. */
export function startSession(profile) {
  const avatar = getPublicImageUrl(profile.imagePath);
  sessionStorage.setItem("isLoggedIn", "true");
  sessionStorage.setItem("Id", profile.id);
  sessionStorage.setItem("url", avatar);
  sessionStorage.setItem("imagePath", avatar);
  sessionStorage.setItem("name", profile.name || profile.username || "");
  sessionStorage.setItem("username", profile.username || "");
  sessionStorage.setItem("role", profile.role || "User");
  if (profile.theme) applyTheme(profile.theme);
}

function clearSession() {
  SESSION_KEYS.forEach((key) => sessionStorage.removeItem(key));
}

/**
 * Runs before the first render: asks the API who the session cookie belongs to
 * (this also issues the CSRF cookie). Restores the session after a tab reopen and
 * drops a stale cache if the cookie expired.
 */
export async function bootstrapSession() {
  // Older builds kept the JWT in sessionStorage; never leave one readable by scripts.
  sessionStorage.removeItem("tokens");
  try {
    const response = await axios.get(getApiUrl("/api/Account/session"));
    if (response.data?.authenticated) startSession(response.data);
    else clearSession();
  } catch {
    // API unreachable: keep any cached profile; requests will re-check the cookie.
  }
}

export function updateSessionProfile({ name, username, imagePath }) {
  if (name) sessionStorage.setItem("name", name);
  if (username) sessionStorage.setItem("username", username);
  if (imagePath) {
    const avatar = getPublicImageUrl(imagePath);
    sessionStorage.setItem("url", avatar);
    sessionStorage.setItem("imagePath", avatar);
  }
  window.dispatchEvent(new CustomEvent("session:updated"));
}

export async function logout() {
  try {
    // The cookie is HttpOnly, so only the server can clear it.
    await axios.post(getApiUrl("/api/Account/logout"));
  } catch {
    // Still clear locally; an expired cookie is rejected by the API anyway.
  }
  clearSession();
  window.location.assign("/login");
}
