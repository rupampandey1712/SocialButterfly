import { useCallback, useEffect, useRef, useState } from "react";
import { playback } from "../../utils/media";

/** Tracks whether an element is mostly on screen (Instagram plays media at ~60% visibility). */
export function useInView(ref, threshold = 0.6) {
  const [inView, setInView] = useState(false);

  useEffect(() => {
    const element = ref.current;
    if (!element || typeof IntersectionObserver === "undefined") return undefined;
    const observer = new IntersectionObserver(
      ([entry]) => setInView(entry.isIntersecting && entry.intersectionRatio >= threshold),
      { threshold: [0, threshold, 1] }
    );
    observer.observe(element);
    return () => observer.disconnect();
  }, [ref, threshold]);

  return inView;
}

/** Subscribes to the app-wide mute switch. */
export function useGlobalMute() {
  const [muted, setMuted] = useState(playback.isMuted());
  useEffect(() => playback.onMuteChange(setMuted), []);
  const toggle = useCallback(() => playback.setMuted(!playback.isMuted()), []);
  return [muted, toggle];
}

/**
 * Wires a <video>/<audio> element to the shared playback rules and exposes
 * play/pause state, progress and seeking for custom controls.
 */
export function useMediaElement() {
  const mediaRef = useRef(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [progress, setProgress] = useState(0);
  const [current, setCurrent] = useState(0);
  const [duration, setDuration] = useState(0);
  const [isBuffering, setIsBuffering] = useState(false);

  useEffect(() => {
    const media = mediaRef.current;
    if (!media) return undefined;

    // Only audible media takes over playback; muted autoplay (or a video under its soundtrack) can coexist.
    const onPlay = () => { if (!media.muted) playback.claim(media); setIsPlaying(true); };
    const onPause = () => setIsPlaying(false);
    const onTime = () => {
      setCurrent(media.currentTime);
      setProgress(media.duration ? media.currentTime / media.duration : 0);
    };
    const onMeta = () => setDuration(media.duration || 0);
    const onWaiting = () => setIsBuffering(true);
    const onPlaying = () => setIsBuffering(false);

    media.addEventListener("play", onPlay);
    media.addEventListener("pause", onPause);
    media.addEventListener("timeupdate", onTime);
    media.addEventListener("loadedmetadata", onMeta);
    media.addEventListener("durationchange", onMeta);
    media.addEventListener("waiting", onWaiting);
    media.addEventListener("playing", onPlaying);
    return () => {
      media.removeEventListener("play", onPlay);
      media.removeEventListener("pause", onPause);
      media.removeEventListener("timeupdate", onTime);
      media.removeEventListener("loadedmetadata", onMeta);
      media.removeEventListener("durationchange", onMeta);
      media.removeEventListener("waiting", onWaiting);
      media.removeEventListener("playing", onPlaying);
      media.pause();
      playback.release(media);
    };
  }, []);

  const play = useCallback(() => {
    const media = mediaRef.current;
    if (!media) return;
    if (!media.muted) playback.claim(media);
    // Autoplay can be refused (e.g. unmuted before any user gesture); stay paused quietly.
    media.play().catch(() => setIsPlaying(false));
  }, []);

  const pause = useCallback(() => mediaRef.current?.pause(), []);

  const toggle = useCallback(() => {
    const media = mediaRef.current;
    if (!media) return;
    if (media.paused) play();
    else pause();
  }, [play, pause]);

  const seek = useCallback((fraction) => {
    const media = mediaRef.current;
    if (!media?.duration) return;
    media.currentTime = Math.min(Math.max(fraction, 0), 0.999) * media.duration;
  }, []);

  return { mediaRef, isPlaying, progress, current, duration, isBuffering, play, pause, toggle, seek };
}
