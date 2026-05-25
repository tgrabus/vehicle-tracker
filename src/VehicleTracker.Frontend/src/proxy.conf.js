// API_HTTPS is injected by Aspire service discovery when the AppHost wires up the backend.
// Variable name pattern: <RESOURCENAME>_<SCHEME> (uppercase). Assumes backend resource named "api".
// Fallback to localhost for running Angular standalone without Aspire.
const target = process.env.API_HTTPS ?? 'https://localhost:7246';

const PROXY_CONFIG = [
  {
    context: ["/api"],
    target,
    secure: false,
    changeOrigin: true
  }
];

module.exports = PROXY_CONFIG;
