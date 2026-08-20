import { useMemo, useState } from 'react'
import type { BoardColumn, TenantMembership, WorkItem, WorkItemStatus } from '../../api/types'
import { groupWorkItemsByStatus, neighborsForDrop } from '../../board'
import { statusMeta } from './constants'
import { WorkItemCard } from './WorkItemCard'

export function KanbanBoard({
  columns,
  workItems,
  loading,
  members = [],
  onStatusChange,
  onReorder,
  onOpen,
  onAssigneeChange,
  assigneeChangePending = false,
  compact = false,
}: {
  columns: readonly BoardColumn[]
  workItems: WorkItem[]
  loading: boolean
  members?: TenantMembership[]
  onStatusChange: (workItem: WorkItem, status: WorkItemStatus) => void
  onReorder: (workItem: WorkItem, neighbors: { beforeId: string | null; afterId: string | null }) => void
  onOpen?: (workItem: WorkItem) => void
  onAssigneeChange?: (workItem: WorkItem, assigneeUserId: string | null) => void
  assigneeChangePending?: boolean
  compact?: boolean
}) {
  const [draggedId, setDraggedId] = useState<string | null>(null)
  const [dragOverColumnStatus, setDragOverColumnStatus] = useState<WorkItemStatus | null>(null)
  const orderedColumns = useMemo(() => [...columns].sort((a, b) => a.order - b.order), [columns])
  const grouped = useMemo(
    () => groupWorkItemsByStatus(orderedColumns.map((column) => column.status), workItems),
    [orderedColumns, workItems],
  )

  if (loading) return <div className="board-loading">Loading board…</div>

  function drop(column: BoardColumn, columnItems: WorkItem[], dropIndex: number) {
    const dragged = draggedId && workItems.find((item) => item.id === draggedId)
    setDragOverColumnStatus(null)
    setDraggedId(null)
    if (!dragged) return

    if (dragged.status !== column.status) {
      if (
        column.wipLimit != null &&
        column.wipLimitMode === 'Block' &&
        columnItems.length >= column.wipLimit
      ) {
        return
      }
      onStatusChange(dragged, column.status)
      return
    }

    onReorder(dragged, neighborsForDrop(columnItems, dragged.id, dropIndex))
  }

  return (
    <div className={`kanban${compact ? ' kanban--lane' : ''}`} aria-label="Kanban board">
      {orderedColumns.map((column) => {
        const columnItems = grouped.get(column.status) ?? []
        const meta = statusMeta[column.status]
        const overLimit = column.wipLimit != null && columnItems.length > column.wipLimit
        const isDragOver = dragOverColumnStatus === column.status
        const isBlocked =
          Boolean(draggedId) &&
          dragOverColumnStatus === column.status &&
          column.wipLimit != null &&
          column.wipLimitMode === 'Block' &&
          columnItems.length >= column.wipLimit &&
          workItems.find((i) => i.id === draggedId)?.status !== column.status

        return (
          <section
            className={`kanban-column${isDragOver ? ' kanban-column--drag-over' : ''}${isBlocked ? ' kanban-column--drag-blocked' : ''}`}
            key={column.status}
            aria-labelledby={`column-${column.status}`}
            onDragEnter={() => setDragOverColumnStatus(column.status)}
            onDragLeave={(event) => {
              if (!event.currentTarget.contains(event.relatedTarget as Node)) {
                setDragOverColumnStatus((current) => (current === column.status ? null : current))
              }
            }}
          >
            <header>
              <span className={`status-dot ${meta.tone}`} />
              <h2 id={`column-${column.status}`}>{meta.label}</h2>
              <span className={`item-count${overLimit ? ' item-count--over-limit' : ''}`}>
                {columnItems.length}
                {column.wipLimit != null ? ` / ${column.wipLimit}` : ''}
              </span>
            </header>
            <div
              className="card-list"
              onDragOver={(event) => {
                event.preventDefault()
                if (event.dataTransfer) {
                  event.dataTransfer.dropEffect = isBlocked ? 'none' : 'move'
                }
              }}
              onDrop={(event) => {
                event.preventDefault()
                drop(column, columnItems, columnItems.length)
              }}
            >
              {columnItems.map((item, index) => (
                <WorkItemCard
                  key={item.id}
                  item={item}
                  columns={orderedColumns}
                  columnCounts={grouped}
                  members={members}
                  onStatusChange={onStatusChange}
                  onOpen={onOpen}
                  onAssigneeChange={onAssigneeChange}
                  assigneeChangePending={assigneeChangePending}
                  dragging={item.id === draggedId}
                  onDragStart={() => setDraggedId(item.id)}
                  onDragEnd={() => {
                    setDraggedId(null)
                    setDragOverColumnStatus(null)
                  }}
                  onDragOver={(event) => {
                    event.preventDefault()
                    if (event.dataTransfer) {
                      event.dataTransfer.dropEffect = isBlocked ? 'none' : 'move'
                    }
                  }}
                  onDrop={(event) => {
                    event.preventDefault()
                    event.stopPropagation()
                    drop(column, columnItems, index)
                  }}
                />
              ))}
              {columnItems.length === 0 && (
                <div className="empty-column">No work here</div>
              )}
            </div>
          </section>
        )
      })}
    </div>
  )
}
