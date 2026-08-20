import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { FilterBar } from './FilterBar'
import type { WorkItemFilterFieldState } from '../../hooks/useWorkItemFilters'

function makeFields(overrides: Partial<Record<'status' | 'assignee', Partial<WorkItemFilterFieldState>>> = {}): WorkItemFilterFieldState[] {
  const statusToggle = vi.fn()
  const assigneeToggle = vi.fn()
  return [
    {
      key: 'status',
      label: 'Status',
      options: [
        { value: 'Backlog', label: 'To Do' },
        { value: 'Done', label: 'Done' },
      ],
      selected: [],
      toggle: statusToggle,
      clear: vi.fn(),
      ...overrides.status,
    },
    {
      key: 'assignee',
      label: 'Assignee',
      options: [{ value: 'user-1', label: 'Ada Lovelace' }],
      selected: [],
      toggle: assigneeToggle,
      clear: vi.fn(),
      ...overrides.assignee,
    },
  ]
}

describe('FilterBar', () => {
  it('opens the filter popover and lists options for the first field', () => {
    render(
      <FilterBar
        searchTerm=""
        onSearchChange={() => {}}
        searchPlaceholder="Search"
        fields={makeFields()}
        activeCount={0}
        onClearAll={() => {}}
      />,
    )

    fireEvent.click(screen.getByLabelText('Filter'))

    expect(screen.getByText('To Do')).toBeInTheDocument()
    expect(screen.getByText('Done')).toBeInTheDocument()
  })

  it('switches the right pane when a different field is selected', () => {
    render(
      <FilterBar
        searchTerm=""
        onSearchChange={() => {}}
        searchPlaceholder="Search"
        fields={makeFields()}
        activeCount={0}
        onClearAll={() => {}}
      />,
    )

    fireEvent.click(screen.getByLabelText('Filter'))
    fireEvent.click(screen.getByText('Assignee'))

    expect(screen.getByText('Ada Lovelace')).toBeInTheDocument()
    expect(screen.queryByText('To Do')).not.toBeInTheDocument()
  })

  it('calls the field toggle handler when an option checkbox is clicked', () => {
    const toggle = vi.fn()
    render(
      <FilterBar
        searchTerm=""
        onSearchChange={() => {}}
        searchPlaceholder="Search"
        fields={makeFields({ status: { toggle } })}
        activeCount={0}
        onClearAll={() => {}}
      />,
    )

    fireEvent.click(screen.getByLabelText('Filter'))
    fireEvent.click(screen.getByLabelText('To Do'))

    expect(toggle).toHaveBeenCalledWith('Backlog')
  })

  it('shows the active filter count badge and calls onClearAll', () => {
    const onClearAll = vi.fn()
    render(
      <FilterBar
        searchTerm="bug"
        onSearchChange={() => {}}
        searchPlaceholder="Search"
        fields={makeFields({ status: { selected: ['Done'] } })}
        activeCount={2}
        onClearAll={onClearAll}
      />,
    )

    expect(screen.getByText('2')).toBeInTheDocument()
    fireEvent.click(screen.getByText('Clear filters'))
    expect(onClearAll).toHaveBeenCalled()
  })

  it('filters the visible options by the in-panel search box', () => {
    render(
      <FilterBar
        searchTerm=""
        onSearchChange={() => {}}
        searchPlaceholder="Search"
        fields={makeFields()}
        activeCount={0}
        onClearAll={() => {}}
      />,
    )

    fireEvent.click(screen.getByLabelText('Filter'))
    fireEvent.change(screen.getByPlaceholderText('Search status'), { target: { value: 'done' } })

    expect(screen.getByText('Done')).toBeInTheDocument()
    expect(screen.queryByText('To Do')).not.toBeInTheDocument()
  })
})
