import { useState, type FormEvent } from 'react'
import { orbitApi } from '../../api/client'
import { getOidcConfig, startOidcLogin } from './oidcPkce'
import { AuthLogo } from './AuthLogo'
import backlogBackground from '../../assets/backlog-blurred-bg.webp'

export function AcceptInvitationView({
  token,
  tenantId,
  logoUrl,
}: {
  token: string
  tenantId: string
  logoUrl?: string | null
}) {
  const [displayName, setDisplayName] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [done, setDone] = useState(false)
  const oidcConfigured = getOidcConfig() !== null

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await orbitApi.acceptInvitation(tenantId, { token, displayName, password })
      setDone(true)
    } catch (acceptError) {
      setError(acceptError instanceof Error ? acceptError.message : 'Unable to accept the invitation.')
    } finally {
      setSubmitting(false)
    }
  }

  const handleSsoAccept = () => {
    setError(null)
    if (displayName.trim().length < 2) {
      setError('Enter a display name before continuing with SSO.')
      return
    }
    startOidcLogin('accept-invitation', { token, tenantId, displayName }).catch((ssoError: Error) =>
      setError(ssoError.message),
    )
  }

  return (
    <div
      className="flex min-h-screen items-center justify-center bg-gray-50 bg-cover bg-center px-4"
      style={{ backgroundImage: `url(${backlogBackground})` }}
    >
      <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white/95 p-8 shadow-lg backdrop-blur-sm">
        <AuthLogo logoUrl={logoUrl} />
        <h1 className="mb-2 text-center text-xl font-semibold text-gray-900">Join this workspace</h1>
        {done ? (
          <div className="space-y-4 pt-4">
            <p className="text-center text-sm text-gray-700">Invitation accepted. Sign in to open your new workspace.</p>
            <a
              href="/"
              className="block w-full rounded-md bg-blue-600 px-3 py-2 text-center text-sm font-medium text-white hover:bg-blue-700"
            >
              Sign in
            </a>
          </div>
        ) : (
          <div className="pt-2">
            <p className="mb-4 text-center text-sm text-gray-600">
              Enter your existing Orbit password, or choose a password if this is your first workspace.
            </p>
            {error && (
              <p className="mb-5 rounded-md border border-red-200 bg-red-50 px-3.5 py-2.5 text-sm text-red-700" role="alert">
                {error}
              </p>
            )}
            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label htmlFor="invitation-display-name" className="mb-1.5 block text-sm font-medium text-gray-700">
                  Display name <span className="text-red-600">*</span>
                </label>
                <input
                  id="invitation-display-name"
                  required
                  minLength={2}
                  maxLength={120}
                  autoComplete="name"
                  value={displayName}
                  onChange={(event) => setDisplayName(event.target.value)}
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
                />
              </div>
              <div>
                <label htmlFor="invitation-password" className="mb-1.5 block text-sm font-medium text-gray-700">
                  Password <span className="text-red-600">*</span>
                </label>
                <input
                  id="invitation-password"
                  required
                  minLength={12}
                  maxLength={128}
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
                />
              </div>
              <button
                type="submit"
                disabled={submitting}
                className="w-full rounded-md bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
              >
                {submitting ? 'Accepting…' : 'Accept invitation'}
              </button>
              {oidcConfigured && (
                <>
                  <div className="flex items-center gap-3 text-xs text-gray-400">
                    <div className="h-px flex-1 bg-gray-200" />
                    or
                    <div className="h-px flex-1 bg-gray-200" />
                  </div>
                  <button
                    type="button"
                    onClick={handleSsoAccept}
                    className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
                  >
                    Continue with SSO
                  </button>
                </>
              )}
            </form>
          </div>
        )}
      </div>
    </div>
  )
}
