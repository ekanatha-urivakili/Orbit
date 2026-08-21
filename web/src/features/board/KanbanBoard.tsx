import { useMemo, useState } from 'react'
import type { BoardColumn, TenantMembership, WorkItem, WorkItemStatusDefinition } from '../../api/types'
import { groupWorkItemsByStatus, neighborsForDrop } from '../../board'
import { statusMeta } from './constants'
import { WorkItemCard } from './WorkItemCard'

export function KanbanBoard({
  columns,
  statuses,
  workItems,
  loading,
  members = [],
  onStatusChange,
  onReorder,
  onOpen,
  onAssigneeChange,
  assigneeChangePending = false,
  compact = false,
  hiddenFields = [],
  columnSizeMode = 'Flexible',
}: {
  columns: readonly BoardColumn[]
  statuses: readonly WorkItemStatusDefinition[]
  workItems: WorkItem[]
  loading: boolean
  members?: TenantMembership[]
  onStatusChange: (workItem: WorkItem, statusId: string) => void
  onReorder: (workItem: WorkItem, neighbors: { beforeId: string | null; afterId: string | null }) => void
  onOpen?: (workItem: WorkItem) => void
  onAssigneeChange?: (workItem: WorkItem, assigneeUserId: string | null) => void
  assigneeChangePending?: boolean
  compact?: boolean
  hiddenFields?: readonly string[]
  columnSizeMode?: 'Fixed' | 'Flexible'
}) {
  const [draggedId, setDraggedId] = useState<string | null>(null)
  const [dragOverColumnStatusId, setDragOverColumnStatusId] = useState<string | null>(null)
  const statusesById = useMemo(() => new Map(statuses.map((status) => [status.id, status])), [statuses])
  const orderedColumns = useMemo(() => [...columns].sort((a, b) => a.order - b.order), [columns])
  const grouped = useMemo(
    () => groupWorkItemsByStatus(orderedColumns.map((column) => column.statusId), workItems),
    [orderedColumns, workItems],
  )

  if (loading) return <div className="board-loading">Loading board…</div>

  function drop(column: BoardColumn, columnItems: WorkItem[], dropIndex: number) {
    const dragged = draggedId && workItems.find((item) => item.id === draggedId)
    setDragOverColumnStatusId(null)
    setDraggedId(null)
    if (!dragged) return

    if (dragged.statusId !== column.statusId) {
      if (
        column.wipLimit != null &&
        column.wipLimitMode === 'Block' &&
        columnItems.length >= column.wipLimit
      ) {
        return
      }
      onStatusChange(dragged, column.statusId)
      return
    }

    onReorder(dragged, neighborsForDrop(columnItems, dragged.id, dropIndex))
  }

  return (
    <div className={`kanban${compact ? ' kanban--lane' : ''}${columnSizeMode === 'Fixed' ? ' kanban--fixed-columns' : ''}`} aria-label="Kanban board">
      {orderedColumns.map((column) => {
        const columnItems = grouped.get(column.statusId) ?? []
        const status = statusesById.get(column.statusId)
        if (!status) return null
        const meta = statusMeta(status)
        const overLimit = column.wipLimit != null && columnItems.length > column.wipLimit
        const isDragOver = dragOverColumnStatusId === column.statusId
        const isBlocked =
          Boolean(draggedId) &&
          dragOverColumnStatusId === column.statusId &&
          column.wipLimit != null &&
          column.wipLimitMode === 'Block' &&
          columnItems.length >= column.wipLimit &&
          workItems.find((i) => i.id === draggedId)?.statusId !== column.statusId

        return (
          <section
            className={`kanban-column${isDragOver ? ' kanban-column--drag-over' : ''}${isBlocked ? ' kanban-column--drag-blocked' : ''}`}
            key={column.statusId}
            aria-labelledby={`column-${column.statusId}`}
            onDragEnter={() => setDragOverColumnStatusId(column.statusId)}
            onDragLeave={(event) => {
              if (!event.currentTarget.contains(event.relatedTarget as Node)) {
                setDragOverColumnStatusId((current) => (current === column.statusId ? null : current))
              }
            }}
          >
            <header>
              <span className={`status-dot ${meta.tone}`} />
              <h2 id={`column-${column.statusId}`}>{meta.label}</h2>
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
                  statuses={statuses}
                  columnCounts={grouped}
                  members={members}
                  allWorkItems={workItems}
                  onStatusChange={onStatusChange}
                  onOpen={onOpen}
                  onAssigneeChange={onAssigneeChange}
                  assigneeChangePending={assigneeChangePending}
                  hiddenFields={hiddenFields}
                  dragging={item.id === draggedId}
                  onDragStart={() => setDraggedId(item.id)}
                  onDragEnd={() => {
                    setDraggedId(null)
                    setDragOverColumnStatusId(null)
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
