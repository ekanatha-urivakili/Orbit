import { useQuery } from '@tanstack/react-query'
import { X } from 'lucide-react'
import { orbitApi } from '../../api/client'
import type { SprintScopeChange } from '../../api/types'

const CHART_WIDTH = 600
const CHART_HEIGHT = 220
const CHART_PADDING = 32

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
          <div><h2 id="sprint-report-title">{sprintName} report</h2><p className="mt-1 text-xs text-gray-500">Burndown and scope changes, computed from the sprint's immutable fact log.</p></div>
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
          </div>
        )}

        <footer className="sticky bottom-0 -mx-6 -mb-6 border-t border-gray-200 bg-white px-6 py-4">
          <button type="button" className="secondary-button" onClick={onClose}>Close</button>
        </footer>
      </section>
    </div>
  )
}
