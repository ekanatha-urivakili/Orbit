import { useState, type ReactNode } from 'react'
import {
  CheckCircle2,
  Edit3,
  PlusSquare,
  Calendar,
  Filter,
  ChevronDown,
  Info,
  User,
  Maximize2,
} from 'lucide-react'
import { groupWorkItemsByStatus } from '../../board'
import { statusMeta } from '../board/constants'
import { getInitials } from '../../lib/initials'
import { WorkItemTypeIcon } from '../workitems/typeIcons'
import type { Profile, TenantMembership, WorkItem, Priority, WorkItemStatusDefinition } from '../../api/types'

const toneColors: Record<string, string> = {
  slate: '#6b778c',
  cyan: '#00a3bf',
  blue: '#0052cc',
  amber: '#ff991f',
  green: '#36b37e',
  red: '#de350b',
}

const priorityColors: Record<Priority, string> = {
  Highest: '#de350b',
  High: '#ff5630',
  Medium: '#ffab00',
  Low: '#0065ff',
  Lowest: '#6554c0',
}

const circumference = 251.2
const sevenDaysMs = 7 * 24 * 60 * 60 * 1000

function formatRelativeTime(iso: string): string {
  const diffMs = Date.now() - new Date(iso).getTime()
  const minutes = Math.round(diffMs / 60000)
  if (minutes < 1) return 'just now'
  if (minutes < 60) return `${minutes} minute${minutes === 1 ? '' : 's'} ago`
  const hours = Math.round(minutes / 60)
  if (hours < 24) return `${hours} hour${hours === 1 ? '' : 's'} ago`
  const days = Math.round(hours / 24)
  return `${days} day${days === 1 ? '' : 's'} ago`
}

function formatDateGroup(iso: string): string {
  const date = new Date(iso)
  const now = new Date()
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const itemDate = new Date(date.getFullYear(), date.getMonth(), date.getDate())
  const diffDays = Math.round((today.getTime() - itemDate.getTime()) / (24 * 60 * 60 * 1000))

  if (diffDays === 0) return 'Today'
  if (diffDays === 1) return 'Yesterday'
  return date.toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })
}

export function SummaryView({
  workItems,
  statuses,
  profile,
  members = [],
  onOpenWorkItem,
  onSwitchTab,
}: {
  workItems: WorkItem[]
  statuses: WorkItemStatusDefinition[]
  profile?: Profile
  members?: TenantMembership[]
  onOpenWorkItem?: (item: WorkItem) => void
  onSwitchTab?: (tab: 'Board' | 'Backlog') => void
}) {
  const statusesById = new Map(statuses.map((status) => [status.id, status]))
  const doneStatusIds = new Set(statuses.filter((status) => status.category === 'Done').map((status) => status.id))
  const [bannerDismissed, setBannerDismissed] = useState(false)
  const [assigneeFilter, setAssigneeFilter] = useState<string | null>(null)
  const [filterMenuOpen, setFilterMenuOpen] = useState(false)

  const filteredItems = workItems.filter((item) => {
    if (assigneeFilter === null) return true
    return item.assigneeUserId === assigneeFilter
  })

  const [now] = useState(() => Date.now())
  const sevenDaysAgo = now - sevenDaysMs
  const nextSevenDays = now + sevenDaysMs

  const createdRecently = filteredItems.filter((item) => new Date(item.createdAt).getTime() >= sevenDaysAgo).length
  const updatedRecently = filteredItems.filter((item) => new Date(item.updatedAt).getTime() >= sevenDaysAgo).length
  const completedRecently = filteredItems.filter(
    (item) => doneStatusIds.has(item.statusId) && new Date(item.updatedAt).getTime() >= sevenDaysAgo,
  ).length
  const dueSoonRecently = filteredItems.filter((item) => {
    if (doneStatusIds.has(item.statusId)) return false
    const dueDateSource = item.dueDate ?? item.description?.match(/Due date:\s*(\d{4}-\d{2}-\d{2})/)?.[1]
    if (!dueDateSource) return false
    const dueTime = new Date(dueDateSource).getTime()
    return dueTime >= now && dueTime <= nextSevenDays
  }).length

  const statusCounts = groupWorkItemsByStatus(statuses.map((status) => status.id), filteredItems)
  const nonEmptyStatuses = statuses
    .map((status) => ({ status, ...statusMeta(status) }))
    .filter((column) => (statusCounts.get(column.status.id)?.length ?? 0) > 0)

  const segmentLengths = nonEmptyStatuses.map((column) => {
    const count = statusCounts.get(column.status.id)?.length ?? 0
    return filteredItems.length === 0 ? 0 : (count / filteredItems.length) * circumference
  })
  const segments = nonEmptyStatuses.map((column, index) => ({
    column,
    dasharray: `${segmentLengths[index]} ${circumference}`,
    dashoffset: -segmentLengths.slice(0, index).reduce((sum, length) => sum + length, 0),
  }))

  const sortedActivity = [...filteredItems]
    .sort((a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime())
    .slice(0, 10)

  // Group activity items by date
  const activityGroups: { dateLabel: string; items: WorkItem[] }[] = []
  for (const item of sortedActivity) {
    const groupLabel = formatDateGroup(item.updatedAt)
    const existing = activityGroups.find((g) => g.dateLabel === groupLabel)
    if (existing) {
      existing.items.push(item)
    } else {
      activityGroups.push({ dateLabel: groupLabel, items: [item] })
    }
  }

  // Priority breakdown counts
  const priorities: Priority[] = ['Highest', 'High', 'Medium', 'Low', 'Lowest']
  const priorityCounts = priorities.map((p) => ({
    priority: p,
    count: filteredItems.filter((i) => i.priority === p).length,
    color: priorityColors[p],
  }))

  // Types of work counts
  const typeCounts = [
    { type: 'Task' as const, count: filteredItems.filter((i) => i.type === 'Task').length },
    { type: 'Bug' as const, count: filteredItems.filter((i) => i.type === 'Bug').length },
    { type: 'Story' as const, count: filteredItems.filter((i) => i.type === 'Story').length },
    { type: 'Epic' as const, count: filteredItems.filter((i) => i.type === 'Epic').length },
  ].filter((t) => t.count > 0 || filteredItems.length === 0)

  const assigneeFilterMember = members.find((member) => member.userId === assigneeFilter)

  return (
    <div className="p-6 md:p-8 w-full bg-[#f4f5f7] min-h-screen text-[#172b4d]">
      {/* Top Banner (Customise Reports View) */}
      {!bannerDismissed && (
        <div className="relative overflow-hidden rounded-xl border border-[#c0d8ff] bg-gradient-to-r from-[#e9f2ff] via-[#f0f6ff] to-[#e4efff] p-5 mb-6 shadow-sm flex flex-col md:flex-row items-start md:items-center justify-between gap-4">
          <div className="flex items-start gap-3.5 max-w-2xl">
            <div className="w-7 h-7 rounded-full bg-[#0052cc] text-white flex items-center justify-center shrink-0 mt-0.5 shadow-sm">
              <Info size={16} />
            </div>
            <div>
              <h3 className="font-bold text-[#091e42] text-[15px] leading-snug">
                Customise your Reports view to suit your space.
              </h3>
              <p className="text-sm text-[#42526e] mt-1 leading-relaxed">
                Head to the Reports tab to easily customise charts and widgets for a dashboard tailored to your space.
              </p>
              <div className="flex items-center gap-2 mt-2.5 text-sm font-medium">
                <button
                  type="button"
                  onClick={() => onSwitchTab?.('Board')}
                  className="text-[#0052cc] hover:underline font-semibold"
                >
                  Take me to Reports
                </button>
                <span className="text-[#6b778c]">·</span>
                <button
                  type="button"
                  onClick={() => setBannerDismissed(true)}
                  className="text-[#42526e] hover:text-[#091e42] hover:underline"
                >
                  Dismiss
                </button>
              </div>
            </div>
          </div>

          {/* Banner Graphic Illustration */}
          <div className="hidden sm:flex items-center shrink-0 self-center pr-2">
            <div className="relative bg-white/95 rounded-xl border border-[#d2e3fc] shadow-md p-3 flex items-center gap-3">
              <div className="flex items-end gap-1.5 h-9 px-1">
                <div className="w-2.5 h-6 bg-[#ffab00] rounded-t-sm" />
                <div className="w-2.5 h-9 bg-[#0052cc] rounded-t-sm" />
                <div className="w-2.5 h-4 bg-[#36b37e] rounded-t-sm" />
              </div>
              <div className="w-8 h-8 rounded-full border-4 border-[#0052cc] border-t-[#36b37e] border-r-[#ffab00]" />
              <div className="w-6 h-6 rounded-md bg-[#ebecf0] flex items-center justify-center text-[#6b778c] font-bold text-xs">
                +
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Filter Row */}
      <div className="flex items-center gap-3 mb-6">
        <div className="w-8 h-8 rounded-full bg-[#ffab00] text-white flex items-center justify-center text-xs font-bold shadow-sm ring-2 ring-white">
          {assigneeFilterMember
            ? getInitials(assigneeFilterMember.displayName ?? undefined)
            : profile?.displayName
            ? getInitials(profile.displayName)
            : 'EU'}
        </div>

        <div className="relative">
          <button
            type="button"
            onClick={() => setFilterMenuOpen(!filterMenuOpen)}
            className="flex items-center gap-2 px-3 py-1.5 bg-white hover:bg-[#ebecf0] rounded-md border border-[#dfe1e6] text-sm font-semibold text-[#172b4d] shadow-sm transition-colors"
          >
            <Filter size={14} className="text-[#6b778c]" />
            {assigneeFilterMember?.displayName ?? 'Filter'}
            <ChevronDown size={14} className="text-[#6b778c]" />
          </button>

          {filterMenuOpen && (
            <div className="absolute left-0 top-full mt-1.5 w-60 bg-white border border-[#dfe1e6] shadow-xl rounded-lg py-1.5 z-50 animate-in fade-in">
              <div className="px-3 py-1 text-[11px] font-bold text-[#6b778c] uppercase tracking-wider">
                Assignee
              </div>
              <button
                type="button"
                onClick={() => {
                  setAssigneeFilter(null)
                  setFilterMenuOpen(false)
                }}
                className={`w-full text-left px-3 py-2 text-sm hover:bg-[#f4f5f7] flex items-center gap-2 ${
                  assigneeFilter === null ? 'bg-[#deebff] text-[#0052cc] font-semibold' : 'text-[#172b4d]'
                }`}
              >
                <div className="w-5 h-5 rounded-full bg-gray-200 text-gray-500 flex items-center justify-center text-xs">
                  <User size={12} />
                </div>
                All assignees
              </button>
              {members
                .filter((member) => member.userId)
                .map((member) => (
                  <button
                    key={member.id}
                    type="button"
                    onClick={() => {
                      setAssigneeFilter(member.userId)
                      setFilterMenuOpen(false)
                    }}
                    className={`w-full text-left px-3 py-2 text-sm hover:bg-[#f4f5f7] flex items-center gap-2 ${
                      assigneeFilter === member.userId ? 'bg-[#deebff] text-[#0052cc] font-semibold' : 'text-[#172b4d]'
                    }`}
                  >
                    <div className="w-5 h-5 rounded-full bg-[#ffab00] text-white flex items-center justify-center text-[10px] font-bold">
                      {getInitials(member.displayName ?? undefined)}
                    </div>
                    <span className="truncate">{member.displayName ?? 'Unnamed member'}</span>
                  </button>
                ))}
            </div>
          )}
        </div>
      </div>

      {/* 4 Metric Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatTile
          icon={<CheckCircle2 size={20} className="text-[#42526e]" />}
          value={`${completedRecently} completed`}
          label="in the last 7 days"
        />
        <StatTile
          icon={<Edit3 size={20} className="text-[#42526e]" />}
          value={`${updatedRecently} updated`}
          label="in the last 7 days"
        />
        <StatTile
          icon={<PlusSquare size={20} className="text-[#42526e]" />}
          value={`${createdRecently} created`}
          label="in the last 7 days"
        />
        <StatTile
          icon={<Calendar size={20} className="text-[#42526e]" />}
          value={`${dueSoonRecently} due soon`}
          label="in the next 7 days"
        />
      </div>

      {/* Middle 2-Column Section */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
        {/* Status overview */}
        <div className="bg-white border border-[#dfe1e6] rounded-xl shadow-sm p-6 flex flex-col justify-between">
          <div>
            <div className="flex items-baseline justify-between mb-1">
              <h3 className="font-bold text-[#091e42] text-base">Status overview</h3>
            </div>
            <p className="text-sm text-[#5e6c84] mb-6">
              Get a snapshot of the status of your work items.{' '}
              <button
                type="button"
                onClick={() => onSwitchTab?.('Backlog')}
                className="text-[#0052cc] hover:underline font-medium ml-1 inline-flex items-center"
              >
                View all work items
              </button>
            </p>
          </div>

          {filteredItems.length === 0 ? (
            <div className="py-12 text-center text-sm text-[#6b778c]">No work items yet.</div>
          ) : (
            <div className="flex flex-col sm:flex-row items-center justify-around gap-8 my-2">
              {/* Donut Chart */}
              <div className="relative w-48 h-48 shrink-0">
                <svg viewBox="0 0 100 100" className="w-full h-full transform -rotate-90">
                  {segments.map(({ column, dasharray, dashoffset }) => (
                    <circle
                      key={column.status.id}
                      cx="50"
                      cy="50"
                      r="40"
                      fill="transparent"
                      stroke={toneColors[column.tone] ?? '#6b778c'}
                      strokeWidth="16"
                      strokeDasharray={dasharray}
                      strokeDashoffset={dashoffset}
                      className="transition-all duration-500"
                    />
                  ))}
                </svg>
                <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
                  <span className="text-4xl font-extrabold text-[#091e42] leading-none">
                    {filteredItems.length}
                  </span>
                  <span className="text-xs text-[#5e6c84] font-semibold text-center mt-1 leading-tight max-w-[90px] truncate">
                    Total work item...
                  </span>
                </div>
              </div>

              {/* Legend List */}
              <div className="space-y-3.5 w-full sm:w-48">
                {nonEmptyStatuses.map((column) => (
                  <div key={column.status.id} className="flex items-center justify-between text-sm">
                    <div className="flex items-center gap-2.5 text-[#172b4d] font-medium">
                      <div
                        className="w-3.5 h-3.5 rounded-sm shrink-0 shadow-xs"
                        style={{ backgroundColor: toneColors[column.tone] ?? '#6b778c' }}
                      />
                      <span>{column.status.category === 'ToDo' ? 'To Do' : column.label}</span>
                    </div>
                    <span className="font-bold text-[#091e42]">
                      : {statusCounts.get(column.status.id)?.length ?? 0}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Recent activity */}
        <div className="bg-white border border-[#dfe1e6] rounded-xl shadow-sm p-6 flex flex-col">
          <div className="flex justify-between items-start mb-4">
            <div>
              <h3 className="font-bold text-[#091e42] text-base mb-1">Recent activity</h3>
              <p className="text-sm text-[#5e6c84]">
                Stay up to date with what&apos;s happening across the space.
              </p>
            </div>
            <button
              type="button"
              className="p-1 hover:bg-[#ebecf0] rounded text-[#6b778c] transition-colors"
              title="Expand"
              aria-label="Expand recent activity"
            >
              <Maximize2 size={16} />
            </button>
          </div>

          <div className="flex-1 overflow-y-auto max-h-[340px] pr-2 custom-scrollbar">
            {sortedActivity.length === 0 ? (
              <p className="text-sm text-[#6b778c] py-8 text-center">No activity yet.</p>
            ) : (
              <div className="space-y-5">
                {activityGroups.map((group) => (
                  <div key={group.dateLabel}>
                    <div className="text-xs font-bold text-[#6b778c] mb-3 pb-1 border-b border-[#ebecf0]">
                      {group.dateLabel}
                    </div>
                    <div className="space-y-4">
                      {group.items.map((item) => {
                        const assignee = item.assigneeUserId
                          ? members.find((m) => m.userId === item.assigneeUserId)
                          : undefined
                        const actorName = assignee?.displayName ?? profile?.displayName ?? 'Ekanatha Reddy Urivakili'

                        return (
                          <div key={item.id} className="flex gap-3.5 items-start text-sm">
                            <div className="w-8 h-8 rounded-full bg-[#ffab00] text-white flex items-center justify-center text-xs font-bold shrink-0 shadow-sm mt-0.5">
                              {getInitials(actorName)}
                            </div>
                            <div className="flex-1 min-w-0">
                              <div className="text-[#172b4d] leading-snug">
                                <span className="font-semibold text-[#091e42] hover:underline cursor-pointer">
                                  {actorName}
                                </span>{' '}
                                <span className="text-[#42526e]">updated field &quot;Sprint&quot; on</span>{' '}
                                <button
                                  type="button"
                                  onClick={() => onOpenWorkItem?.(item)}
                                  className="inline-flex items-center gap-1 font-semibold text-[#0052cc] hover:underline text-xs bg-[#f4f5f7] hover:bg-[#ebecf0] px-1.5 py-0.5 rounded border border-[#dfe1e6]"
                                >
                                  <WorkItemTypeIcon type={item.type} size={14} />
                                  <span>
                                    {item.key}: {item.summary}
                                  </span>
                                </button>{' '}
                                <span
                                  className={`inline-block px-1.5 py-0.5 text-[11px] font-bold rounded ${
                                    statusesById.get(item.statusId)?.category === 'Done'
                                      ? 'bg-[#e3fcef] text-[#006644]'
                                      : statusesById.get(item.statusId)?.category === 'InProgress'
                                      ? 'bg-[#deebff] text-[#0052cc]'
                                      : 'bg-[#ebecf0] text-[#42526e]'
                                  }`}
                                >
                                  {statusesById.get(item.statusId)?.category === 'ToDo'
                                    ? 'To Do'
                                    : statusesById.get(item.statusId)?.name ?? 'Unknown'}
                                </span>
                              </div>
                              <div className="text-xs text-[#6b778c] mt-1">
                                {formatRelativeTime(item.updatedAt)}
                              </div>
                            </div>
                          </div>
                        )
                      })}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Bottom 2-Column Section (Priority breakdown & Types of work) */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Priority breakdown */}
        <div className="bg-white border border-[#dfe1e6] rounded-xl shadow-sm p-6">
          <h3 className="font-bold text-[#091e42] text-base mb-1">Priority breakdown</h3>
          <p className="text-sm text-[#5e6c84] mb-6">
            Get a snapshot of the priority of your work items.
          </p>

          {filteredItems.length === 0 ? (
            <p className="text-sm text-[#6b778c]">No work items.</p>
          ) : (
            <div>
              {/* Stacked Bar */}
              <div className="h-3 w-full bg-[#ebecf0] rounded-full overflow-hidden flex mb-5 shadow-inner">
                {priorityCounts.map((p) => {
                  const pct = filteredItems.length > 0 ? (p.count / filteredItems.length) * 100 : 0
                  if (pct === 0) return null
                  return (
                    <div
                      key={p.priority}
                      style={{ width: `${pct}%`, backgroundColor: p.color }}
                      title={`${p.priority}: ${p.count}`}
                      className="h-full first:rounded-l-full last:rounded-r-full"
                    />
                  )
                })}
              </div>

              {/* Priority list */}
              <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
                {priorityCounts.map((p) => (
                  <div key={p.priority} className="flex items-center gap-2 text-sm p-2 rounded-lg bg-[#f4f5f7] border border-[#ebecf0]">
                    <div className="w-3 h-3 rounded-full shrink-0" style={{ backgroundColor: p.color }} />
                    <span className="text-[#172b4d] font-medium truncate">{p.priority}</span>
                    <span className="font-bold text-[#091e42] ml-auto">{p.count}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Types of work */}
        <div className="bg-white border border-[#dfe1e6] rounded-xl shadow-sm p-6">
          <h3 className="font-bold text-[#091e42] text-base mb-1">Types of work</h3>
          <p className="text-sm text-[#5e6c84] mb-6">
            See the breakdown of work items by type across this space.
          </p>

          {filteredItems.length === 0 ? (
            <p className="text-sm text-[#6b778c]">No work items.</p>
          ) : (
            <div className="space-y-3.5">
              {typeCounts.map((t) => {
                const pct = filteredItems.length > 0 ? Math.round((t.count / filteredItems.length) * 100) : 0
                return (
                  <div key={t.type} className="flex items-center gap-3">
                    <div className="p-1.5 rounded bg-[#f4f5f7] border border-[#dfe1e6]">
                      <WorkItemTypeIcon type={t.type} size={16} />
                    </div>
                    <span className="text-sm font-semibold text-[#172b4d] w-20">{t.type}</span>
                    <div className="flex-1 h-2.5 bg-[#ebecf0] rounded-full overflow-hidden">
                      <div
                        className="h-full bg-[#0052cc] rounded-full"
                        style={{ width: `${pct}%` }}
                      />
                    </div>
                    <span className="text-sm font-bold text-[#091e42] w-12 text-right">
                      {t.count} ({pct}%)
                    </span>
                  </div>
                )
              })}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

function StatTile({ icon, value, label }: { icon: ReactNode; value: string; label: string }) {
  return (
    <div className="bg-white border border-[#dfe1e6] p-4 rounded-xl shadow-sm flex items-start gap-3.5 hover:shadow-md transition-shadow">
      <div className="p-2.5 bg-[#f4f5f7] rounded-lg text-[#42526e] shrink-0 border border-[#ebecf0]">
        {icon}
      </div>
      <div className="min-w-0">
        <div className="font-bold text-[#091e42] text-base leading-tight truncate">{value}</div>
        <div className="text-xs text-[#5e6c84] mt-1 font-medium">{label}</div>
      </div>
    </div>
  )
}
