import { useState, type FormEvent } from 'react'
import * as auth from '../../api/auth'
import { GoogleButton } from './LoginView'
import { AuthLogo } from './AuthLogo'
import backlogBackground from '../../assets/backlog-blurred-bg.webp'

export function RegisterView({
  onSuccess,
  onBack,
  logoUrl,
}: {
  onSuccess?: () => void
  onBack: () => void
  logoUrl?: string | null
}) {
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [organizationName, setOrganizationName] = useState('')
  const [workspaceName, setWorkspaceName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await auth.register({ displayName, email, password, organizationName, workspaceName })
      onSuccess?.()
    } catch (registerError) {
      setError(registerError instanceof Error ? registerError.message : 'Unable to create an account.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div
      className="flex min-h-screen items-center justify-center bg-gray-50 bg-cover bg-center px-4"
      style={{ backgroundImage: `url(${backlogBackground})` }}
    >
      <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white/95 p-8 shadow-lg backdrop-blur-sm">
        <AuthLogo logoUrl={logoUrl} />
        <h1 className="mb-6 text-center text-xl font-semibold text-gray-900">Create your organization</h1>
        {error && (
          <p className="mb-5 rounded-md border border-red-200 bg-red-50 px-3.5 py-2.5 text-sm text-red-700" role="alert">
            {error}
          </p>
        )}
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label htmlFor="register-name" className="mb-1.5 block text-sm font-medium text-gray-700">
              Your name <span className="text-red-600">*</span>
            </label>
            <input
              id="register-name"
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
            <label htmlFor="register-email" className="mb-1.5 block text-sm font-medium text-gray-700">
              Email <span className="text-red-600">*</span>
            </label>
            <input
              id="register-email"
              type="email"
              required
              maxLength={320}
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
            />
          </div>
          <div>
            <label htmlFor="register-password" className="mb-1.5 block text-sm font-medium text-gray-700">
              Password <span className="text-red-600">*</span>
            </label>
            <input
              id="register-password"
              type="password"
              required
              minLength={12}
              maxLength={128}
              autoComplete="new-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
            />
          </div>
          <div>
            <label htmlFor="register-org" className="mb-1.5 block text-sm font-medium text-gray-700">
              Organization name <span className="text-red-600">*</span>
            </label>
            <input
              id="register-org"
              required
              minLength={2}
              maxLength={120}
              value={organizationName}
              onChange={(event) => setOrganizationName(event.target.value)}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
            />
          </div>
          <div>
            <label htmlFor="register-workspace" className="mb-1.5 block text-sm font-medium text-gray-700">
              Workspace name <span className="text-red-600">*</span>
            </label>
            <input
              id="register-workspace"
              required
              minLength={2}
              maxLength={120}
              value={workspaceName}
              onChange={(event) => setWorkspaceName(event.target.value)}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
            />
          </div>
          <button
            type="submit"
            disabled={submitting}
            className="w-full rounded-md bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
          >
            {submitting ? 'Creating account…' : 'Create account'}
          </button>
          <button
            type="button"
            onClick={onBack}
            className="w-full text-center text-sm text-gray-600 hover:underline"
          >
            Back to sign in
          </button>
        </form>
        <div className="mt-4">
          <GoogleButton mode="register" />
        </div>
      </div>
    </div>
  )
}
