import { useRef, useState } from "react";
import { Favorite } from "@mui/icons-material";
import VideoPlayer from "../media/VideoPlayer";
import MusicPlayer from "../media/MusicPlayer";
import PostSoundtrack from "../media/PostSoundtrack";
import { useInView } from "../media/useMediaPlayback";
import { getPublicImageUrl } from "../../utils/image";
import { getMediaType } from "../../utils/media";

/**
 * A post's media: photo, video (autoplay in view, tap to pause) or song (music
 * player). Photos and videos can carry a soundtrack. Double-tap likes, with
 * Instagram's heart burst.
 */
export default function PostMedia({ post, onDoubleTap, className = "" }) {
  const [burstKey, setBurstKey] = useState(0);
  const [isLoaded, setIsLoaded] = useState(false);
  const lastTapRef = useRef(0);
  const containerRef = useRef(null);
  const inView = useInView(containerRef);
  const type = getMediaType(post.imagePath);
  const contained = className.includes("isContained");
  if (!type) return null;

  const src = getPublicImageUrl(post.imagePath, "");
  const musicSrc = post.musicPath ? getPublicImageUrl(post.musicPath, "") : null;

  const like = () => {
    setBurstKey((key) => key + 1);
    onDoubleTap?.();
  };

  const handleImageTap = () => {
    const now = Date.now();
    if (now - lastTapRef.current < 300) {
      like();
      lastTapRef.current = 0;
    } else {
      lastTapRef.current = now;
    }
  };

  return (
    <div ref={containerRef} className={`postMedia isType-${type} ${className}`}>
      {type === "image" && (
        <div className="postMediaImage" onClick={handleImageTap}>
          {!isLoaded && <div className="postMediaPlaceholder skeleton" />}
          <img
            src={src}
            alt=""
            className={`postMediaContent ${isLoaded ? "isLoaded" : ""}`}
            onLoad={() => setIsLoaded(true)}
            onError={() => setIsLoaded(true)}
            draggable={false}
          />
        </div>
      )}

      {type === "video" && (
        <VideoPlayer src={src} onDoubleTap={like} silenced={Boolean(musicSrc)} contained={contained} />
      )}

      {type === "audio" && (
        <MusicPlayer
          src={src}
          title={post.musicTitle || post.description}
          artist={post.musicArtist || post.user?.name}
          cover={getPublicImageUrl(post.user?.imagePath)}
          onDoubleTap={like}
          contained={contained}
        />
      )}

      {musicSrc && type !== "audio" && (
        <PostSoundtrack src={musicSrc} title={post.musicTitle} artist={post.musicArtist} active={inView} />
      )}

      {burstKey > 0 && <Favorite key={burstKey} className="postHeartBurst" aria-hidden="true" />}
    </div>
  );
}
