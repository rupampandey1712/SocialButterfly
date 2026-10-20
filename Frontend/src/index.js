import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import { BrowserRouter } from 'react-router-dom';
import "bootstrap/dist/css/bootstrap.min.css"
import "@fortawesome/fontawesome-free/css/all.min.css";
import "./index.css"
import { applyTheme } from "./utils/theme";
import { setupApiAuth } from "./utils/api";
import { bootstrapSession } from "./utils/session";

// Apply the saved light/dark preference before the first paint.
applyTheme();
setupApiAuth();

const root = ReactDOM.createRoot(document.getElementById('root'));

// Resolve the cookie session (and get a CSRF token) before routing decides login vs. app.
bootstrapSession().finally(() => {
  root.render(
    <BrowserRouter>
    <React.StrictMode>
      <App />
    </React.StrictMode>
    </BrowserRouter>
  );
});
