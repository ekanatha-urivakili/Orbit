import { useEffect, useRef, useState } from 'react'
import { User } from 'lucide-react'
import { getInitials } from '../lib/initials'
import type { TenantMembership, WorkItem } from '../api/types'

export function AssigneePicker({
  workItem,
  members,
  onChange,
  size = 'md',
  value,
  disabled = false,
}: {
  workItem?: WorkItem
  members: TenantMembership[]
  onChange: (assigneeUserId: string | null) => void
  size?: 'sm' | 'md'
  value?: string | null
  disabled?: boolean
}) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const handleClickOutside = (event: MouseEvent) => {
      if (ref.current && !ref.current.contains(event.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [open])

  const currentAssigneeId = value !== undefined ? value : workItem?.assigneeUserId
  const member = currentAssigneeId ? members.find((m) => m.userId === currentAssigneeId) : undefined
  const dimension = size === 'sm' ? 'w-5 h-5 text-[10px]' : 'w-6 h-6 text-xs'

  return (
    <div className="relative shrink-0" ref={ref} onClick={(event) => event.stopPropagation()}>
      <button
        type="button"
        disabled={disabled}
        onClick={() => setOpen((current) => !current)}
        className={`${dimension} rounded-full flex items-center justify-center font-bold border border-white transition-transform hover:scale-105 disabled:cursor-not-allowed disabled:opacity-50 ${
          member ? 'bg-orange-500 text-white' : 'bg-gray-200 text-gray-500'
        }`}
        title={member?.displayName ?? 'Unassigned — click to assign'}
        aria-label="Change assignee"
      >
        {member ? getInitials(member.displayName ?? undefined) : <User size={size === 'sm' ? 10 : 12} />}
      </button>
      {open && (
        <div className="absolute right-0 top-full mt-1 w-48 bg-white border border-gray-200 shadow-xl rounded-lg py-1 z-50 max-h-64 overflow-y-auto">
          <button
            type="button"
            disabled={disabled}
            onClick={() => {
              onChange(null)
              setOpen(false)
            }}
            className={`w-full text-left px-3 py-2 text-sm hover:bg-gray-100 flex items-center gap-2 ${
              currentAssigneeId === null || currentAssigneeId === undefined ? 'bg-blue-50 text-blue-700' : ''
            }`}
          >
            <div className="w-5 h-5 rounded-full bg-gray-200 text-gray-500 flex items-center justify-center text-xs">
              <User size={12} />
            </div>
            Unassigned
          </button>
          {members.filter((m) => m.userId).map((m) => (
            <button
              key={m.id}
              type="button"
              disabled={disabled}
              aria-label={m.displayName ?? 'Unnamed member'}
              onClick={() => {
                onChange(m.userId)
                setOpen(false)
              }}
              className={`w-full text-left px-3 py-2 text-sm hover:bg-gray-100 flex items-center gap-2 ${
                currentAssigneeId === m.userId ? 'bg-blue-50 text-blue-700' : ''
              }`}
            >
              <div className="w-5 h-5 rounded-full bg-orange-500 text-white flex items-center justify-center text-xs font-bold">
                {getInitials(m.displayName ?? undefined)}
              </div>
              {m.displayName ?? 'Unnamed member'}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
