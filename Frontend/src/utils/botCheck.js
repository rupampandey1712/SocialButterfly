export const BOT_CHECK_EVENT = "botcheck";
export const PROOF_HEADER = "X-Bot-Proof";

/**
 * Solves a proof-of-work challenge from the API (HTTP 428) off the main thread.
 * Usually takes a fraction of a second; the UI shows "Checking you're human…".
 */
export function solveChallenge(challenge, difficulty) {
  window.dispatchEvent(new CustomEvent(BOT_CHECK_EVENT, { detail: { active: true } }));
  const worker = new Worker(new URL("./powWorker.js", import.meta.url), { type: "module" });

  return new Promise((resolve, reject) => {
    const done = () => {
      worker.terminate();
      window.dispatchEvent(new CustomEvent(BOT_CHECK_EVENT, { detail: { active: false } }));
    };
    worker.onmessage = ({ data }) => {
      done();
      if (data.error) reject(new Error(data.error));
      else resolve(`${challenge}:${data.nonce}`);
    };
    worker.onerror = (error) => {
      done();
      reject(error);
    };
    worker.postMessage({ challenge, difficulty });
  });
}

/** Hidden "website" field: people never see it, form-filling bots do (honeypot). */
export const honeypotProps = {
  type: "text",
  name: "website",
  tabIndex: -1,
  autoComplete: "off",
  "aria-hidden": "true",
  className: "authHoneypot",
};
