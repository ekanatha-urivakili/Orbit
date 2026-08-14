import { useState, type FormEvent } from 'react'
import { orbitApi } from '../../api/client'
import { getOidcConfig, startOidcLogin } from './oidcPkce'

export function AcceptInvitationView({ token, tenantId }: { token: string; tenantId: string }) {
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
    <div className="flex min-h-screen items-center justify-center bg-gray-50 px-4">
      <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white p-8 shadow-sm">
        <h1 className="mb-2 text-xl font-semibold text-gray-900">Join this workspace</h1>
        {done ? (
          <p className="text-sm text-gray-700">Invitation accepted. Sign in to open your new workspace.</p>
        ) : (
          <form onSubmit={handleSubmit} className="mt-6 space-y-4">
            <p className="text-sm text-gray-600">
              Enter your existing Orbit password, or choose a password if this is your first workspace.
            </p>
            {error && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}
            <label className="block text-sm font-medium text-gray-700">
              Display name
              <input
                required
                minLength={2}
                maxLength={120}
                autoComplete="name"
                value={displayName}
                onChange={(event) => setDisplayName(event.target.value)}
                className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2"
              />
            </label>
            <label className="block text-sm font-medium text-gray-700">
              Password
              <input
                required
                minLength={12}
                maxLength={128}
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                className="mt-1 w-full rounded-md border border-gray-300 px-3 py-2"
              />
            </label>
            <button
              type="submit"
              disabled={submitting}
              className="w-full rounded-md bg-blue-600 px-3 py-2 text-sm font-medium text-white disabled:opacity-50"
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
        )}
      </div>
    </div>
  )
}
