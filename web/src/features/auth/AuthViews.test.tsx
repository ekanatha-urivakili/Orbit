import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { LoginView, LoginForm, ForgotPasswordForm } from './LoginView'
import { RegisterView } from './RegisterView'
import { ResetPasswordView } from './ResetPasswordView'
import * as auth from '../../api/auth'

vi.mock('../../api/auth', async () => {
  const actual = await vi.importActual<typeof import('../../api/auth')>('../../api/auth')
  return {
    ...actual,
    login: vi.fn(),
    register: vi.fn(),
    requestPasswordReset: vi.fn(),
    confirmPasswordReset: vi.fn(),
    googleOAuthStartUrl: vi.fn((mode: string) => `/mock-google-start?mode=${mode}`),
  }
})

describe('LoginView and LoginForm', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
  })

  it('renders Orbit logo at top center, centered title, and email label with proper padding space', () => {
    render(<LoginView onRegister={vi.fn()} />)

    // Top center logo
    const defaultLogo = screen.getByTestId('auth-default-logo')
    expect(defaultLogo).toBeInTheDocument()

    // Title centered
    const heading = screen.getByRole('heading', { level: 1, name: /Sign in to Orbit/i })
    expect(heading).toBeInTheDocument()
    expect(heading.className).toContain('text-center')
    expect(heading.className).toContain('mb-6')

    // Email label exists and has margin below it
    const emailLabel = screen.getByLabelText(/^Email/i)
    expect(emailLabel).toBeInTheDocument()
    const labelEl = screen.getByText(/^Email/i, { selector: 'label' })
    expect(labelEl.className).toContain('mb-1.5')

    // Inputs are present
    expect(screen.getByLabelText(/^Password/i)).toBeInTheDocument()
  })

  it('renders custom logo when logoUrl is provided', () => {
    render(<LoginView logoUrl="https://example.com/custom.png" />)

    const customLogo = screen.getByTestId('auth-custom-logo') as HTMLImageElement
    expect(customLogo).toBeInTheDocument()
    expect(customLogo.src).toBe('https://example.com/custom.png')
  })

  it('displays error message with proper margins and alert role when login fails', async () => {
    vi.mocked(auth.login).mockRejectedValueOnce(new Error('Invalid email or password.'))

    render(<LoginForm />)

    fireEvent.change(screen.getByLabelText(/^Email/i), { target: { value: 'user@example.com' } })
    fireEvent.change(screen.getByLabelText(/^Password/i), { target: { value: 'wrong-pass' } })
    fireEvent.click(screen.getByRole('button', { name: /Sign in/i }))

    const errorAlert = await screen.findByRole('alert')
    expect(errorAlert).toBeInTheDocument()
    expect(errorAlert).toHaveTextContent('Invalid email or password.')
    expect(errorAlert.className).toContain('mb-5')
    expect(errorAlert.className).toContain('px-3.5')
    expect(errorAlert.className).toContain('py-2.5')
  })

  it('switches to forgot password form with updated title and proper email spacing', async () => {
    render(<LoginView />)

    fireEvent.click(screen.getByRole('button', { name: /Forgot password\?/i }))

    // Title changes to Reset your password
    expect(screen.getByRole('heading', { level: 1, name: /Reset your password/i })).toBeInTheDocument()

    // Forgot password email field has margin
    const labelEl = screen.getByText(/^Email/i, { selector: 'label' })
    expect(labelEl.className).toContain('mb-1.5')
    expect(screen.getByRole('button', { name: /Send reset link/i })).toBeInTheDocument()

    // Can go back to sign in
    fireEvent.click(screen.getByRole('button', { name: /Back to sign in/i }))
    expect(screen.getByRole('heading', { level: 1, name: /Sign in to Orbit/i })).toBeInTheDocument()
  })

  it('displays error message in ForgotPasswordForm with proper spacing when reset request fails', async () => {
    vi.mocked(auth.requestPasswordReset).mockRejectedValueOnce(new Error('Rate limit exceeded.'))

    render(<ForgotPasswordForm onBack={vi.fn()} />)

    fireEvent.change(screen.getByLabelText(/^Email/i), { target: { value: 'user@example.com' } })
    fireEvent.click(screen.getByRole('button', { name: /Send reset link/i }))

    const errorAlert = await screen.findByRole('alert')
    expect(errorAlert).toBeInTheDocument()
    expect(errorAlert).toHaveTextContent('Rate limit exceeded.')
    expect(errorAlert.className).toContain('mb-5')
  })
})

describe('RegisterView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
  })

  it('renders Orbit logo at top center, centered title, labels and fields with proper spacing', () => {
    render(<RegisterView onBack={vi.fn()} />)

    expect(screen.getByTestId('auth-default-logo')).toBeInTheDocument()
    const heading = screen.getByRole('heading', { level: 1, name: /Create your organization/i })
    expect(heading.className).toContain('text-center')
    expect(heading.className).toContain('mb-6')

    const emailLabel = screen.getByText(/^Email/i, { selector: 'label' })
    expect(emailLabel.className).toContain('mb-1.5')
  })

  it('displays error alert with proper spacing when registration fails', async () => {
    vi.mocked(auth.register).mockRejectedValueOnce(new Error('An organization with this name already exists.'))

    render(<RegisterView onBack={vi.fn()} />)

    fireEvent.change(screen.getByLabelText(/Your name/i), { target: { value: 'Alice' } })
    fireEvent.change(screen.getByLabelText(/^Email/i), { target: { value: 'alice@example.com' } })
    fireEvent.change(screen.getByLabelText(/^Password/i), { target: { value: 'Password12345!' } })
    fireEvent.change(screen.getByLabelText(/Organization name/i), { target: { value: 'Acme Corp' } })
    fireEvent.change(screen.getByLabelText(/Workspace name/i), { target: { value: 'Core' } })
    fireEvent.click(screen.getByRole('button', { name: /Create account/i }))

    const errorAlert = await screen.findByRole('alert')
    expect(errorAlert).toBeInTheDocument()
    expect(errorAlert).toHaveTextContent('An organization with this name already exists.')
    expect(errorAlert.className).toContain('mb-5')
  })
})

describe('ResetPasswordView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
  })

  it('renders Orbit logo at top center, centered title, and password fields with proper spacing', () => {
    render(<ResetPasswordView token="test-token" />)

    expect(screen.getByTestId('auth-default-logo')).toBeInTheDocument()
    const heading = screen.getByRole('heading', { level: 1, name: /Reset your password/i })
    expect(heading.className).toContain('text-center')
    expect(heading.className).toContain('mb-6')

    const newPassLabel = screen.getByText(/^New password/i, { selector: 'label' })
    expect(newPassLabel.className).toContain('mb-1.5')
  })

  it('shows password mismatch error with proper alert styling', async () => {
    render(<ResetPasswordView token="test-token" />)

    fireEvent.change(screen.getByLabelText(/^New password/i), { target: { value: 'Password12345!' } })
    fireEvent.change(screen.getByLabelText(/^Confirm new password/i), { target: { value: 'Mismatch12345!' } })
    fireEvent.click(screen.getByRole('button', { name: /Update password/i }))

    const errorAlert = await screen.findByRole('alert')
    expect(errorAlert).toBeInTheDocument()
    expect(errorAlert).toHaveTextContent('Passwords do not match.')
    expect(errorAlert.className).toContain('mb-5')
  })
})
