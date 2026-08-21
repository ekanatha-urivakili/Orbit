import { ChevronDown, ChevronRight, AlertTriangle, GitFork } from 'lucide-react'
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

function formatDueDate(dueDateStr: string): string {
  try {
    const d = new Date(dueDateStr)
    return d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })
  } catch {
    return dueDateStr
  }
}

function PriorityIcon({ priority }: { priority: string }) {
  switch (priority.toLowerCase()) {
    case 'highest':
    case 'high':
      return (
        <span className="text-red-600 font-bold inline-flex items-center text-xs tracking-tighter" title={`Priority: ${priority}`}>
          <span className="flex flex-col -space-y-1">
            <span>⌃</span>
            <span>⌃</span>
          </span>
        </span>
      )
    case 'medium':
      return (
        <span className="text-amber-600 font-bold inline-flex items-center text-xs" title={`Priority: ${priority}`}>
          =
        </span>
      )
    case 'low':
    case 'lowest':
      return (
        <span className="text-blue-500 font-bold inline-flex items-center text-xs" title={`Priority: ${priority}`}>
          ⌄
        </span>
      )
    default:
      return null
  }
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
  hiddenFields?: readonly string[]
}) {
  const isHidden = (field: string) => hiddenFields.includes(field)
  const currentStatus = statuses.find((s) => s.id === item.statusId)
  const parent = item.parentId ? allWorkItems.find((candidate) => candidate.id === item.parentId) : undefined
  const subtasks = allWorkItems.filter((candidate) => candidate.parentId === item.id)
  const doneSubtasks = subtasks.filter((candidate) =>
    statuses.find((status) => status.id === candidate.statusId)?.category === 'Done',
  ).length

  const isOverdue = item.dueDate ? new Date(item.dueDate) < new Date() : false

  return (
    <article
      className={`work-card bg-white dark:bg-[#22272b] border border-gray-200 dark:border-[#394047] rounded-lg p-3.5 shadow-sm hover:shadow-md transition-shadow cursor-grab ${
        dragging ? 'opacity-40 cursor-grabbing' : ''
      }`}
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
      {/* Card Summary / Title */}
      <a
        href={`/browse/${item.key}`}
        className={`block text-sm font-semibold text-gray-900 dark:text-gray-100 hover:text-blue-600 mb-2 ${
          onOpen ? 'cursor-pointer' : ''
        }`}
        onClick={(event) => {
          if (!onOpen) return
          event.preventDefault()
          onOpen(item)
        }}
      >
        <h4>{item.summary}</h4>
      </a>

      {/* Status Pill Badge */}
      {!isHidden('status') && currentStatus && (
        <div className="mb-2.5">
          <span
            className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold ${
              currentStatus.category === 'Done'
                ? 'bg-green-100 text-green-800 dark:bg-green-950/50 dark:text-green-300'
                : currentStatus.category === 'InProgress'
                ? 'bg-blue-100 text-blue-800 dark:bg-blue-950/50 dark:text-blue-300'
                : 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300'
            }`}
          >
            {statusMeta(currentStatus).label}
          </span>
        </div>
      )}

      {/* Due Date Row */}
      {!isHidden('dueDate') && item.dueDate && (
        <div className="mb-2 text-xs">
          <span className="text-[11px] text-gray-500 dark:text-gray-400 block mb-0.5">Due date</span>
          <div className="flex items-center gap-1 font-medium text-gray-800 dark:text-gray-200">
            <span>{formatDueDate(item.dueDate)}</span>
            {isOverdue && <AlertTriangle size={13} className="text-red-500 shrink-0" />}
          </div>
        </div>
      )}

      {/* Parent Epic Chip */}
      {!isHidden('parent') && parent && (
        <div className="mb-2.5 text-xs">
          <span className="text-[11px] text-gray-500 dark:text-gray-400 block mb-0.5">Parent</span>
          <div
            className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded bg-gray-100 dark:bg-[#1e2327] border border-gray-200 dark:border-[#394047] text-gray-800 dark:text-gray-200 text-xs font-medium"
            title={`Parent: ${parent.key} ${parent.summary}`}
          >
            <span className="w-2.5 h-2.5 rounded-sm bg-teal-600 shrink-0" />
            <span className="truncate max-w-[120px]">{parent.summary || parent.key}</span>
            <ChevronDown size={12} className="text-gray-500 shrink-0" />
          </div>
        </div>
      )}

      {/* Subtasks summary row */}
      {!isHidden('subtaskSummary') && subtasks.length > 0 && (
        <div className="mb-3">
          <button
            type="button"
            className="w-full flex items-center justify-between px-2.5 py-1 rounded bg-gray-50 dark:bg-[#1e2327] border border-gray-200 dark:border-[#394047] text-xs text-gray-700 dark:text-gray-300 hover:bg-gray-100"
            onClick={(e) => {
              if (onOpen) {
                e.preventDefault()
                onOpen(item)
              }
            }}
          >
            <span className="flex items-center gap-1.5">
              <GitFork size={13} className="text-gray-500" />
              <span>Subtasks</span>
              <span className="font-semibold bg-gray-200 dark:bg-gray-700 px-1 rounded text-[10px]">
                {doneSubtasks}/{subtasks.length}
              </span>
            </span>
            <ChevronRight size={13} className="text-gray-400" />
          </button>
        </div>
      )}

      {/* Labels */}
      {!isHidden('labels') && item.labels.length > 0 && (
        <div className="flex flex-wrap gap-1 mb-2.5">
          {item.labels.map((label) => (
            <span key={label} className="px-1.5 py-0.5 rounded bg-blue-50 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300 text-[10px] font-medium">
              {label}
            </span>
          ))}
        </div>
      )}

      {/* Card Footer: Left (Type + Key), Right (Story Points, Priority, Assignee) */}
      <div className="flex items-center justify-between pt-2 border-t border-gray-100 dark:border-[#394047] text-xs">
        {/* Left: Type icon & Key */}
        <div className="flex items-center gap-1.5">
          <WorkItemTypeIcon type={item.type} size={15} />
          {!isHidden('workItemKey') && (
            <a
              href={`/browse/${item.key}`}
              className="font-medium text-gray-700 dark:text-gray-300 hover:text-blue-600 hover:underline uppercase text-[11px]"
              onClick={(event) => {
                if (!onOpen) return
                event.preventDefault()
                onOpen(item)
              }}
            >
              {item.key}
            </a>
          )}
        </div>

        {/* Right side items */}
        <div className="flex items-center gap-2">
          {!isHidden('storyPointEstimate') && item.storyPoints != null && (
            <span
              className="inline-flex items-center justify-center px-1.5 py-0.5 rounded bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300 text-[11px] font-semibold min-w-[18px]"
              title="Story points"
            >
              {item.storyPoints}
            </span>
          )}
          {!isHidden('priority') && <PriorityIcon priority={item.priority} />}
          {!isHidden('updated') && (
            <span className="text-[10px] text-gray-400 whitespace-nowrap" title={`Updated ${item.updatedAt}`}>
              {relativeTime(item.updatedAt)}
            </span>
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
        </div>
      </div>
    </article>
  )
}
