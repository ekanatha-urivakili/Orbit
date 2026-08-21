import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { TimelineView } from './TimelineView'
import type { WorkItem } from '../../api/types'

function makeItem(overrides: Partial<WorkItem> = {}): WorkItem {
  return {
    id: 'item-1',
    projectId: 'project-1',
    key: 'ORB-1',
    summary: 'Build feature',
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
    dueDate: null,
    teamId: null,
    storyPoints: null,
    labels: [],
    countries: [],
    attachmentNames: [],
    type: 'Story',
    statusId: 'Backlog',
    priority: 'High',
    rank: 1024,
    isFlagged: false,
    coverAttachmentId: null,
    isArchived: false,
    archivedAt: null,
    version: 1,
    createdAt: '2026-08-11T00:00:00Z',
    updatedAt: '2026-08-11T00:00:00Z',
    ...overrides,
  }
}

describe('TimelineView', () => {
  it('shows an empty state when nothing has dates', () => {
    render(<TimelineView workItems={[makeItem()]} sprints={[]} onOpenWorkItem={() => {}} />)

    expect(screen.getByText('Nothing to show on the timeline yet')).toBeInTheDocument()
  })

  it('renders an epic row with its bar and month headers', () => {
    const epic = makeItem({
      id: 'epic-1',
      key: 'ORB-1',
      type: 'Epic',
      epicName: 'Epic 1',
      summary: 'Ship the release',
      startDate: '2026-08-10',
      dueDate: '2026-08-25',
    })

    render(<TimelineView workItems={[epic]} sprints={[]} onOpenWorkItem={() => {}} />)

    expect(screen.getByText('ORB-1')).toBeInTheDocument()
    expect(screen.getByText('August')).toBeInTheDocument()
  })

  it('expands an epic to reveal child work items and opens them on click', () => {
    const onOpenWorkItem = vi.fn()
    const epic = makeItem({
      id: 'epic-1',
      key: 'ORB-1',
      type: 'Epic',
      epicName: 'Epic 1',
      summary: 'Ship the release',
      startDate: '2026-08-10',
      dueDate: '2026-08-25',
    })
    const child = makeItem({
      id: 'child-1',
      key: 'ORB-2',
      parentId: 'epic-1',
      summary: 'Do the work',
      startDate: '2026-08-12',
      dueDate: '2026-08-14',
    })

    render(<TimelineView workItems={[epic, child]} sprints={[]} onOpenWorkItem={onOpenWorkItem} />)

    fireEvent.click(screen.getByLabelText('Toggle ORB-1'))
    fireEvent.click(screen.getByTitle('ORB-2: Do the work'))

    expect(onOpenWorkItem).toHaveBeenCalledWith(child)
  })
})
