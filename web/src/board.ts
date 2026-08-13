import type { WorkItem, WorkItemStatus } from './api/types'

export function groupWorkItemsByStatus(
  statuses: readonly WorkItemStatus[],
  workItems: readonly WorkItem[],
): ReadonlyMap<WorkItemStatus, WorkItem[]> {
  return new Map(
    statuses.map((status) => [status, workItems.filter((item) => item.status === status)]),
  )
}

// dropIndex is a position in columnItems (0..length); the dragged item's own slot is skipped when present.
export function neighborsForDrop(
  columnItems: readonly WorkItem[],
  draggedId: string,
  dropIndex: number,
): { beforeId: string | null; afterId: string | null } {
  const clampedIndex = Math.max(0, Math.min(dropIndex, columnItems.length))
  const before = columnItems.slice(0, clampedIndex).reverse().find((item) => item.id !== draggedId)
  const after = columnItems.slice(clampedIndex).find((item) => item.id !== draggedId)
  return { beforeId: before?.id ?? null, afterId: after?.id ?? null }
}
