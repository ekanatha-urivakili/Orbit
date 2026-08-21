import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { AlertTriangle, Ban, Clock, Flag, X } from 'lucide-react'
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

  return (
    <aside className="w-[340px] shrink-0 border-l border-gray-200 dark:border-[#394047] bg-white dark:bg-[#1d2125] h-full overflow-y-auto">
      <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100 dark:border-[#394047]">
        <h2 className="text-sm font-bold text-[#172b4d] dark:text-gray-100">Sprint insights</h2>
        <button className="icon-button" type="button" aria-label="Close" onClick={onClose}><X size={16} /></button>
      </div>
      <p className="px-4 pt-3 text-xs text-gray-500 dark:text-gray-400">
        View your sprint health and progress towards your goals.
      </p>
      {insights && <p className="px-4 pt-1 text-xs text-gray-500 dark:text-gray-400">Sprint: {insights.sprintName}</p>}

      {query.isPending && <p className="px-4 py-4 text-sm text-gray-500">Loading…</p>}
      {query.isError && <p className="form-error px-4 py-4">{query.error.message}</p>}

      {insights && (
        <div className="px-4 py-4 space-y-6">
          <section>
            <div className="flex items-center justify-between mb-2">
              <h3 className="text-xs font-semibold text-gray-600 dark:text-gray-300 uppercase">Work items for attention</h3>
            </div>
            <div className="flex gap-1 border-b border-gray-100 dark:border-[#394047] mb-2 text-xs">
              {(['All', 'Due', 'Stuck', 'Blocked', 'Flagged'] as AttentionTab[]).map((candidate) => (
                <button
                  key={candidate}
                  type="button"
                  className={`px-2 py-1.5 -mb-px border-b-2 ${
                    tab === candidate
                      ? 'border-blue-600 text-blue-700 dark:text-blue-400 font-semibold'
                      : 'border-transparent text-gray-500 hover:text-gray-700'
                  }`}
                  onClick={() => setTab(candidate)}
                >
                  {candidate}
                </button>
              ))}
            </div>
            {filteredAttention.length === 0 ? (
              <p className="text-xs text-gray-500 dark:text-gray-400">Nothing needs attention here.</p>
            ) : (
              <ul className="space-y-2">
                {filteredAttention.map((item) => (
                  <li key={item.workItemId} className="rounded-lg border border-gray-100 dark:border-[#394047] px-2.5 py-2">
                    <div className="text-xs font-medium text-[#172b4d] dark:text-gray-100">{item.key}</div>
                    <div className="text-xs text-gray-600 dark:text-gray-400 truncate">{item.summary}</div>
                    <div className="flex items-center gap-2 mt-1 text-[11px] text-gray-500">
                      {item.isOverdue && item.dueDate && (
                        <span className="flex items-center gap-1 text-red-600"><AlertTriangle size={11} /> Due {item.dueDate}</span>
                      )}
                      {item.isBlocked && <span className="flex items-center gap-1 text-red-600"><Ban size={11} /> Blocked</span>}
                      {item.isStuck && <span className="flex items-center gap-1 text-amber-600"><Clock size={11} /> Stuck</span>}
                      {item.isFlagged && <span className="flex items-center gap-1 text-orange-500"><Flag size={11} /> Flagged</span>}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section>
            <h3 className="text-xs font-semibold text-gray-600 dark:text-gray-300 uppercase mb-2">Sprint progress</h3>
            <div className="h-2 w-full rounded-full bg-gray-100 dark:bg-[#2c333a] overflow-hidden flex">
              <div className="h-full bg-green-500" style={{ width: `${insights.percentDone}%` }} />
            </div>
            <p className="text-xs text-gray-500 mt-1">{insights.percentDone}% done</p>
            <dl className="grid grid-cols-3 gap-2 mt-2 text-center">
              <div>
                <dt className="text-[10px] uppercase text-gray-500">Done</dt>
                <dd className="text-sm font-semibold text-gray-900 dark:text-gray-100">{insights.doneItems}</dd>
              </div>
              <div>
                <dt className="text-[10px] uppercase text-gray-500">In progress</dt>
                <dd className="text-sm font-semibold text-gray-900 dark:text-gray-100">{insights.inProgressItems}</dd>
              </div>
              <div>
                <dt className="text-[10px] uppercase text-gray-500">Not started</dt>
                <dd className="text-sm font-semibold text-gray-900 dark:text-gray-100">{insights.notStartedItems}</dd>
              </div>
            </dl>
          </section>

          <section>
            <h3 className="text-xs font-semibold text-gray-600 dark:text-gray-300 uppercase mb-2">Sprint burndown</h3>
            <p className="text-xs text-gray-500">
              {insights.completedPoints} points done, {Math.max(insights.committedPoints - insights.completedPoints, 0)} points to go
            </p>
            {(insights.addedAfterStartPoints !== 0 || insights.removedAfterStartPoints !== 0) && (
              <p className="text-xs text-amber-700 bg-amber-50 dark:bg-amber-950/30 rounded px-2 py-1.5 mt-2">
                Sprint scope has changed by {insights.addedAfterStartPoints - insights.removedAfterStartPoints} points since it started.
              </p>
            )}
          </section>

          {insights.epics.length > 0 && (
            <section>
              <h3 className="text-xs font-semibold text-gray-600 dark:text-gray-300 uppercase mb-2">Epic progress</h3>
              <ul className="space-y-2">
                {insights.epics.map((epic) => (
                  <li key={epic.epicId}>
                    <div className="flex items-center justify-between text-xs">
                      <span className="text-[#172b4d] dark:text-gray-200 truncate">{epic.key} {epic.name}</span>
                      <span className="text-gray-500">{epic.percentDone}%</span>
                    </div>
                    <div className="h-1.5 w-full rounded-full bg-gray-100 dark:bg-[#2c333a] overflow-hidden mt-1">
                      <div className="h-full bg-purple-500" style={{ width: `${epic.percentDone}%` }} />
                    </div>
                  </li>
                ))}
              </ul>
            </section>
          )}
        </div>
      )}
    </aside>
  )
}
