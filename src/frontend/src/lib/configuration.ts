const defaultApiBaseUrl = 'http://localhost:8080'

export const configuration = Object.freeze({
  apiBaseUrl: (import.meta.env.VITE_API_BASE_URL || defaultApiBaseUrl).replace(/\/$/, ''),
  appName: import.meta.env.VITE_APP_NAME || 'Family Dashboard',
})
