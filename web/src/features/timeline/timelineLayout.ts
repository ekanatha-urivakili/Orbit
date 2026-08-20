import type { Sprint, WorkItem } from '../../api/types'

export interface TimelineBar {
  id: string
  key: string
  summary: string
  start: Date
  end: Date
  startOffsetDays: number
  durationDays: number
}

export interface TimelineEpicRow {
  epic: WorkItem
  bar: TimelineBar | null
  children: TimelineBar[]
}

export interface TimelineSprintBar {
  sprint: Sprint
  start: Date
  end: Date
  startOffsetDays: number
  durationDays: number
}

export interface TimelineMonth {
  label: string
  startOffsetDays: number
  days: number
}

export interface TimelineLayout {
  rangeStart: Date
  rangeEnd: Date
  totalDays: number
  todayOffsetDays: number | null
  months: TimelineMonth[]
  sprintBars: TimelineSprintBar[]
  epicRows: TimelineEpicRow[]
  unscheduledEpicCount: number
}

export function parseDateOnly(value: string | null | undefined): Date | null {
  if (!value) return null
  const [year, month, day] = value.split('-').map(Number)
  if (!year || !month || !day) return null
  return new Date(Date.UTC(year, month - 1, day))
}

function startOfMonth(date: Date): Date {
  return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), 1))
}

function endOfMonth(date: Date): Date {
  return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0))
}

function daysBetween(a: Date, b: Date): number {
  return Math.round((b.getTime() - a.getTime()) / 86_400_000)
}

function addDays(date: Date, days: number): Date {
  return new Date(date.getTime() + days * 86_400_000)
}

function itemDateRange(item: WorkItem): { start: Date; end: Date } | null {
  const start = parseDateOnly(item.startDate)
  const end = parseDateOnly(item.dueDate)
  if (start && end) return { start: start <= end ? start : end, end: start <= end ? end : start }
  if (start) return { start, end: start }
  if (end) return { start: end, end }
  return null
}

function mergeRange(
  a: { start: Date; end: Date } | null,
  b: { start: Date; end: Date } | null,
): { start: Date; end: Date } | null {
  if (!a) return b
  if (!b) return a
  return { start: a.start < b.start ? a.start : b.start, end: a.end > b.end ? a.end : b.end }
}

export function buildTimelineLayout(
  workItems: readonly WorkItem[],
  sprints: readonly Sprint[],
  today: Date = new Date(),
): TimelineLayout | null {
  const epics = workItems.filter((item) => item.type === 'Epic' && !item.isArchived)
  const childrenByEpicId = new Map<string, WorkItem[]>()
  for (const item of workItems) {
    if (!item.parentId || item.isArchived) continue
    const bucket = childrenByEpicId.get(item.parentId)
    if (bucket) bucket.push(item)
    else childrenByEpicId.set(item.parentId, [item])
  }

  const sprintRanges = sprints
    .map((sprint) => {
      const start = parseDateOnly(sprint.startDate)
      const end = parseDateOnly(sprint.endDate)
      return start && end ? { sprint, start, end: start <= end ? end : start } : null
    })
    .filter((value): value is { sprint: Sprint; start: Date; end: Date } => value !== null)

  const epicRanges = epics.map((epic) => {
    const children = childrenByEpicId.get(epic.id) ?? []
    let range = itemDateRange(epic)
    for (const child of children) {
      range = mergeRange(range, itemDateRange(child))
    }
    return { epic, children, range }
  })

  let overall: { start: Date; end: Date } | null = null
  for (const { start, end } of sprintRanges) {
    overall = mergeRange(overall, { start, end })
  }
  for (const { range } of epicRanges) {
    overall = mergeRange(overall, range)
  }

  if (!overall) return null

  const rangeStart = startOfMonth(overall.start)
  const rangeEnd = endOfMonth(overall.end)
  const totalDays = daysBetween(rangeStart, rangeEnd) + 1

  const months: TimelineMonth[] = []
  let cursor = rangeStart
  while (cursor <= rangeEnd) {
    const monthEnd = endOfMonth(cursor) < rangeEnd ? endOfMonth(cursor) : rangeEnd
    months.push({
      label: cursor.toLocaleDateString('en-US', { month: 'long', timeZone: 'UTC' }),
      startOffsetDays: daysBetween(rangeStart, cursor),
      days: daysBetween(cursor, monthEnd) + 1,
    })
    cursor = addDays(monthEnd, 1)
  }

  const toBar = (id: string, key: string, summary: string, range: { start: Date; end: Date }): TimelineBar => ({
    id,
    key,
    summary,
    start: range.start,
    end: range.end,
    startOffsetDays: daysBetween(rangeStart, range.start),
    durationDays: daysBetween(range.start, range.end) + 1,
  })

  const sprintBars: TimelineSprintBar[] = sprintRanges.map(({ sprint, start, end }) => ({
    sprint,
    start,
    end,
    startOffsetDays: daysBetween(rangeStart, start),
    durationDays: daysBetween(start, end) + 1,
  }))

  let unscheduledEpicCount = 0
  const epicRows: TimelineEpicRow[] = epicRanges.map(({ epic, children, range }) => {
    if (!range) unscheduledEpicCount += 1
    return {
      epic,
      bar: range ? toBar(epic.id, epic.key, epic.summary, range) : null,
      children: children
        .map((child) => {
          const childRange = itemDateRange(child)
          return childRange ? toBar(child.id, child.key, child.summary, childRange) : null
        })
        .filter((bar): bar is TimelineBar => bar !== null),
    }
  })

  const todayUtc = new Date(Date.UTC(today.getFullYear(), today.getMonth(), today.getDate()))
  const todayOffsetDays =
    todayUtc >= rangeStart && todayUtc <= rangeEnd ? daysBetween(rangeStart, todayUtc) : null

  return { rangeStart, rangeEnd, totalDays, todayOffsetDays, months, sprintBars, epicRows, unscheduledEpicCount }
}
