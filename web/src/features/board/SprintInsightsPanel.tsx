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

export function SprintInsightsPanel({ sprintId, onClose }: { sprintId: string; onClose: () => void }) {
  const [tab, setTab] = useState<AttentionTab>('All')
  const query = useQuery({
    queryKey: ['sprint-insights', sprintId],
    queryFn: () => orbitApi.getSprintInsights(sprintId),
  })
  const insights = query.data

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

            {/* SVG Burndown Chart Graphic */}
            <div className="border border-gray-200 dark:border-[#394047] rounded-lg p-3 bg-white dark:bg-[#22272b] mb-3">
              <svg viewBox="0 0 300 120" className="w-full h-28">
                {/* Horizontal Grid lines */}
                <line x1="20" y1="20" x2="290" y2="20" stroke="#e5e7eb" strokeWidth="1" />
                <line x1="20" y1="50" x2="290" y2="50" stroke="#e5e7eb" strokeWidth="1" />
                <line x1="20" y1="80" x2="290" y2="80" stroke="#e5e7eb" strokeWidth="1" />
                <line x1="20" y1="105" x2="290" y2="105" stroke="#9ca3af" strokeWidth="1" />

                {/* Y-axis labels */}
                <text x="5" y="24" fontSize="9" fill="#9ca3af">5</text>
                <text x="5" y="54" fontSize="9" fill="#9ca3af">4</text>
                <text x="5" y="84" fontSize="9" fill="#9ca3af">3</text>
                <text x="5" y="108" fontSize="9" fill="#9ca3af">0</text>

                {/* Guideline diagonal line */}
                <line x1="25" y1="20" x2="285" y2="105" stroke="#9ca3af" strokeWidth="1.5" strokeDasharray="3 3" />

                {/* Remaining Work Area + Line */}
                <polygon
                  points="25,105 25,25 60,20 110,20 110,105"
                  fill="#3b82f6"
                  fillOpacity="0.15"
                />
                <polyline
                  points="25,25 60,20 110,20"
                  fill="none"
                  stroke="#2563eb"
                  strokeWidth="2"
                />

                {/* X-axis date labels */}
                <text x="25" y="118" fontSize="8" fill="#6b7280">Aug 19</text>
                <text x="260" y="118" fontSize="8" fill="#6b7280">Sep 2</text>
              </svg>

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
                  <span className="block text-[10px] text-gray-400">↑ 0 work items</span>
                </div>
                <div>
                  <span className="block text-gray-500">Removed</span>
                  <strong className="text-gray-900 dark:text-gray-100">{insights.removedAfterStartPoints} points</strong>
                  <span className="block text-[10px] text-gray-400">↓ 0 work items</span>
                </div>
                <div>
                  <span className="block text-gray-500">Modified</span>
                  <strong className="text-amber-600 dark:text-amber-400">+{scopeChange} points</strong>
                  <span className="block text-[10px] text-gray-400">● 1 work item</span>
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
              <div className="space-y-1">
                <div className="flex items-center justify-between text-xs">
                  <span className="font-semibold text-blue-600 dark:text-blue-400">SCRUM-6 Test epic</span>
                  <span className="text-gray-500 text-[11px]">0% done</span>
                </div>
                <div className="h-1.5 w-full rounded-full bg-gray-200 dark:bg-[#2c333a] overflow-hidden">
                  <div className="h-full bg-purple-500" style={{ width: '0%' }} />
                </div>
              </div>
            )}
          </section>
        </div>
      )}
    </aside>
  )
}
