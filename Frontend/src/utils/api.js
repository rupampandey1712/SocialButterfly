import axios from "axios";
import { PROOF_HEADER, solveChallenge } from "./botCheck";

/**
 * API calls are always same-origin (/api/...). In development Vite proxies them to the
 * backend (VITE_API_ORIGIN is only the proxy target); in Docker the app/nginx serves both.
 * Same-origin is required for the SameSite=Strict HttpOnly session cookie and the CSRF cookie.
 */
export function getApiUrl(path) {
  return path.startsWith("/") ? path : `/${path}`;
}

// Endpoints where a 401 means "wrong password/code", not "session expired".
const credentialEndpoints = ["/api/Account/login", "/api/Account/code/"];
export const SESSION_KEYS = ["isLoggedIn", "Id", "url", "imagePath", "name", "role", "username"];

/**
 * Authentication is an HttpOnly cookie set by the API, so no token ever lives in
 * JavaScript. Every request sends cookies, and axios copies the XSRF-TOKEN cookie
 * into the X-XSRF-TOKEN header (the API's CSRF check).
 */
export function setupApiAuth() {
  axios.defaults.withCredentials = true;
  axios.defaults.xsrfCookieName = "XSRF-TOKEN";
  axios.defaults.xsrfHeaderName = "X-XSRF-TOKEN";

  axios.interceptors.response.use(
    (response) => response,
    async (error) => {
      // Bot defence: the API asked for a proof-of-work (suspicious traffic / attack mode).
      // Solve it in a worker and replay the same request once, transparently.
      const challenge = error.response?.status === 428 && error.response.data?.error === "challenge_required"
        ? error.response.data
        : null;
      if (challenge && error.config && !error.config.__botProofSent) {
        const proof = await solveChallenge(challenge.challenge, challenge.difficulty);
        return axios({
          ...error.config,
          __botProofSent: true,
          headers: { ...error.config.headers, [PROOF_HEADER]: proof },
        });
      }

      const url = error.config?.url ?? "";
      const isCredentialCheck = credentialEndpoints.some((endpoint) => url.includes(endpoint));
      if (error.response?.status === 401 && !isCredentialCheck && sessionStorage.getItem("Id")) {
        SESSION_KEYS.forEach((key) => sessionStorage.removeItem(key));
        window.location.assign("/login?expired=1");
      }
      return Promise.reject(error);
    }
  );
}
