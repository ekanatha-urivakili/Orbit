import { useState } from 'react'
import { X, CheckCircle2, CircleDot } from 'lucide-react'
import type { Sprint, WorkItem, WorkItemStatusDefinition } from '../../api/types'
import { SearchableSelect } from '../../components/form/SearchableSelect'

interface CompleteSprintDialogProps {
  sprint: Sprint
  workItems: WorkItem[]
  statuses: WorkItemStatusDefinition[]
  futureSprints: Sprint[]
  pending?: boolean
  onClose: () => void
  onComplete: (rolloverTargetSprintId: string | null) => void
}

export function CompleteSprintDialog({
  sprint,
  workItems,
  statuses,
  futureSprints,
  pending = false,
  onClose,
  onComplete,
}: CompleteSprintDialogProps) {
  const [rolloverTargetId, setRolloverTargetId] = useState<string>('')

  // Determine sprint items and counts
  const sprintItemIds = new Set(sprint.workItemIds)
  const sprintItems = workItems.filter((item) => sprintItemIds.has(item.id))

  const statusById = new Map(statuses.map((status) => [status.id, status]))
  const completedItems = sprintItems.filter((item) => {
    const status = statusById.get(item.statusId)
    return status?.category === 'Done'
  })
  const openItems = sprintItems.filter((item) => {
    const status = statusById.get(item.statusId)
    return status?.category !== 'Done'
  })

  const otherFutureSprints = futureSprints.filter((s) => s.id !== sprint.id && s.state === 'Future')

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    onComplete(rolloverTargetId || null)
  }

  return (
    <div
      className="dialog-backdrop"
      role="presentation"
      onMouseDown={(event) => event.target === event.currentTarget && onClose()}
    >
      <section
        className="dialog w-[500px] max-w-full"
        role="dialog"
        aria-modal="true"
        aria-labelledby="complete-sprint-title"
      >
        <header className="flex items-center justify-between px-6 py-5 border-b border-gray-100 dark:border-[#394047]">
          <h2 id="complete-sprint-title" className="text-xl font-semibold text-gray-900 dark:text-gray-100">
            Complete {sprint.name}
          </h2>
          <button className="icon-button text-gray-500 hover:text-gray-700" type="button" aria-label="Close" onClick={onClose}>
            <X size={18} />
          </button>
        </header>

        <form onSubmit={handleSubmit} className="p-6 space-y-5">
          <div className="space-y-3 text-sm text-gray-700 dark:text-gray-300">
            <p className="font-medium text-gray-900 dark:text-gray-100">This sprint contains:</p>
            <div className="space-y-2 pl-1">
              <div className="flex items-center gap-2.5 text-green-600 dark:text-green-400">
                <CheckCircle2 size={16} />
                <span>
                  <strong className="font-semibold text-gray-900 dark:text-gray-100">{completedItems.length}</strong> completed work item{completedItems.length === 1 ? '' : 's'}
                </span>
              </div>
              <div className="flex items-center gap-2.5 text-amber-600 dark:text-amber-400">
                <CircleDot size={16} />
                <span>
                  <strong className="font-semibold text-gray-900 dark:text-gray-100">{openItems.length}</strong> open work item{openItems.length === 1 ? '' : 's'}
                </span>
              </div>
            </div>
          </div>

          {openItems.length > 0 && (
            <div className="space-y-2 pt-2 border-t border-gray-100 dark:border-[#394047]">
              <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase tracking-wide">
                Select where all open work items should be moved:
              </label>
              <SearchableSelect
                value={rolloverTargetId}
                onChange={(val) => setRolloverTargetId(val)}
                options={[
                  { value: '', label: 'Backlog' },
                  ...otherFutureSprints.map((target) => ({
                    value: target.id,
                    label: target.name,
                  })),
                ]}
                searchable={false}
              />
            </div>
          )}

          <div className="flex justify-end items-center gap-3 pt-3">
            <button type="button" onClick={onClose} className="secondary-button">
              Cancel
            </button>
            <button
              type="submit"
              disabled={pending}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white font-medium text-sm rounded-md shadow-sm transition-colors disabled:opacity-50"
            >
              {pending ? 'Completing…' : 'Complete sprint'}
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}
