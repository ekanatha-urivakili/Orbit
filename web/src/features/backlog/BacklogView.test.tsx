import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BacklogView } from './BacklogView'
import type { Project, Sprint, TenantMembership, WorkItem } from '../../api/types'

vi.mock('../../api/client', () => ({
  orbitApi: {
    listWorkItemStatuses: vi.fn().mockResolvedValue([
      { id: 'todo', name: 'To Do', category: 'ToDo', order: 1 },
      { id: 'in_progress', name: 'In Progress', category: 'InProgress', order: 2 },
      { id: 'done', name: 'Done', category: 'Done', order: 3 },
    ]),
    listWorkItemAttachments: vi.fn().mockResolvedValue([]),
    getWorkItemWatchers: vi.fn().mockResolvedValue({ isWatching: false, count: 0 }),
    watchWorkItem: vi.fn().mockResolvedValue(undefined),
    unwatchWorkItem: vi.fn().mockResolvedValue(undefined),
    listWorkItemTypes: vi.fn().mockResolvedValue([]),
    changeWorkItemType: vi.fn(),
    updateWorkItem: vi.fn(),
    listWorkItemLinks: vi.fn().mockResolvedValue([]),
    listWorkItemComments: vi.fn().mockResolvedValue([]),
    listTeams: vi.fn().mockResolvedValue([]),
  },
}))

function buildWorkItem(overrides: Partial<WorkItem> = {}): WorkItem {
  return {
    id: 'item-1',
    projectId: 'project-1',
    key: 'ORB-1',
    summary: 'Build backlog split pane',
    description: 'Detailed description for test',
    parentId: null,
    epicName: null,
    acceptanceCriteria: 'AC criteria content',
    stepsToConduct: null,
    assigneeUserId: null,
    developerUserId: null,
    productOwnerUserId: null,
    sprintName: null,
    identifiedOn: null,
    startDate: null,
    dueDate: null,
    teamId: null,
    storyPoints: 5,
    labels: ['frontend'],
    countries: [],
    attachmentNames: [],
    type: 'Story',
    statusId: 'todo',
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

const mockProject: Project = { id: 'project-1', key: 'ORB', name: 'Orbit Workspace' } as Project
const mockMembers: TenantMembership[] = []
const mockSprints: Sprint[] = []

function renderBacklogView(workItems: WorkItem[] = [buildWorkItem()]) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <BacklogView
        workItems={workItems}
        projectId="project-1"
        project={mockProject}
        members={mockMembers}
        sprints={mockSprints}
        sprintsLoading={false}
        onCreateSprint={vi.fn()}
        onStartSprint={vi.fn()}
        onCompleteSprint={vi.fn()}
        onReopenSprint={vi.fn()}
        onAssignToSprint={vi.fn()}
        onRemoveFromSprint={vi.fn()}
      />
    </QueryClientProvider>,
  )
}

describe('BacklogView in-page ticket split panel', () => {
  it('renders backlog items and clicking a ticket opens it in the in-page split panel', async () => {
    const item = buildWorkItem()
    renderBacklogView([item])

    // Ticket summary is in the backlog list
    expect(screen.getByText('Build backlog split pane')).toBeInTheDocument()

    // Click the ticket key link
    fireEvent.click(screen.getByText('ORB-1'))

    // The detail panel is displayed on the same page
    expect(await screen.findByText('Description')).toBeInTheDocument()
    expect(screen.getByText('Acceptance criteria')).toBeInTheDocument()
    expect(screen.getByText('Details')).toBeInTheDocument()
    expect(screen.getByText('Activity')).toBeInTheDocument()
  })

  it('clicking close button closes the in-page split panel', async () => {
    const item = buildWorkItem()
    renderBacklogView([item])

    // Open ticket
    fireEvent.click(screen.getByText('ORB-1'))
    expect(await screen.findByText('Description')).toBeInTheDocument()

    // Click close button
    fireEvent.click(screen.getByTitle('Close details'))

    // Detail panel is closed
    expect(screen.queryByText('Description')).not.toBeInTheDocument()
  })
})
