import { useEffect, useState } from 'react'
import { registerSW } from 'virtual:pwa-register'

type Listener = () => void

let updateSW: ((reloadPage?: boolean) => Promise<void>) | null = null
let needRefresh = false
const listeners = new Set<Listener>()

function notify() {
  listeners.forEach((listener) => listener())
}

export function initServiceWorker() {
  if (updateSW || !('serviceWorker' in navigator)) return
  updateSW = registerSW({
    onNeedRefresh() {
      needRefresh = true
      notify()
    },
  })
}

export function subscribeToUpdates(listener: Listener) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export function isUpdateAvailable() {
  return needRefresh
}

export function applyUpdate() {
  void updateSW?.(true)
}

export function useServiceWorkerUpdate() {
  const [available, setAvailable] = useState(needRefresh)
  useEffect(() => subscribeToUpdates(() => setAvailable(true)), [])
  return { available, applyUpdate }
}
