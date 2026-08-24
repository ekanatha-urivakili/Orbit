import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Calendar, HelpCircle, X } from 'lucide-react'
import { orbitApi } from '../../api/client'
import type { SprintAttentionItem } from '../../api/types'

type AttentionTab = 'All' | 'Due' | 'Stuck' | 'Blocked' | 'Flagged'

function matchesTab(item: SprintAttentionItem, tab: AttentionTab): boolean {
  switch (tab) {
    case 'Due':
      return item.isOverdue
    case 'Stuck':
      return item.isStuck
    case 'Blocked':
      return item.isBlocked
    case 'Flagged':
      return item.isFlagged
    default:
      return true
  }
}

function formatAttentionDate(dateStr?: string | null): string {
  if (!dateStr) return ''
  try {
    const d = new Date(dateStr)
    return d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })
  } catch {
    return dateStr
  }
}

const BURNDOWN_WIDTH = 300
const BURNDOWN_HEIGHT = 120
const BURNDOWN_PADDING = 24

function burndownShortDate(isoDate: string): string {
  const [, month, day] = isoDate.split('-').map(Number)
  return new Date(Date.UTC(2000, month - 1, day)).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}

export function SprintInsightsPanel({ sprintId, onClose }: { sprintId: string; onClose: () => void }) {
  const [tab, setTab] = useState<AttentionTab>('All')
  const query = useQuery({
    queryKey: ['sprint-insights', sprintId],
    queryFn: () => orbitApi.getSprintInsights(sprintId),
  })
  const insights = query.data
  const reportQuery = useQuery({
    queryKey: ['sprint-report', sprintId],
    queryFn: () => orbitApi.getSprintReport(sprintId),
  })
  const burndown = reportQuery.data?.burndown ?? []
  const maxBurndownPoints = reportQuery.data
    ? Math.max(reportQuery.data.committedPoints, ...burndown.map((point) => point.remainingPoints), 1)
    : 1
  const toBurndownX = (index: number) =>
    BURNDOWN_PADDING + (index / Math.max(burndown.length - 1, 1)) * (BURNDOWN_WIDTH - BURNDOWN_PADDING * 2)
  const toBurndownY = (value: number) =>
    BURNDOWN_HEIGHT - BURNDOWN_PADDING - (value / maxBurndownPoints) * (BURNDOWN_HEIGHT - BURNDOWN_PADDING * 2)
  const burndownActualPath = burndown
    .map((point, index) => `${index === 0 ? 'M' : 'L'} ${toBurndownX(index)} ${toBurndownY(point.remainingPoints)}`)
    .join(' ')
  const burndownGuidelinePath =
    reportQuery.data && burndown.length > 1
      ? `M ${toBurndownX(0)} ${toBurndownY(reportQuery.data.committedPoints)} L ${toBurndownX(burndown.length - 1)} ${toBurndownY(0)}`
      : ''

  const filteredAttention = useMemo(
    () => (insights ? insights.itemsForAttention.filter((item) => matchesTab(item, tab)) : []),
    [insights, tab],
  )

  const overdueCount = insights?.itemsForAttention.filter((i) => i.isOverdue).length ?? 0
  const scopeChange = (insights?.addedAfterStartPoints ?? 0) - (insights?.removedAfterStartPoints ?? 0)

  return (
    <aside className="w-[360px] shrink-0 border-l border-gray-200 dark:border-[#394047] bg-white dark:bg-[#1d2125] h-full overflow-y-auto z-20">
      {/* Header */}
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100 dark:border-[#394047]">
        <h2 className="text-base font-bold text-[#172b4d] dark:text-gray-100">Sprint insights</h2>
        <button
          className="icon-button text-gray-500 hover:text-gray-700"
          type="button"
          aria-label="Close"
          onClick={onClose}
        >
          <X size={18} />
        </button>
      </div>

      <div className="px-5 pt-3 pb-2 text-xs text-gray-500 dark:text-gray-400 space-y-1">
        <p>View your sprint health and progress towards your goals.</p>
        {insights && <p className="font-semibold text-gray-700 dark:text-gray-300">Sprint: {insights.sprintName}</p>}
      </div>

      {query.isPending && <p className="px-5 py-6 text-sm text-gray-500">Loading insights…</p>}
      {query.isError && <p className="form-error px-5 py-4">{query.error.message}</p>}

      {insights && (
        <div className="px-5 py-3 space-y-6">
          {/* Work items for attention */}
          <section>
            <div className="flex items-center justify-between mb-2">
              <h3 className="text-xs font-bold text-gray-900 dark:text-gray-100 uppercase tracking-wide">
                Work items for attention
              </h3>
              <HelpCircle size={14} className="text-gray-400" />
            </div>

            <div className="flex items-center gap-1 border-b border-gray-100 dark:border-[#394047] mb-2.5 text-xs">
              {(['All', 'Due', 'Stuck', 'Blocked', 'Flagged'] as AttentionTab[]).map((candidate) => (
                <button
                  key={candidate}
                  type="button"
                  className={`px-2 py-1.5 -mb-px border-b-2 font-medium transition-colors ${
                    tab === candidate
                      ? 'border-blue-600 text-blue-600 dark:text-blue-400 font-semibold'
                      : 'border-transparent text-gray-500 hover:text-gray-700'
                  }`}
                  onClick={() => setTab(candidate)}
                >
                  {candidate}
                </button>
              ))}
            </div>

            {overdueCount > 0 && tab === 'All' && (
              <p className="text-xs text-gray-600 dark:text-gray-400 mb-2">
                {overdueCount} work item{overdueCount === 1 ? '' : 's'} {overdueCount === 1 ? 'is' : 'are'} overdue in the current sprint.
              </p>
            )}

            {filteredAttention.length === 0 ? (
              <p className="text-xs text-gray-500 dark:text-gray-400 py-2">Nothing needs attention here.</p>
            ) : (
              <div className="space-y-2">
                {filteredAttention.map((item) => (
                  <div
                    key={item.workItemId}
                    className="rounded-lg border border-gray-200 dark:border-[#394047] p-3 bg-white dark:bg-[#22272b] space-y-1.5 shadow-sm"
                  >
                    <div className="flex items-center gap-1.5">
                      <span className="text-[10px] font-bold uppercase px-1.5 py-0.5 rounded bg-green-50 text-green-700 dark:bg-green-950/50 dark:text-green-300">
                        {item.key}
                      </span>
                    </div>
                    <div className="text-xs font-semibold text-gray-900 dark:text-gray-100">{item.summary}</div>
                    <div className="flex items-center justify-between pt-1">
                      {item.isOverdue && item.dueDate ? (
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded border border-red-200 dark:border-red-900/60 text-red-600 dark:text-red-400 text-[11px] font-medium bg-red-50/50 dark:bg-red-950/30">
                          <Calendar size={11} /> Due on {formatAttentionDate(item.dueDate)}
                        </span>
                      ) : (
                        <span />
                      )}
                      <span className="text-red-500 font-bold text-xs">⌃</span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>

          {/* Sprint progress */}
          <section>
            <div className="flex items-center justify-between mb-2">
              <h3 className="text-xs font-bold text-gray-900 dark:text-gray-100 uppercase tracking-wide">
                Sprint progress
              </h3>
              <HelpCircle size={14} className="text-gray-400" />
            </div>

            {/* Progress Bar */}
            <div className="h-2 w-full rounded-full bg-gray-200 dark:bg-[#2c333a] overflow-hidden flex mb-2">
              <div className="h-full bg-green-500" style={{ width: `${insights.percentDone}%` }} />
              <div
                className="h-full bg-blue-500"
                style={{
                  width: `${
                    insights.totalItems > 0 ? (insights.inProgressItems / insights.totalItems) * 100 : 0
                  }%`,
                }}
              />
            </div>

            <div className="flex items-center justify-between text-xs text-gray-600 dark:text-gray-400">
              <div className="flex items-center gap-3">
                <span>
                  Done <strong className="text-gray-900 dark:text-gray-100">{insights.percentDone}%</strong>
                </span>
                <span>
                  In progress{' '}
                  <strong className="text-gray-900 dark:text-gray-100">
                    {insights.totalItems > 0
                      ? Math.round((insights.inProgressItems / insights.totalItems) * 100)
                      : 0}
                    %
                  </strong>
                </span>
                <span>
                  Not started{' '}
                  <strong className="text-gray-900 dark:text-gray-100">
                    {insights.totalItems > 0
                      ? Math.round((insights.notStartedItems / insights.totalItems) * 100)
                      : 100}
                    %
                  </strong>
                </span>
              </div>
              <span className="font-semibold text-gray-900 dark:text-gray-100">{insights.percentDone}% done</span>
            </div>
          </section>

          {/* Sprint burndown */}
          <section>
            <div className="flex items-center justify-between mb-1">
              <h3 className="text-xs font-bold text-gray-900 dark:text-gray-100 uppercase tracking-wide">
                Sprint burndown
              </h3>
              <HelpCircle size={14} className="text-gray-400" />
            </div>
            <p className="text-xs text-gray-500 dark:text-gray-400 mb-3">
              {insights.completedPoints} points done,{' '}
              {Math.max(insights.committedPoints - insights.completedPoints, 0)} points to go
            </p>

            {/* Burndown chart, computed from the sprint's immutable fact log (same source as the sprint report) */}
            <div className="border border-gray-200 dark:border-[#394047] rounded-lg p-3 bg-white dark:bg-[#22272b] mb-3">
              {burndown.length > 1 ? (
                <svg viewBox={`0 0 ${BURNDOWN_WIDTH} ${BURNDOWN_HEIGHT}`} className="w-full h-28" role="img" aria-label="Sprint burndown chart">
                  <line
                    x1={BURNDOWN_PADDING}
                    y1={BURNDOWN_HEIGHT - BURNDOWN_PADDING}
                    x2={BURNDOWN_WIDTH - BURNDOWN_PADDING}
                    y2={BURNDOWN_HEIGHT - BURNDOWN_PADDING}
                    stroke="#d1d5db"
                  />
                  <line x1={BURNDOWN_PADDING} y1={BURNDOWN_PADDING} x2={BURNDOWN_PADDING} y2={BURNDOWN_HEIGHT - BURNDOWN_PADDING} stroke="#d1d5db" />
                  {burndownGuidelinePath && (
                    <path d={burndownGuidelinePath} fill="none" stroke="#9ca3af" strokeWidth={1.5} strokeDasharray="3 3" />
                  )}
                  <path d={burndownActualPath} fill="none" stroke="#2563eb" strokeWidth={2} />
                  {burndown.map((point, index) => (
                    <circle key={point.date} cx={toBurndownX(index)} cy={toBurndownY(point.remainingPoints)} r={2.5} fill="#2563eb" />
                  ))}
                  <text x={toBurndownX(0)} y={BURNDOWN_HEIGHT - BURNDOWN_PADDING + 14} fontSize={8} fill="#6b7280">
                    {burndownShortDate(burndown[0].date)}
                  </text>
                  <text x={toBurndownX(burndown.length - 1)} y={BURNDOWN_HEIGHT - BURNDOWN_PADDING + 14} fontSize={8} textAnchor="end" fill="#6b7280">
                    {burndownShortDate(burndown[burndown.length - 1].date)}
                  </text>
                </svg>
              ) : (
                <p className="text-xs text-gray-500 dark:text-gray-400 py-4 text-center">
                  {reportQuery.isPending ? 'Loading burndown…' : "This sprint hasn't started yet, so there's no burndown baseline."}
                </p>
              )}

              <div className="flex items-center justify-center gap-4 text-[11px] text-gray-500 pt-2 border-t border-gray-100 dark:border-[#394047]">
                <span className="flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-full bg-blue-600" /> Remaining work
                </span>
                <span className="flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-full bg-gray-400" /> Guideline
                </span>
              </div>
            </div>

            {/* Scope Stats Box */}
            <div className="rounded-lg bg-blue-50/60 dark:bg-blue-950/20 border border-blue-100 dark:border-blue-900/40 p-3 space-y-2 text-xs">
              <p className="font-semibold text-gray-800 dark:text-gray-200">
                Your sprint scope has {scopeChange >= 0 ? 'increased' : 'decreased'} by {Math.abs(scopeChange)} points
              </p>
              <div className="grid grid-cols-3 gap-2 pt-1 border-t border-blue-100 dark:border-blue-900/40 text-[11px]">
                <div>
                  <span className="block text-gray-500">Added</span>
                  <strong className="text-gray-900 dark:text-gray-100">{insights.addedAfterStartPoints} points</strong>
                  <span className="block text-[10px] text-gray-400">
                    ↑ {insights.addedAfterStartCount} work item{insights.addedAfterStartCount === 1 ? '' : 's'}
                  </span>
                </div>
                <div>
                  <span className="block text-gray-500">Removed</span>
                  <strong className="text-gray-900 dark:text-gray-100">{insights.removedAfterStartPoints} points</strong>
                  <span className="block text-[10px] text-gray-400">
                    ↓ {insights.removedAfterStartCount} work item{insights.removedAfterStartCount === 1 ? '' : 's'}
                  </span>
                </div>
                <div>
                  <span className="block text-gray-500">Net change</span>
                  <strong className={scopeChange === 0 ? 'text-gray-900 dark:text-gray-100' : 'text-amber-600 dark:text-amber-400'}>
                    {scopeChange >= 0 ? '+' : ''}
                    {scopeChange} points
                  </strong>
                  <span className="block text-[10px] text-gray-400">
                    ● {insights.addedAfterStartCount + insights.removedAfterStartCount} work item
                    {insights.addedAfterStartCount + insights.removedAfterStartCount === 1 ? '' : 's'}
                  </span>
                </div>
              </div>
            </div>
          </section>

          {/* Epic progress */}
          <section>
            <div className="flex items-center justify-between mb-1">
              <h3 className="text-xs font-bold text-gray-900 dark:text-gray-100 uppercase tracking-wide">
                Epic progress
              </h3>
              <HelpCircle size={14} className="text-gray-400" />
            </div>
            <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">
              This sprint is working towards {insights.epics.length} epic{insights.epics.length === 1 ? '' : 's'}
            </p>

            {insights.epics.length > 0 ? (
              <div className="space-y-3">
                {insights.epics.map((epic) => (
                  <div key={epic.epicId} className="space-y-1">
                    <div className="flex items-center justify-between text-xs">
                      <span className="font-semibold text-blue-600 dark:text-blue-400 truncate">
                        {epic.key} {epic.name}
                      </span>
                      <span className="text-gray-500 text-[11px]">{epic.percentDone}% done</span>
                    </div>
                    <div className="h-1.5 w-full rounded-full bg-gray-200 dark:bg-[#2c333a] overflow-hidden">
                      <div className="h-full bg-purple-500" style={{ width: `${epic.percentDone}%` }} />
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-xs text-gray-500 dark:text-gray-400 py-1">No epics in this sprint yet.</p>
            )}
          </section>
        </div>
      )}
    </aside>
  )
}
