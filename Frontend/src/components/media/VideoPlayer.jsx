import { useEffect, useRef, useState } from "react";
import { PauseRounded, PlayArrowRounded, VolumeOffRounded, VolumeUpRounded } from "@mui/icons-material";
import { useGlobalMute, useInView, useMediaElement } from "./useMediaPlayback";
import { formatDuration } from "../../utils/media";
import "./media.css";

const doubleTapMs = 260;

/**
 * Instagram-style video: plays muted while mostly on screen and pauses when it
 * leaves; tap toggles play/pause, double-tap likes, the speaker is a global mute.
 * `silenced` keeps the video's own audio off (used when the post has a soundtrack).
 */
export default function VideoPlayer({ src, onDoubleTap, silenced = false, contained = false, autoPlay = true }) {
  const containerRef = useRef(null);
  const inView = useInView(containerRef);
  const [muted, toggleMute] = useGlobalMute();
  const { mediaRef, isPlaying, progress, current, duration, isBuffering, play, pause, toggle, seek } = useMediaElement();
  const [flash, setFlash] = useState(null);
  const [isScrubbing, setIsScrubbing] = useState(false);
  const userPaused = useRef(false);
  const tapTimer = useRef(null);
  const barRef = useRef(null);

  // Autoplay in view unless the viewer explicitly paused this video.
  useEffect(() => {
    if (!autoPlay) return;
    if (inView && !userPaused.current) play();
    if (!inView) {
      pause();
      userPaused.current = false;
    }
  }, [inView, autoPlay, play, pause]);

  useEffect(() => () => clearTimeout(tapTimer.current), []);

  const handleTap = () => {
    if (tapTimer.current) {
      clearTimeout(tapTimer.current);
      tapTimer.current = null;
      onDoubleTap?.();
      return;
    }
    tapTimer.current = setTimeout(() => {
      tapTimer.current = null;
      const willPlay = !isPlaying;
      userPaused.current = !willPlay;
      toggle();
      setFlash({ icon: willPlay ? "play" : "pause", key: Date.now() });
    }, onDoubleTap ? doubleTapMs : 0);
  };

  const seekFromPointer = (event) => {
    const rect = barRef.current.getBoundingClientRect();
    seek((event.clientX - rect.left) / rect.width);
  };

  return (
    <div ref={containerRef} className={`videoPlayer ${contained ? "isContained" : ""}`}>
      <video
        ref={mediaRef}
        src={src}
        loop
        playsInline
        preload="metadata"
        muted={silenced || muted}
        onClick={handleTap}
      />

      {isBuffering && isPlaying && <span className="mediaSpinner" aria-label="Loading" />}

      {!isPlaying && !flash && (
        <button type="button" className="videoBigPlay" onClick={handleTap} aria-label="Play video">
          <PlayArrowRounded />
        </button>
      )}

      {flash && (
        <span key={flash.key} className="videoFlash" onAnimationEnd={() => setFlash(null)} aria-hidden="true">
          {flash.icon === "play" ? <PlayArrowRounded /> : <PauseRounded />}
        </span>
      )}

      <span className="videoTime">{formatDuration(Math.max(duration - current, 0))}</span>

      {!silenced && (
        <button
          type="button"
          className="mediaMuteButton"
          onClick={(e) => { e.stopPropagation(); toggleMute(); if (!isPlaying) play(); }}
          aria-label={muted ? "Unmute" : "Mute"}
        >
          {muted ? <VolumeOffRounded /> : <VolumeUpRounded />}
        </button>
      )}

      <div
        ref={barRef}
        className={`videoProgress ${isScrubbing ? "isScrubbing" : ""}`}
        role="slider"
        aria-label="Seek"
        aria-valuemin={0}
        aria-valuemax={Math.round(duration)}
        aria-valuenow={Math.round(current)}
        tabIndex={0}
        onKeyDown={(e) => {
          if (e.key === "ArrowRight") seek(progress + 5 / (duration || 1));
          if (e.key === "ArrowLeft") seek(progress - 5 / (duration || 1));
        }}
        onPointerDown={(e) => {
          e.stopPropagation();
          e.currentTarget.setPointerCapture(e.pointerId);
          setIsScrubbing(true);
          seekFromPointer(e);
        }}
        onPointerMove={(e) => isScrubbing && seekFromPointer(e)}
        onPointerUp={() => setIsScrubbing(false)}
      >
        <span className="videoProgressFill" style={{ transform: `scaleX(${progress})` }} />
        <span className="videoProgressHandle" style={{ left: `${progress * 100}%` }} />
      </div>
    </div>
  );
}
