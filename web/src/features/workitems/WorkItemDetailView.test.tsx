import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { WorkItemDetailView } from './WorkItemDetailView'
import { orbitApi } from '../../api/client'
import type { Project, TenantMembership, WorkItem, WorkItemTypeDefinition } from '../../api/types'

vi.mock('../../api/client', () => ({
  orbitApi: {
    listWorkItemAttachments: vi.fn().mockResolvedValue([]),
    getWorkItemWatchers: vi.fn().mockResolvedValue({ isWatching: false, count: 0 }),
    watchWorkItem: vi.fn().mockResolvedValue(undefined),
    unwatchWorkItem: vi.fn().mockResolvedValue(undefined),
    listWorkItemTypes: vi.fn().mockResolvedValue([]),
    changeWorkItemType: vi.fn(),
    updateWorkItem: vi.fn(),
    listWorkItemLinks: vi.fn().mockResolvedValue([]),
    listWorkItemComments: vi.fn().mockResolvedValue([]),
  },
}))

function buildWorkItem(overrides: Partial<WorkItem> = {}): WorkItem {
  return {
    id: 'item-1',
    projectId: 'project-1',
    key: 'ORB-1',
    summary: 'Investigate latency',
    description: '',
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
    dueDate: null,
    teamId: null,
    storyPoints: null,
    labels: [],
    countries: [],
    attachmentNames: [],
    type: 'Story',
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

const project: Project = { id: 'project-1', key: 'ORB', name: 'Orbit' } as Project
const members: TenantMembership[] = []

function renderDetailView(props: Partial<Parameters<typeof WorkItemDetailView>[0]> = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const onOpenWorkItem = vi.fn()
  const epic = buildWorkItem({ id: 'epic-1', key: 'ORB-2', summary: 'Platform revamp', type: 'Epic' })
  const item = buildWorkItem({ parentId: epic.id })

  const utils = render(
    <QueryClientProvider client={queryClient}>
      <WorkItemDetailView
        item={item}
        project={project}
        workItems={[item, epic]}
        members={members}
        priorities={['Low', 'Medium', 'High']}
        onBack={vi.fn()}
        onStatusChange={vi.fn()}
        onOpenWorkItem={onOpenWorkItem}
        {...props}
      />
    </QueryClientProvider>,
  )

  return { ...utils, onOpenWorkItem, item, epic }
}

describe('WorkItemDetailView epic breadcrumb', () => {
  it('clicking the epic icon opens the Unlink parent / View all epics menu', async () => {
    renderDetailView()

    fireEvent.click(screen.getByTitle('Epic - Change epic'))

    expect(await screen.findByText('Unlink parent')).toBeInTheDocument()
    expect(screen.getByText('View all epics')).toBeInTheDocument()
  })

  it('clicking the epic title navigates to the epic in the same tab', async () => {
    const { onOpenWorkItem, epic } = renderDetailView()

    fireEvent.click(screen.getByTitle(`${epic.key}: ${epic.summary}`))

    expect(onOpenWorkItem).toHaveBeenCalledWith(epic)
    await waitFor(() => expect(orbitApi.listWorkItemComments).toHaveBeenCalled())
  })
})

describe('WorkItemDetailView copy link', () => {
  beforeEach(() => {
    Object.assign(navigator, { clipboard: { writeText: vi.fn().mockResolvedValue(undefined) } })
  })

  it('copies the ticket link and shows a Copied! confirmation', async () => {
    const { item } = renderDetailView()

    fireEvent.click(screen.getByLabelText('Copy link to this ticket'))

    await waitFor(() =>
      expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
        `${window.location.origin}/browse/${item.key}`,
      ),
    )
    expect(await screen.findByText('Copied!')).toBeInTheDocument()
  })
})

describe('WorkItemDetailView change work type', () => {
  it('persists the new type via orbitApi when a menu entry is selected', async () => {
    const typeDefinitions: WorkItemTypeDefinition[] = [
      { id: 'Story', label: 'Story', description: '', order: 1, colorToken: '', enabled: true, canAdminister: true, version: 1 },
      { id: 'Bug', label: 'Bug', description: '', order: 2, colorToken: '', enabled: true, canAdminister: true, version: 1 },
    ]
    vi.mocked(orbitApi.listWorkItemTypes).mockResolvedValue(typeDefinitions)
    const { item } = renderDetailView()
    vi.mocked(orbitApi.changeWorkItemType).mockResolvedValue({ ...item, type: 'Bug' })

    fireEvent.click(screen.getByTitle('Story - Click to change work type'))
    const bugOption = await screen.findByText('Bug')
    fireEvent.click(bugOption)

    await waitFor(() => expect(orbitApi.changeWorkItemType).toHaveBeenCalledWith(item, 'Bug'))
    await screen.findByTitle('Bug - Click to change work type')
  })
})
