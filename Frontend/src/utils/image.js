export const DEFAULT_AVATAR = "/assets/person/1.jpeg";

/**
 * Resolves an image URL so it is accessible from the browser.
 * Rewrites any internal Docker hostnames (such as azurite) to localhost,
 * and provides a fallback avatar if no URL is provided.
 */
export function getPublicImageUrl(url, fallback = DEFAULT_AVATAR) {
  if (!url || typeof url !== "string" || !url.trim()) {
    return fallback;
  }

  let resolved = url.trim();

  if (resolved.includes("://azurite:10000")) {
    resolved = resolved.replace("://azurite:10000", "://localhost:10000");
  } else if (resolved.startsWith("http://azurite")) {
    resolved = resolved.replace("http://azurite", "http://localhost");
  }

  return resolved;
}
