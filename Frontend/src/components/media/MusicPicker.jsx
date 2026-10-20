import { useEffect, useRef, useState } from "react";
import { CloseRounded, LibraryMusicRounded, PauseRounded, PlayArrowRounded } from "@mui/icons-material";
import { showToast } from "../toast/Toaster";
import { useMediaElement } from "./useMediaPlayback";
import "./musicPicker.css";

const maxMusicBytes = 25 * 1024 * 1024;

export function titleFromFile(file) {
  return file.name.replace(/\.[^.]+$/, "").replace(/[_-]+/g, " ").trim();
}

export function isAudioFile(file) {
  return file?.type.startsWith("audio/") || /\.(mp3|m4a|aac|wav|ogg|oga|opus|flac|weba)$/i.test(file?.name || "");
}

/**
 * "Add music" control for posts and stories. `value` is { file, title, artist } or null.
 * The track is previewed locally before upload to the "music" Blob container.
 */
export default function MusicPicker({ value, onChange, compact = false }) {
  const inputRef = useRef(null);
  const [previewUrl, setPreviewUrl] = useState(null);
  const { mediaRef, isPlaying, toggle, progress } = useMediaElement();

  useEffect(() => {
    if (!value?.file) {
      setPreviewUrl(null);
      return undefined;
    }
    const url = URL.createObjectURL(value.file);
    setPreviewUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [value?.file]);

  const choose = (file) => {
    if (!file) return;
    if (!isAudioFile(file)) {
      showToast("Choose an audio file (MP3, M4A, WAV, OGG, FLAC)");
      return;
    }
    if (file.size > maxMusicBytes) {
      showToast("Music files must be 25 MB or smaller");
      return;
    }
    onChange({ file, title: titleFromFile(file), artist: "" });
  };

  return (
    <div className={`musicPicker ${compact ? "isCompact" : ""}`}>
      <audio ref={mediaRef} src={previewUrl || undefined} loop />
      <input ref={inputRef} type="file" accept="audio/*" hidden onChange={(e) => { choose(e.target.files?.[0]); e.target.value = ""; }} />

      {!value ? (
        <button type="button" className="musicPickerAdd" onClick={() => inputRef.current?.click()}>
          <span className="musicPickerIcon"><LibraryMusicRounded /></span>
          <span>
            <strong>Add music</strong>
            <small>Plays while people view it</small>
          </span>
        </button>
      ) : (
        <div className="musicPickerSelected">
          <button
            type="button"
            className={`musicPickerPlay ${isPlaying ? "isPlaying" : ""}`}
            onClick={toggle}
            aria-label={isPlaying ? "Pause preview" : "Play preview"}
            style={{ "--progress": `${progress * 360}deg` }}
          >
            {isPlaying ? <PauseRounded /> : <PlayArrowRounded />}
          </button>
          <div className="musicPickerFields">
            <input
              value={value.title}
              maxLength={120}
              placeholder="Song title"
              onChange={(e) => onChange({ ...value, title: e.target.value })}
              aria-label="Song title"
            />
            <input
              value={value.artist}
              maxLength={120}
              placeholder="Artist (optional)"
              onChange={(e) => onChange({ ...value, artist: e.target.value })}
              aria-label="Artist"
            />
          </div>
          <button type="button" className="musicPickerRemove" onClick={() => onChange(null)} aria-label="Remove music">
            <CloseRounded fontSize="small" />
          </button>
        </div>
      )}
    </div>
  );
}
