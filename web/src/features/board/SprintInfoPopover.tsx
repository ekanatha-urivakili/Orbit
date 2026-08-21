import { useEffect, useRef } from 'react'
import type { Sprint } from '../../api/types'

function formatDate(dateStr?: string | null): string {
  if (!dateStr) return 'None'
  try {
    const d = new Date(dateStr)
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
  } catch {
    return dateStr
  }
}

function calculateDaysLeft(endDateStr?: string | null): string {
  if (!endDateStr) return 'No end date'
  try {
    const end = new Date(endDateStr).getTime()
    const now = Date.now()
    const diffDays = Math.ceil((end - now) / (1000 * 60 * 60 * 24))
    if (diffDays < 0) return `${Math.abs(diffDays)} days overdue`
    if (diffDays === 0) return 'Ends today'
    if (diffDays === 1) return '1 day left'
    return `${diffDays} days left`
  } catch {
    return ''
  }
}

export function SprintInfoPopover({
  sprint,
  onClose,
}: {
  sprint: Sprint
  onClose: () => void
}) {
  const popoverRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function handleMouseDown(e: MouseEvent) {
      if (popoverRef.current && !popoverRef.current.contains(e.target as Node)) {
        onClose()
      }
    }
    document.addEventListener('mousedown', handleMouseDown)
    return () => document.removeEventListener('mousedown', handleMouseDown)
  }, [onClose])

  const daysLeft = calculateDaysLeft(sprint.endDate)

  return (
    <div
      ref={popoverRef}
      className="absolute right-0 top-full mt-2 w-64 p-4 bg-white dark:bg-[#1d2125] border border-gray-200 dark:border-[#394047] rounded-xl shadow-xl z-50 text-left"
    >
      <h4 className="text-sm font-bold text-[#172b4d] dark:text-gray-100 mb-1">{sprint.name}</h4>
      <p className="text-xs text-gray-500 dark:text-gray-400 mb-3">{daysLeft}</p>
      
      <div className="grid grid-cols-2 gap-4 text-xs">
        <div>
          <span className="block text-[11px] text-gray-400 dark:text-gray-500 mb-0.5">Start date</span>
          <span className="font-medium text-gray-700 dark:text-gray-200">{formatDate(sprint.startDate)}</span>
        </div>
        <div>
          <span className="block text-[11px] text-gray-400 dark:text-gray-500 mb-0.5">End date</span>
          <span className="font-medium text-gray-700 dark:text-gray-200">{formatDate(sprint.endDate)}</span>
        </div>
      </div>
      {sprint.goal && (
        <div className="mt-3 pt-2 border-t border-gray-100 dark:border-[#394047] text-xs">
          <span className="block text-[11px] text-gray-400 dark:text-gray-500 mb-0.5">Goal</span>
          <p className="text-gray-700 dark:text-gray-300 italic">{sprint.goal}</p>
        </div>
      )}
    </div>
  )
}
