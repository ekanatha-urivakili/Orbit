import * as Sentry from '@sentry/react'

// §4.5: vendor choice, not the doc's requirement - it only specifies the join key (correlation
// id, attached where errors are captured). No-ops when no DSN is configured, so local dev/CI
// behaves identically to today.
export function initSentry() {
  const dsn = import.meta.env.VITE_SENTRY_DSN
  if (!dsn) return

  Sentry.init({
    dsn,
    environment: import.meta.env.MODE,
  })
}

export { Sentry }
