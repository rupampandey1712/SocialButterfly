const videoExtensions = [".mp4", ".mov", ".webm", ".m4v", ".ogv"];
const audioExtensions = [".mp3", ".m4a", ".aac", ".wav", ".ogg", ".oga", ".opus", ".flac", ".weba"];

export function getMediaType(path) {
  if (!path) return null;
  const clean = path.split("?")[0].toLowerCase();
  if (videoExtensions.some((ext) => clean.endsWith(ext))) return "video";
  if (audioExtensions.some((ext) => clean.endsWith(ext))) return "audio";
  return "image";
}

export function formatDuration(seconds) {
  if (!Number.isFinite(seconds) || seconds < 0) return "0:00";
  const whole = Math.floor(seconds);
  return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, "0")}`;
}

/**
 * Instagram-style playback rules shared by every player:
 * - only one video/song plays at a time (starting one pauses the previous);
 * - one global mute switch: feed videos start muted, and unmuting applies everywhere.
 */
let activeElement = null;
let muted = true;
const muteListeners = new Set();

export const playback = {
  /** Call right before playing `element`; pauses whatever else was playing. */
  claim(element) {
    if (activeElement && activeElement !== element && !activeElement.paused) {
      activeElement.pause();
    }
    activeElement = element;
  },

  release(element) {
    if (activeElement === element) activeElement = null;
  },

  isMuted() {
    return muted;
  },

  setMuted(value) {
    muted = value;
    muteListeners.forEach((listener) => listener(muted));
  },

  onMuteChange(listener) {
    muteListeners.add(listener);
    return () => muteListeners.delete(listener);
  },
};
