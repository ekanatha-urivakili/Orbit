import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { Header } from './Header'

describe('Header search', () => {
  it('opens the command palette when the search field is focused', () => {
    const handler = vi.fn()
    window.addEventListener('orbit:open-command-palette', handler)

    render(
      <Header
        online
        onHomeClick={() => {}}
        onOpenSettings={() => {}}
        onThemeChange={() => {}}
      />,
    )

    fireEvent.focus(screen.getByLabelText('Search Orbit'))

    expect(handler).toHaveBeenCalledTimes(1)
    window.removeEventListener('orbit:open-command-palette', handler)
  })

  it('renders the search field as read-only so typing happens in the command palette', () => {
    render(
      <Header
        online
        onHomeClick={() => {}}
        onOpenSettings={() => {}}
        onThemeChange={() => {}}
      />,
    )

    expect(screen.getByLabelText('Search Orbit')).toHaveAttribute('readonly')
  })
})
