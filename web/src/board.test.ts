import { describe, expect, it } from 'vitest'
import { groupWorkItemsByStatus, neighborsForDrop } from './board'
import type { WorkItem } from './api/types'

const item: WorkItem = {
  id: 'item-1',
  projectId: 'project-1',
  key: 'ORB-1',
  summary: 'Build the board',
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
  linkType: null,
  linkedWorkItemId: null,
  labels: [],
  countries: [],
  attachmentNames: [],
  type: 'Story',
  status: 'InProgress',
  priority: 'High',
  rank: 1024,
  version: 1,
  createdAt: '2026-08-11T00:00:00Z',
  updatedAt: '2026-08-11T00:00:00Z',
}

describe('groupWorkItemsByStatus', () => {
  it('preserves defined columns and places each item in its status', () => {
    const grouped = groupWorkItemsByStatus(['Backlog', 'InProgress', 'Done'], [item])

    expect(grouped.get('Backlog')).toEqual([])
    expect(grouped.get('InProgress')).toEqual([item])
    expect(grouped.get('Done')).toEqual([])
  })
})

describe('neighborsForDrop', () => {
  const items: WorkItem[] = [
    { ...item, id: 'a', rank: 1024 },
    { ...item, id: 'b', rank: 2048 },
    { ...item, id: 'c', rank: 3072 },
  ]

  it('returns null before-id when dropped at the top', () => {
    expect(neighborsForDrop(items, 'c', 0)).toEqual({ beforeId: null, afterId: 'a' })
  })

  it('returns null after-id when dropped at the bottom', () => {
    expect(neighborsForDrop(items, 'a', items.length)).toEqual({ beforeId: 'c', afterId: null })
  })

  it('skips the dragged item when it precedes the drop index', () => {
    expect(neighborsForDrop(items, 'a', 2)).toEqual({ beforeId: 'b', afterId: 'c' })
  })

  it('skips the dragged item when it is the drop target itself', () => {
    expect(neighborsForDrop(items, 'b', 0)).toEqual({ beforeId: null, afterId: 'a' })
  })

  it('clamps an out-of-range drop index', () => {
    expect(neighborsForDrop(items, 'a', 99)).toEqual({ beforeId: 'c', afterId: null })
  })
})
