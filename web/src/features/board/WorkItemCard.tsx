import { ChevronDown } from 'lucide-react'
import type { BoardColumn, TenantMembership, WorkItem, WorkItemStatus } from '../../api/types'
import { statusMeta } from './constants'
import { WorkItemTypeIcon } from '../workitems/typeIcons'
import { AssigneePicker } from '../../components/AssigneePicker'

export function WorkItemCard({
  item,
  columns,
  columnCounts,
  members = [],
  onStatusChange,
  onOpen,
  onAssigneeChange,
  assigneeChangePending = false,
  onDragStart,
  onDragEnd,
  onDragOver,
  onDrop,
  dragging,
}: {
  item: WorkItem
  columns: readonly BoardColumn[]
  columnCounts: ReadonlyMap<WorkItemStatus, WorkItem[]>
  members?: TenantMembership[]
  onStatusChange: (workItem: WorkItem, status: WorkItemStatus) => void
  onOpen?: (workItem: WorkItem) => void
  onAssigneeChange?: (workItem: WorkItem, assigneeUserId: string | null) => void
  assigneeChangePending?: boolean
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
      onDragStart={(event) => {
        if (event.dataTransfer) {
          event.dataTransfer.setData('text/plain', item.id)
          event.dataTransfer.effectAllowed = 'move'
        }
        onDragStart?.(event)
      }}
      onDragEnd={onDragEnd}
      onDragOver={onDragOver}
      onDrop={onDrop}
    >
      <div className="card-meta">
        <span className={`type-badge type-${item.type.toLowerCase()}`}><WorkItemTypeIcon type={item.type} size={12} />{item.type}</span>
        <span className={`priority priority-${item.priority.toLowerCase()}`}>{item.priority}</span>
      </div>
      <a
        href={`/browse/${item.key}`}
        className={onOpen ? 'block hover:underline hover:text-blue-700' : undefined}
        onClick={(event) => {
          if (!onOpen) return
          event.preventDefault()
          onOpen(item)
        }}
      >
        <h3>{item.summary}</h3>
      </a>
      <div className="card-footer">
        <a
          href={`/browse/${item.key}`}
          className={onOpen ? 'hover:underline hover:text-blue-700' : undefined}
          onClick={(event) => {
            if (!onOpen) return
            event.preventDefault()
            onOpen(item)
          }}
        >
          {item.key}
        </a>
        <div className="flex items-center gap-2">
          {onAssigneeChange && (
            <AssigneePicker
              workItem={item}
              members={members}
              onChange={(assigneeUserId) => onAssigneeChange(item, assigneeUserId)}
              size="sm"
              disabled={assigneeChangePending}
            />
          )}
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
      </div>
    </article>
  )
}
