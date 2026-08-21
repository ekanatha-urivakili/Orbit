import { useMemo, useState } from 'react'
import { ArrowDown, ArrowUp, ChevronDown, Kanban, Pencil, Plus, User, X } from 'lucide-react'
import type { Board, BoardColumn, BoardType, TenantMembership, WipLimitMode, WorkItem, WorkItemStatusDefinition } from '../../api/types'
import { statusMeta } from './constants'
import { KanbanBoard } from './KanbanBoard'
import { SearchableSelect } from '../../components/form/SearchableSelect'
import { FilterBar } from '../../components/filters/FilterBar'
import { AssigneeAvatarFilter } from '../../components/filters/AssigneeAvatarFilter'
import { useWorkItemFilters } from '../../hooks/useWorkItemFilters'
import { getInitials } from '../../lib/initials'
import { BoardHeaderMenu } from './BoardHeaderMenu'

const UNASSIGNED_LANE = 'unassigned'
type GroupBy = 'none' | 'assignee'

const HIDE_DONE_WINDOWS_MS: Record<'OneDay' | 'OneWeek' | 'TwoWeeks' | 'OneMonth', number> = {
  OneDay: 24 * 60 * 60 * 1000,
  OneWeek: 7 * 24 * 60 * 60 * 1000,
  TwoWeeks: 14 * 24 * 60 * 60 * 1000,
  OneMonth: 30 * 24 * 60 * 60 * 1000,
}

interface MutationShape {
  isPending: boolean
  isError: boolean
  error: Error | null
}

export function BoardView({
  projectName,
  board,
  statuses,
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
  headerMenuExtras,
  hiddenFields = [],
  columnSizeMode = 'Flexible',
  hideDoneItemsAfter = 'Never',
}: {
  projectName: string
  board?: Board
  statuses: WorkItemStatusDefinition[]
  loading: boolean
  mutation: MutationShape
  onSave: (input: { name: string; type: BoardType; columns: BoardColumn[] }) => void
  workItems: WorkItem[]
  workItemsLoading: boolean
  members?: TenantMembership[]
  onStatusChange: (workItem: WorkItem, statusId: string) => void
  onReorder: (workItem: WorkItem, neighbors: { beforeId: string | null; afterId: string | null }) => void
  onOpen?: (workItem: WorkItem) => void
  onAssigneeChange?: (workItem: WorkItem, assigneeUserId: string | null) => void
  assigneeChangePending?: boolean
  headerMenuExtras?: { label: string; onClick: () => void }[]
  hiddenFields?: readonly string[]
  columnSizeMode?: 'Fixed' | 'Flexible'
  /** Hides work items in a Done-category status whose last update is older than this window (§13.5 "View settings"). */
  hideDoneItemsAfter?: 'Never' | 'OneDay' | 'OneWeek' | 'TwoWeeks' | 'OneMonth'
}) {
  const [editing, setEditing] = useState(false)
  const [groupBy, setGroupBy] = useState<GroupBy>('none')
  const [collapsedLanes, setCollapsedLanes] = useState<Record<string, boolean>>({})
  const [now] = useState(() => Date.now())
  const statusLabels = useMemo(
    () => Object.fromEntries(statuses.map((status) => [status.id, status.name])),
    [statuses],
  )
  const doneStatusIds = useMemo(
    () => new Set(statuses.filter((status) => status.category === 'Done').map((status) => status.id)),
    [statuses],
  )
  const visibleWorkItems = useMemo(() => {
    if (hideDoneItemsAfter === 'Never') return workItems
    const windowMs = HIDE_DONE_WINDOWS_MS[hideDoneItemsAfter]
    const cutoff = now - windowMs
    return workItems.filter((item) => !doneStatusIds.has(item.statusId) || new Date(item.updatedAt).getTime() >= cutoff)
  }, [workItems, hideDoneItemsAfter, doneStatusIds, now])
  const { searchTerm, setSearchTerm, fields, activeCount, clearAll, filteredItems: filteredWorkItems } = useWorkItemFilters(
    visibleWorkItems,
    members,
    statusLabels,
    {},
  )

  if (loading || !board) return <div className="board-loading">Loading board…</div>

  const toggleLane = (laneId: string) =>
    setCollapsedLanes((current) => ({ ...current, [laneId]: !current[laneId] }))

  const membersByUserId = new Map(
    members.filter((member): member is TenantMembership & { userId: string } => Boolean(member.userId)).map((member) => [member.userId, member]),
  )

  const lanes =
    groupBy === 'assignee'
      ? (() => {
          const byAssignee = new Map<string, WorkItem[]>()
          for (const item of filteredWorkItems) {
            const laneId = item.assigneeUserId ?? UNASSIGNED_LANE
            const bucket = byAssignee.get(laneId)
            if (bucket) bucket.push(item)
            else byAssignee.set(laneId, [item])
          }
          const assigned = [...byAssignee.entries()]
            .filter(([laneId]) => laneId !== UNASSIGNED_LANE)
            .sort(([a], [b]) => (membersByUserId.get(a)?.displayName ?? '').localeCompare(membersByUserId.get(b)?.displayName ?? ''))
          const unassigned = byAssignee.get(UNASSIGNED_LANE)
          return [...assigned, ...(unassigned ? [[UNASSIGNED_LANE, unassigned] as const] : [])]
        })()
      : null

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
          statuses={statuses}
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
      <div className="flex items-center justify-end gap-2">
        <button className="secondary-button" onClick={() => setEditing(true)}>
          <Pencil size={14} /> Edit board
        </button>
        {headerMenuExtras && headerMenuExtras.length > 0 && <BoardHeaderMenu items={headerMenuExtras} />}
      </div>
      {mutation.isError && <div className="error-banner">{mutation.error?.message}</div>}

      <div className="flex flex-wrap items-center gap-4 mb-6 mt-6">
        <FilterBar
          searchTerm={searchTerm}
          onSearchChange={setSearchTerm}
          searchPlaceholder="Search board"
          fields={fields}
          activeCount={activeCount}
          onClearAll={clearAll}
          betweenSearchAndFilter={
            <AssigneeAvatarFilter field={fields.find((field) => field.key === 'assignee')!} />
          }
        />
        <div className="w-40">
          <SearchableSelect
            size="sm"
            searchable={false}
            value={groupBy}
            onChange={(val) => setGroupBy(val as GroupBy)}
            options={[
              { value: 'none', label: 'No swimlanes' },
              { value: 'assignee', label: 'Group by assignee' },
            ]}
            aria-label="Group board by"
          />
        </div>
      </div>

      {lanes ? (
        <div>
          {lanes.map(([laneId, laneItems]) => {
            const member = laneId === UNASSIGNED_LANE ? undefined : membersByUserId.get(laneId)
            const laneName = laneId === UNASSIGNED_LANE ? 'Unassigned' : member?.displayName ?? 'Unknown member'
            const isCollapsed = Boolean(collapsedLanes[laneId])
            return (
              <div key={laneId} className="swimlane">
                <div className="swimlane-header" onClick={() => toggleLane(laneId)}>
                  <ChevronDown size={16} className={`swimlane-chevron${isCollapsed ? ' swimlane-chevron--collapsed' : ''}`} />
                  {laneId === UNASSIGNED_LANE ? (
                    <div className="w-6 h-6 rounded-full bg-gray-200 flex items-center justify-center text-gray-500">
                      <User size={12} />
                    </div>
                  ) : (
                    <div className="w-6 h-6 rounded-full bg-orange-500 text-white flex items-center justify-center text-xs font-bold" title={laneName}>
                      {getInitials(laneName)}
                    </div>
                  )}
                  <h3>{laneName}</h3>
                  <span className="item-count">{laneItems.length}</span>
                </div>
                {!isCollapsed && (
                  <div className="swimlane-body">
                    <KanbanBoard
                      columns={board.columns}
                      statuses={statuses}
                      workItems={laneItems}
                      loading={workItemsLoading}
                      members={members}
                      onStatusChange={onStatusChange}
                      onReorder={onReorder}
                      onOpen={onOpen}
                      onAssigneeChange={onAssigneeChange}
                      assigneeChangePending={assigneeChangePending}
                      hiddenFields={hiddenFields}
                      columnSizeMode={columnSizeMode}
                      compact
                    />
                  </div>
                )}
              </div>
            )
          })}
          {lanes.length === 0 && <div className="board-loading">No work items match the current filters.</div>}
        </div>
      ) : (
        <KanbanBoard
          columns={board.columns}
          statuses={statuses}
          workItems={filteredWorkItems}
          loading={workItemsLoading}
          members={members}
          onStatusChange={onStatusChange}
          onReorder={onReorder}
          onOpen={onOpen}
          onAssigneeChange={onAssigneeChange}
          assigneeChangePending={assigneeChangePending}
          hiddenFields={hiddenFields}
          columnSizeMode={columnSizeMode}
        />
      )}
    </>
  )
}

function BoardForm({
  initialName,
  initialType,
  initialColumns,
  statuses,
  pending,
  onCancel,
  onSubmit,
}: {
  initialName: string
  initialType: BoardType
  initialColumns: readonly BoardColumn[]
  statuses: WorkItemStatusDefinition[]
  pending: boolean
  onCancel?: () => void
  onSubmit: (input: { name: string; type: BoardType; columns: BoardColumn[] }) => void
}) {
  const [name, setName] = useState(initialName)
  const [type, setType] = useState<BoardType>(initialType)
  const [columns, setColumns] = useState<BoardColumn[]>(() => [...initialColumns].sort((a, b) => a.order - b.order))
  const statusesById = useMemo(() => new Map(statuses.map((status) => [status.id, status])), [statuses])
  const label = (statusId: string) => statusesById.get(statusId)?.name ?? 'Unknown status'

  const includedStatuses = new Set(columns.map((column) => column.statusId))
  const availableStatuses = statuses.filter((status) => !includedStatuses.has(status.id))

  function move(index: number, direction: -1 | 1) {
    setColumns((current) => {
      const target = index + direction
      if (target < 0 || target >= current.length) return current
      const next = [...current]
      ;[next[index], next[target]] = [next[target], next[index]]
      return next
    })
  }

  function updateColumn(statusId: string, patch: Partial<Pick<BoardColumn, 'wipLimit' | 'wipLimitMode'>>) {
    setColumns((current) => current.map((column) => (column.statusId === statusId ? { ...column, ...patch } : column)))
  }

  function removeColumn(statusId: string) {
    setColumns((current) => current.filter((column) => column.statusId !== statusId))
  }

  function addColumn(statusId: string) {
    setColumns((current) => [...current, { statusId, order: current.length, wipLimit: null, wipLimitMode: 'Warn' }])
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
            <li key={column.statusId} className="flex items-center gap-2 border border-gray-200 dark:border-[#394047] rounded-lg px-2.5 py-1.5 bg-white dark:bg-[#1e2327]">
              <div className="flex flex-col">
                <button type="button" disabled={index === 0} onClick={() => move(index, -1)} aria-label={`Move ${label(column.statusId)} up`}>
                  <ArrowUp size={12} />
                </button>
                <button type="button" disabled={index === columns.length - 1} onClick={() => move(index, 1)} aria-label={`Move ${label(column.statusId)} down`}>
                  <ArrowDown size={12} />
                </button>
              </div>
              <span className="flex-1 text-sm font-medium">{label(column.statusId)}</span>
              <input
                type="number"
                min={1}
                placeholder="WIP"
                value={column.wipLimit ?? ''}
                onChange={(event) =>
                  updateColumn(column.statusId, { wipLimit: event.target.value ? Number(event.target.value) : null })
                }
                className="w-16 border border-gray-300 dark:border-gray-600 rounded px-2 py-1 text-xs bg-white dark:bg-[#22272b]"
              />
              <div className="w-24">
                <SearchableSelect
                  size="sm"
                  value={column.wipLimitMode}
                  disabled={column.wipLimit == null}
                  onChange={(val) => updateColumn(column.statusId, { wipLimitMode: val as WipLimitMode })}
                  options={['Warn', 'Block']}
                  searchable={false}
                />
              </div>
              <button
                type="button"
                disabled={columns.length === 1}
                onClick={() => removeColumn(column.statusId)}
                aria-label={`Remove ${label(column.statusId)} column`}
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
                key={status.id}
                type="button"
                onClick={() => addColumn(status.id)}
                className="flex items-center gap-1 text-xs border border-dashed border-gray-300 rounded px-2 py-1 text-gray-600 hover:border-blue-400"
              >
                <Plus size={12} /> {statusMeta(status).label}
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
