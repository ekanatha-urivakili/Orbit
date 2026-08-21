import { useMemo, useState } from 'react'
import { ChevronRight, Plus } from 'lucide-react'
import { buildTimelineLayout, type TimelineBar } from './timelineLayout'
import { WorkItemTypeIcon } from '../workitems/typeIcons'
import type { Sprint, WorkItem } from '../../api/types'

const ROW_HEIGHT = 44

export function TimelineView({
  workItems,
  sprints,
  onOpenWorkItem,
  onCreateEpic,
}: {
  workItems: WorkItem[]
  sprints: Sprint[]
  onOpenWorkItem: (workItem: WorkItem) => void
  onCreateEpic?: () => void
}) {
  const [expanded, setExpanded] = useState<Record<string, boolean>>({})
  const layout = useMemo(() => buildTimelineLayout(workItems, sprints), [workItems, sprints])
  const workItemsById = useMemo(() => new Map(workItems.map((item) => [item.id, item])), [workItems])

  if (!layout) {
    return (
      <div className="p-4 sm:p-8">
        <div className="flex flex-col items-center justify-center py-20 border border-dashed border-gray-200 rounded-lg text-center">
          <p className="text-gray-700 font-medium mb-1">Nothing to show on the timeline yet</p>
          <p className="text-sm text-gray-500 max-w-sm">
            Set a start date and due date on an epic (or its work items), or start a sprint with dates, to see it plotted here.
          </p>
        </div>
      </div>
    )
  }

  const dayWidth = 100 / layout.totalDays
  const toggle = (epicId: string) => setExpanded((current) => ({ ...current, [epicId]: !current[epicId] }))

  const openBar = (bar: TimelineBar) => {
    const item = workItemsById.get(bar.id)
    if (item) onOpenWorkItem(item)
  }

  return (
    <div className="p-4 sm:p-8">
      <div className="border border-gray-200 rounded-lg overflow-hidden bg-white">
        <div className="flex overflow-x-auto" data-testid="timeline-grid">
          <div className="shrink-0 w-[160px] sm:w-[220px] lg:w-[280px]">
            <div className="h-11 border-b border-r border-gray-200 flex items-center px-4 text-xs font-semibold text-gray-500 uppercase">
              Work
            </div>
            <TimelineLeftRow label="Sprints" bold />
            {layout.epicRows.map(({ epic, children }) => (
              <div key={epic.id}>
                <TimelineLeftRow
                  label={
                    <button
                      onClick={() => toggle(epic.id)}
                      className="flex items-center gap-1.5 text-left w-full"
                      aria-expanded={Boolean(expanded[epic.id])}
                      aria-label={`Toggle ${epic.key}`}
                    >
                      <ChevronRight size={14} className={`text-gray-400 shrink-0 transition-transform ${expanded[epic.id] ? 'rotate-90' : ''}`} />
                      <WorkItemTypeIcon type="Epic" size={14} />
                      <span className="text-blue-700 font-medium shrink-0">{epic.key}</span>
                      <span className="text-gray-700 truncate">{epic.summary}</span>
                    </button>
                  }
                />
                {expanded[epic.id] &&
                  children.map((child) => {
                    const childItem = workItemsById.get(child.id)
                    return (
                      <TimelineLeftRow
                        key={child.id}
                        indent
                        label={
                          <span className="flex items-center gap-1.5 min-w-0">
                            {childItem && <WorkItemTypeIcon type={childItem.type} size={13} />}
                            <span className="text-blue-700 shrink-0">{child.key}</span>
                            <span className="text-gray-600 truncate">{child.summary}</span>
                          </span>
                        }
                      />
                    )
                  })}
              </div>
            ))}
            <div className="border-r border-gray-100" style={{ height: ROW_HEIGHT }}>
              <button
                onClick={onCreateEpic}
                disabled={!onCreateEpic}
                className="h-full w-full flex items-center gap-1.5 px-4 text-sm text-gray-600 hover:bg-gray-50 disabled:opacity-50 disabled:hover:bg-transparent"
              >
                <Plus size={14} /> Create Epic
              </button>
            </div>
          </div>

          <div className="flex-1 min-w-[420px] sm:min-w-[600px] relative">
            <div className="h-11 border-b border-gray-200 flex">
              {layout.months.map((month) => (
                <div
                  key={`${month.label}-${month.startOffsetDays}`}
                  className="border-r border-gray-100 last:border-r-0 flex items-center justify-center text-xs font-semibold text-gray-500"
                  style={{ width: `${(month.days / layout.totalDays) * 100}%` }}
                >
                  {month.label}
                </div>
              ))}
            </div>

            <div className="relative">
              {layout.todayOffsetDays !== null && (
                <div
                  className="absolute top-0 bottom-0 w-px bg-blue-500 z-10"
                  style={{ left: `${layout.todayOffsetDays * dayWidth}%` }}
                  data-testid="timeline-today-marker"
                />
              )}

              <TimelineTrackRow>
                {layout.sprintBars.map(({ sprint, startOffsetDays, durationDays }) => (
                  <div
                    key={sprint.id}
                    className="absolute top-1/2 -translate-y-1/2 h-5 rounded bg-slate-700 text-white text-[11px] font-medium flex items-center px-2 truncate"
                    style={{ left: `${startOffsetDays * dayWidth}%`, width: `${Math.max(durationDays * dayWidth, 3)}%` }}
                    title={sprint.name}
                  >
                    {sprint.name}
                  </div>
                ))}
              </TimelineTrackRow>

              {layout.epicRows.map(({ epic, bar, children }) => (
                <div key={epic.id}>
                  <TimelineTrackRow>
                    {bar && (
                      <button
                        onClick={() => openBar(bar)}
                        className="absolute top-1/2 -translate-y-1/2 h-5 rounded-full text-white text-[11px] font-medium flex items-center px-2 truncate hover:brightness-95"
                        style={{
                          left: `${bar.startOffsetDays * dayWidth}%`,
                          width: `${Math.max(bar.durationDays * dayWidth, 2)}%`,
                          backgroundColor: '#8b5cf6',
                        }}
                        title={`${bar.key}: ${bar.summary}`}
                      >
                        {bar.durationDays * dayWidth > 6 ? bar.summary : ''}
                      </button>
                    )}
                  </TimelineTrackRow>
                  {expanded[epic.id] &&
                    children.map((child) => (
                      <TimelineTrackRow key={child.id}>
                        <button
                          onClick={() => openBar(child)}
                          className="absolute top-1/2 -translate-y-1/2 h-4 rounded text-white text-[10px] font-medium flex items-center px-1.5 truncate hover:brightness-95"
                          style={{
                            left: `${child.startOffsetDays * dayWidth}%`,
                            width: `${Math.max(child.durationDays * dayWidth, 2)}%`,
                            backgroundColor: '#2f7fe0',
                          }}
                          title={`${child.key}: ${child.summary}`}
                        >
                          {child.durationDays * dayWidth > 6 ? child.summary : ''}
                        </button>
                      </TimelineTrackRow>
                    ))}
                </div>
              ))}

              <div style={{ height: ROW_HEIGHT }} />
            </div>
          </div>
        </div>
      </div>
      {layout.unscheduledEpicCount > 0 && (
        <p className="mt-3 text-xs text-gray-500">
          {layout.unscheduledEpicCount} epic{layout.unscheduledEpicCount === 1 ? '' : 's'} without a start or due date{' '}
          {layout.unscheduledEpicCount === 1 ? "isn't" : "aren't"} shown on the timeline.
        </p>
      )}
    </div>
  )
}

function TimelineLeftRow({ label, bold = false, indent = false }: { label: React.ReactNode; bold?: boolean; indent?: boolean }) {
  return (
    <div
      className="border-b border-r border-gray-100 flex items-center px-4 text-sm text-gray-700 min-w-0"
      style={{ height: ROW_HEIGHT, paddingLeft: indent ? 40 : 16 }}
    >
      <span className={`truncate ${bold ? 'font-bold text-gray-900' : ''}`}>{label}</span>
    </div>
  )
}

function TimelineTrackRow({ children }: { children: React.ReactNode }) {
  return (
    <div className="relative border-b border-gray-100" style={{ height: ROW_HEIGHT }}>
      {children}
    </div>
  )
}
