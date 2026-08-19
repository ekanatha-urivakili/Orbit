import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { WorkItemSubtasks } from './WorkItemSubtasks'
import type { Project, TenantMembership, WorkItem } from '../../api/types'

vi.mock('../../api/client', () => ({
  orbitApi: {
    updateWorkItem: vi.fn(),
    createWorkItem: vi.fn(),
  },
}))

function buildWorkItem(overrides: Partial<WorkItem> = {}): WorkItem {
  return {
    id: 'item-1',
    projectId: 'project-1',
    key: 'ORB-1',
    summary: 'Parent story',
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

describe('WorkItemSubtasks', () => {
  it('renders each subtask title as a new-tab link to /browse/<key>', () => {
    const parent = buildWorkItem()
    const subtask = buildWorkItem({ id: 'sub-1', key: 'ORB-2', summary: 'Do the thing', parentId: parent.id, type: 'Subtask' })
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })

    render(
      <QueryClientProvider client={queryClient}>
        <WorkItemSubtasks
          parent={parent}
          workItems={[parent, subtask]}
          project={project}
          members={members}
          onStatusChange={vi.fn()}
        />
      </QueryClientProvider>,
    )

    const link = screen.getByText('Do the thing').closest('a')
    expect(link).not.toBeNull()
    expect(link).toHaveAttribute('href', '/browse/ORB-2')
    expect(link).toHaveAttribute('target', '_blank')
    expect(link).toHaveAttribute('rel', 'noopener noreferrer')
  })
})
