import type { WorkItem } from './api/types'

export function groupWorkItemsByStatus(
  statusIds: readonly string[],
  workItems: readonly WorkItem[],
): ReadonlyMap<string, WorkItem[]> {
  return new Map(
    statusIds.map((statusId) => [statusId, workItems.filter((item) => item.statusId === statusId)]),
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
