import axios from "axios";
import { getApiUrl } from "./api";

export const FOLLOW_CHANGED_EVENT = "follows:changed";

/** Extracts a readable message from an API error. */
export function apiError(error, fallback = "Something went wrong. Please try again.") {
  const data = error?.response?.data;
  if (typeof data === "string" && data.trim()) return data.replace(/^Error Logging in user:\s*/, "");
  if (data?.title) return data.title;
  if (data?.message) return data.message;
  return fallback;
}

export async function login(identifier, password, website = "") {
  const formData = new FormData();
  formData.append("identifier", identifier);
  formData.append("password", password);
  if (website) formData.append("website", website);
  const response = await axios.post(getApiUrl("/api/Account/login"), formData);
  return response.data;
}

export async function register(fields) {
  const formData = new FormData();
  Object.entries(fields).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") formData.append(key, value);
  });
  const response = await axios.post(getApiUrl("/api/Account/register"), formData);
  return response.data;
}

export async function checkAvailability(params) {
  const query = new URLSearchParams(Object.entries(params).filter(([, v]) => v));
  const response = await axios.get(getApiUrl(`/api/Account/availability?${query}`));
  return response.data;
}

/** purpose: "Verify" | "Login" | "Reset". Codes travel API -> Service Bus -> Azure Function -> email/SMS. */
export async function sendCode(target, purpose, website = "") {
  const response = await axios.post(getApiUrl("/api/Account/code/send"), { target, purpose, website });
  return response.data;
}

export async function confirmCode(target, purpose, code, newPassword, website = "") {
  const response = await axios.post(getApiUrl("/api/Account/code/confirm"), { target, purpose, code, newPassword, website });
  return response.data;
}

export async function fetchSettings(userId) {
  const response = await axios.get(getApiUrl("/api/Account/me/settings"));
  return response.data;
}

export async function updateSettings(userId, fields) {
  const formData = new FormData();
  Object.entries(fields).forEach(([key, value]) => {
    if (value !== undefined && value !== null) formData.append(key, value);
  });
  const response = await axios.put(getApiUrl("/api/Account/me/settings"), formData);
  return response.data;
}

export async function changePassword(userId, currentPassword, newPassword) {
  await axios.put(getApiUrl("/api/Account/me/password"), { currentPassword, newPassword });
}

export async function toggleFollow(targetId, followerId) {
  const response = await axios.post(getApiUrl(`/api/Follows/${targetId}/toggle`));
  window.dispatchEvent(new CustomEvent(FOLLOW_CHANGED_EVENT, { detail: { targetId, ...response.data } }));
  return response.data;
}

export async function fetchFollowList(userId, kind, viewerId) {
  const response = await axios.get(getApiUrl(`/api/Follows/${userId}/${kind}`));
  return response.data || [];
}

export async function fetchPeople(viewerId, q = "") {
  const params = new URLSearchParams({ take: 60 });
  if (q) params.set("q", q);
  const response = await axios.get(getApiUrl(`/api/Follows/people?${params}`));
  return response.data || [];
}

export async function fetchSuggestions(viewerId, take = 5) {
  const response = await axios.get(getApiUrl(`/api/Follows/suggestions?take=${take}`));
  return response.data || [];
}

/** Scores a password 0-4 for the strength meter. */
export function passwordStrength(password) {
  if (!password) return 0;
  let score = 0;
  if (password.length >= 8) score++;
  if (password.length >= 12) score++;
  if (/[a-z]/.test(password) && /[A-Z]/.test(password)) score++;
  if (/\d/.test(password) && /[^A-Za-z0-9]/.test(password)) score++;
  else if (/\d/.test(password) || /[^A-Za-z0-9]/.test(password)) score += 0.5;
  return Math.min(4, Math.floor(score));
}

export const isEmail = (value) => /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(value.trim());

/** Accepts "+91 98765 43210", "+1 (415) 555-0100" etc. A country code is required. */
export const isPhone = (value) => /^\+[1-9]\d{7,14}$/.test(value.replace(/[\s\-().]/g, "").replace(/^00/, "+"));

export const isUsername = (value) => /^[a-z0-9._]{3,30}$/.test(value) && !value.startsWith(".") && !value.endsWith(".");
