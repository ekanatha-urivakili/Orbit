import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, act } from '@testing-library/react'

// Mock virtual:pwa-register
let registeredCallback: { onNeedRefresh?: () => void } | undefined
const mockUpdateSW = vi.fn().mockResolvedValue(undefined)

vi.mock('virtual:pwa-register', () => ({
  registerSW: vi.fn((options) => {
    registeredCallback = options
    return mockUpdateSW
  }),
}))

describe('PWA Service Worker Lib', () => {
  beforeEach(async () => {
    vi.resetModules()
    registeredCallback = undefined
    mockUpdateSW.mockClear()
    Object.defineProperty(globalThis.navigator, 'serviceWorker', {
      value: {},
      configurable: true,
      writable: true,
    })
  })

  it('registers service worker and triggers update listeners when refresh needed', async () => {
    const pwa = await import('./pwa')
    pwa.initServiceWorker()

    expect(registeredCallback).toBeDefined()
    expect(pwa.isUpdateAvailable()).toBe(false)

    const listener = vi.fn()
    const unsubscribe = pwa.subscribeToUpdates(listener)

    // Trigger onNeedRefresh callback
    act(() => {
      registeredCallback?.onNeedRefresh?.()
    })

    expect(listener).toHaveBeenCalledTimes(1)
    expect(pwa.isUpdateAvailable()).toBe(true)

    unsubscribe()
  })

  it('invokes updateSW on applyUpdate', async () => {
    const pwa = await import('./pwa')
    pwa.initServiceWorker()

    pwa.applyUpdate()
    expect(mockUpdateSW).toHaveBeenCalledWith(true)
  })

  it('useServiceWorkerUpdate hook tracks available state', async () => {
    const pwa = await import('./pwa')
    pwa.initServiceWorker()

    const { result } = renderHook(() => pwa.useServiceWorkerUpdate())
    expect(result.current.available).toBe(false)

    act(() => {
      registeredCallback?.onNeedRefresh?.()
    })

    expect(result.current.available).toBe(true)
  })
})
