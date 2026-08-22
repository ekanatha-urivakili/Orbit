import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ErrorBoundary } from './ErrorBoundary'

function Thrower(): never {
  throw new Error('Boom')
}

describe('ErrorBoundary', () => {
  it('renders the ErrorScreen fallback when a child throws', () => {
    // Suppress the expected React error-boundary console noise for this test.
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})

    render(
      <ErrorBoundary>
        <Thrower />
      </ErrorBoundary>,
    )

    expect(screen.getByText('Orbit is unavailable')).toBeInTheDocument()
    expect(screen.getByText('Boom')).toBeInTheDocument()

    consoleError.mockRestore()
  })

  it('renders children normally when nothing throws', () => {
    render(
      <ErrorBoundary>
        <p>All good</p>
      </ErrorBoundary>,
    )

    expect(screen.getByText('All good')).toBeInTheDocument()
  })
})
