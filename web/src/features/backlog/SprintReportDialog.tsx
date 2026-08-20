import { useQuery } from '@tanstack/react-query'
import { X } from 'lucide-react'
import { orbitApi } from '../../api/client'
import type { CompletedItem, SprintScopeChange, WorkItemStatus } from '../../api/types'

const CHART_WIDTH = 600
const CHART_HEIGHT = 220
const CHART_PADDING = 32

const STATUS_ORDER: WorkItemStatus[] = ['Backlog', 'Selected', 'InProgress', 'InReview', 'Done', 'Blocked']
const STATUS_COLORS: Record<WorkItemStatus, string> = {
  Backlog: '#9ca3af',
  Selected: '#a78bfa',
  InProgress: '#2563eb',
  InReview: '#f59e0b',
  Done: '#16a34a',
  Blocked: '#dc2626',
}

function shortDate(isoDate: string): string {
  const [, month, day] = isoDate.split('-').map(Number)
  return `${month}/${day}`
}

function scopeChangeLabel(change: SprintScopeChange): string {
  switch (change.factType) {
    case 'SprintAdded':
      return 'Added to sprint'
    case 'SprintRemoved':
      return 'Removed from sprint'
    default:
      return change.factType
  }
}

function CumulativeFlowSection({ sprintId }: { sprintId: string }) {
  const query = useQuery({
    queryKey: ['sprint-cumulative-flow', sprintId],
    queryFn: () => orbitApi.getSprintCumulativeFlowDiagram(sprintId),
  })
  const points = query.data?.points ?? []
  const maxCount = Math.max(1, ...points.map((point) => point.statusCounts.reduce((sum, s) => sum + s.count, 0)))
  const barWidth = points.length > 0 ? (CHART_WIDTH - CHART_PADDING * 2) / points.length : 0
  const plotHeight = CHART_HEIGHT - CHART_PADDING * 2

  return (
    <div className="mt-6">
      <h3 className="text-xs font-semibold text-gray-600 uppercase mb-2">Cumulative flow</h3>
      {query.isPending && <p className="text-sm text-gray-500">Loading…</p>}
      {query.isError && <p className="form-error">{query.error.message}</p>}
      {points.length > 0 ? (
        <>
          <svg viewBox={`0 0 ${CHART_WIDTH} ${CHART_HEIGHT}`} className="w-full h-auto border border-gray-200 rounded bg-white" role="img" aria-label="Cumulative flow diagram">
            <line x1={CHART_PADDING} y1={CHART_HEIGHT - CHART_PADDING} x2={CHART_WIDTH - CHART_PADDING} y2={CHART_HEIGHT - CHART_PADDING} stroke="#d1d5db" />
            <line x1={CHART_PADDING} y1={CHART_PADDING} x2={CHART_PADDING} y2={CHART_HEIGHT - CHART_PADDING} stroke="#d1d5db" />
            {points.map((point, dayIndex) => {
              const x = CHART_PADDING + dayIndex * barWidth
              let stackedY = CHART_HEIGHT - CHART_PADDING
              return (
                <g key={point.date}>
                  {STATUS_ORDER.map((status) => {
                    const count = point.statusCounts.find((s) => s.status === status)?.count ?? 0
                    const segmentHeight = (count / maxCount) * plotHeight
                    const y = stackedY - segmentHeight
                    stackedY = y
                    if (count === 0) {
                      return null
                    }
                    return (
                      <rect key={status} x={x + 1} y={y} width={Math.max(barWidth - 2, 1)} height={segmentHeight} fill={STATUS_COLORS[status]}>
                        <title>{`${status}: ${count}`}</title>
                      </rect>
                    )
                  })}
                  <text x={x + barWidth / 2} y={CHART_HEIGHT - CHART_PADDING + 16} fontSize={10} textAnchor="middle" fill="#6b7280">
                    {shortDate(point.date)}
                  </text>
                </g>
              )
            })}
          </svg>
          <div className="flex flex-wrap gap-3 mt-2">
            {STATUS_ORDER.map((status) => (
              <div key={status} className="flex items-center gap-1 text-xs text-gray-600">
                <span className="inline-block w-2.5 h-2.5 rounded-sm" style={{ backgroundColor: STATUS_COLORS[status] }} />
                {status}
              </div>
            ))}
          </div>
        </>
      ) : (
        !query.isPending && <p className="text-sm text-gray-500">This sprint hasn't started yet, so there's no flow to chart.</p>
      )}
    </div>
  )
}

function formatCycleTime(days: number): string {
  return days < 1 ? `${Math.round(days * 24)}h` : `${days.toFixed(1)}d`
}

function CycleTimeSection({ sprintId }: { sprintId: string }) {
  const query = useQuery({
    queryKey: ['sprint-cycle-time', sprintId],
    queryFn: () => orbitApi.getSprintCycleTimeReport(sprintId),
  })
  const report = query.data

  return (
    <div className="mt-6">
      <h3 className="text-xs font-semibold text-gray-600 uppercase mb-2">Cycle time</h3>
      {query.isPending && <p className="text-sm text-gray-500">Loading…</p>}
      {query.isError && <p className="form-error">{query.error.message}</p>}
      {report && report.items.length > 0 ? (
        <>
          <dl className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <div><dt className="text-xs text-gray-500 uppercase">Items completed</dt><dd className="text-lg font-semibold text-gray-900">{report.items.length}</dd></div>
            <div><dt className="text-xs text-gray-500 uppercase">Average</dt><dd className="text-lg font-semibold text-gray-900">{report.averageCycleTimeDays !== null ? formatCycleTime(report.averageCycleTimeDays) : '—'}</dd></div>
            <div><dt className="text-xs text-gray-500 uppercase">Median</dt><dd className="text-lg font-semibold text-gray-900">{report.medianCycleTimeDays !== null ? formatCycleTime(report.medianCycleTimeDays) : '—'}</dd></div>
          </dl>
          <table className="w-full text-sm mt-3">
            <tbody>
              {report.items.map((item: CompletedItem) => (
                <tr key={item.workItemId} className="border-b border-gray-100">
                  <td className="py-1 pr-3 text-gray-500">{new Date(item.completedAt).toLocaleString()}</td>
                  <td className="py-1 text-right text-gray-700">{formatCycleTime(item.cycleTimeDays)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      ) : (
        !query.isPending && <p className="text-sm text-gray-500">No items in this sprint have completed yet.</p>
      )}
    </div>
  )
}

function ControlChartSection({ sprintId }: { sprintId: string }) {
  const query = useQuery({
    queryKey: ['sprint-control-chart', sprintId],
    queryFn: () => orbitApi.getSprintControlChart(sprintId),
  })
  const chart = query.data
  const sortedPoints = [...(chart?.points ?? [])].sort((a, b) => a.completedAt.localeCompare(b.completedAt))
  const maxDays = Math.max(1, ...sortedPoints.map((p) => p.cycleTimeDays), chart?.p85CycleTimeDays ?? 0)
  const toX = (index: number) => CHART_PADDING + (index / Math.max(sortedPoints.length - 1, 1)) * (CHART_WIDTH - CHART_PADDING * 2)
  const toY = (days: number) => CHART_HEIGHT - CHART_PADDING - (days / maxDays) * (CHART_HEIGHT - CHART_PADDING * 2)

  return (
    <div className="mt-6">
      <h3 className="text-xs font-semibold text-gray-600 uppercase mb-2">Control chart</h3>
      {query.isPending && <p className="text-sm text-gray-500">Loading…</p>}
      {query.isError && <p className="form-error">{query.error.message}</p>}
      {sortedPoints.length > 0 ? (
        <svg viewBox={`0 0 ${CHART_WIDTH} ${CHART_HEIGHT}`} className="w-full h-auto border border-gray-200 rounded bg-white" role="img" aria-label="Control chart">
          <line x1={CHART_PADDING} y1={CHART_HEIGHT - CHART_PADDING} x2={CHART_WIDTH - CHART_PADDING} y2={CHART_HEIGHT - CHART_PADDING} stroke="#d1d5db" />
          <line x1={CHART_PADDING} y1={CHART_PADDING} x2={CHART_PADDING} y2={CHART_HEIGHT - CHART_PADDING} stroke="#d1d5db" />
          {chart?.averageCycleTimeDays != null && (
            <line x1={CHART_PADDING} y1={toY(chart.averageCycleTimeDays)} x2={CHART_WIDTH - CHART_PADDING} y2={toY(chart.averageCycleTimeDays)} stroke="#9ca3af" strokeDasharray="4 4" />
          )}
          {chart?.p85CycleTimeDays != null && (
            <line x1={CHART_PADDING} y1={toY(chart.p85CycleTimeDays)} x2={CHART_WIDTH - CHART_PADDING} y2={toY(chart.p85CycleTimeDays)} stroke="#dc2626" strokeDasharray="2 3" />
          )}
          {sortedPoints.map((point, index) => (
            <circle key={point.workItemId} cx={toX(index)} cy={toY(point.cycleTimeDays)} r={4} fill="#2563eb">
              <title>{`${formatCycleTime(point.cycleTimeDays)} - completed ${new Date(point.completedAt).toLocaleDateString()}`}</title>
            </circle>
          ))}
        </svg>
      ) : (
        !query.isPending && <p className="text-sm text-gray-500">No completed items to plot yet.</p>
      )}
      {chart && sortedPoints.length > 0 && (
        <p className="text-xs text-gray-500 mt-1">Dashed grey: average ({chart.averageCycleTimeDays !== null ? formatCycleTime(chart.averageCycleTimeDays) : '—'}). Dashed red: 85th percentile ({chart.p85CycleTimeDays !== null ? formatCycleTime(chart.p85CycleTimeDays) : '—'}).</p>
      )}
    </div>
  )
}

export function SprintReportDialog({ sprintId, sprintName, onClose }: { sprintId: string; sprintName: string; onClose: () => void }) {
  const reportQuery = useQuery({
    queryKey: ['sprint-report', sprintId],
    queryFn: () => orbitApi.getSprintReport(sprintId),
  })
  const report = reportQuery.data

  const maxPoints = report ? Math.max(report.committedPoints, ...report.burndown.map((point) => point.remainingPoints), 1) : 1
  const points = report?.burndown ?? []
  const toX = (index: number) => CHART_PADDING + (index / Math.max(points.length - 1, 1)) * (CHART_WIDTH - CHART_PADDING * 2)
  const toY = (value: number) => CHART_HEIGHT - CHART_PADDING - (value / maxPoints) * (CHART_HEIGHT - CHART_PADDING * 2)

  const actualPath = points.map((point, index) => `${index === 0 ? 'M' : 'L'} ${toX(index)} ${toY(point.remainingPoints)}`).join(' ')
  const idealPath = report && points.length > 1
    ? `M ${toX(0)} ${toY(report.committedPoints)} L ${toX(points.length - 1)} ${toY(0)}`
    : ''

  return (
    <div className="dialog-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <section className="dialog create-work-dialog" role="dialog" aria-modal="true" aria-labelledby="sprint-report-title">
        <header>
          <div><h2 id="sprint-report-title">{sprintName} report</h2><p className="mt-1 text-xs text-gray-500">Burndown, flow, and cycle time, computed from the sprint's immutable fact log.</p></div>
          <button className="icon-button" type="button" aria-label="Close" onClick={onClose}><X size={20} /></button>
        </header>

        {reportQuery.isPending && <p className="px-6 py-4 text-sm text-gray-500">Loading report…</p>}
        {reportQuery.isError && <p className="form-error px-6">{reportQuery.error.message}</p>}

        {report && (
          <div className="px-6 pb-6">
            {points.length > 1 ? (
              <svg viewBox={`0 0 ${CHART_WIDTH} ${CHART_HEIGHT}`} className="w-full h-auto border border-gray-200 rounded bg-white" role="img" aria-label="Sprint burndown chart">
                <line x1={CHART_PADDING} y1={CHART_HEIGHT - CHART_PADDING} x2={CHART_WIDTH - CHART_PADDING} y2={CHART_HEIGHT - CHART_PADDING} stroke="#d1d5db" />
                <line x1={CHART_PADDING} y1={CHART_PADDING} x2={CHART_PADDING} y2={CHART_HEIGHT - CHART_PADDING} stroke="#d1d5db" />
                {idealPath && <path d={idealPath} fill="none" stroke="#9ca3af" strokeDasharray="4 4" strokeWidth={1.5} />}
                <path d={actualPath} fill="none" stroke="#2563eb" strokeWidth={2} />
                {points.map((point, index) => (
                  <circle key={point.date} cx={toX(index)} cy={toY(point.remainingPoints)} r={3} fill="#2563eb" />
                ))}
                {points.map((point, index) => (
                  <text key={point.date} x={toX(index)} y={CHART_HEIGHT - CHART_PADDING + 16} fontSize={10} textAnchor="middle" fill="#6b7280">
                    {shortDate(point.date)}
                  </text>
                ))}
              </svg>
            ) : (
              <p className="text-sm text-gray-500">This sprint hasn't started yet, so there's no burndown baseline.</p>
            )}

            <dl className="grid grid-cols-2 gap-4 mt-4 sm:grid-cols-4">
              <div><dt className="text-xs text-gray-500 uppercase">Committed</dt><dd className="text-lg font-semibold text-gray-900">{report.committedPoints}</dd></div>
              <div><dt className="text-xs text-gray-500 uppercase">Completed</dt><dd className="text-lg font-semibold text-green-700">{report.completedPoints}</dd></div>
              <div><dt className="text-xs text-gray-500 uppercase">Added after start</dt><dd className="text-lg font-semibold text-gray-900">{report.addedAfterStartPoints}</dd></div>
              <div><dt className="text-xs text-gray-500 uppercase">Removed after start</dt><dd className="text-lg font-semibold text-gray-900">{report.removedAfterStartPoints}</dd></div>
            </dl>

            {report.scopeChanges.length > 0 && (
              <div className="mt-4">
                <h3 className="text-xs font-semibold text-gray-600 uppercase mb-2">Scope changes</h3>
                <table className="w-full text-sm">
                  <tbody>
                    {report.scopeChanges.map((change, index) => (
                      <tr key={index} className="border-b border-gray-100">
                        <td className="py-1 pr-3 text-gray-500">{new Date(change.occurredAt).toLocaleString()}</td>
                        <td className="py-1 pr-3">{scopeChangeLabel(change)}</td>
                        <td className="py-1 text-right text-gray-700">{change.estimateDelta ?? 0} pts</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            <CumulativeFlowSection sprintId={sprintId} />
            <CycleTimeSection sprintId={sprintId} />
            <ControlChartSection sprintId={sprintId} />
          </div>
        )}

        <footer className="sticky bottom-0 -mx-6 -mb-6 border-t border-gray-200 bg-white px-6 py-4">
          <button type="button" className="secondary-button" onClick={onClose}>Close</button>
        </footer>
      </section>
    </div>
  )
}
