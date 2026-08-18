import { describe, expect, it, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { AuthLogo } from './AuthLogo'
import { STORAGE_KEY_CUSTOM_LOGO, getStoredLogoUrl, setStoredLogoUrl } from '../../lib/branding'

describe('AuthLogo', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('renders default Orbit logo when no custom logo is supplied', () => {
    render(<AuthLogo />)

    const defaultLogo = screen.getByTestId('auth-default-logo')
    expect(defaultLogo).toBeInTheDocument()
    expect(screen.getByText('Orbit')).toBeInTheDocument()
    expect(screen.queryByTestId('auth-custom-logo')).not.toBeInTheDocument()
  })

  it('renders uploaded custom logo with resizing to fit when logoUrl is provided', () => {
    const customUrl = 'https://example.com/uploaded-logo.png'
    render(<AuthLogo logoUrl={customUrl} />)

    const customLogo = screen.getByTestId('auth-custom-logo') as HTMLImageElement
    expect(customLogo).toBeInTheDocument()
    expect(customLogo.src).toBe(customUrl)
    expect(customLogo.className).toContain('max-h-12')
    expect(customLogo.className).toContain('max-w-[200px]')
    expect(customLogo.className).toContain('object-contain')
    expect(screen.queryByTestId('auth-default-logo')).not.toBeInTheDocument()
  })

  it('falls back to localStorage stored logo URL when logoUrl prop is undefined', () => {
    setStoredLogoUrl('https://example.com/cached-logo.svg')
    expect(getStoredLogoUrl()).toBe('https://example.com/cached-logo.svg')

    render(<AuthLogo />)

    const customLogo = screen.getByTestId('auth-custom-logo') as HTMLImageElement
    expect(customLogo).toBeInTheDocument()
    expect(customLogo.src).toBe('https://example.com/cached-logo.svg')
  })

  it('clears stored logo URL when set to null or empty', () => {
    setStoredLogoUrl('https://example.com/temp-logo.png')
    expect(localStorage.getItem(STORAGE_KEY_CUSTOM_LOGO)).toBe('https://example.com/temp-logo.png')

    setStoredLogoUrl(null)
    expect(localStorage.getItem(STORAGE_KEY_CUSTOM_LOGO)).toBeNull()
    expect(getStoredLogoUrl()).toBeNull()
  })
})
