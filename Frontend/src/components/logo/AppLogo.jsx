import "./appLogo.css";

export default function AppLogo({ compact = false, light = false, showTagline = false }) {
  return (
    <div className={`appLogo ${compact ? "appLogoCompact" : ""} ${light ? "appLogoLight" : ""}`}>
      <span className="appLogoMark" aria-hidden="true">
        <svg viewBox="0 0 64 64" role="img" focusable="false">
          <path className="wing wingLeft" d="M30 30C22 12 7 8 5 22c-2 13 10 20 25 13z" />
          <path className="wing wingRight" d="M34 30C42 12 57 8 59 22c2 13-10 20-25 13z" />
          <path className="wing wingLeftLower" d="M29 36C18 36 10 45 16 54c5 8 17 4 18-14z" />
          <path className="wing wingRightLower" d="M35 36c11 0 19 9 13 18-5 8-17 4-18-14z" />
          <circle cx="32" cy="33" r="4.5" className="body" />
          <path d="M32 29v-9M26 17c2 0 4 1 6 3M38 17c-2 0-4 1-6 3" className="antenna" />
        </svg>
      </span>
      <span className="appLogoText">
        <span className="appLogoName">SocialButterfly</span>
        {showTagline && <span className="appLogoTagline">Connect. Share. Grow.</span>}
      </span>
    </div>
  );
}
