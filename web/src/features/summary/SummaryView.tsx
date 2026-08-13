import type { ReactNode } from 'react'
import { CheckCircle2, Edit3, PlusSquare, Maximize2 } from 'lucide-react'
import { groupWorkItemsByStatus } from '../../board'
import { allStatuses, statusMeta } from '../board/constants'
import { getInitials } from '../../lib/initials'
import type { Profile, WorkItem } from '../../api/types'

const toneColors: Record<string, string> = {
  slate: '#94a3b8',
  cyan: '#10a7b5',
  blue: '#3861fb',
  amber: '#d97706',
  green: '#28a06b',
  red: '#dc2626',
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

export function SummaryView({ workItems, profile }: { workItems: WorkItem[]; profile?: Profile }) {
  const sevenDaysAgo = Date.now() - sevenDaysMs
  const createdRecently = workItems.filter((item) => new Date(item.createdAt).getTime() >= sevenDaysAgo).length
  const updatedRecently = workItems.filter((item) => new Date(item.updatedAt).getTime() >= sevenDaysAgo).length
  const completedRecently = workItems.filter(
    (item) => item.status === 'Done' && new Date(item.updatedAt).getTime() >= sevenDaysAgo,
  ).length

  const statusCounts = groupWorkItemsByStatus(allStatuses, workItems)
  const nonEmptyStatuses = allStatuses
    .map((status) => ({ status, ...statusMeta[status] }))
    .filter((column) => (statusCounts.get(column.status)?.length ?? 0) > 0)

  let offset = 0
  const segments = nonEmptyStatuses.map((column) => {
    const count = statusCounts.get(column.status)?.length ?? 0
    const length = workItems.length === 0 ? 0 : (count / workItems.length) * circumference
    const segment = { column, dasharray: `${length} ${circumference}`, dashoffset: -offset }
    offset += length
    return segment
  })

  const recentActivity = [...workItems]
    .sort((a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime())
    .slice(0, 5)

  return (
    <div className="p-8 max-w-5xl bg-gray-50 min-h-screen">
      <div className="grid grid-cols-3 gap-4 mb-6">
        <StatTile icon={<CheckCircle2 size={20} />} value={completedRecently} label="completed in the last 7 days" />
        <StatTile icon={<Edit3 size={20} />} value={updatedRecently} label="updated in the last 7 days" />
        <StatTile icon={<PlusSquare size={20} />} value={createdRecently} label="created in the last 7 days" />
      </div>

      <div className="grid grid-cols-2 gap-6">
        <div className="bg-white border border-gray-200 rounded-lg shadow-sm p-6">
          <h3 className="font-bold text-gray-900 mb-1">Status overview</h3>
          <p className="text-sm text-gray-500 mb-8">A snapshot of the status of your work items.</p>

          {workItems.length === 0 ? (
            <p className="text-sm text-gray-500">No work items yet.</p>
          ) : (
            <div className="flex items-center gap-12">
              <div className="relative w-48 h-48">
                <svg viewBox="0 0 100 100" className="w-full h-full transform -rotate-90">
                  {segments.map(({ column, dasharray, dashoffset }) => (
                    <circle
                      key={column.status}
                      cx="50"
                      cy="50"
                      r="40"
                      fill="transparent"
                      stroke={toneColors[column.tone] ?? '#94a3b8'}
                      strokeWidth="16"
                      strokeDasharray={dasharray}
                      strokeDashoffset={dashoffset}
                    />
                  ))}
                </svg>
                <div className="absolute inset-0 flex flex-col items-center justify-center">
                  <span className="text-3xl font-bold text-gray-900">{workItems.length}</span>
                  <span className="text-xs text-gray-500 font-medium text-center w-20 leading-tight">Total work items</span>
                </div>
              </div>

              <div className="space-y-4 flex-1">
                {nonEmptyStatuses.map((column) => (
                  <div key={column.status} className="flex items-center justify-between text-sm">
                    <div className="flex items-center gap-2 text-gray-700">
                      <div className="w-3 h-3 rounded-sm" style={{ backgroundColor: toneColors[column.tone] ?? '#94a3b8' }} /> {column.label}
                    </div>
                    <span className="font-medium">{statusCounts.get(column.status)?.length ?? 0}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        <div className="bg-white border border-gray-200 rounded-lg shadow-sm p-6 flex flex-col">
          <div className="flex justify-between items-start mb-6">
            <div>
              <h3 className="font-bold text-gray-900 mb-1">Recent activity</h3>
              <p className="text-sm text-gray-500">Work items updated most recently in this project.</p>
            </div>
            <button className="p-1.5 hover:bg-gray-100 rounded text-gray-500"><Maximize2 size={16} /></button>
          </div>

          <div className="flex-1 overflow-y-auto max-h-[300px] pr-2 custom-scrollbar">
            {recentActivity.length === 0 ? (
              <p className="text-sm text-gray-500">No activity yet.</p>
            ) : (
              <div className="space-y-6">
                {recentActivity.map((item) => (
                  <div key={item.id} className="flex gap-4">
                    <div className="w-8 h-8 rounded-full bg-orange-500 text-white flex items-center justify-center text-xs font-bold flex-shrink-0">
                      {getInitials(profile?.displayName)}
                    </div>
                    <div>
                      <div className="text-sm text-gray-800 mb-1">
                        <span className="font-medium text-blue-600">{profile?.displayName ?? 'Someone'}</span> updated{' '}
                        <span className="inline-flex items-center gap-1 text-xs px-1.5 py-0.5 bg-gray-100 rounded font-medium text-gray-700">{item.key}: {item.summary}</span>
                      </div>
                      <div className="inline-block px-2 py-0.5 bg-blue-100 text-blue-800 text-xs font-medium rounded mb-1">{item.status === 'Backlog' ? 'To Do' : item.status}</div>
                      <div className="text-xs text-gray-400">{formatRelativeTime(item.updatedAt)}</div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

function StatTile({ icon, value, label }: { icon: ReactNode; value: number; label: string }) {
  return (
    <div className="bg-white border border-gray-200 p-4 rounded-lg shadow-sm flex items-start gap-3">
      <div className="p-2 bg-gray-100 rounded text-gray-600">{icon}</div>
      <div>
        <div className="font-bold text-gray-900 text-lg leading-tight">{value}</div>
        <div className="text-xs text-gray-500 mt-1">{label}</div>
      </div>
    </div>
  )
}
