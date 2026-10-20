import { useMemo, useRef, useState } from "react";
import { MusicNoteRounded, PauseRounded, PlayArrowRounded, Replay10Rounded, Forward10Rounded } from "@mui/icons-material";
import { useMediaElement } from "./useMediaPlayback";
import { formatDuration } from "../../utils/media";
import { DEFAULT_AVATAR } from "../../utils/image";
import "./media.css";

const palettes = [
  ["#fa7e1e", "#d62976", "#962fbf"],
  ["#4f5bd5", "#962fbf", "#d62976"],
  ["#00c6ff", "#0072ff", "#7f00ff"],
  ["#f7971e", "#ffd200", "#f857a6"],
  ["#11998e", "#38ef7d", "#0575e6"],
];

function paletteFor(text = "") {
  let hash = 0;
  for (const char of text) hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  return palettes[hash % palettes.length];
}

/** Song post: spinning vinyl, title/artist, play/pause, ±10s, seekable timeline and an equalizer. */
export default function MusicPlayer({ src, title, artist, cover, onDoubleTap, contained = false }) {
  const { mediaRef, isPlaying, progress, current, duration, isBuffering, toggle, seek } = useMediaElement();
  const [isScrubbing, setIsScrubbing] = useState(false);
  const barRef = useRef(null);
  const lastTap = useRef(0);
  const [c1, c2, c3] = useMemo(() => paletteFor(title || src), [title, src]);

  const seekFromPointer = (event) => {
    const rect = barRef.current.getBoundingClientRect();
    seek((event.clientX - rect.left) / rect.width);
  };

  const onSurfaceClick = (event) => {
    if (event.target.closest("button, [role='slider']")) return;
    const now = Date.now();
    if (now - lastTap.current < 300) onDoubleTap?.();
    lastTap.current = now;
  };

  const skip = (seconds) => {
    const media = mediaRef.current;
    if (media?.duration) media.currentTime = Math.min(Math.max(media.currentTime + seconds, 0), media.duration - 0.1);
  };

  return (
    <div
      className={`musicPlayer ${isPlaying ? "isPlaying" : ""} ${contained ? "isContained" : ""}`}
      style={{ "--music-a": c1, "--music-b": c2, "--music-c": c3 }}
      onClick={onSurfaceClick}
    >
      <audio ref={mediaRef} src={src} preload="metadata" />

      <div className="musicVinylWrap">
        <div className="musicVinyl">
          <img src={cover || DEFAULT_AVATAR} alt="" onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }} />
        </div>
        <span className="musicTonearm" aria-hidden="true" />
      </div>

      <div className="musicInfo">
        <span className="musicLabel"><MusicNoteRounded fontSize="inherit" /> Original audio</span>
        <strong className="musicTitle">{title || "Untitled track"}</strong>
        {artist && <span className="musicArtist">{artist}</span>}

        <div className="musicEqualizer" aria-hidden="true">
          {Array.from({ length: 18 }, (_, i) => (
            <span key={i} style={{ animationDelay: `${(i % 6) * -0.17}s`, animationDuration: `${0.7 + (i % 4) * 0.18}s` }} />
          ))}
        </div>

        <div
          ref={barRef}
          className={`musicTimeline ${isScrubbing ? "isScrubbing" : ""}`}
          role="slider"
          aria-label="Seek"
          aria-valuemin={0}
          aria-valuemax={Math.round(duration)}
          aria-valuenow={Math.round(current)}
          tabIndex={0}
          onKeyDown={(e) => {
            if (e.key === "ArrowRight") skip(5);
            if (e.key === "ArrowLeft") skip(-5);
          }}
          onPointerDown={(e) => {
            e.currentTarget.setPointerCapture(e.pointerId);
            setIsScrubbing(true);
            seekFromPointer(e);
          }}
          onPointerMove={(e) => isScrubbing && seekFromPointer(e)}
          onPointerUp={() => setIsScrubbing(false)}
        >
          <span className="musicTimelineFill" style={{ transform: `scaleX(${progress})` }} />
          <span className="musicTimelineHandle" style={{ left: `${progress * 100}%` }} />
        </div>
        <div className="musicTimes">
          <span>{formatDuration(current)}</span>
          <span>{formatDuration(duration)}</span>
        </div>

        <div className="musicControls">
          <button type="button" onClick={() => skip(-10)} aria-label="Back 10 seconds"><Replay10Rounded /></button>
          <button type="button" className="musicPlayButton" onClick={toggle} aria-label={isPlaying ? "Pause" : "Play"}>
            {isBuffering && isPlaying ? <span className="mediaSpinner isInline" /> : isPlaying ? <PauseRounded /> : <PlayArrowRounded />}
          </button>
          <button type="button" onClick={() => skip(10)} aria-label="Forward 10 seconds"><Forward10Rounded /></button>
        </div>
      </div>
    </div>
  );
}
