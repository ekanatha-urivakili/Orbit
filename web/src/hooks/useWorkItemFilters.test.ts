import { describe, expect, it } from 'vitest'
import { act, renderHook } from '@testing-library/react'
import { NO_PARENT, UNASSIGNED, useWorkItemFilters } from './useWorkItemFilters'
import type { TenantMembership, WorkItem } from '../api/types'

const statusLabels: Record<string, string> = {
  Backlog: 'To Do',
  Selected: 'Selected',
  InProgress: 'In progress',
  InReview: 'In review',
  Done: 'Done',
  Blocked: 'Blocked',
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
    id: 'membership-1',
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

describe('useWorkItemFilters', () => {
  it('matches everything when no filters are active', () => {
    const items = [makeItem({ id: 'a' }), makeItem({ id: 'b', summary: 'Other' })]
    const { result } = renderHook(() => useWorkItemFilters(items, [], statusLabels, {}))
    expect(result.current.filteredItems).toHaveLength(2)
    expect(result.current.activeCount).toBe(0)
  })

  it('filters by search term across key and summary', () => {
    const items = [makeItem({ id: 'a', key: 'ORB-1', summary: 'Fix login bug' }), makeItem({ id: 'b', key: 'ORB-2', summary: 'Add export' })]
    const { result } = renderHook(() => useWorkItemFilters(items, [], statusLabels, {}))

    act(() => result.current.setSearchTerm('login'))

    expect(result.current.filteredItems.map((item) => item.id)).toEqual(['a'])
    expect(result.current.activeCount).toBe(1)
  })

  it('filters by assignee including the unassigned sentinel', () => {
    const member = makeMember({ userId: 'user-1' })
    const items = [
      makeItem({ id: 'a', assigneeUserId: 'user-1' }),
      makeItem({ id: 'b', assigneeUserId: null }),
    ]
    const { result } = renderHook(() => useWorkItemFilters(items, [member], statusLabels, {}))
    const assigneeField = () => result.current.fields.find((field) => field.key === 'assignee')!

    act(() => assigneeField().toggle(UNASSIGNED))

    expect(result.current.filteredItems.map((item) => item.id)).toEqual(['b'])
  })

  it('filters by status', () => {
    const items = [makeItem({ id: 'a', statusId: 'Done' }), makeItem({ id: 'b', statusId: 'Backlog' })]
    const { result } = renderHook(() => useWorkItemFilters(items, [], statusLabels, {}))
    const statusField = () => result.current.fields.find((field) => field.key === 'status')!

    act(() => statusField().toggle('Done'))

    expect(result.current.filteredItems.map((item) => item.id)).toEqual(['a'])
  })

  it('filters by parent including the no-parent sentinel', () => {
    const items = [
      makeItem({ id: 'epic', key: 'ORB-1', type: 'Epic', epicName: 'Epic' }),
      makeItem({ id: 'child', key: 'ORB-2', parentId: 'epic' }),
      makeItem({ id: 'orphan', key: 'ORB-3', parentId: null }),
    ]
    const { result } = renderHook(() => useWorkItemFilters(items, [], statusLabels, {}))
    const parentField = () => result.current.fields.find((field) => field.key === 'parent')!

    act(() => parentField().toggle(NO_PARENT))

    expect(result.current.filteredItems.map((item) => item.id).sort()).toEqual(['epic', 'orphan'])
  })

  it('combines multiple active filters with AND semantics', () => {
    const items = [
      makeItem({ id: 'a', statusId: 'Done', assigneeUserId: 'user-1' }),
      makeItem({ id: 'b', statusId: 'Done', assigneeUserId: null }),
      makeItem({ id: 'c', statusId: 'Backlog', assigneeUserId: 'user-1' }),
    ]
    const member = makeMember({ userId: 'user-1' })
    const { result } = renderHook(() => useWorkItemFilters(items, [member], statusLabels, {}))
    const statusField = () => result.current.fields.find((field) => field.key === 'status')!
    const assigneeField = () => result.current.fields.find((field) => field.key === 'assignee')!

    act(() => statusField().toggle('Done'))
    act(() => assigneeField().toggle('user-1'))

    expect(result.current.filteredItems.map((item) => item.id)).toEqual(['a'])
  })

  it('clearAll resets search term and every field', () => {
    const items = [makeItem({ id: 'a', statusId: 'Done' })]
    const { result } = renderHook(() => useWorkItemFilters(items, [], statusLabels, {}))
    const statusField = () => result.current.fields.find((field) => field.key === 'status')!

    act(() => result.current.setSearchTerm('foo'))
    act(() => statusField().toggle('Done'))
    expect(result.current.activeCount).toBeGreaterThan(0)

    act(() => result.current.clearAll())

    expect(result.current.activeCount).toBe(0)
    expect(result.current.searchTerm).toBe('')
  })
})
