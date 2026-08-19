import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { WorkItemActionsMenu } from './WorkItemActionsMenu'
import { orbitApi } from '../../api/client'
import type { WorkItem } from '../../api/types'

vi.mock('../../api/client', () => ({
  orbitApi: {
    getWorkItemVotes: vi.fn().mockResolvedValue({ hasVoted: false, count: 0 }),
    toggleWorkItemFlag: vi.fn(),
    addWorkItemVote: vi.fn(),
    removeWorkItemVote: vi.fn(),
    cloneWorkItem: vi.fn(),
    archiveWorkItem: vi.fn(),
    unarchiveWorkItem: vi.fn(),
    deleteWorkItem: vi.fn().mockResolvedValue(undefined),
    exportWorkItem: vi.fn(),
    listWorkItemAttachments: vi.fn().mockResolvedValue([]),
    listProjects: vi.fn().mockResolvedValue({ items: [], totalCount: 0 }),
    startSlackConnect: vi.fn().mockResolvedValue({ url: 'https://slack.example/authorize?state=abc' }),
  },
}))

function buildWorkItem(overrides: Partial<WorkItem> = {}): WorkItem {
  return {
    id: 'item-1',
    projectId: 'project-1',
    key: 'ORB-1',
    summary: 'Do the thing',
    description: null,
    parentId: null,
    epicName: null,
    acceptanceCriteria: null,
    stepsToConduct: null,
    assigneeUserId: null,
    developerUserId: null,
    productOwnerUserId: null,
    sprintName: null,
    identifiedOn: null,
    storyPoints: null,
    labels: [],
    countries: [],
    attachmentNames: [],
    type: 'Task',
    status: 'Backlog',
    priority: 'Medium',
    rank: 1024,
    isFlagged: false,
    coverAttachmentId: null,
    isArchived: false,
    archivedAt: null,
    version: 1,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    ...overrides,
  }
}

function renderMenu(overrides: Partial<WorkItem> = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const item = buildWorkItem(overrides)
  const onOpenWorkItem = vi.fn()
  const onDeleted = vi.fn()
  render(
    <QueryClientProvider client={queryClient}>
      <WorkItemActionsMenu
        item={item}
        onOpenWorkItem={onOpenWorkItem}
        onFocusParentField={vi.fn()}
        onDeleted={onDeleted}
      />
    </QueryClientProvider>,
  )
  return { item, onOpenWorkItem, onDeleted }
}

describe('WorkItemActionsMenu', () => {
  it('toggles the flag via orbitApi when "Add flag" is clicked', async () => {
    const { item } = renderMenu()
    fireEvent.click(screen.getByLabelText('Actions'))

    fireEvent.click(await screen.findByText('Add flag'))

    await waitFor(() => expect(orbitApi.toggleWorkItemFlag).toHaveBeenCalledWith(item, true))
  })

  it('shows "Remove flag" when the item is already flagged', async () => {
    renderMenu({ isFlagged: true })
    fireEvent.click(screen.getByLabelText('Actions'))

    expect(await screen.findByText('Remove flag')).toBeInTheDocument()
  })

  it('deletes the work item after confirmation and calls onDeleted', async () => {
    const { item, onDeleted } = renderMenu()
    fireEvent.click(screen.getByLabelText('Actions'))
    fireEvent.click(await screen.findByText('Delete'))
    fireEvent.click(await screen.findByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(orbitApi.deleteWorkItem).toHaveBeenCalledWith(item))
    await waitFor(() => expect(onDeleted).toHaveBeenCalled())
  })

  it('clones the work item and opens the clone', async () => {
    const clone = buildWorkItem({ id: 'clone-1', key: 'ORB-2', summary: 'Copy of Do the thing' })
    vi.mocked(orbitApi.cloneWorkItem).mockResolvedValue(clone)
    const { onOpenWorkItem } = renderMenu()
    fireEvent.click(screen.getByLabelText('Actions'))

    fireEvent.click(await screen.findByText('Clone'))

    await waitFor(() => expect(onOpenWorkItem).toHaveBeenCalledWith(clone))
  })

  it('starts the Slack connect flow and redirects to the authorize URL', async () => {
    const { item } = renderMenu()
    const originalLocation = window.location
    Object.defineProperty(window, 'location', {
      writable: true,
      value: { ...originalLocation, href: '', pathname: '/browse/ORB-1' },
    })

    fireEvent.click(screen.getByLabelText('Actions'))
    fireEvent.click(await screen.findByText('Connect Slack channel'))

    await waitFor(() => expect(orbitApi.startSlackConnect).toHaveBeenCalledWith(item.projectId))
    await waitFor(() => expect(window.location.href).toBe('https://slack.example/authorize?state=abc'))
    expect(sessionStorage.getItem('slack-connect-return-path')).toBe('/browse/ORB-1')

    Object.defineProperty(window, 'location', { writable: true, value: originalLocation })
  })
})
