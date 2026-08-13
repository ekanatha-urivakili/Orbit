import type { AuthSession } from './types'

const apiUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5014/api/v1'
const refreshTokenStorageKey = 'orbit.refresh-token'

let accessToken: string | null = null
let accessTokenExpiresAt = 0
let refreshPromise: Promise<AuthSession | null> | null = null

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

function applySession(session: AuthSession) {
  accessToken = session.accessToken
  accessTokenExpiresAt = new Date(session.accessTokenExpiresAt).getTime()
  sessionStorage.setItem(refreshTokenStorageKey, session.refreshToken)
  notify()
}

export function getAccessToken(): string | null {
  return accessToken
}

export function isAuthenticated(): boolean {
  return accessToken !== null
}

export async function login(email: string, password: string): Promise<AuthSession> {
  const response = await fetch(`${apiUrl}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  const session = await parseResponse<AuthSession>(response)
  applySession(session)
  return session
}

async function refresh(): Promise<AuthSession | null> {
  const refreshToken = sessionStorage.getItem(refreshTokenStorageKey)
  if (!refreshToken) return null

  const response = await fetch(`${apiUrl}/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  })
  if (!response.ok) {
    sessionStorage.removeItem(refreshTokenStorageKey)
    accessToken = null
    notify()
    return null
  }

  const session = await parseResponse<AuthSession>(response)
  applySession(session)
  return session
}

function refreshOnce(): Promise<AuthSession | null> {
  refreshPromise ??= refresh().finally(() => {
    refreshPromise = null
  })
  return refreshPromise
}

export async function logout(): Promise<void> {
  const refreshToken = sessionStorage.getItem(refreshTokenStorageKey)
  sessionStorage.removeItem(refreshTokenStorageKey)
  accessToken = null
  notify()
  if (!refreshToken) return

  await fetch(`${apiUrl}/auth/logout`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  }).catch(() => undefined)
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
