import { ChevronDown, Flag, GitBranch, Calendar } from 'lucide-react'
import type { BoardColumn, TenantMembership, WorkItem, WorkItemStatusDefinition } from '../../api/types'
import { statusMeta } from './constants'
import { WorkItemTypeIcon } from '../workitems/typeIcons'
import { AssigneePicker } from '../../components/AssigneePicker'

function relativeTime(iso: string): string {
  const diffMs = Date.now() - new Date(iso).getTime()
  const minutes = Math.round(diffMs / 60000)
  if (minutes < 1) return 'just now'
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.round(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.round(hours / 24)
  return `${days}d ago`
}

export function WorkItemCard({
  item,
  columns,
  statuses,
  columnCounts,
  members = [],
  allWorkItems = [],
  onStatusChange,
  onOpen,
  onAssigneeChange,
  assigneeChangePending = false,
  onDragStart,
  onDragEnd,
  onDragOver,
  onDrop,
  dragging,
  hiddenFields = [],
}: {
  item: WorkItem
  columns: readonly BoardColumn[]
  statuses: readonly WorkItemStatusDefinition[]
  columnCounts: ReadonlyMap<string, WorkItem[]>
  members?: TenantMembership[]
  /** The project's full (unfiltered) work item list, used to resolve the parent chip and subtask summary. */
  allWorkItems?: readonly WorkItem[]
  onStatusChange: (workItem: WorkItem, statusId: string) => void
  onOpen?: (workItem: WorkItem) => void
  onAssigneeChange?: (workItem: WorkItem, assigneeUserId: string | null) => void
  assigneeChangePending?: boolean
  onDragStart?: (event: React.DragEvent) => void
  onDragEnd?: (event: React.DragEvent) => void
  onDragOver?: (event: React.DragEvent) => void
  onDrop?: (event: React.DragEvent) => void
  dragging?: boolean
  /** Field keys hidden via the board "View settings" panel (§13.5). */
  hiddenFields?: readonly string[]
}) {
  const isHidden = (field: string) => hiddenFields.includes(field)
  const parent = item.parentId ? allWorkItems.find((candidate) => candidate.id === item.parentId) : undefined
  const subtasks = allWorkItems.filter((candidate) => candidate.parentId === item.id)
  const doneSubtasks = subtasks.filter((candidate) =>
    statuses.find((status) => status.id === candidate.statusId)?.category === 'Done').length

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
        {!isHidden('priority') && (
          <span className={`priority priority-${item.priority.toLowerCase()}`}>{item.priority}</span>
        )}
        {!isHidden('flagged') && item.isFlagged && (
          <span className="card-flag" title="Flagged"><Flag size={12} /></span>
        )}
      </div>

      {!isHidden('parent') && parent && (
        <div className="card-parent-chip" title={`Parent: ${parent.key} ${parent.summary}`}>
          <GitBranch size={11} /> {parent.key}
        </div>
      )}

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

      {!isHidden('subtaskSummary') && subtasks.length > 0 && (
        <div className="card-subtask-summary">
          {doneSubtasks}/{subtasks.length} subtasks done
        </div>
      )}

      {(!isHidden('dueDate') && item.dueDate) || (!isHidden('startDate') && item.startDate) ? (
        <div className="card-dates">
          {!isHidden('startDate') && item.startDate && (
            <span><Calendar size={11} /> Start {item.startDate}</span>
          )}
          {!isHidden('dueDate') && item.dueDate && (
            <span className={new Date(item.dueDate) < new Date() ? 'card-date-overdue' : ''}>
              <Calendar size={11} /> Due {item.dueDate}
            </span>
          )}
        </div>
      ) : null}

      {!isHidden('labels') && item.labels.length > 0 && (
        <div className="card-labels">
          {item.labels.map((label) => (
            <span key={label} className="card-label-chip">{label}</span>
          ))}
        </div>
      )}

      <div className="card-footer">
        {!isHidden('workItemKey') ? (
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
        ) : <span />}
        <div className="flex items-center gap-2">
          {!isHidden('storyPointEstimate') && item.storyPoints != null && (
            <span className="card-story-points" title="Story point estimate">{item.storyPoints}</span>
          )}
          {!isHidden('created') && (
            <span className="card-updated" title={`Created ${item.createdAt}`}>Created {relativeTime(item.createdAt)}</span>
          )}
          {!isHidden('updated') && (
            <span className="card-updated" title={`Updated ${item.updatedAt}`}>{relativeTime(item.updatedAt)}</span>
          )}
          {onAssigneeChange && !isHidden('assignee') && (
            <AssigneePicker
              workItem={item}
              members={members}
              onChange={(assigneeUserId) => onAssigneeChange(item, assigneeUserId)}
              size="sm"
              disabled={assigneeChangePending}
            />
          )}
          {!isHidden('status') && (
            <label className="move-control">
              <span className="sr-only">Move {item.key}</span>
              <select value={item.statusId} onChange={(event) => onStatusChange(item, event.target.value)}>
                {columns.map((column) => {
                  const status = statuses.find((candidate) => candidate.id === column.statusId)
                  if (!status) return null
                  const atLimit =
                    column.statusId !== item.statusId &&
                    column.wipLimit != null &&
                    column.wipLimitMode === 'Block' &&
                    (columnCounts.get(column.statusId)?.length ?? 0) >= column.wipLimit
                  return (
                    <option key={column.statusId} value={column.statusId} disabled={atLimit}>
                      {statusMeta(status).label}
                      {atLimit ? ' (WIP limit reached)' : ''}
                    </option>
                  )
                })}
              </select>
              <ChevronDown size={14} aria-hidden="true" />
            </label>
          )}
        </div>
      </div>
    </article>
  )
}
