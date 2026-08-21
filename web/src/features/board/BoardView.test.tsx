import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent, within } from '@testing-library/react'
import { BoardView } from './BoardView'
import type { Board, Sprint, TenantMembership, WorkItem, WorkItemStatusDefinition } from '../../api/types'

function selectGroupBy(label: string) {
  fireEvent.click(screen.getByLabelText('Group board by'))
  fireEvent.click(screen.getByRole('option', { name: label }))
}

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

function makeMember(overrides: Partial<TenantMembership> = {}): TenantMembership {
  return {
    id: 'member-1',
    userId: 'user-1',
    issuer: null,
    subject: null,
    principalType: 'User',
    role: 'Member',
    tier: 'Standard',
    isActive: true,
    createdAt: '2026-08-11T00:00:00Z',
    displayName: 'Ada Lovelace',
    avatarUrl: null,
    ...overrides,
  }
}

const board: Board = {
  projectId: 'project-1',
  name: 'Team Board',
  type: 'Kanban',
  version: 1,
  columns: [
    { statusId: 'Backlog', order: 0, wipLimit: null, wipLimitMode: 'Warn' },
    { statusId: 'InProgress', order: 1, wipLimit: null, wipLimitMode: 'Warn' },
    { statusId: 'Done', order: 2, wipLimit: null, wipLimitMode: 'Warn' },
  ],
}

const statuses: WorkItemStatusDefinition[] = [
  { id: 'Backlog', key: 'backlog', name: 'Backlog', category: 'ToDo', order: 0, colorToken: 'slate', isSystem: true, isDefault: false, version: 1 },
  { id: 'InProgress', key: 'in-progress', name: 'In progress', category: 'InProgress', order: 1, colorToken: 'blue', isSystem: true, isDefault: false, version: 1 },
  { id: 'Done', key: 'done', name: 'Done', category: 'Done', order: 2, colorToken: 'green', isSystem: true, isDefault: false, version: 1 },
]

const activeSprint: Sprint = {
  id: 'sprint-1',
  projectId: 'project-1',
  name: 'SCRUM Sprint 1',
  state: 'Active',
  goal: 'Ship MVP',
  startDate: '2026-08-19T00:00:00Z',
  endDate: '2026-09-02T00:00:00Z',
  startedAt: '2026-08-19T00:00:00Z',
  closedAt: null,
  reopenedAt: null,
  version: 1,
  workItemIds: ['item-1'],
}

describe('BoardView swimlanes', () => {
  it('renders a single board with no swimlanes by default', () => {
    render(
      <BoardView
        projectName="Orbit"
        board={board}
        statuses={statuses}
        loading={false}
        mutation={{ isPending: false, isError: false, error: null }}
        onSave={vi.fn()}
        workItems={[makeItem({ id: 'item-1', summary: 'Item 1' })]}
        workItemsLoading={false}
        onStatusChange={vi.fn()}
        onReorder={vi.fn()}
      />,
    )

    expect(screen.queryByText('Unassigned')).not.toBeInTheDocument()
    expect(screen.getByText('Item 1')).toBeInTheDocument()
  })

  it('groups work items into per-assignee swimlanes, including an Unassigned lane', () => {
    const ada = makeMember({ id: 'member-1', userId: 'user-1', displayName: 'Ada Lovelace' })
    render(
      <BoardView
        projectName="Orbit"
        board={board}
        statuses={statuses}
        loading={false}
        mutation={{ isPending: false, isError: false, error: null }}
        onSave={vi.fn()}
        workItems={[
          makeItem({ id: 'item-1', summary: 'Assigned item', assigneeUserId: 'user-1' }),
          makeItem({ id: 'item-2', summary: 'Unassigned item', assigneeUserId: null }),
        ]}
        workItemsLoading={false}
        members={[ada]}
        onStatusChange={vi.fn()}
        onReorder={vi.fn()}
      />,
    )

    selectGroupBy('Group by assignee')

    expect(screen.getByText('Ada Lovelace')).toBeInTheDocument()
    expect(screen.getByText('Unassigned')).toBeInTheDocument()

    const adaLane = screen.getByText('Ada Lovelace').closest<HTMLElement>('.swimlane')!
    expect(within(adaLane).getByText('Assigned item')).toBeInTheDocument()
    expect(within(adaLane).queryByText('Unassigned item')).not.toBeInTheDocument()

    const unassignedLane = screen.getByText('Unassigned').closest<HTMLElement>('.swimlane')!
    expect(within(unassignedLane).getByText('Unassigned item')).toBeInTheDocument()
  })

  it('collapses a swimlane body when its header is clicked', () => {
    const ada = makeMember({ id: 'member-1', userId: 'user-1', displayName: 'Ada Lovelace' })
    render(
      <BoardView
        projectName="Orbit"
        board={board}
        statuses={statuses}
        loading={false}
        mutation={{ isPending: false, isError: false, error: null }}
        onSave={vi.fn()}
        workItems={[makeItem({ id: 'item-1', summary: 'Assigned item', assigneeUserId: 'user-1' })]}
        workItemsLoading={false}
        members={[ada]}
        onStatusChange={vi.fn()}
        onReorder={vi.fn()}
      />,
    )

    selectGroupBy('Group by assignee')
    expect(screen.getByText('Assigned item')).toBeInTheDocument()

    fireEvent.click(screen.getByText('Ada Lovelace'))
    expect(screen.queryByText('Assigned item')).not.toBeInTheDocument()
  })
})

describe('BoardView Sprint controls', () => {
  it('renders Complete sprint button and triggers CompleteSprintDialog when clicked', () => {
    const onCompleteSprint = vi.fn()
    render(
      <BoardView
        projectName="Orbit"
        board={board}
        statuses={statuses}
        loading={false}
        mutation={{ isPending: false, isError: false, error: null }}
        onSave={vi.fn()}
        workItems={[makeItem({ id: 'item-1', summary: 'Sprint Item' })]}
        workItemsLoading={false}
        activeSprint={activeSprint}
        onCompleteSprint={onCompleteSprint}
        onStatusChange={vi.fn()}
        onReorder={vi.fn()}
      />,
    )

    const completeBtn = screen.getByRole('button', { name: 'Complete sprint' })
    expect(completeBtn).toBeInTheDocument()

    fireEvent.click(completeBtn)
    expect(screen.getByText('Complete SCRUM Sprint 1')).toBeInTheDocument()
    expect(screen.getByText(/1 open work item/)).toBeInTheDocument()
  })

  it('renders sprint timer and opens countdown popover on click', () => {
    render(
      <BoardView
        projectName="Orbit"
        board={board}
        statuses={statuses}
        loading={false}
        mutation={{ isPending: false, isError: false, error: null }}
        onSave={vi.fn()}
        workItems={[makeItem({ id: 'item-1', summary: 'Sprint Item' })]}
        workItemsLoading={false}
        activeSprint={activeSprint}
        onCompleteSprint={vi.fn()}
        onStatusChange={vi.fn()}
        onReorder={vi.fn()}
      />,
    )

    const timerBtn = screen.getByLabelText('Sprint countdown')
    expect(timerBtn).toBeInTheDocument()

    fireEvent.click(timerBtn)
    expect(screen.getByText('Start date')).toBeInTheDocument()
    expect(screen.getByText('End date')).toBeInTheDocument()
  })
})
