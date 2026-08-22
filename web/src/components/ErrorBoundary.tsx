import type { ReactNode } from 'react'
import { Sentry } from '../lib/sentry'
import { ApiError } from '../api/client'
import { ErrorScreen } from './layout/FeedbackScreens'

export function ErrorBoundary({ children }: { children: ReactNode }) {
  return (
    <Sentry.ErrorBoundary
      fallback={({ error }) => <ErrorScreen message={error instanceof Error ? error.message : 'An unexpected error occurred.'} />}
      beforeCapture={(scope, error) => {
        if (error instanceof ApiError && error.correlationId) {
          scope.setTag('correlationId', error.correlationId)
        }
      }}
    >
      {children}
    </Sentry.ErrorBoundary>
  )
}
