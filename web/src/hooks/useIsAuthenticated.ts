import { useSyncExternalStore } from 'react'
import { isAuthenticated, subscribe } from '../api/auth'

export function useIsAuthenticated(): boolean {
  return useSyncExternalStore(subscribe, isAuthenticated)
}
