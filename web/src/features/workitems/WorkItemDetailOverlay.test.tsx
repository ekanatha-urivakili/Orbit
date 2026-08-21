import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { WorkItemDetailOverlay } from './WorkItemDetailOverlay'
import type { WorkItem } from '../../api/types'

vi.mock('./WorkItemDetailView', () => ({
  WorkItemDetailView: () => <button type="button">Detail action</button>,
}))

const item: WorkItem = {
  id: 'item-1',
  projectId: 'project-1',
  key: 'ORB-1',
  summary: 'Secure assignments',
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
  type: 'Task',
  statusId: 'Backlog',
  priority: 'Medium',
  rank: 1024,
  isFlagged: false,
  coverAttachmentId: null,
  isArchived: false,
  archivedAt: null,
  version: 1,
  createdAt: '2026-08-20T00:00:00Z',
  updatedAt: '2026-08-20T00:00:00Z',
}

describe('WorkItemDetailOverlay', () => {
  it('focuses the close control and closes on Escape', () => {
    const onClose = vi.fn()
    render(
      <WorkItemDetailOverlay
        variant="modal"
        item={item}
        workItems={[item]}
        members={[]}
        priorities={['Medium']}
        onClose={onClose}
        onStatusChange={vi.fn()}
        onOpenWorkItem={vi.fn()}
      />,
    )

    const close = screen.getByRole('button', { name: 'Close' })
    expect(close).toHaveFocus()

    fireEvent.keyDown(document, { key: 'Escape' })
    expect(onClose).toHaveBeenCalledOnce()
  })
})
