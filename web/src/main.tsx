import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import App from './App'
import { ErrorBoundary } from './components/ErrorBoundary'
import { initServiceWorker } from './lib/pwa'
import { createQueryClient } from './lib/queryClient'
import { initSentry, Sentry } from './lib/sentry'
import { ApiError } from './api/client'
import './index.css'

initServiceWorker()
initSentry()

const queryClient = createQueryClient((error) => {
  Sentry.captureException(error, {
    tags: error instanceof ApiError && error.correlationId ? { correlationId: error.correlationId } : undefined,
  })
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <App />
      </QueryClientProvider>
    </ErrorBoundary>
  </StrictMode>,
)
