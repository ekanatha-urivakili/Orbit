import { describe, expect, it } from 'vitest'
import { buildTimelineLayout, parseDateOnly } from './timelineLayout'
import type { Sprint, WorkItem } from '../../api/types'

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
    status: 'Backlog',
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

function makeSprint(overrides: Partial<Sprint> = {}): Sprint {
  return {
    id: 'sprint-1',
    projectId: 'project-1',
    name: 'Sprint 1',
    state: 'Active',
    goal: null,
    startDate: null,
    endDate: null,
    workItemIds: [],
    version: 1,
    ...overrides,
  } as Sprint
}

describe('parseDateOnly', () => {
  it('parses a yyyy-MM-dd string as a UTC date', () => {
    const date = parseDateOnly('2026-08-20')
    expect(date?.getUTCFullYear()).toBe(2026)
    expect(date?.getUTCMonth()).toBe(7)
    expect(date?.getUTCDate()).toBe(20)
  })

  it('returns null for null or empty input', () => {
    expect(parseDateOnly(null)).toBeNull()
    expect(parseDateOnly(undefined)).toBeNull()
    expect(parseDateOnly('')).toBeNull()
  })
})

describe('buildTimelineLayout', () => {
  it('returns null when nothing has dates', () => {
    const items = [makeItem({ type: 'Epic', epicName: 'Epic' })]
    expect(buildTimelineLayout(items, [])).toBeNull()
  })

  it('builds a bar for an epic with its own start/due dates', () => {
    const epic = makeItem({
      id: 'epic-1',
      key: 'ORB-1',
      type: 'Epic',
      epicName: 'Epic 1',
      startDate: '2026-08-10',
      dueDate: '2026-08-20',
    })
    const layout = buildTimelineLayout([epic], [])

    expect(layout).not.toBeNull()
    expect(layout!.epicRows).toHaveLength(1)
    expect(layout!.epicRows[0].bar).not.toBeNull()
    expect(layout!.epicRows[0].bar!.durationDays).toBe(11)
    expect(layout!.unscheduledEpicCount).toBe(0)
  })

  it('rolls up a child work item date range into the parent epic bar', () => {
    const epic = makeItem({ id: 'epic-1', key: 'ORB-1', type: 'Epic', epicName: 'Epic 1', startDate: '2026-08-10', dueDate: '2026-08-15' })
    const child = makeItem({ id: 'child-1', key: 'ORB-2', parentId: 'epic-1', startDate: '2026-08-12', dueDate: '2026-08-25' })
    const layout = buildTimelineLayout([epic, child], [])

    expect(layout!.epicRows[0].bar!.end.getUTCDate()).toBe(25)
    expect(layout!.epicRows[0].children).toHaveLength(1)
  })

  it('counts epics without any resolvable date as unscheduled and excludes their bar', () => {
    const datedEpic = makeItem({ id: 'epic-1', key: 'ORB-1', type: 'Epic', epicName: 'Dated', startDate: '2026-08-10', dueDate: '2026-08-12' })
    const undatedEpic = makeItem({ id: 'epic-2', key: 'ORB-2', type: 'Epic', epicName: 'Undated' })
    const layout = buildTimelineLayout([datedEpic, undatedEpic], [])

    expect(layout!.unscheduledEpicCount).toBe(1)
    const undatedRow = layout!.epicRows.find((row) => row.epic.id === 'epic-2')
    expect(undatedRow?.bar).toBeNull()
  })

  it('includes sprint bars from sprint start/end dates', () => {
    const sprint = makeSprint({ startDate: '2026-08-01', endDate: '2026-08-14' })
    const layout = buildTimelineLayout([], [sprint])

    expect(layout).not.toBeNull()
    expect(layout!.sprintBars).toHaveLength(1)
    expect(layout!.sprintBars[0].durationDays).toBe(14)
  })

  it('marks todayOffsetDays only when today falls within the rendered range', () => {
    const epic = makeItem({ id: 'epic-1', key: 'ORB-1', type: 'Epic', epicName: 'Epic 1', startDate: '2020-01-01', dueDate: '2020-01-05' })
    const layout = buildTimelineLayout([epic], [], new Date())

    expect(layout!.todayOffsetDays).toBeNull()
  })
})
