import type { AuthSession, RegisterInput } from './types'

const apiUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5014/api/v1'
const refreshTokenStorageKey = 'orbit.refresh-token'
export const tenantStorageKey = 'orbit.tenant-id'

let accessToken: string | null = null
let accessTokenExpiresAt = 0
let refreshPromise: Promise<AuthSession | null> | null = null
let currentSession: AuthSession | null = null

type Listener = () => void
const listeners = new Set<Listener>()

function notify() {
  for (const listener of listeners) listener()
}

/** Subscribes to auth-state changes (login, logout, silent refresh). Returns an unsubscribe function. */
export function subscribe(listener: Listener): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

interface ProblemDetails {
  title?: string
  detail?: string
}

async function parseResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as ProblemDetails
    throw new Error(problem.detail ?? problem.title ?? `Request failed (${response.status})`)
  }

  return (await response.json()) as T
}

/** Reads the refresh token from whichever storage currently holds it (localStorage survives a
 * browser restart for a "remember me" session; sessionStorage clears when the tab closes). */
function getStoredRefreshToken(): { token: string; remember: boolean } | null {
  const persisted = localStorage.getItem(refreshTokenStorageKey)
  if (persisted) return { token: persisted, remember: true }
  const session = sessionStorage.getItem(refreshTokenStorageKey)
  return session ? { token: session, remember: false } : null
}

function setStoredRefreshToken(token: string, remember: boolean) {
  if (remember) {
    localStorage.setItem(refreshTokenStorageKey, token)
    sessionStorage.removeItem(refreshTokenStorageKey)
  } else {
    sessionStorage.setItem(refreshTokenStorageKey, token)
    localStorage.removeItem(refreshTokenStorageKey)
  }
}

function clearStoredRefreshToken() {
  localStorage.removeItem(refreshTokenStorageKey)
  sessionStorage.removeItem(refreshTokenStorageKey)
}

function applySession(session: AuthSession, remember: boolean) {
  accessToken = session.accessToken
  accessTokenExpiresAt = new Date(session.accessTokenExpiresAt).getTime()
  setStoredRefreshToken(session.refreshToken, remember)
  localStorage.setItem(tenantStorageKey, session.workspaceId)
  currentSession = session
  notify()
}

export function getAccessToken(): string | null {
  return accessToken
}

export function isAuthenticated(): boolean {
  return accessToken !== null
}

export function getCurrentSession(): AuthSession | null {
  return currentSession
}

export async function login(email: string, password: string, rememberMe = false): Promise<AuthSession> {
  const response = await fetch(`${apiUrl}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password, rememberMe }),
  })
  const session = await parseResponse<AuthSession>(response)
  applySession(session, rememberMe)
  return session
}

/** Full-page navigation target for "Sign in with Google" - the backend brokers the OAuth code
 * exchange server-side, so this is a real redirect, not a fetch. */
export function googleOAuthStartUrl(mode: 'login' | 'register'): string {
  return `${apiUrl}/auth/google/start?mode=${mode}`
}

export async function exchangeGoogleHandoff(code: string): Promise<AuthSession> {
  const response = await fetch(`${apiUrl}/auth/google/exchange`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ code }),
  })
  const session = await parseResponse<AuthSession>(response)
  applySession(session, false)
  return session
}

export async function register(input: RegisterInput): Promise<AuthSession> {
  const response = await fetch(`${apiUrl}/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
  const session = await parseResponse<AuthSession>(response)
  applySession(session, false)
  return session
}

async function refresh(workspaceId?: string): Promise<AuthSession | null> {
  const stored = getStoredRefreshToken()
  if (!stored) return null

  const response = await fetch(`${apiUrl}/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken: stored.token, workspaceId }),
  })
  if (!response.ok) {
    // Only a 401 means the refresh token itself is invalid/expired/revoked - clear the session.
    // A 403 (e.g. switching to a workspace the user is no longer an active member of) leaves the
    // existing session valid, so it must not be torn down here.
    if (response.status === 401) {
      clearStoredRefreshToken()
      accessToken = null
      currentSession = null
      notify()
    }
    return null
  }

  const session = await parseResponse<AuthSession>(response)
  applySession(session, stored.remember)
  return session
}

function refreshOnce(): Promise<AuthSession | null> {
  refreshPromise ??= refresh().finally(() => {
    refreshPromise = null
  })
  return refreshPromise
}

export async function logout(): Promise<void> {
  const refreshToken = getStoredRefreshToken()?.token ?? null
  clearStoredRefreshToken()
  accessToken = null
  currentSession = null
  notify()
  if (!refreshToken) return

  await fetch(`${apiUrl}/auth/logout`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  }).catch(() => undefined)
}

export async function switchWorkspace(workspaceId: string): Promise<AuthSession> {
  if (refreshPromise) await refreshPromise
  refreshPromise = refresh(workspaceId).finally(() => {
    refreshPromise = null
  })
  const session = await refreshPromise
  if (!session) throw new Error('The workspace session could not be activated.')
  return session
}

export async function requestPasswordReset(email: string): Promise<void> {
  const response = await fetch(`${apiUrl}/auth/password-reset/request`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email }),
  })
  if (!response.ok) {
    throw new Error('Unable to request a password reset. Please try again.')
  }
}

export async function confirmPasswordReset(token: string, newPassword: string): Promise<void> {
  const response = await fetch(`${apiUrl}/auth/password-reset/confirm`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token, newPassword }),
  })
  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as ProblemDetails
    throw new Error(problem.detail ?? problem.title ?? `Request failed (${response.status})`)
  }
}

function jwtExpiryMillis(token: string): number {
  try {
    const payload = token.split('.')[1]
    const normalized = payload.replace(/-/g, '+').replace(/_/g, '/')
    const padded = normalized.padEnd(normalized.length + ((4 - (normalized.length % 4)) % 4), '=')
    const claims = JSON.parse(atob(padded)) as { exp?: number }
    return typeof claims.exp === 'number' ? claims.exp * 1000 : Date.now() + 5 * 60_000
  } catch {
    return Date.now() + 5 * 60_000
  }
}

/**
 * Adopts a bearer token obtained directly from an external OIDC provider (see oidcPkce.ts) as the
 * active session. There is no Orbit-issued refresh token for this path - once the token expires
 * the user re-authenticates via SSO rather than silently refreshing, unlike a local-login session.
 */
export function setExternalAccessToken(token: string): void {
  accessToken = token
  accessTokenExpiresAt = jwtExpiryMillis(token)
  currentSession = null
  notify()
}

async function ensureAccessToken(): Promise<string | null> {
  const needsRefresh = !accessToken || Date.now() >= accessTokenExpiresAt - 5_000
  if (needsRefresh) {
    await refreshOnce()
  }

  return accessToken
}

/** Attaches a bearer Authorization header when a session is active, refreshing it first if stale. */
export async function withAuthHeader(headers: Headers): Promise<Headers> {
  const token = await ensureAccessToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  return headers
}
