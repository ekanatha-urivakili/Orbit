const verifierStorageKey = 'orbit.oidc.verifier'
const stateStorageKey = 'orbit.oidc.state'
const modeStorageKey = 'orbit.oidc.mode'
const invitationStorageKey = 'orbit.oidc.invitation'

export type OidcMode = 'login' | 'link' | 'accept-invitation'

export interface PendingInvitation {
  token: string
  tenantId: string
  displayName: string
}

export interface OidcConfig {
  authority: string
  clientId: string
  redirectUri: string
}

/**
 * Reads the frontend's own OIDC client config from build-time env vars (same pattern as
 * VITE_API_URL in api/client.ts), not from the backend - Orbit never brokers the PKCE token
 * exchange, so the SPA needs to know the provider directly. Returns null when SSO isn't
 * configured for this deployment.
 */
export function getOidcConfig(): OidcConfig | null {
  const authority = import.meta.env.VITE_OIDC_AUTHORITY as string | undefined
  const clientId = import.meta.env.VITE_OIDC_CLIENT_ID as string | undefined
  if (!authority || !clientId) return null
  return { authority, clientId, redirectUri: window.location.origin + '/' }
}

function base64UrlEncode(bytes: Uint8Array): string {
  let binary = ''
  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte)
  })
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function randomString(byteLength: number): string {
  const bytes = new Uint8Array(byteLength)
  crypto.getRandomValues(bytes)
  return base64UrlEncode(bytes)
}

async function codeChallengeFor(verifier: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier))
  return base64UrlEncode(new Uint8Array(digest))
}

/**
 * Redirects the browser to the IdP's authorize endpoint, starting an authorization-code + PKCE
 * flow. `pendingInvitation` is required for and only used by the `'accept-invitation'` mode: the
 * redirect round-trip lands back on the app root with no URL state of its own, so the invitation
 * being accepted has to survive the trip the same way the PKCE verifier/state already do.
 */
export async function startOidcLogin(mode: OidcMode, pendingInvitation?: PendingInvitation): Promise<void> {
  const config = getOidcConfig()
  if (!config) throw new Error('SSO is not configured for this deployment.')

  const verifier = randomString(32)
  const state = randomString(16)
  sessionStorage.setItem(verifierStorageKey, verifier)
  sessionStorage.setItem(stateStorageKey, state)
  sessionStorage.setItem(modeStorageKey, mode)
  if (mode === 'accept-invitation' && pendingInvitation) {
    sessionStorage.setItem(invitationStorageKey, JSON.stringify(pendingInvitation))
  }

  const challenge = await codeChallengeFor(verifier)
  const authorizeUrl = new URL('/authorize', config.authority)
  authorizeUrl.searchParams.set('response_type', 'code')
  authorizeUrl.searchParams.set('client_id', config.clientId)
  authorizeUrl.searchParams.set('redirect_uri', config.redirectUri)
  authorizeUrl.searchParams.set('scope', 'openid profile email')
  authorizeUrl.searchParams.set('state', state)
  authorizeUrl.searchParams.set('code_challenge', challenge)
  authorizeUrl.searchParams.set('code_challenge_method', 'S256')

  window.location.assign(authorizeUrl.toString())
}

export interface OidcCallbackResult {
  mode: OidcMode
  accessToken: string
  idToken: string | null
  pendingInvitation: PendingInvitation | null
}

/**
 * Reads `code`/`state` from the current URL, if present, and exchanges the code for tokens
 * directly against the IdP's token endpoint - Orbit's backend is never involved in this exchange,
 * only in validating the resulting access token as a bearer credential (Program.cs's Authority
 * path). Clears the query string and the pending PKCE state either way. Returns null when there
 * is no pending callback.
 */
export async function completeOidcCallback(): Promise<OidcCallbackResult | null> {
  const params = new URLSearchParams(window.location.search)
  const code = params.get('code')
  const state = params.get('state')
  if (!code || !state) return null

  const expectedState = sessionStorage.getItem(stateStorageKey)
  const verifier = sessionStorage.getItem(verifierStorageKey)
  const mode = (sessionStorage.getItem(modeStorageKey) as OidcMode | null) ?? 'login'
  const storedInvitation = sessionStorage.getItem(invitationStorageKey)
  sessionStorage.removeItem(stateStorageKey)
  sessionStorage.removeItem(verifierStorageKey)
  sessionStorage.removeItem(modeStorageKey)
  sessionStorage.removeItem(invitationStorageKey)
  window.history.replaceState({}, '', window.location.pathname)

  const config = getOidcConfig()
  if (!config || !verifier || state !== expectedState) {
    throw new Error('The sign-in request could not be verified. Please try again.')
  }

  const tokenUrl = new URL('/token', config.authority)
  const response = await fetch(tokenUrl.toString(), {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'authorization_code',
      code,
      redirect_uri: config.redirectUri,
      client_id: config.clientId,
      code_verifier: verifier,
    }),
  })
  if (!response.ok) {
    throw new Error('The identity provider rejected the sign-in request.')
  }

  const tokens = (await response.json()) as { access_token: string; id_token?: string }
  const pendingInvitation: PendingInvitation | null = storedInvitation
    ? (JSON.parse(storedInvitation) as PendingInvitation)
    : null
  return { mode, accessToken: tokens.access_token, idToken: tokens.id_token ?? null, pendingInvitation }
}
