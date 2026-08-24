import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import './index.css'

console.log('--- VITE APP INITIALIZING ---');

const container = document.getElementById('root');

if (!container) {
    console.error('CRITICAL: Root container not found in index.html');
} else {
    console.log('Root container found, rendering App...');
    try {
        const root = ReactDOM.createRoot(container);
        root.render(
          <React.StrictMode>
            <App />
          </React.StrictMode>
        );
        console.log('App render called successfully');
    } catch (error) {
        console.error('CRITICAL ERROR DURING RENDER:', error);
        container.innerHTML = `<div style="padding: 20px; background: red; color: white;">
            <h1>Render Error</h1>
            <pre>${error}</pre>
        </div>`;
    }
}
