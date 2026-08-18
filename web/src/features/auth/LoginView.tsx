import { useState, type FormEvent } from 'react'
import { Eye, EyeOff } from 'lucide-react'
import * as auth from '../../api/auth'
import { getOidcConfig, startOidcLogin } from './oidcPkce'
import { AuthLogo } from './AuthLogo'
import backlogBackground from '../../assets/backlog-blurred-bg.webp'

function GoogleIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 48 48" aria-hidden="true">
      <path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3C33.7 32.7 29.3 36 24 36c-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.1 8 3l5.7-5.7C34.6 6.1 29.6 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.3-.1-2.7-.4-3.5z" />
      <path fill="#FF3D00" d="M6.3 14.7l6.6 4.8C14.7 15.9 19 13 24 13c3.1 0 5.8 1.1 8 3l5.7-5.7C34.6 6.1 29.6 4 24 4 16.3 4 9.7 8.3 6.3 14.7z" />
      <path fill="#4CAF50" d="M24 44c5.2 0 10.1-2 13.7-5.2l-6.3-5.3C29.3 35.4 26.8 36 24 36c-5.2 0-9.6-3.3-11.3-8l-6.5 5C9.5 39.6 16.2 44 24 44z" />
      <path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.3-2.3 4.3-4.2 5.7l6.3 5.3C40.9 36.5 44 31 44 24c0-1.3-.1-2.7-.4-3.5z" />
    </svg>
  )
}

export function GoogleButton({ mode }: { mode: 'login' | 'register' }) {
  return (
    <a
      href={auth.googleOAuthStartUrl(mode)}
      className="flex w-full items-center justify-center gap-2 rounded-md border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
    >
      <GoogleIcon />
      {mode === 'login' ? 'Sign in with Google' : 'Sign up with Google'}
    </a>
  )
}

export function ForgotPasswordForm({ onBack }: { onBack: () => void }) {
  const [email, setEmail] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [sent, setSent] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await auth.requestPasswordReset(email)
      setSent(true)
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to request a password reset.')
    } finally {
      setSubmitting(false)
    }
  }

  if (sent) {
    return (
      <div className="space-y-4 pt-1">
        <p className="text-sm text-gray-700">
          If that email is registered, we sent a link to reset your password.
        </p>
        <button
          onClick={onBack}
          className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
        >
          Back to sign in
        </button>
      </div>
    )
  }

  return (
    <div className="pt-1">
      {error && (
        <p className="mb-5 rounded-md border border-red-200 bg-red-50 px-3.5 py-2.5 text-sm text-red-700" role="alert">
          {error}
        </p>
      )}
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="forgot-password-email" className="mb-1.5 block text-sm font-medium text-gray-700">
            Email <span className="text-red-600">*</span>
          </label>
          <input
            id="forgot-password-email"
            type="email"
            required
            autoComplete="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
          />
        </div>
        <button
          type="submit"
          disabled={submitting}
          className="w-full rounded-md bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
        >
          {submitting ? 'Sending…' : 'Send reset link'}
        </button>
        <button
          type="button"
          onClick={onBack}
          className="w-full text-center text-sm text-gray-600 hover:underline"
        >
          Back to sign in
        </button>
      </form>
    </div>
  )
}

export function LoginForm({
  onSuccess,
  onForgotPasswordToggle,
}: {
  onSuccess?: () => void
  onForgotPasswordToggle?: (isForgot: boolean) => void
}) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [rememberMe, setRememberMe] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [forgotPassword, setForgotPassword] = useState(false)
  const [showPassword, setShowPassword] = useState(false)
  const oidcConfigured = getOidcConfig() !== null

  const handleToggleForgot = (show: boolean) => {
    setForgotPassword(show)
    onForgotPasswordToggle?.(show)
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await auth.login(email, password, rememberMe)
      onSuccess?.()
    } catch (loginError) {
      setError(loginError instanceof Error ? loginError.message : 'Sign in failed.')
    } finally {
      setSubmitting(false)
    }
  }

  if (forgotPassword) {
    return <ForgotPasswordForm onBack={() => handleToggleForgot(false)} />
  }

  return (
    <div className="pt-1">
      {error && (
        <p className="mb-5 rounded-md border border-red-200 bg-red-50 px-3.5 py-2.5 text-sm text-red-700" role="alert">
          {error}
        </p>
      )}
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="login-email" className="mb-1.5 block text-sm font-medium text-gray-700">
            Email <span className="text-red-600">*</span>
          </label>
          <input
            id="login-email"
            type="email"
            required
            autoComplete="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
          />
        </div>
        <div>
          <label htmlFor="login-password" className="mb-1.5 block text-sm font-medium text-gray-700">
            Password <span className="text-red-600">*</span>
          </label>
          <div className="relative">
            <input
              id="login-password"
              type={showPassword ? 'text' : 'password'}
              required
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="w-full rounded-md border border-gray-300 px-3 py-2 pr-10 text-sm focus:border-blue-500 focus:outline-none"
            />
            <button
              type="button"
              onClick={() => setShowPassword((current) => !current)}
              aria-label={showPassword ? 'Hide password' : 'Show password'}
              className="absolute inset-y-0 right-0 flex items-center px-3 text-gray-400 hover:text-gray-600"
            >
              {showPassword ? <EyeOff size={16} /> : <Eye size={16} />}
            </button>
          </div>
        </div>
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input
            type="checkbox"
            checked={rememberMe}
            onChange={(event) => setRememberMe(event.target.checked)}
            className="rounded border-gray-300"
          />
          Remember me
        </label>
        <button
          type="submit"
          disabled={submitting}
          className="w-full rounded-md bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
        >
          {submitting ? 'Signing in…' : 'Sign in'}
        </button>
        <button
          type="button"
          onClick={() => handleToggleForgot(true)}
          className="w-full text-center text-sm text-gray-600 hover:underline"
        >
          Forgot password?
        </button>
      </form>
      <div className="mt-4 space-y-2">
        <GoogleButton mode="login" />
        {oidcConfigured && (
          <button
            onClick={() => startOidcLogin('login')}
            className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
          >
            Continue with SSO
          </button>
        )}
      </div>
    </div>
  )
}

export function LoginView({
  onAuthenticated,
  onRegister,
  logoUrl,
}: {
  onAuthenticated?: () => void
  onRegister?: () => void
  logoUrl?: string | null
}) {
  const [isForgotPassword, setIsForgotPassword] = useState(false)

  return (
    <div
      className="flex min-h-screen items-center justify-center bg-gray-50 bg-cover bg-center px-4"
      style={{ backgroundImage: `url(${backlogBackground})` }}
    >
      <div className="w-full max-w-sm rounded-xl border border-gray-200 bg-white/95 p-8 shadow-lg backdrop-blur-sm">
        <AuthLogo logoUrl={logoUrl} />
        <h1 className="mb-6 text-center text-xl font-semibold text-gray-900">
          {isForgotPassword ? 'Reset your password' : 'Sign in to Orbit'}
        </h1>
        <LoginForm
          onSuccess={onAuthenticated}
          onForgotPasswordToggle={setIsForgotPassword}
        />
        {!isForgotPassword && onRegister && (
          <button
            type="button"
            onClick={onRegister}
            className="mt-4 w-full text-center text-sm text-gray-600 hover:underline"
          >
            Create an account
          </button>
        )}
      </div>
    </div>
  )
}
