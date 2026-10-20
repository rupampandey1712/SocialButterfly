import { useEffect, useRef, useState } from "react";
import { AddPhotoAlternateOutlined, CheckRounded, TextFieldsRounded } from "@mui/icons-material";
import AnimatedModal from "../modal/AnimatedModal";
import MusicPicker from "../media/MusicPicker";
import PostSoundtrack from "../media/PostSoundtrack";
import { showToast } from "../toast/Toaster";
import { createStory } from "../../utils/stories";
import { getCurrentUser } from "../../utils/session";
import { apiError } from "../../utils/account";
import "./storyComposer.css";

const maxBytes = 50 * 1024 * 1024;

/** Create a 24-hour story. The media is stored in the "stories" Azure Blob container. */
export default function StoryComposer({ onClose }) {
  const user = getCurrentUser();
  const [file, setFile] = useState(null);
  const [preview, setPreview] = useState(null);
  const [caption, setCaption] = useState("");
  const [showCaption, setShowCaption] = useState(false);
  const [progress, setProgress] = useState(null);
  const [isDone, setIsDone] = useState(false);
  const [isDragging, setIsDragging] = useState(false);
  const [music, setMusic] = useState(null);
  const [musicPreview, setMusicPreview] = useState(null);
  const inputRef = useRef(null);

  useEffect(() => () => preview && URL.revokeObjectURL(preview), [preview]);

  useEffect(() => {
    if (!music?.file) {
      setMusicPreview(null);
      return undefined;
    }
    const url = URL.createObjectURL(music.file);
    setMusicPreview(url);
    return () => URL.revokeObjectURL(url);
  }, [music?.file]);

  const choose = (selected) => {
    if (!selected) return;
    if (!selected.type.startsWith("image/") && !selected.type.startsWith("video/")) {
      showToast("Stories can be photos or videos");
      return;
    }
    if (selected.size > maxBytes) {
      showToast("Story media must be 50 MB or smaller");
      return;
    }
    setFile(selected);
    setPreview(URL.createObjectURL(selected));
  };

  const share = async (close) => {
    if (!file || progress !== null) return;
    setProgress(0);
    try {
      await createStory(user.id, file, caption.trim(), setProgress, music);
      setIsDone(true);
      setTimeout(close, 1100);
    } catch (error) {
      setProgress(null);
      showToast(apiError(error, "Couldn't share your story"));
    }
  };

  const isVideo = file?.type.startsWith("video/");
  const percent = Math.round((progress ?? 0) * 100);

  return (
    <AnimatedModal onClose={onClose} className="storyComposer" label="Add to your story">
      {(close) => (
        <>
          <header className="storyComposerHeader">
            <h2>{isDone ? "Shared!" : progress !== null ? "Sharing to your story" : "Add to your story"}</h2>
          </header>

          {!file ? (
            <div
              className={`storyComposerDrop ${isDragging ? "isDragging" : ""}`}
              onDragOver={(e) => { e.preventDefault(); setIsDragging(true); }}
              onDragLeave={() => setIsDragging(false)}
              onDrop={(e) => { e.preventDefault(); setIsDragging(false); choose(e.dataTransfer.files?.[0]); }}
            >
              <span className="storyComposerIcon"><AddPhotoAlternateOutlined /></span>
              <p>Drag a photo or video here</p>
              <span className="storyComposerHint">Stories disappear after 24 hours.</span>
              <button type="button" className="igButton" onClick={() => inputRef.current?.click()}>
                Select from computer
              </button>
              <input ref={inputRef} type="file" accept="image/*,video/*" hidden onChange={(e) => choose(e.target.files?.[0])} />
            </div>
          ) : (
            <div className="storyComposerBody">
              <div className="storyComposerPreview">
                {isVideo ? (
                  <video src={preview} autoPlay muted loop playsInline />
                ) : (
                  <img src={preview} alt="Story preview" />
                )}

                {musicPreview && (
                  <PostSoundtrack
                    key={musicPreview}
                    src={musicPreview}
                    title={music.title}
                    artist={music.artist}
                    active={progress === null}
                  />
                )}

                {(showCaption || caption) && (
                  <input
                    className="storyComposerCaption"
                    value={caption}
                    autoFocus
                    maxLength={200}
                    placeholder="Add text…"
                    onChange={(e) => setCaption(e.target.value)}
                  />
                )}

                {progress === null && (
                  <button type="button" className="storyComposerTool" onClick={() => setShowCaption(true)} aria-label="Add text">
                    <TextFieldsRounded />
                  </button>
                )}

                {progress !== null && (
                  <div className="storyComposerOverlay">
                    {isDone ? (
                      <span className="storyComposerDone"><CheckRounded /></span>
                    ) : (
                      <span className="storyComposerProgress" style={{ "--progress": `${percent * 3.6}deg` }}>
                        <span>{percent}%</span>
                      </span>
                    )}
                  </div>
                )}
              </div>

              {progress === null && (
                <div className="storyComposerMusic">
                  <MusicPicker value={music} onChange={setMusic} compact />
                </div>
              )}

              <footer className="storyComposerFooter">
                <button
                  type="button"
                  className="igButton secondary"
                  disabled={progress !== null}
                  onClick={() => { setFile(null); setPreview(null); setCaption(""); setShowCaption(false); setMusic(null); }}
                >
                  Change
                </button>
                <button type="button" className="igButton storyComposerShare" onClick={() => share(close)} disabled={progress !== null}>
                  <span className="storyComposerAvatar">
                    <img src={user.image} alt="" />
                  </span>
                  Your story
                </button>
              </footer>
            </div>
          )}
        </>
      )}
    </AnimatedModal>
  );
}
