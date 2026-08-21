import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { KanbanBoard } from './KanbanBoard'
import type { BoardColumn, WorkItem, WorkItemStatusDefinition } from '../../api/types'

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

const columns: BoardColumn[] = [
  { statusId: 'Backlog', order: 0, wipLimit: null, wipLimitMode: 'Warn' },
  { statusId: 'InProgress', order: 1, wipLimit: null, wipLimitMode: 'Warn' },
  { statusId: 'Done', order: 2, wipLimit: 1, wipLimitMode: 'Block' },
]

const statuses: WorkItemStatusDefinition[] = [
  { id: 'Backlog', key: 'backlog', name: 'Backlog', category: 'ToDo', order: 0, colorToken: 'slate', isSystem: true, isDefault: false, version: 1 },
  { id: 'InProgress', key: 'in-progress', name: 'In progress', category: 'InProgress', order: 1, colorToken: 'blue', isSystem: true, isDefault: false, version: 1 },
  { id: 'Done', key: 'done', name: 'Done', category: 'Done', order: 2, colorToken: 'green', isSystem: true, isDefault: false, version: 1 },
]

describe('KanbanBoard Drag and Drop', () => {
  it('calls onStatusChange when dragging an item to a different status column', () => {
    const onStatusChange = vi.fn()
    const onReorder = vi.fn()
    const item1 = makeItem({ id: 'item-1', statusId: 'Backlog', summary: 'Item 1' })

    render(
      <KanbanBoard
        columns={columns}
        statuses={statuses}
        workItems={[item1]}
        loading={false}
        onStatusChange={onStatusChange}
        onReorder={onReorder}
      />,
    )

    const card = screen.getByText('Item 1').closest('article')!
    fireEvent.dragStart(card)

    const inProgressHeader = screen.getByRole('heading', { name: /in progress/i })
    const inProgressColumn = inProgressHeader.closest('section')!
    const cardList = inProgressColumn.querySelector('.card-list')!

    fireEvent.dragOver(cardList)
    fireEvent.drop(cardList)

    expect(onStatusChange).toHaveBeenCalledWith(item1, 'InProgress')
    expect(onReorder).not.toHaveBeenCalled()
  })

  it('calls onReorder when dropping within the same status column', () => {
    const onStatusChange = vi.fn()
    const onReorder = vi.fn()
    const item1 = makeItem({ id: 'item-1', statusId: 'Backlog', rank: 1000, summary: 'Item 1' })
    const item2 = makeItem({ id: 'item-2', statusId: 'Backlog', rank: 2000, summary: 'Item 2' })

    render(
      <KanbanBoard
        columns={columns}
        statuses={statuses}
        workItems={[item1, item2]}
        loading={false}
        onStatusChange={onStatusChange}
        onReorder={onReorder}
      />,
    )

    const card2 = screen.getByText('Item 2').closest('article')!
    fireEvent.dragStart(card2)

    const card1 = screen.getByText('Item 1').closest('article')!
    fireEvent.dragOver(card1)
    fireEvent.drop(card1)

    expect(onStatusChange).not.toHaveBeenCalled()
    expect(onReorder).toHaveBeenCalledWith(item2, { beforeId: null, afterId: 'item-1' })
  })

  it('blocks drop when destination column reaches WIP limit and mode is Block', () => {
    const onStatusChange = vi.fn()
    const onReorder = vi.fn()
    const item1 = makeItem({ id: 'item-1', statusId: 'Backlog', summary: 'Item 1' })
    const itemDone = makeItem({ id: 'item-done', statusId: 'Done', summary: 'Done Item' })

    render(
      <KanbanBoard
        columns={columns}
        statuses={statuses}
        workItems={[item1, itemDone]}
        loading={false}
        onStatusChange={onStatusChange}
        onReorder={onReorder}
      />,
    )

    const card1 = screen.getByText('Item 1').closest('article')!
    fireEvent.dragStart(card1)

    const doneHeader = screen.getByRole('heading', { name: 'Done' })
    const doneColumn = doneHeader.closest('section')!
    const cardList = doneColumn.querySelector('.card-list')!

    fireEvent.dragOver(cardList)
    fireEvent.drop(cardList)

    expect(onStatusChange).not.toHaveBeenCalled()
  })
})
