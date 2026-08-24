import { useMemo } from 'react'
import { HelpCircle, X } from 'lucide-react'
import type { Sprint, WorkItem, WorkItemType } from '../../api/types'

const TYPE_COLORS: Record<WorkItemType, string> = {
  Initiative: '#7c3aed',
  Epic: '#9333ea',
  Task: '#2563eb',
  Story: '#16a34a',
  Spike: '#0891b2',
  Test: '#db2777',
  Feature: '#4f46e5',
  Request: '#0d9488',
  Bug: '#dc2626',
  Subtask: '#6b7280',
}

export function BacklogInsightsPanel({
  workItems,
  sprints,
  onClose,
}: {
  workItems: WorkItem[]
  sprints: Sprint[]
  onClose: () => void
}) {
  const planningSprint = useMemo(
    () =>
      sprints.find((sprint) => sprint.state === 'Active' || sprint.state === 'Reopened') ??
      sprints.find((sprint) => sprint.state === 'Future') ??
      null,
    [sprints],
  )

  const workItemsById = useMemo(() => new Map(workItems.map((item) => [item.id, item])), [workItems])

  const committedPoints = useMemo(() => {
    if (!planningSprint) return 0
    return planningSprint.workItemIds.reduce((sum, id) => sum + (workItemsById.get(id)?.storyPoints ?? 0), 0)
  }, [planningSprint, workItemsById])

  const typeBreakdown = useMemo(() => {
    const scope = planningSprint
      ? planningSprint.workItemIds.map((id) => workItemsById.get(id)).filter((item): item is WorkItem => Boolean(item))
      : workItems
    const counts = new Map<WorkItemType, number>()
    for (const item of scope) {
      counts.set(item.type, (counts.get(item.type) ?? 0) + 1)
    }
    const total = scope.length
    return [...counts.entries()]
      .sort(([, a], [, b]) => b - a)
      .slice(0, 5)
      .map(([type, count]) => ({ type, count, percent: total === 0 ? 0 : Math.round((count / total) * 100) }))
  }, [planningSprint, workItemsById, workItems])

  return (
    <aside className="w-[360px] shrink-0 border-l border-gray-200 dark:border-[#394047] bg-white dark:bg-[#1d2125] h-full overflow-y-auto z-20">
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100 dark:border-[#394047]">
        <h2 className="text-base font-bold text-[#172b4d] dark:text-gray-100">Backlog insights</h2>
        <button className="icon-button text-gray-500 hover:text-gray-700" type="button" aria-label="Close" onClick={onClose}>
          <X size={18} />
        </button>
      </div>

      <div className="px-5 pt-3 pb-2 text-xs text-gray-500 dark:text-gray-400">
        <p>Use these insights to plan your next sprint.</p>
        {planningSprint && <p className="font-semibold text-gray-700 dark:text-gray-300 mt-1">Sprint: {planningSprint.name}</p>}
      </div>

      <div className="px-5 py-3 space-y-6">
        <section>
          <div className="flex items-center justify-between mb-2">
            <h3 className="text-xs font-bold text-gray-900 dark:text-gray-100 uppercase tracking-wide">Sprint commitment</h3>
            <HelpCircle size={14} className="text-gray-400" />
          </div>

          {planningSprint ? (
            <div className="rounded-lg border border-gray-200 dark:border-[#394047] p-3 bg-white dark:bg-[#22272b] space-y-3">
              <div className="grid grid-cols-2 gap-3 text-xs">
                <div>
                  <span className="block text-gray-500">Committed</span>
                  <strong className="text-lg text-gray-900 dark:text-gray-100">{committedPoints} points</strong>
                </div>
                <div>
                  <span className="block text-gray-500">Recommended</span>
                  <strong className="text-lg text-gray-400">Not available yet</strong>
                </div>
              </div>
              <p className="text-[11px] text-gray-400 dark:text-gray-500">
                The points from completed sprints are added up to find your average. Complete a few sprints to see a recommendation here.
              </p>
            </div>
          ) : (
            <p className="text-xs text-gray-500 dark:text-gray-400 py-2">Create a sprint to see commitment insights.</p>
          )}
        </section>

        <section>
          <div className="flex items-center justify-between mb-2">
            <h3 className="text-xs font-bold text-gray-900 dark:text-gray-100 uppercase tracking-wide">Work type breakdown</h3>
            <HelpCircle size={14} className="text-gray-400" />
          </div>
          <p className="text-[11px] text-gray-500 dark:text-gray-400 mb-2">
            Your top work item types to focus on in {planningSprint ? 'this sprint' : 'the backlog'}.
          </p>

          {typeBreakdown.length === 0 ? (
            <p className="text-xs text-gray-500 dark:text-gray-400 py-2">No work items to summarize yet.</p>
          ) : (
            <div className="space-y-2.5">
              {typeBreakdown.map((row) => (
                <div key={row.type} className="space-y-1">
                  <div className="flex items-center justify-between text-xs">
                    <span className="font-medium text-gray-700 dark:text-gray-300">{row.type}</span>
                    <span className="text-gray-500 text-[11px]">{row.count}</span>
                  </div>
                  <div className="h-2 w-full rounded-full bg-gray-200 dark:bg-[#2c333a] overflow-hidden">
                    <div
                      className="h-full rounded-full"
                      style={{ width: `${row.percent}%`, backgroundColor: TYPE_COLORS[row.type] }}
                    />
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>
    </aside>
  )
}
