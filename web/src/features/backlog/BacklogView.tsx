import { useState } from 'react'
import { ChevronDown, MoreHorizontal, CheckSquare, Search, Filter, LineChart, SlidersHorizontal, Plus, Calendar, User, CornerDownLeft, ArrowLeftRight } from 'lucide-react'
import { useCreateWorkItem } from '../../hooks/useCreateWorkItem'
import { groupWorkItemsByStatus } from '../../board'
import { getInitials } from '../../lib/initials'
import { SprintReportDialog } from './SprintReportDialog'
import type { Sprint, TenantMembership, WorkItem } from '../../api/types'

const trackedStatuses: WorkItem['status'][] = ['Backlog', 'InProgress', 'Done']

function matchesSearch(item: WorkItem, term: string): boolean {
  if (!term) return true
  const haystack = `${item.key} ${item.summary}`.toLowerCase()
  return haystack.includes(term.toLowerCase())
}

interface BacklogViewProps {
  workItems: WorkItem[]
  projectId: string
  members: TenantMembership[]
  sprints: Sprint[]
  sprintsLoading: boolean
  onCreateSprint: (name: string) => void
  onStartSprint: (sprint: Sprint) => void
  onCompleteSprint: (sprint: Sprint, rolloverTargetSprintId: string | null) => void
  onReopenSprint: (sprint: Sprint) => void
  onAssignToSprint: (workItemId: string, sprintId: string) => void
  onRemoveFromSprint: (workItemId: string) => void
  onOpenWorkItem: (workItem: WorkItem) => void
  error: string | null
}

export function BacklogView({
  workItems,
  projectId,
  members,
  sprints,
  sprintsLoading,
  onCreateSprint,
  onStartSprint,
  onCompleteSprint,
  onReopenSprint,
  onAssignToSprint,
  onRemoveFromSprint,
  onOpenWorkItem,
  error,
}: BacklogViewProps) {
  const activeSprint = sprints.find((sprint) => sprint.state === 'Active')
  const futureSprints = sprints.filter((sprint) => sprint.state === 'Future')
  const reopenedSprints = sprints.filter((sprint) => sprint.state === 'Reopened')
  const closingSprints = sprints.filter((sprint) => sprint.state === 'Closing')
  const closedSprints = sprints.filter((sprint) => sprint.state === 'Closed')
  // Items in a still-Closing sprint remain its current memberships until each is processed, so they
  // must stay out of the backlog view even though a Closing sprint can't accept new assignments.
  const openSprints = [
    ...(activeSprint ? [activeSprint] : []),
    ...reopenedSprints,
    ...closingSprints,
    ...futureSprints,
  ]
  const assignableSprints = [
    ...(activeSprint ? [activeSprint] : []),
    ...reopenedSprints,
    ...futureSprints,
  ]
  const assignedItemIds = new Set(openSprints.flatMap((sprint) => sprint.workItemIds))
  const workItemsById = new Map(workItems.map((item) => [item.id, item]))
  const membersByUserId = new Map(
    members.filter((member): member is TenantMembership & { userId: string } => Boolean(member.userId)).map((member) => [member.userId, member]),
  )

  const [inlineCreateOpen, setInlineCreateOpen] = useState(false)
  const [inlineSummary, setInlineSummary] = useState('')
  const [inlineDueDateOpen, setInlineDueDateOpen] = useState(false)
  const [inlineAssigneeOpen, setInlineAssigneeOpen] = useState(false)
  const [inlineAssigneeUserId, setInlineAssigneeUserId] = useState<string | null>(null)
  const [rolloverTargets, setRolloverTargets] = useState<Record<string, string>>({})
  const [closedSectionOpen, setClosedSectionOpen] = useState(false)
  const [reportSprint, setReportSprint] = useState<Sprint | null>(null)
  const [searchTerm, setSearchTerm] = useState('')
  const [assigneeFilter, setAssigneeFilter] = useState<string | null>(null)
  const [filterMenuOpen, setFilterMenuOpen] = useState(false)

  const matchesFilters = (item: WorkItem) =>
    matchesSearch(item, searchTerm) && (assigneeFilter === null || item.assigneeUserId === assigneeFilter)

  const backlogItems = workItems.filter((item) => !assignedItemIds.has(item.id) && matchesFilters(item))
  const backlogStatusCounts = groupWorkItemsByStatus(trackedStatuses, backlogItems)
  const assigneeFilterMember = members.find((member) => member.userId === assigneeFilter)

  const mutation = useCreateWorkItem(projectId)

  const handleInlineCreate = (e?: React.FormEvent) => {
    e?.preventDefault()
    if (!inlineSummary.trim() || mutation.isPending) return

    mutation.mutate(
      {
        projectId,
        summary: inlineSummary,
        description: null,
        type: 'Task',
        priority: 'Medium',
        assigneeUserId: inlineAssigneeUserId,
      },
      {
        onSuccess: () => {
          setInlineSummary('')
          setInlineCreateOpen(false)
          setInlineAssigneeUserId(null)
        },
      },
    )
  }

  const renderAssigneeAvatar = (item: WorkItem) => {
    const member = item.assigneeUserId ? membersByUserId.get(item.assigneeUserId) : undefined
    return member ? (
      <div
        className="w-6 h-6 rounded-full bg-orange-500 text-white flex items-center justify-center text-xs font-bold border border-white"
        title={member.displayName ?? undefined}
      >
        {getInitials(member.displayName ?? undefined)}
      </div>
    ) : (
      <div className="w-6 h-6 rounded-full bg-gray-200 flex items-center justify-center text-gray-500 border border-white" title="Unassigned">
        <User size={12} />
      </div>
    )
  }

  return (
    <div className="p-8 max-w-5xl">
      {error && <div className="error-banner mb-4">{error}</div>}
      <div className="flex items-center gap-4 mb-6">
        <div className="relative">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-500" />
          <input
            type="text"
            value={searchTerm}
            onChange={(event) => setSearchTerm(event.target.value)}
            placeholder="Search backlog"
            className="pl-9 pr-4 py-1.5 border border-gray-300 rounded hover:bg-gray-50 focus:outline-none focus:border-blue-500 text-sm w-64"
          />
        </div>
        <div className="relative">
          <button
            onClick={() => setFilterMenuOpen(!filterMenuOpen)}
            className="flex items-center gap-2 px-3 py-1.5 hover:bg-gray-100 rounded border border-transparent hover:border-gray-200 text-sm font-medium text-gray-700"
          >
            {assigneeFilterMember ? (
              <span className="w-6 h-6 rounded-full bg-orange-500 text-white flex items-center justify-center text-[10px] font-bold">
                {getInitials(assigneeFilterMember.displayName ?? undefined)}
              </span>
            ) : (
              <Filter size={16} />
            )}
            {assigneeFilterMember?.displayName ?? 'Filter'}
            <ChevronDown size={14} />
          </button>
          {filterMenuOpen && (
            <div className="absolute left-0 top-full mt-1 w-56 bg-white border border-gray-200 shadow-xl rounded-lg py-1 z-50">
              <div className="px-3 py-1 text-xs font-semibold text-gray-500 uppercase">Assignee</div>
              <button
                onClick={() => { setAssigneeFilter(null); setFilterMenuOpen(false) }}
                className={`w-full text-left px-3 py-2 text-sm hover:bg-gray-100 flex items-center gap-2 ${assigneeFilter === null ? 'bg-blue-50 text-blue-700' : ''}`}
              >
                <div className="w-5 h-5 rounded-full bg-gray-200 text-gray-500 flex items-center justify-center text-xs"><User size={12} /></div> All assignees
              </button>
              {members.filter((member) => member.userId).map((member) => (
                <button
                  key={member.id}
                  onClick={() => { setAssigneeFilter(member.userId); setFilterMenuOpen(false) }}
                  className={`w-full text-left px-3 py-2 text-sm hover:bg-gray-100 flex items-center gap-2 ${assigneeFilter === member.userId ? 'bg-blue-50 text-blue-700' : ''}`}
                >
                  <div className="w-5 h-5 rounded-full bg-orange-500 text-white flex items-center justify-center text-xs font-bold">
                    {getInitials(member.displayName ?? undefined)}
                  </div>
                  {member.displayName ?? 'Unnamed member'}
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="ml-auto flex items-center gap-2">
          <button className="p-1.5 hover:bg-gray-100 rounded text-gray-600"><LineChart size={18} /></button>
          <button className="p-1.5 hover:bg-gray-100 rounded text-gray-600"><SlidersHorizontal size={18} /></button>
          <button className="p-1.5 hover:bg-gray-100 rounded text-gray-600"><MoreHorizontal size={18} /></button>
        </div>
      </div>

      {!sprintsLoading && openSprints.map((sprint) => {
        const sprintItems = sprint.workItemIds
          .map((id) => workItemsById.get(id))
          .filter((item): item is WorkItem => Boolean(item))
          .filter(matchesFilters)
        const sprintStatusCounts = groupWorkItemsByStatus(trackedStatuses, sprintItems)

        return (
          <div key={sprint.id} className="bg-gray-50 rounded-lg border border-gray-200 mb-8 overflow-hidden">
            <div className="flex items-center justify-between px-4 py-3 border-b border-gray-200">
              <div className="flex items-center gap-2">
                <button className="p-1 hover:bg-gray-200 rounded"><ChevronDown size={18} className="text-gray-600" /></button>
                <h2 className="font-bold text-gray-900 text-sm">{sprint.name}</h2>
                <span className="text-sm text-gray-500 ml-2">({sprintItems.length} work items)</span>
              </div>
              <div className="flex items-center gap-3">
                <div className="flex items-center text-xs font-semibold gap-1">
                  <span className="bg-gray-200 text-gray-600 px-2 py-0.5 rounded-full">{sprintStatusCounts.get('Backlog')?.length ?? 0}</span>
                  <span className="bg-blue-100 text-blue-700 px-2 py-0.5 rounded-full">{sprintStatusCounts.get('InProgress')?.length ?? 0}</span>
                  <span className="bg-green-100 text-green-700 px-2 py-0.5 rounded-full">{sprintStatusCounts.get('Done')?.length ?? 0}</span>
                </div>
                {sprint.state === 'Future' && (
                  <button
                    onClick={() => onStartSprint(sprint)}
                    className="px-3 py-1 bg-gray-100 hover:bg-gray-200 text-gray-700 font-medium text-sm rounded"
                  >
                    Start sprint
                  </button>
                )}
                {(sprint.state === 'Active' || sprint.state === 'Reopened') && (
                  <>
                    <select
                      value={rolloverTargets[sprint.id] ?? ''}
                      onChange={(e) => setRolloverTargets((current) => ({ ...current, [sprint.id]: e.target.value }))}
                      className="text-xs border border-gray-300 rounded px-2 py-1 bg-white text-gray-600"
                      aria-label="Move incomplete items to"
                    >
                      <option value="">Return incomplete items to backlog</option>
                      {futureSprints
                        .filter((candidate) => candidate.id !== sprint.id)
                        .map((candidate) => (
                          <option key={candidate.id} value={candidate.id}>
                            Move incomplete items to {candidate.name}
                          </option>
                        ))}
                    </select>
                    <button
                      onClick={() => onCompleteSprint(sprint, rolloverTargets[sprint.id] || null)}
                      className="px-3 py-1 bg-gray-100 hover:bg-gray-200 text-gray-700 font-medium text-sm rounded"
                    >
                      Complete sprint
                    </button>
                  </>
                )}
                {sprint.state === 'Closing' && (
                  <button
                    onClick={() => onCompleteSprint(sprint, null)}
                    className="px-3 py-1 bg-amber-100 hover:bg-amber-200 text-amber-800 font-medium text-sm rounded"
                  >
                    Resume closing
                  </button>
                )}
                {sprint.state !== 'Future' && (
                  <button
                    onClick={() => setReportSprint(sprint)}
                    className="flex items-center gap-1 px-3 py-1 hover:bg-gray-200 text-gray-700 font-medium text-sm rounded"
                    title="View sprint report"
                  >
                    <LineChart size={14} /> Report
                  </button>
                )}
                <button className="p-1 hover:bg-gray-200 rounded text-gray-600"><MoreHorizontal size={16} /></button>
              </div>
            </div>
            <div className="bg-white">
              {sprintItems.map((item) => (
                <div key={item.id} className="flex items-center gap-3 px-4 py-2 border-b border-gray-100 hover:bg-blue-50 group cursor-pointer transition-colors">
                  <CheckSquare size={16} className="text-blue-500 flex-shrink-0" />
                  <span className="text-sm text-gray-500 w-16">{item.key}</span>
                  <span
                className="text-sm text-gray-900 flex-1 truncate hover:underline"
                onClick={() => onOpenWorkItem(item)}
              >
                {item.summary}
              </span>

                  <div className="flex items-center gap-3 opacity-0 group-hover:opacity-100 transition-opacity">
                    <button
                      onClick={() => onRemoveFromSprint(item.id)}
                      className="p-1 hover:bg-gray-200 rounded text-gray-600"
                      aria-label="Move to backlog"
                      title="Move to backlog"
                    >
                      <ArrowLeftRight size={16} />
                    </button>
                  </div>

                  <div className="flex items-center gap-3 ml-4">
                    <div className="px-2 py-1 bg-gray-100 rounded text-xs font-medium text-gray-600 uppercase flex items-center gap-1">
                      {item.status === 'Backlog' ? 'To Do' : item.status} <ChevronDown size={14} />
                    </div>
                    {renderAssigneeAvatar(item)}
                  </div>
                </div>
              ))}
              {sprintItems.length === 0 && (
                <div className="px-4 py-6 text-center text-gray-500 text-sm border-b border-gray-100">
                  Drag or move backlog items in to plan this sprint.
                </div>
              )}
            </div>
          </div>
        )
      })}

      {closedSprints.length > 0 && (
        <div className="bg-gray-50 rounded-lg border border-gray-200 mb-8 overflow-hidden">
          <button
            onClick={() => setClosedSectionOpen(!closedSectionOpen)}
            className="w-full flex items-center gap-2 px-4 py-3 text-left"
          >
            <ChevronDown size={18} className={`text-gray-600 transition-transform ${closedSectionOpen ? '' : '-rotate-90'}`} />
            <h2 className="font-bold text-gray-900 text-sm">Closed sprints</h2>
            <span className="text-sm text-gray-500 ml-2">({closedSprints.length})</span>
          </button>
          {closedSectionOpen && (
            <div className="bg-white border-t border-gray-200">
              {closedSprints.map((sprint) => (
                <div key={sprint.id} className="flex items-center justify-between px-4 py-2 border-b border-gray-100 last:border-b-0">
                  <span className="text-sm text-gray-700">{sprint.name}</span>
                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => setReportSprint(sprint)}
                      className="flex items-center gap-1 px-3 py-1 hover:bg-gray-200 text-gray-700 font-medium text-sm rounded"
                    >
                      <LineChart size={14} /> Report
                    </button>
                    <button
                      onClick={() => onReopenSprint(sprint)}
                      className="px-3 py-1 bg-gray-100 hover:bg-gray-200 text-gray-700 font-medium text-sm rounded"
                    >
                      Reopen
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      <div className="bg-gray-50 rounded-lg border border-gray-200 overflow-hidden">
        <div className="flex items-center justify-between px-4 py-3 border-b border-gray-200">
          <div className="flex items-center gap-2">
            <button className="p-1 hover:bg-gray-200 rounded"><ChevronDown size={18} className="text-gray-600" /></button>
            <h2 className="font-bold text-gray-900 text-sm">Backlog</h2>
            <span className="text-sm text-gray-500 ml-2">({backlogItems.length} work items)</span>
          </div>
          <div className="flex items-center gap-3">
             <div className="flex items-center text-xs font-semibold gap-1">
              <span className="bg-gray-200 text-gray-600 px-2 py-0.5 rounded-full">{backlogStatusCounts.get('Backlog')?.length ?? 0}</span>
              <span className="bg-blue-100 text-blue-700 px-2 py-0.5 rounded-full">{backlogStatusCounts.get('InProgress')?.length ?? 0}</span>
              <span className="bg-green-100 text-green-700 px-2 py-0.5 rounded-full">{backlogStatusCounts.get('Done')?.length ?? 0}</span>
            </div>
            <button
              onClick={() => onCreateSprint(`Sprint ${sprints.length + 1}`)}
              className="px-3 py-1 bg-gray-100 hover:bg-gray-200 text-gray-700 font-medium text-sm rounded"
            >
              Create sprint
            </button>
          </div>
        </div>
        <div className="bg-white">
          {backlogItems.map((item) => (
            <div key={item.id} className="flex items-center gap-3 px-4 py-2 border-b border-gray-100 hover:bg-blue-50 group cursor-pointer transition-colors">
              <CheckSquare size={16} className="text-blue-500 flex-shrink-0" />
              <span className="text-sm text-gray-500 w-16">{item.key}</span>
              <span
                className="text-sm text-gray-900 flex-1 truncate hover:underline"
                onClick={() => onOpenWorkItem(item)}
              >
                {item.summary}
              </span>

              {assignableSprints.length > 0 && (
                <select
                  onChange={(e) => {
                    if (e.target.value) onAssignToSprint(item.id, e.target.value)
                  }}
                  value=""
                  className="opacity-0 group-hover:opacity-100 transition-opacity text-xs border border-gray-300 rounded px-2 py-1 bg-white text-gray-600"
                  aria-label="Move to sprint"
                >
                  <option value="" disabled>Move to sprint</option>
                  {assignableSprints.map((sprint) => (
                    <option key={sprint.id} value={sprint.id}>{sprint.name}</option>
                  ))}
                </select>
              )}

              <div className="flex items-center gap-3 ml-4">
                <div className="px-2 py-1 bg-blue-100 rounded text-xs font-medium text-blue-800 uppercase flex items-center gap-1">
                  {item.status === 'Backlog' ? 'To Do' : item.status} <ChevronDown size={14} />
                </div>
                {renderAssigneeAvatar(item)}
              </div>
            </div>
          ))}
          {backlogItems.length === 0 && (
             <div className="px-4 py-8 text-center text-gray-500 text-sm border-b border-gray-100">
               Your backlog is empty.
             </div>
          )}
          {inlineCreateOpen ? (
            <div className="px-4 py-2 border-2 border-blue-500 m-[-1px] relative z-10 bg-white flex items-center gap-3">
              <input type="checkbox" className="w-3 h-3 text-blue-600 border-gray-300 rounded" />
              <ChevronDown size={16} className="text-gray-400" />
              <form onSubmit={handleInlineCreate} className="flex-1">
                <input
                  autoFocus
                  type="text"
                  value={inlineSummary}
                  onChange={(e) => setInlineSummary(e.target.value)}
                  placeholder="Describe what needs to be done."
                  className="w-full text-sm text-gray-900 focus:outline-none placeholder-gray-400"
                />
              </form>
              <div className="flex items-center gap-2">
                <div className="relative">
                  <button
                    onClick={() => { setInlineDueDateOpen(!inlineDueDateOpen); setInlineAssigneeOpen(false) }}
                    className="p-1 hover:bg-gray-100 rounded text-gray-500"
                  >
                    <Calendar size={18} />
                  </button>
                  {inlineDueDateOpen && (
                    <div className="absolute right-0 top-full mt-1 w-64 bg-white border border-gray-200 shadow-xl rounded-lg p-3 z-50">
                      <div className="text-xs font-semibold text-gray-700 mb-2">Due date</div>
                      <input type="date" className="w-full border border-gray-300 rounded px-2 py-1 text-sm mb-2 focus:outline-none focus:border-blue-500" />
                    </div>
                  )}
                </div>

                <div className="relative">
                  <button
                    onClick={() => { setInlineAssigneeOpen(!inlineAssigneeOpen); setInlineDueDateOpen(false) }}
                    className="p-1 hover:bg-gray-100 rounded text-gray-500"
                  >
                    <User size={18} />
                  </button>
                  {inlineAssigneeOpen && (
                    <div className="absolute right-0 top-full mt-1 w-48 bg-white border border-gray-200 shadow-xl rounded-lg py-1 z-50">
                      <button
                        onClick={() => { setInlineAssigneeUserId(null); setInlineAssigneeOpen(false) }}
                        className={`w-full text-left px-3 py-2 text-sm hover:bg-gray-100 flex items-center gap-2 ${inlineAssigneeUserId === null ? 'bg-blue-50 text-blue-700' : ''}`}
                      >
                        <div className="w-5 h-5 rounded-full bg-gray-200 text-gray-500 flex items-center justify-center text-xs"><User size={12} /></div> Unassigned
                      </button>
                      {members.filter((member) => member.userId).map((member) => (
                        <button
                          key={member.id}
                          onClick={() => { setInlineAssigneeUserId(member.userId); setInlineAssigneeOpen(false) }}
                          className={`w-full text-left px-3 py-2 text-sm hover:bg-gray-100 flex items-center gap-2 ${inlineAssigneeUserId === member.userId ? 'bg-blue-50 text-blue-700' : ''}`}
                        >
                          <div className="w-5 h-5 rounded-full bg-orange-500 text-white flex items-center justify-center text-xs font-bold">
                            {getInitials(member.displayName ?? undefined)}
                          </div>
                          {member.displayName ?? 'Unnamed member'}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
                <button
                  onClick={handleInlineCreate}
                  disabled={!inlineSummary.trim()}
                  className="px-3 py-1 flex items-center gap-1 bg-gray-100 hover:bg-gray-200 text-gray-500 font-medium text-sm rounded disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  Create <CornerDownLeft size={14} />
                </button>
              </div>
            </div>
          ) : (
            <button
              onClick={() => setInlineCreateOpen(true)}
              className="w-full px-4 py-2 hover:bg-gray-50 text-left text-sm font-medium text-gray-600 flex items-center gap-2"
            >
              <Plus size={16} /> Create
            </button>
          )}
        </div>
      </div>

      {reportSprint && (
        <SprintReportDialog
          sprintId={reportSprint.id}
          sprintName={reportSprint.name}
          onClose={() => setReportSprint(null)}
        />
      )}
    </div>
  )
}
