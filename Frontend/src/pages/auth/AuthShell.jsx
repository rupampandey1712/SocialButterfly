import { useEffect, useState } from "react";
import { ChatBubbleRounded, FavoriteRounded, PersonAddAlt1Rounded, VerifiedUserRounded } from "@mui/icons-material";
import { BOT_CHECK_EVENT } from "../../utils/botCheck";
import AppLogo from "../../components/logo/AppLogo";
import "./auth.css";

const showcase = [
  "/assets/post/1.jpeg",
  "/assets/post/3.jpeg",
  "/assets/post/5.jpeg",
  "/assets/post/8.jpeg",
  "/assets/post/10.jpeg",
];

const bubbles = [
  { icon: <FavoriteRounded />, label: "128", className: "bubbleLike" },
  { icon: <ChatBubbleRounded />, label: "24", className: "bubbleComment" },
  { icon: <PersonAddAlt1Rounded />, label: "+3", className: "bubbleFollow" },
];

/**
 * Instagram-style auth layout: an animated phone showcase on the left and the
 * form card(s) on the right, over a slowly drifting gradient background.
 */
export default function AuthShell({ children, footer, showcaseVisible = true }) {
  const [slide, setSlide] = useState(0);
  const [isCheckingHuman, setIsCheckingHuman] = useState(false);

  useEffect(() => {
    const onCheck = (event) => setIsCheckingHuman(event.detail.active);
    window.addEventListener(BOT_CHECK_EVENT, onCheck);
    return () => window.removeEventListener(BOT_CHECK_EVENT, onCheck);
  }, []);

  useEffect(() => {
    const timer = setInterval(() => setSlide((index) => (index + 1) % showcase.length), 3800);
    return () => clearInterval(timer);
  }, []);

  return (
    <div className="authPage">
      <div className="authAurora" aria-hidden="true">
        <span className="auroraBlob blobOne" />
        <span className="auroraBlob blobTwo" />
        <span className="auroraBlob blobThree" />
      </div>

      <main className="authMain">
        {showcaseVisible && (
          <section className="authShowcase" aria-hidden="true">
            <div className="authPhone">
              <div className="authPhoneNotch" />
              <div className="authPhoneScreen">
                {showcase.map((src, index) => (
                  <img
                    key={src}
                    src={src}
                    alt=""
                    className={`authSlide ${index === slide ? "isActive" : ""}`}
                  />
                ))}
                <div className="authPhoneHeader">
                  <span className="authPhoneAvatar" />
                  <span className="authPhoneLine" />
                </div>
              </div>
            </div>
            {bubbles.map((bubble, index) => (
              <span key={bubble.className} className={`authBubble ${bubble.className}`} style={{ animationDelay: `${index * 0.9}s` }}>
                {bubble.icon} {bubble.label}
              </span>
            ))}
          </section>
        )}

        <section className="authColumn">
          <div className="authCard authCardMain">
            <div className="authBrand">
              <AppLogo />
            </div>
            {isCheckingHuman && (
              <div className="authHumanCheck" role="status">
                <VerifiedUserRounded fontSize="small" />
                <span>Checking you're human…</span>
                <span className="authFieldSpinner" />
              </div>
            )}
            {children}
          </div>
          {footer && <div className="authCard authCardFooter">{footer}</div>}
          <p className="authLegal">© {new Date().getFullYear()} SocialButterfly · Connect. Share. Grow.</p>
        </section>
      </main>
    </div>
  );
}
