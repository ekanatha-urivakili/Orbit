import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { WorkItemShareMenu } from './WorkItemShareMenu'
import { orbitApi } from '../../api/client'
import type { WorkItem } from '../../api/types'

vi.mock('../../api/client', () => ({
  orbitApi: {
    listMemberships: vi.fn().mockResolvedValue([
      { id: 'member-1', userId: 'user-1', issuer: null, subject: null, principalType: 'User', role: 'Member', tier: 'Standard', isActive: true, createdAt: '', displayName: 'Maria' },
    ]),
    listTeams: vi.fn().mockResolvedValue([]),
    shareWorkItem: vi.fn().mockResolvedValue(undefined),
    getSlackConnection: vi.fn(),
    postWorkItemToSlack: vi.fn().mockResolvedValue(undefined),
  },
}))

const item: WorkItem = {
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
  startDate: null,
  teamId: null,
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
}

function renderMenu() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <WorkItemShareMenu item={item} onClose={vi.fn()} />
    </QueryClientProvider>,
  )
}

describe('WorkItemShareMenu', () => {
  it('disables Share until a recipient is selected, then shares on click', async () => {
    renderMenu()

    const shareButton = screen.getByRole('button', { name: /Share$/ })
    expect(shareButton).toBeDisabled()

    fireEvent.change(screen.getByPlaceholderText('e.g. Maria, Team Orange'), { target: { value: 'Mar' } })
    fireEvent.click(await screen.findByText('Maria'))

    expect(shareButton).not.toBeDisabled()
    fireEvent.click(shareButton)

    await waitFor(() =>
      expect(orbitApi.shareWorkItem).toHaveBeenCalledWith('item-1', {
        membershipIds: ['member-1'],
        teamIds: [],
        message: null,
      }),
    )
  })

  it('shows a connect prompt when no Slack channel is connected', async () => {
    vi.mocked(orbitApi.getSlackConnection).mockResolvedValue(null)
    renderMenu()

    fireEvent.click(screen.getByText('Share in Slack'))

    expect(await screen.findByText(/No Slack channel is connected/)).toBeInTheDocument()
  })

  it('posts to Slack when a channel is connected', async () => {
    vi.mocked(orbitApi.getSlackConnection).mockResolvedValue({
      id: 'conn-1',
      projectId: 'project-1',
      teamName: 'Acme',
      channelName: 'general',
      createdAt: new Date().toISOString(),
    })
    renderMenu()

    fireEvent.click(screen.getByText('Share in Slack'))
    fireEvent.click(await screen.findByText('Post to Slack'))

    await waitFor(() => expect(orbitApi.postWorkItemToSlack).toHaveBeenCalledWith('item-1', null))
  })
})
