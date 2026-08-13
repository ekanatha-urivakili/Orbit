import { ChevronDown } from 'lucide-react'
import type { BoardColumn, WorkItem, WorkItemStatus } from '../../api/types'
import { statusMeta } from './constants'

export function WorkItemCard({
  item,
  columns,
  columnCounts,
  onStatusChange,
  onOpen,
  onDragStart,
  onDragEnd,
  onDragOver,
  onDrop,
  dragging,
}: {
  item: WorkItem
  columns: readonly BoardColumn[]
  columnCounts: ReadonlyMap<WorkItemStatus, WorkItem[]>
  onStatusChange: (workItem: WorkItem, status: WorkItemStatus) => void
  onOpen?: (workItem: WorkItem) => void
  onDragStart?: (event: React.DragEvent) => void
  onDragEnd?: (event: React.DragEvent) => void
  onDragOver?: (event: React.DragEvent) => void
  onDrop?: (event: React.DragEvent) => void
  dragging?: boolean
}) {
  return (
    <article
      className={`work-card${dragging ? ' work-card-dragging' : ''}`}
      draggable={Boolean(onDragStart)}
      onDragStart={onDragStart}
      onDragEnd={onDragEnd}
      onDragOver={onDragOver}
      onDrop={onDrop}
    >
      <div className="card-meta">
        <span className={`type-badge type-${item.type.toLowerCase()}`}>{item.type}</span>
        <span className={`priority priority-${item.priority.toLowerCase()}`}>{item.priority}</span>
      </div>
      <h3 className={onOpen ? 'cursor-pointer hover:underline' : undefined} onClick={() => onOpen?.(item)}>{item.summary}</h3>
      <div className="card-footer">
        <span>{item.key}</span>
        <label className="move-control">
          <span className="sr-only">Move {item.key}</span>
          <select value={item.status} onChange={(event) => onStatusChange(item, event.target.value as WorkItemStatus)}>
            {columns.map((column) => {
              const atLimit =
                column.status !== item.status &&
                column.wipLimit != null &&
                column.wipLimitMode === 'Block' &&
                (columnCounts.get(column.status)?.length ?? 0) >= column.wipLimit
              return (
                <option key={column.status} value={column.status} disabled={atLimit}>
                  {statusMeta[column.status].label}
                  {atLimit ? ' (WIP limit reached)' : ''}
                </option>
              )
            })}
          </select>
          <ChevronDown size={14} aria-hidden="true" />
        </label>
      </div>
    </article>
  )
}
