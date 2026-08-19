import { useState, type FormEvent } from 'react'
import * as auth from '../../api/auth'
import { AuthLogo } from './AuthLogo'
import backlogBackground from '../../assets/backlog-blurred-bg.webp'

export function ResetPasswordView({
  token,
  logoUrl,
}: {
  token: string
  logoUrl?: string | null
}) {
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [done, setDone] = useState(false)

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setError(null)
    if (newPassword !== confirmPassword) {
      setError('Passwords do not match.')
      return
    }

    setSubmitting(true)
    try {
      await auth.confirmPasswordReset(token, newPassword)
      setDone(true)
    } catch (resetError) {
      setError(resetError instanceof Error ? resetError.message : 'Password reset failed.')
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
        <h1 className="mb-6 text-center text-xl font-semibold text-gray-900">Reset your password</h1>
        {done ? (
          <div className="space-y-4 pt-1">
            <p className="text-sm text-gray-700">
              Your password has been updated. You can now sign in with your new password.
            </p>
            <a
              href="/"
              className="block w-full rounded-md bg-blue-600 px-3 py-2 text-center text-sm font-medium text-white hover:bg-blue-700"
            >
              Sign in
            </a>
          </div>
        ) : (
          <div className="pt-1">
            {error && (
              <p className="mb-5 rounded-md border border-red-200 bg-red-50 px-3.5 py-2.5 text-sm text-red-700" role="alert">
                {error}
              </p>
            )}
            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label htmlFor="reset-new-password" className="mb-1.5 block text-sm font-medium text-gray-700">
                  New password <span className="text-red-600">*</span>
                </label>
                <input
                  id="reset-new-password"
                  type="password"
                  required
                  minLength={12}
                  maxLength={128}
                  autoComplete="new-password"
                  value={newPassword}
                  onChange={(event) => setNewPassword(event.target.value)}
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
                />
              </div>
              <div>
                <label htmlFor="reset-confirm-password" className="mb-1.5 block text-sm font-medium text-gray-700">
                  Confirm new password <span className="text-red-600">*</span>
                </label>
                <input
                  id="reset-confirm-password"
                  type="password"
                  required
                  minLength={12}
                  maxLength={128}
                  autoComplete="new-password"
                  value={confirmPassword}
                  onChange={(event) => setConfirmPassword(event.target.value)}
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
                />
              </div>
              <button
                type="submit"
                disabled={submitting}
                className="w-full rounded-md bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
              >
                {submitting ? 'Updating…' : 'Update password'}
              </button>
            </form>
          </div>
        )}
      </div>
    </div>
  )
}
