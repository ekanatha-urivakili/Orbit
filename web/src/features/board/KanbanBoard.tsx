import { useMemo, useState } from 'react'
import { Plus } from 'lucide-react'
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
  onCreateWorkItem,
  onAddColumn,
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
  onCreateWorkItem?: (statusId: string) => void
  onAddColumn?: () => void
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
    <div
      className={`kanban flex items-start gap-3 overflow-x-auto pb-4 ${
        compact ? 'kanban--lane' : ''
      } ${columnSizeMode === 'Fixed' ? 'kanban--fixed-columns' : ''}`}
      aria-label="Kanban board"
    >
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
            className={`kanban-column bg-[#f4f5f7] dark:bg-[#181b1f] rounded-xl p-2.5 flex flex-col shrink-0 min-w-[260px] max-w-[320px] flex-1 ${
              isDragOver ? 'kanban-column--drag-over ring-2 ring-blue-500' : ''
            } ${isBlocked ? 'kanban-column--drag-blocked ring-2 ring-red-500' : ''}`}
            key={column.statusId}
            aria-labelledby={`column-${column.statusId}`}
            onDragEnter={() => setDragOverColumnStatusId(column.statusId)}
            onDragLeave={(event) => {
              if (!event.currentTarget.contains(event.relatedTarget as Node)) {
                setDragOverColumnStatusId((current) => (current === column.statusId ? null : current))
              }
            }}
          >
            {/* Column Header */}
            <header className="flex items-center gap-2 px-1 py-1.5 mb-2">
              <span className={`w-2 h-2 rounded-full ${meta.tone === 'blue' ? 'bg-blue-500' : meta.tone === 'green' ? 'bg-green-500' : meta.tone === 'amber' ? 'bg-amber-500' : 'bg-gray-400'}`} />
              <h2 id={`column-${column.statusId}`} className="text-xs font-bold text-gray-700 dark:text-gray-200 uppercase tracking-wide flex-1 truncate">
                {meta.label}
              </h2>
              <span
                className={`text-[11px] font-semibold px-2 py-0.5 rounded-full ${
                  overLimit
                    ? 'bg-red-100 text-red-700 dark:bg-red-950/60 dark:text-red-300'
                    : 'bg-gray-200 text-gray-700 dark:bg-gray-800 dark:text-gray-300'
                }`}
              >
                {columnItems.length}
                {column.wipLimit != null ? ` / ${column.wipLimit}` : ''}
              </span>
            </header>

            {/* Column Card List */}
            <div
              className="card-list flex flex-col gap-2 min-h-[80px]"
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
                  statuses={statuses}
                  members={members}
                  allWorkItems={workItems}
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

              {/* Create Work Item in Column Button */}
              {onCreateWorkItem && (
                <button
                  type="button"
                  onClick={() => onCreateWorkItem(column.statusId)}
                  className="flex items-center gap-1 px-2 py-1.5 text-xs text-gray-500 dark:text-gray-400 hover:text-gray-800 dark:hover:text-gray-100 hover:bg-gray-200 dark:hover:bg-gray-800 rounded transition-colors mt-1"
                >
                  <Plus size={14} />
                  <span>Create</span>
                </button>
              )}
            </div>
          </section>
        )
      })}

      {/* Add Column Button on far right */}
      {onAddColumn && (
        <button
          type="button"
          onClick={onAddColumn}
          className="h-10 w-10 shrink-0 rounded-xl bg-gray-100 hover:bg-gray-200 dark:bg-[#181b1f] dark:hover:bg-gray-800 border border-transparent hover:border-gray-300 dark:hover:border-gray-700 flex items-center justify-center text-gray-500 transition-colors"
          title="Add column"
          aria-label="Add column"
        >
          <Plus size={18} />
        </button>
      )}
    </div>
  )
}
