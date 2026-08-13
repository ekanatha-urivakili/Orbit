import { useMemo, useState } from 'react'
import type { BoardColumn, WorkItem, WorkItemStatus } from '../../api/types'
import { groupWorkItemsByStatus, neighborsForDrop } from '../../board'
import { statusMeta } from './constants'
import { WorkItemCard } from './WorkItemCard'

export function KanbanBoard({
  columns,
  workItems,
  loading,
  onStatusChange,
  onReorder,
  onOpen,
}: {
  columns: readonly BoardColumn[]
  workItems: WorkItem[]
  loading: boolean
  onStatusChange: (workItem: WorkItem, status: WorkItemStatus) => void
  onReorder: (workItem: WorkItem, neighbors: { beforeId: string | null; afterId: string | null }) => void
  onOpen?: (workItem: WorkItem) => void
}) {
  const [draggedId, setDraggedId] = useState<string | null>(null)
  const orderedColumns = useMemo(() => [...columns].sort((a, b) => a.order - b.order), [columns])
  const grouped = useMemo(
    () => groupWorkItemsByStatus(orderedColumns.map((column) => column.status), workItems),
    [orderedColumns, workItems],
  )

  if (loading) return <div className="board-loading">Loading board…</div>

  function drop(column: BoardColumn, columnItems: WorkItem[], dropIndex: number) {
    const dragged = draggedId && workItems.find((item) => item.id === draggedId)
    if (!dragged || dragged.status !== column.status) return
    onReorder(dragged, neighborsForDrop(columnItems, dragged.id, dropIndex))
  }

  return (
    <div className="kanban" aria-label="Kanban board">
      {orderedColumns.map((column) => {
        const columnItems = grouped.get(column.status) ?? []
        const meta = statusMeta[column.status]
        const overLimit = column.wipLimit != null && columnItems.length > column.wipLimit
        return (
          <section className="kanban-column" key={column.status} aria-labelledby={`column-${column.status}`}>
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
              onDragOver={(event) => event.preventDefault()}
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
                  onStatusChange={onStatusChange}
                  onOpen={onOpen}
                  dragging={item.id === draggedId}
                  onDragStart={() => setDraggedId(item.id)}
                  onDragEnd={() => setDraggedId(null)}
                  onDragOver={(event) => event.preventDefault()}
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
