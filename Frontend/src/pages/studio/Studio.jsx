import { useCallback, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  AutoAwesomeRounded,
  CasinoOutlined,
  CropLandscapeRounded,
  CropPortraitRounded,
  CropSquareRounded,
  DownloadRounded,
  ErrorOutlineRounded,
  RefreshRounded,
  SendRounded,
} from "@mui/icons-material";
import Topbar from "../../components/topbar/Topbar";
import { showToast } from "../../components/toast/Toaster";
import { apiError } from "../../utils/account";
import { fetchImageJob, fetchMyImages, fetchStudioStatus, generateImage, shareImage, TERMINAL_STATUSES } from "../../utils/images";
import { timeAgo } from "../../utils/time";
import "./studio.css";

const styleLabels = {
  none: "No style",
  photo: "Photo",
  cinematic: "Cinematic",
  anime: "Anime",
  watercolor: "Watercolor",
  "3d": "3D render",
  neon: "Neon",
  sketch: "Sketch",
};

const aspects = [
  { key: "square", label: "Square", icon: <CropSquareRounded /> },
  { key: "portrait", label: "Portrait", icon: <CropPortraitRounded /> },
  { key: "landscape", label: "Landscape", icon: <CropLandscapeRounded /> },
];

const surprises = [
  "A cozy treehouse library glowing at dusk, fireflies, warm lanterns",
  "A butterfly made of stained glass resting on a dewy leaf, macro",
  "An astronaut having tea with a fox in a sunflower field",
  "A floating island city above the clouds at sunrise",
  "A retro diner on Mars with neon signs and red dust",
  "A tiny dragon curled up asleep in a teacup",
  "Koi fish swimming through a night sky full of lanterns",
  "A street market in the rain, reflections, Tokyo at night",
];

function useElapsed(since, active) {
  const [, tick] = useState(0);
  useEffect(() => {
    if (!active) return undefined;
    const timer = setInterval(() => tick((t) => t + 1), 1000);
    return () => clearInterval(timer);
  }, [active]);
  if (!since) return 0;
  const started = new Date(since.endsWith("Z") ? since : `${since}Z`).getTime();
  return Math.max(0, Math.floor((Date.now() - started) / 1000));
}

/**
 * AI Studio: text-to-image with the local ComfyUI Flux workflow. Jobs are processed
 * asynchronously by the Azure Function, so this page polls the job until it finishes.
 */
export default function Studio() {
  const navigate = useNavigate();
  const [status, setStatus] = useState(null);
  const [prompt, setPrompt] = useState("");
  const [style, setStyle] = useState("none");
  const [aspect, setAspect] = useState("square");
  const [job, setJob] = useState(null);
  const [history, setHistory] = useState([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [caption, setCaption] = useState("");
  const [isSharing, setIsSharing] = useState(false);
  const [revealed, setRevealed] = useState(false);
  const surpriseIndex = useRef(Math.floor(Math.random() * surprises.length));

  const isWorking = job && !TERMINAL_STATUSES.includes(job.status);
  const elapsed = useElapsed(job?.startedAt || job?.createdAt, isWorking);

  const refreshStatus = useCallback(() => fetchStudioStatus().then(setStatus).catch(() => setStatus({ online: false })), []);
  const refreshHistory = useCallback(() => fetchMyImages().then(setHistory).catch(() => {}), []);

  useEffect(() => {
    refreshStatus();
    refreshHistory();
    const timer = setInterval(refreshStatus, 15000);
    return () => clearInterval(timer);
  }, [refreshStatus, refreshHistory]);

  // Resume an unfinished job after a reload.
  useEffect(() => {
    if (!job && history.length && !TERMINAL_STATUSES.includes(history[0].status)) setJob(history[0]);
  }, [history, job]);

  // Poll the active job until it succeeds or fails.
  useEffect(() => {
    if (!isWorking) return undefined;
    const timer = setInterval(async () => {
      try {
        const latest = await fetchImageJob(job.id);
        setJob(latest);
        if (TERMINAL_STATUSES.includes(latest.status)) {
          refreshHistory();
          refreshStatus();
          if (latest.status === "Succeeded") showToast("Your image is ready ✨");
        }
      } catch {
        // Keep polling; transient network errors are fine.
      }
    }, 2000);
    return () => clearInterval(timer);
  }, [isWorking, job?.id, refreshHistory, refreshStatus]);

  useEffect(() => setRevealed(false), [job?.id, job?.imagePath]);

  const submit = async (event, overrides = {}) => {
    event?.preventDefault();
    const request = { prompt: prompt.trim(), style, aspect, ...overrides };
    if (request.prompt.length < 3 || isSubmitting) return;
    setIsSubmitting(true);
    try {
      const created = await generateImage(request);
      setJob(created);
      setCaption("");
      setHistory((prev) => [created, ...prev]);
      if (created.status === "Failed") showToast(created.error || "Couldn't start this image");
    } catch (error) {
      showToast(apiError(error, "Couldn't start generating"));
    } finally {
      setIsSubmitting(false);
      refreshStatus();
    }
  };

  const surprise = () => {
    surpriseIndex.current = (surpriseIndex.current + 1) % surprises.length;
    setPrompt(surprises[surpriseIndex.current]);
  };

  const share = async () => {
    if (!job || isSharing) return;
    setIsSharing(true);
    try {
      const postId = await shareImage(job.id, caption.trim() || job.prompt);
      showToast("Shared to your feed");
      navigate(`/p/${postId}`);
    } catch (error) {
      showToast(apiError(error, "Couldn't share this image"));
      setIsSharing(false);
    }
  };

  const loadFromHistory = (item) => {
    setJob(item);
    setPrompt(item.prompt);
    setStyle(item.style || "none");
    setAspect(item.width > item.height ? "landscape" : item.height > item.width ? "portrait" : "square");
  };

  const offline = status && !status.online;
  const remaining = status?.remainingToday;
  const canvasAspect = job ? `${job.width} / ${job.height}` : aspect === "portrait" ? "2 / 3" : aspect === "landscape" ? "3 / 2" : "1 / 1";
  const stage = job?.status === "Queued" ? "Waiting for the GPU…" : elapsed < 8 ? "Loading the Flux model…" : "Painting your image…";

  return (
    <>
      <Topbar />
      <div className="studioPage">
        <header className="studioHeader igFadeUp">
          <span className="studioIcon"><AutoAwesomeRounded /></span>
          <div>
            <h1>AI Studio</h1>
            <p>Describe anything and create an image with Flux, running on your own ComfyUI.</p>
          </div>
          <span className={`studioStatus ${status ? (status.online ? "isOnline" : "isOffline") : ""}`}>
            <span className="studioStatusDot" />
            {status ? (status.online ? "Generator online" : "Generator offline") : "Checking…"}
            {remaining !== undefined && <small>{remaining} left today</small>}
          </span>
        </header>

        {offline && (
          <div className="studioOffline igFadeIn">
            <ErrorOutlineRounded />
            <span>
              ComfyUI isn't reachable. Start it with <code>D:\ComfyUI-data\start-comfyui.bat</code>, then this page reconnects automatically.
            </span>
          </div>
        )}

        <div className="studioLayout">
          <form className="studioControls igFadeUp" style={{ animationDelay: "60ms" }} onSubmit={submit}>
            <label className="studioLabel" htmlFor="studioPrompt">
              Prompt
              <button type="button" className="studioSurprise" onClick={surprise}>
                <CasinoOutlined fontSize="small" /> Surprise me
              </button>
            </label>
            <div className="studioPromptWrap">
              <textarea
                id="studioPrompt"
                value={prompt}
                maxLength={800}
                placeholder="A cozy café on a rainy evening, warm light spilling onto the street…"
                onChange={(e) => setPrompt(e.target.value)}
                onKeyDown={(e) => { if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) submit(e); }}
              />
              <span className="studioCount">{prompt.length}/800</span>
            </div>

            <span className="studioLabel">Style</span>
            <div className="studioStyles">
              {(status?.styles?.length ? status.styles : Object.keys(styleLabels)).map((key) => (
                <button
                  key={key}
                  type="button"
                  className={`studioStyle style-${key} ${style === key ? "isActive" : ""}`}
                  onClick={() => setStyle(key)}
                >
                  <span className="studioStyleSwatch" />
                  {styleLabels[key] || key}
                </button>
              ))}
            </div>

            <span className="studioLabel">Aspect ratio</span>
            <div className="studioAspects">
              {aspects.map((a) => (
                <button key={a.key} type="button" className={aspect === a.key ? "isActive" : ""} onClick={() => setAspect(a.key)}>
                  {a.icon}
                  {a.label}
                </button>
              ))}
            </div>

            <button
              type="submit"
              className={`studioGenerate ${isSubmitting ? "isLoading" : ""}`}
              disabled={prompt.trim().length < 3 || isSubmitting || isWorking || offline || remaining === 0}
            >
              <AutoAwesomeRounded />
              <span>{isWorking ? "Creating…" : "Generate"}</span>
            </button>
            <small className="studioHint">Ctrl + Enter to generate · up to 2 at a time</small>
          </form>

          <section className="studioStage igFadeUp" style={{ animationDelay: "120ms" }}>
            <div className={`studioCanvas ${isWorking ? "isWorking" : ""}`} style={{ aspectRatio: canvasAspect }}>
              {!job && (
                <div className="studioEmpty">
                  <AutoAwesomeRounded />
                  <p>Your image will appear here</p>
                </div>
              )}

              {isWorking && (
                <div className="studioDreaming" aria-live="polite">
                  <span className="dreamBlob dreamA" />
                  <span className="dreamBlob dreamB" />
                  <span className="dreamBlob dreamC" />
                  <span className="dreamGrain" />
                  <div className="studioProgress">
                    <span className="studioSpinner" />
                    <strong>{stage}</strong>
                    <small>{elapsed}s · this usually takes under a minute</small>
                  </div>
                </div>
              )}

              {job?.status === "Succeeded" && job.imagePath && (
                <img
                  key={job.imagePath}
                  src={job.imagePath}
                  alt={job.prompt}
                  className={`studioImage ${revealed ? "isRevealed" : ""}`}
                  onLoad={() => setRevealed(true)}
                />
              )}

              {job?.status === "Failed" && (
                <div className="studioFailed igFadeIn">
                  <ErrorOutlineRounded />
                  <p>{job.error || "This image couldn't be created."}</p>
                  <button type="button" className="igButton secondary" onClick={(e) => submit(e)}>
                    <RefreshRounded fontSize="small" /> Try again
                  </button>
                </div>
              )}
            </div>

            {job?.status === "Succeeded" && (
              <div className="studioActions igFadeUp">
                <p className="studioUsedPrompt">“{job.prompt}”</p>
                <div className="studioShareRow">
                  <input
                    value={caption}
                    maxLength={2200}
                    placeholder="Write a caption (optional)…"
                    onChange={(e) => setCaption(e.target.value)}
                  />
                  <button type="button" className="igButton" onClick={share} disabled={isSharing}>
                    <SendRounded fontSize="small" /> {job.postId ? "View post" : "Share as post"}
                  </button>
                </div>
                <div className="studioSecondary">
                  <a className="igButton secondary" href={job.imagePath} target="_blank" rel="noreferrer" download>
                    <DownloadRounded fontSize="small" /> Download
                  </a>
                  <button type="button" className="igButton secondary" onClick={(e) => submit(e, { prompt: job.prompt })} disabled={isWorking}>
                    <RefreshRounded fontSize="small" /> Regenerate
                  </button>
                  <span className="studioSeed">seed {job.seed}</span>
                </div>
              </div>
            )}
          </section>
        </div>

        {history.length > 0 && (
          <section className="studioHistory">
            <h2>Your creations</h2>
            <div className="studioHistoryGrid">
              {history.map((item, i) => (
                <button
                  key={item.id}
                  type="button"
                  className={`studioThumb igFadeUp ${job?.id === item.id ? "isActive" : ""}`}
                  style={{ animationDelay: `${Math.min(i, 12) * 35}ms` }}
                  onClick={() => loadFromHistory(item)}
                  title={item.prompt}
                >
                  {item.status === "Succeeded" && item.imagePath ? (
                    <img src={item.imagePath} alt={item.prompt} loading="lazy" />
                  ) : (
                    <span className={`studioThumbState is${item.status}`}>
                      {item.status === "Failed" ? <ErrorOutlineRounded /> : <span className="studioSpinner" />}
                    </span>
                  )}
                  <span className="studioThumbMeta">{timeAgo(item.createdAt)}</span>
                </button>
              ))}
            </div>
          </section>
        )}
      </div>
    </>
  );
}
