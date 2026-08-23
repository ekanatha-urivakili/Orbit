import { describe, expect, it } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { CustomFiltersPanel } from './CustomFiltersPanel'

describe('CustomFiltersPanel', () => {
  it('renders WQL preview banner and templates', () => {
    render(<CustomFiltersPanel />)

    expect(screen.getByText(/Work Query Language \(WQL\) Engine/i)).toBeInTheDocument()
    expect(screen.getByText('My Open Items')).toBeInTheDocument()
    expect(screen.getByText('High Priority Bugs')).toBeInTheDocument()
  })

  it('populates form fields when template chip is clicked', () => {
    render(<CustomFiltersPanel />)

    fireEvent.click(screen.getByText('My Open Items'))

    const nameInput = screen.getByPlaceholderText('e.g. My Open Bugs') as HTMLInputElement
    const queryInput = screen.getByPlaceholderText(/status = 'To Do'/i) as HTMLTextAreaElement

    expect(nameInput.value).toBe('My Open Items')
    expect(queryInput.value).toContain('assignee = currentUser()')
  })

  it('toggles WQL syntax reference guide when help button is clicked', () => {
    render(<CustomFiltersPanel />)

    const helpBtn = screen.getByRole('button', { name: /Toggle WQL Syntax Help/i })
    fireEvent.click(helpBtn)

    expect(screen.getByText('WQL Syntax Reference')).toBeInTheDocument()
    expect(screen.getByText('Fields & Attributes')).toBeInTheDocument()
  })
})
