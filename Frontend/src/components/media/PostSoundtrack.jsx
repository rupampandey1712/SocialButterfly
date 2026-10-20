import { useEffect } from "react";
import { MusicNoteRounded, VolumeOffRounded, VolumeUpRounded } from "@mui/icons-material";
import { useGlobalMute, useMediaElement } from "./useMediaPlayback";
import "./media.css";

/**
 * Music added to a photo/video post (or story): a "♪ Title · Artist" chip plus a
 * looping track that plays while `active` and the global mute is off.
 */
export default function PostSoundtrack({ src, title, artist, active, paused = false, showMuteButton = true }) {
  const [muted, toggleMute] = useGlobalMute();
  const { mediaRef, isPlaying, play, pause } = useMediaElement();

  useEffect(() => {
    if (active && !muted && !paused) play();
    else pause();
  }, [active, muted, paused, play, pause]);

  const label = [title || "Original audio", artist].filter(Boolean).join(" · ");

  return (
    <>
      <audio ref={mediaRef} src={src} loop preload="auto" />
      <div className={`soundtrackChip ${isPlaying ? "isPlaying" : ""}`} title={label}>
        <span className="soundtrackIcon">
          {isPlaying ? (
            <span className="miniEqualizer" aria-hidden="true"><span /><span /><span /></span>
          ) : (
            <MusicNoteRounded fontSize="inherit" />
          )}
        </span>
        <span className="soundtrackMarquee">
          <span className={label.length > 22 ? "isScrolling" : ""}>{label}</span>
        </span>
      </div>
      {showMuteButton && (
        <button
          type="button"
          className="mediaMuteButton"
          onClick={(e) => { e.stopPropagation(); toggleMute(); }}
          aria-label={muted ? "Play music" : "Mute music"}
        >
          {muted ? <VolumeOffRounded /> : <VolumeUpRounded />}
        </button>
      )}
    </>
  );
}
