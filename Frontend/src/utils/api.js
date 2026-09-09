export const API_ORIGIN = (import.meta.env.VITE_API_ORIGIN ?? "").replace(/\/$/, "");

export function getApiUrl(path) {
  const cleanPath = path.startsWith("/") ? path : `/${path}`;
  return API_ORIGIN ? `${API_ORIGIN}${cleanPath}` : cleanPath;
}

export function getAuthHeaders() {
  const token = sessionStorage.getItem("tokens");
  return token ? { Authorization: `Bearer ${token}` } : {};
}

