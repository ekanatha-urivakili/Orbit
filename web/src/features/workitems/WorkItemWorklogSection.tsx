import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Trash2 } from 'lucide-react'
import { orbitApi } from '../../api/client'
import type { TenantMembership } from '../../api/types'

function formatDuration(minutes: number): string {
  const hours = Math.floor(minutes / 60)
  const remainder = minutes % 60
  if (hours === 0) return `${remainder}m`
  if (remainder === 0) return `${hours}h`
  return `${hours}h ${remainder}m`
}

export function WorkItemWorklogSection({
  workItemId,
  members,
  currentMembershipId,
}: {
  workItemId: string
  members: TenantMembership[]
  currentMembershipId?: string
}) {
  const queryClient = useQueryClient()
  const worklogsQuery = useQuery({
    queryKey: ['work-item-worklogs', workItemId],
    queryFn: () => orbitApi.listWorklogs(workItemId),
  })
  const worklogs = worklogsQuery.data?.items ?? []
  const membersById = new Map(members.map((member) => [member.id, member]))

  const deleteMutation = useMutation({
    mutationFn: (worklogId: string) => orbitApi.deleteWorklog(workItemId, worklogId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['work-item-worklogs', workItemId] }),
  })

  if (worklogs.length === 0) return null

  const totalMinutes = worklogs.reduce((sum, worklog) => sum + worklog.minutesSpent, 0)

  return (
    <section className="mt-8 border-t border-gray-200 pt-6">
      <div className="flex items-center justify-between mb-3">
        <h2 className="text-sm font-bold text-[#172b4d]">Work log</h2>
        <span className="text-xs text-gray-500">Total: {formatDuration(totalMinutes)}</span>
      </div>
      <div className="space-y-1.5">
        {worklogs.map((worklog) => (
          <div
            key={worklog.id}
            className="flex items-center gap-2.5 py-1.5 px-2 rounded-md hover:bg-gray-50 text-sm border border-transparent hover:border-gray-200 group"
          >
            <span className="font-semibold text-gray-700 text-xs w-14 shrink-0">
              {formatDuration(worklog.minutesSpent)}
            </span>
            <span className="text-xs text-gray-500 w-24 shrink-0">{worklog.workDate}</span>
            <span className="text-xs text-gray-500 w-32 shrink-0 truncate">
              {membersById.get(worklog.authorMembershipId)?.displayName ?? 'Unknown'}
            </span>
            <span className="flex-1 text-[#172b4d] truncate">{worklog.description}</span>
            {worklog.authorMembershipId === currentMembershipId && (
              <button
                type="button"
                className="p-1 text-gray-400 hover:text-red-600 opacity-0 group-hover:opacity-100 transition-opacity"
                aria-label="Delete work log entry"
                onClick={() => deleteMutation.mutate(worklog.id)}
              >
                <Trash2 size={14} />
              </button>
            )}
          </div>
        ))}
      </div>
    </section>
  )
}
