import { useEffect, useRef, useState } from "react";
import { ArrowBackRounded, CheckRounded, PhotoLibraryOutlined } from "@mui/icons-material";
import AnimatedModal from "../modal/AnimatedModal";
import MusicPicker, { isAudioFile, titleFromFile } from "../media/MusicPicker";
import MusicPlayer from "../media/MusicPlayer";
import { apiError } from "../../utils/account";
import { createPost } from "../../utils/posts";
import { getCurrentUser } from "../../utils/session";
import { DEFAULT_AVATAR } from "../../utils/image";
import { showToast } from "../toast/Toaster";
import "./createPost.css";

const maxCaptionLength = 2200;
const maxFileBytes = 80 * 1024 * 1024;

/**
 * Instagram's create flow: pick a photo, video or song -> caption (+ optional music)
 * -> share. Files go to Azure Blob Storage ("post-images" / "music") and a PostCreated
 * event goes to Service Bus so friends are notified by the Functions worker.
 */
export default function CreatePostModal({ onClose }) {
  const user = getCurrentUser();
  const [file, setFile] = useState(null);
  const [previewUrl, setPreviewUrl] = useState(null);
  const [step, setStep] = useState("select");
  const [caption, setCaption] = useState("");
  const [isDragging, setIsDragging] = useState(false);
  const [status, setStatus] = useState("idle");
  const [music, setMusic] = useState(null);
  const [songInfo, setSongInfo] = useState({ title: "", artist: "" });
  const [progress, setProgress] = useState(0);
  const inputRef = useRef(null);
  const isSong = isAudioFile(file);

  useEffect(() => () => previewUrl && URL.revokeObjectURL(previewUrl), [previewUrl]);

  const chooseFile = (selected) => {
    if (!selected) return;
    if (!selected.type.startsWith("image/") && !selected.type.startsWith("video/") && !isAudioFile(selected)) {
      showToast("Choose a photo, video or song");
      return;
    }
    if (selected.size > maxFileBytes) {
      showToast("Files must be 80 MB or smaller");
      return;
    }
    setFile(selected);
    setPreviewUrl(URL.createObjectURL(selected));
    if (isAudioFile(selected)) {
      setSongInfo({ title: titleFromFile(selected), artist: "" });
      setMusic(null);
    }
    setStep("details");
  };

  const share = async (close) => {
    if (status === "sharing" || (!file && !caption.trim())) return;
    setStatus("sharing");
    try {
      const soundtrack = isSong ? { title: songInfo.title.trim(), artist: songInfo.artist.trim() } : music;
      await createPost(user.id, caption.trim(), file, soundtrack, setProgress);
      setStatus("done");
      setTimeout(close, 1400);
    } catch (error) {
      setStatus("idle");
      setProgress(0);
      showToast(apiError(error, "Couldn't share your post"));
    }
  };

  const goBack = () => {
    setFile(null);
    setPreviewUrl(null);
    setMusic(null);
    setStep("select");
  };

  return (
    <AnimatedModal onClose={onClose} className={`createPost ${step === "details" ? "isDetails" : ""}`} label="Create new post">
      {(close) => (
        <>
          <header className="createPostHeader">
            {step === "details" && status === "idle" ? (
              <button type="button" className="igIconButton" onClick={goBack} aria-label="Back">
                <ArrowBackRounded />
              </button>
            ) : <span className="createPostHeaderSpacer" />}
            <h2>{status === "done" ? "Post shared" : status === "sharing" ? "Sharing" : "Create new post"}</h2>
            {step === "details" && status === "idle" ? (
              <button type="button" className="igLinkButton" onClick={() => share(close)} disabled={!file && !caption.trim()}>
                Share
              </button>
            ) : <span className="createPostHeaderSpacer" />}
          </header>

          {status !== "idle" ? (
            <div className="createPostStatus">
              {status === "sharing" ? (
                <div className="createPostRingWrap" aria-label="Sharing">
                  <div className="createPostRing" />
                  <span className="createPostPercent">{Math.round(progress * 100)}%</span>
                </div>
              ) : (
                <div className="createPostRing isDone">
                  <CheckRounded />
                </div>
              )}
              <p>{status === "sharing" ? "Uploading to your feed…" : "Your post has been shared."}</p>
            </div>
          ) : step === "select" ? (
            <div
              className={`createPostDrop ${isDragging ? "isDragging" : ""}`}
              onDragOver={(e) => { e.preventDefault(); setIsDragging(true); }}
              onDragLeave={() => setIsDragging(false)}
              onDrop={(e) => {
                e.preventDefault();
                setIsDragging(false);
                chooseFile(e.dataTransfer.files?.[0]);
              }}
            >
              <PhotoLibraryOutlined className="createPostDropIcon" />
              <p>Drag photos, videos or songs here</p>
              <button type="button" className="igButton" onClick={() => inputRef.current?.click()}>
                Select from computer
              </button>
              <button type="button" className="igLinkButton createPostTextOnly" onClick={() => setStep("details")}>
                Or write a text post
              </button>
              <input
                ref={inputRef}
                type="file"
                accept="image/*,video/*,audio/*"
                hidden
                onChange={(e) => chooseFile(e.target.files?.[0])}
              />
            </div>
          ) : (
            <div className="createPostDetails">
              {previewUrl && (
                <div className="createPostPreview">
                  {isSong ? (
                    <MusicPlayer
                      src={previewUrl}
                      title={songInfo.title}
                      artist={songInfo.artist || user.name}
                      cover={user.image}
                      contained
                    />
                  ) : file?.type.startsWith("video/") ? (
                    <video src={previewUrl} controls autoPlay muted loop playsInline />
                  ) : (
                    <img src={previewUrl} alt="Preview" />
                  )}
                </div>
              )}
              <div className="createPostCaption">
                <div className="createPostAuthor">
                  <img
                    className="igAvatar"
                    width={28}
                    height={28}
                    src={user.image}
                    alt=""
                    onError={(e) => { e.currentTarget.src = DEFAULT_AVATAR; }}
                  />
                  <strong>{user.name}</strong>
                </div>
                <textarea
                  autoFocus
                  value={caption}
                  onChange={(e) => setCaption(e.target.value.slice(0, maxCaptionLength))}
                  placeholder={previewUrl ? "Write a caption…" : "What's on your mind?"}
                  aria-label="Caption"
                />
                <span className="createPostCount">{caption.length.toLocaleString()}/{maxCaptionLength.toLocaleString()}</span>
                {isSong ? (
                  <div className="createPostSongFields">
                    <input value={songInfo.title} maxLength={120} placeholder="Song title"
                      onChange={(e) => setSongInfo((prev) => ({ ...prev, title: e.target.value }))} />
                    <input value={songInfo.artist} maxLength={120} placeholder="Artist (optional)"
                      onChange={(e) => setSongInfo((prev) => ({ ...prev, artist: e.target.value }))} />
                  </div>
                ) : file ? (
                  <MusicPicker value={music} onChange={setMusic} />
                ) : null}
              </div>
            </div>
          )}
        </>
      )}
    </AnimatedModal>
  );
}
