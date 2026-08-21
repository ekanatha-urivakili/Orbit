import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

vi.mock('@sentry/react', () => ({ init: vi.fn() }))

describe('initSentry', () => {
  const originalEnv = { ...import.meta.env }

  beforeEach(() => {
    vi.resetModules()
  })

  afterEach(() => {
    Object.assign(import.meta.env, originalEnv)
  })

  it('does not call Sentry.init when VITE_SENTRY_DSN is unset', async () => {
    import.meta.env.VITE_SENTRY_DSN = ''
    const Sentry = await import('@sentry/react')
    const { initSentry } = await import('./sentry')

    initSentry()

    expect(Sentry.init).not.toHaveBeenCalled()
  })

  it('calls Sentry.init when VITE_SENTRY_DSN is set', async () => {
    import.meta.env.VITE_SENTRY_DSN = 'https://example.ingest.sentry.io/1'
    const Sentry = await import('@sentry/react')
    const { initSentry } = await import('./sentry')

    initSentry()

    expect(Sentry.init).toHaveBeenCalledWith(expect.objectContaining({ dsn: 'https://example.ingest.sentry.io/1' }))
  })
})
