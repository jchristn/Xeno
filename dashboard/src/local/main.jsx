import React from 'react';
import ReactDOM from 'react-dom/client';
import LocalDashboardApp from './LocalDashboardApp.jsx';
import '../i18n/index.js';
import '../index.css';

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <LocalDashboardApp />
  </React.StrictMode>
);
