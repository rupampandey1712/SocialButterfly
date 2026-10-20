const storageKey = "theme";
let mediaQuery;

function readStoredTheme() {
  try {
    return localStorage.getItem(storageKey) || "system";
  } catch {
    return "system";
  }
}

function resolve(theme) {
  if (theme === "light" || theme === "dark") return theme;
  return window.matchMedia?.("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

/** Applies "light" | "dark" | "system" to <html data-theme> and remembers it on this device. */
export function applyTheme(theme = readStoredTheme()) {
  const choice = theme || "system";
  try {
    localStorage.setItem(storageKey, choice);
  } catch {
    // Private mode: the theme still applies for this page load.
  }

  document.documentElement.dataset.theme = resolve(choice);
  document.documentElement.dataset.themeChoice = choice;

  mediaQuery?.removeEventListener?.("change", onSystemChange);
  if (choice === "system" && window.matchMedia) {
    mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
    mediaQuery.addEventListener?.("change", onSystemChange);
  }
}

function onSystemChange() {
  document.documentElement.dataset.theme = resolve("system");
}

export function currentThemeChoice() {
  return document.documentElement.dataset.themeChoice || readStoredTheme();
}
