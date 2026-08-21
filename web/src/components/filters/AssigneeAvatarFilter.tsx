import { User } from 'lucide-react'
import { getInitials } from '../../lib/initials'
import { UNASSIGNED, type WorkItemFilterFieldState } from '../../hooks/useWorkItemFilters'

export function AssigneeAvatarFilter({ field }: { field: WorkItemFilterFieldState }) {
  if (field.options.length === 0) return null

  return (
    <div className="flex items-center -space-x-2">
      {field.options.map((option) => {
        const selected = field.selected.includes(option.value)
        const isUnassigned = option.value === UNASSIGNED
        return (
          <button
            key={option.value}
            type="button"
            onClick={() => field.toggle(option.value)}
            title={option.label}
            aria-pressed={selected}
            className={`w-7 h-7 rounded-full flex items-center justify-center text-[10px] font-bold text-white transition-transform hover:z-10 hover:scale-110 ring-2 ${
              selected ? 'ring-blue-600' : 'ring-white dark:ring-[#101214]'
            } ${isUnassigned ? 'bg-gray-400' : 'bg-orange-500'}`}
          >
            {isUnassigned ? <User size={12} /> : getInitials(option.label)}
          </button>
        )
      })}
    </div>
  )
}
