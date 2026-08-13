import type { WorkItemStatus } from '../../api/types'

export const statusMeta: Readonly<Record<WorkItemStatus, { label: string; tone: string }>> = {
  Backlog: { label: 'Backlog', tone: 'slate' },
  Selected: { label: 'Selected', tone: 'cyan' },
  InProgress: { label: 'In progress', tone: 'blue' },
  InReview: { label: 'In review', tone: 'amber' },
  Done: { label: 'Done', tone: 'green' },
  Blocked: { label: 'Blocked', tone: 'red' },
}

export const allStatuses: readonly WorkItemStatus[] = Object.keys(statusMeta) as WorkItemStatus[]
