import { useState } from 'react'
import { ArrowDown, ArrowUp, Kanban, Pencil, Plus, X } from 'lucide-react'
import type { Board, BoardColumn, BoardType, TenantMembership, WipLimitMode, WorkItem, WorkItemStatus } from '../../api/types'
import { allStatuses, statusMeta } from './constants'
import { KanbanBoard } from './KanbanBoard'
import { SearchableSelect } from '../../components/form/SearchableSelect'
import { FilterBar } from '../../components/filters/FilterBar'
import { useWorkItemFilters } from '../../hooks/useWorkItemFilters'

const statusLabels = Object.fromEntries(
  Object.entries(statusMeta).map(([status, meta]) => [status, meta.label]),
) as Record<WorkItemStatus, string>

interface MutationShape {
  isPending: boolean
  isError: boolean
  error: Error | null
}

export function BoardView({
  projectName,
  board,
  loading,
  mutation,
  onSave,
  workItems,
  workItemsLoading,
  members = [],
  onStatusChange,
  onReorder,
  onOpen,
  onAssigneeChange,
  assigneeChangePending = false,
}: {
  projectName: string
  board?: Board
  loading: boolean
  mutation: MutationShape
  onSave: (input: { name: string; type: BoardType; columns: BoardColumn[] }) => void
  workItems: WorkItem[]
  workItemsLoading: boolean
  members?: TenantMembership[]
  onStatusChange: (workItem: WorkItem, status: WorkItemStatus) => void
  onReorder: (workItem: WorkItem, neighbors: { beforeId: string | null; afterId: string | null }) => void
  onOpen?: (workItem: WorkItem) => void
  onAssigneeChange?: (workItem: WorkItem, assigneeUserId: string | null) => void
  assigneeChangePending?: boolean
}) {
  const [editing, setEditing] = useState(false)
  const { searchTerm, setSearchTerm, fields, activeCount, clearAll, filteredItems: filteredWorkItems } = useWorkItemFilters(
    workItems,
    members,
    statusLabels,
    {},
  )

  if (loading || !board) return <div className="board-loading">Loading board…</div>

  const exists = board.version > 0

  if (!exists || editing) {
    return (
      <div className="flex flex-col items-center py-16 px-8 border border-gray-100 rounded-lg bg-white max-w-xl">
        <div className="p-4 bg-gray-100 rounded-full mb-6 text-gray-500">
          <Kanban size={28} />
        </div>
        <h3 className="text-xl font-bold text-gray-900 mb-2">{exists ? 'Edit board' : 'Set up your board'}</h3>
        {!exists && (
          <p className="text-gray-500 text-center max-w-md text-sm mb-6">
            Name your board and choose Kanban or Scrum. You can change this later.
          </p>
        )}
        <BoardForm
          initialName={exists ? board.name : `${projectName} Board`}
          initialType={board.type}
          initialColumns={board.columns}
          pending={mutation.isPending}
          onCancel={exists ? () => setEditing(false) : undefined}
          onSubmit={(input) => {
            onSave(input)
            setEditing(false)
          }}
        />
        {mutation.isError && <p className="form-error mt-3">{mutation.error?.message}</p>}
      </div>
    )
  }

  return (
    <>
      <div className="board-header">
        <div>
          <p className="eyebrow">Board</p>
          <div className="title-line">
            <h1>{board.name}</h1>
          </div>
          <p className="subtitle">{board.type} board</p>
        </div>
        <div className="flex items-center gap-4">
          <button className="secondary-button" onClick={() => setEditing(true)}>
            <Pencil size={14} /> Edit board
          </button>
        </div>
      </div>
      {mutation.isError && <div className="error-banner">{mutation.error?.message}</div>}

      <div className="flex flex-wrap items-center gap-4 mb-6 mt-4">
        <FilterBar
          searchTerm={searchTerm}
          onSearchChange={setSearchTerm}
          searchPlaceholder="Search board"
          fields={fields}
          activeCount={activeCount}
          onClearAll={clearAll}
        />
      </div>

      <KanbanBoard
        columns={board.columns}
        workItems={filteredWorkItems}
        loading={workItemsLoading}
        members={members}
        onStatusChange={onStatusChange}
        onReorder={onReorder}
        onOpen={onOpen}
        onAssigneeChange={onAssigneeChange}
        assigneeChangePending={assigneeChangePending}
      />
    </>
  )
}

function BoardForm({
  initialName,
  initialType,
  initialColumns,
  pending,
  onCancel,
  onSubmit,
}: {
  initialName: string
  initialType: BoardType
  initialColumns: readonly BoardColumn[]
  pending: boolean
  onCancel?: () => void
  onSubmit: (input: { name: string; type: BoardType; columns: BoardColumn[] }) => void
}) {
  const [name, setName] = useState(initialName)
  const [type, setType] = useState<BoardType>(initialType)
  const [columns, setColumns] = useState<BoardColumn[]>(() => [...initialColumns].sort((a, b) => a.order - b.order))

  const includedStatuses = new Set(columns.map((column) => column.status))
  const availableStatuses = allStatuses.filter((status) => !includedStatuses.has(status))

  function move(index: number, direction: -1 | 1) {
    setColumns((current) => {
      const target = index + direction
      if (target < 0 || target >= current.length) return current
      const next = [...current]
      ;[next[index], next[target]] = [next[target], next[index]]
      return next
    })
  }

  function updateColumn(status: WorkItemStatus, patch: Partial<Pick<BoardColumn, 'wipLimit' | 'wipLimitMode'>>) {
    setColumns((current) => current.map((column) => (column.status === status ? { ...column, ...patch } : column)))
  }

  function removeColumn(status: WorkItemStatus) {
    setColumns((current) => current.filter((column) => column.status !== status))
  }

  function addColumn(status: WorkItemStatus) {
    setColumns((current) => [...current, { status, order: current.length, wipLimit: null, wipLimitMode: 'Warn' }])
  }

  return (
    <form
      className="flex flex-col gap-3 w-full max-w-md"
      onSubmit={(event) => {
        event.preventDefault()
        if (!name.trim() || columns.length === 0) return
        onSubmit({
          name: name.trim(),
          type,
          columns: columns.map((column, index) => ({ ...column, order: index })),
        })
      }}
    >
      <input
        type="text"
        required
        minLength={2}
        maxLength={120}
        value={name}
        onChange={(event) => setName(event.target.value)}
        className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-blue-500 bg-white dark:bg-[#22272b] dark:border-gray-600"
      />
      <SearchableSelect
        value={type}
        onChange={(val) => setType(val as BoardType)}
        options={['Kanban', 'Scrum']}
        searchable={false}
      />

      <div className="text-left">
        <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Columns</p>
        <ul className="flex flex-col gap-2">
          {columns.map((column, index) => (
            <li key={column.status} className="flex items-center gap-2 border border-gray-200 dark:border-[#394047] rounded-lg px-2.5 py-1.5 bg-white dark:bg-[#1e2327]">
              <div className="flex flex-col">
                <button type="button" disabled={index === 0} onClick={() => move(index, -1)} aria-label={`Move ${statusMeta[column.status].label} up`}>
                  <ArrowUp size={12} />
                </button>
                <button type="button" disabled={index === columns.length - 1} onClick={() => move(index, 1)} aria-label={`Move ${statusMeta[column.status].label} down`}>
                  <ArrowDown size={12} />
                </button>
              </div>
              <span className="flex-1 text-sm font-medium">{statusMeta[column.status].label}</span>
              <input
                type="number"
                min={1}
                placeholder="WIP"
                value={column.wipLimit ?? ''}
                onChange={(event) =>
                  updateColumn(column.status, { wipLimit: event.target.value ? Number(event.target.value) : null })
                }
                className="w-16 border border-gray-300 dark:border-gray-600 rounded px-2 py-1 text-xs bg-white dark:bg-[#22272b]"
              />
              <div className="w-24">
                <SearchableSelect
                  size="sm"
                  value={column.wipLimitMode}
                  disabled={column.wipLimit == null}
                  onChange={(val) => updateColumn(column.status, { wipLimitMode: val as WipLimitMode })}
                  options={['Warn', 'Block']}
                  searchable={false}
                />
              </div>
              <button
                type="button"
                disabled={columns.length === 1}
                onClick={() => removeColumn(column.status)}
                aria-label={`Remove ${statusMeta[column.status].label} column`}
              >
                <X size={14} />
              </button>
            </li>
          ))}
        </ul>
        {availableStatuses.length > 0 && (
          <div className="flex flex-wrap gap-2 mt-2">
            {availableStatuses.map((status) => (
              <button
                key={status}
                type="button"
                onClick={() => addColumn(status)}
                className="flex items-center gap-1 text-xs border border-dashed border-gray-300 rounded px-2 py-1 text-gray-600 hover:border-blue-400"
              >
                <Plus size={12} /> {statusMeta[status].label}
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="flex items-center gap-2">
        <button type="submit" disabled={pending || columns.length === 0} className="primary-button">
          {pending ? 'Saving…' : 'Save board'}
        </button>
        {onCancel && (
          <button type="button" onClick={onCancel} className="secondary-button">
            Cancel
          </button>
        )}
      </div>
    </form>
  )
}
