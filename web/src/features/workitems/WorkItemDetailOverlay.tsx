import { useEffect, useRef } from 'react'
import { X } from 'lucide-react'
import { WorkItemDetailView } from './WorkItemDetailView'
import type { Priority, Profile, Project, Sprint, TenantMembership, WorkItem } from '../../api/types'

export function WorkItemDetailOverlay({
  variant,
  item,
  project,
  workItems,
  profile,
  members,
  priorities,
  sprints,
  onClose,
  onStatusChange,
  onOpenWorkItem,
  onManageWorkTypes,
}: {
  variant: 'modal' | 'drawer'
  item: WorkItem
  project?: Project
  workItems: WorkItem[]
  profile?: Profile
  members: TenantMembership[]
  priorities: Priority[]
  sprints?: Sprint[]
  onClose: () => void
  onStatusChange: (workItem: WorkItem, statusId: string) => void
  onOpenWorkItem: (workItem: WorkItem) => void
  onManageWorkTypes?: () => void
}) {
  const dialogRef = useRef<HTMLDivElement>(null)
  const closeButtonRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    const previousActiveElement = document.activeElement instanceof HTMLElement ? document.activeElement : null
    closeButtonRef.current?.focus()

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        onClose()
        return
      }

      if (event.key !== 'Tab') return
      const focusable = dialogRef.current?.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
      )
      if (!focusable?.length) return

      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      previousActiveElement?.focus()
    }
  }, [onClose])

  const backdropClass = variant === 'modal' ? 'work-item-modal-backdrop' : 'work-item-drawer-backdrop'
  const containerClass = variant === 'modal' ? 'work-item-modal' : 'work-item-drawer'
  const closeClass = variant === 'modal' ? 'work-item-modal-close' : 'work-item-drawer-close'

  return (
    <div
      className={backdropClass}
      role="presentation"
      onMouseDown={(event) => event.target === event.currentTarget && onClose()}
    >
      <div className={containerClass} ref={dialogRef} role="dialog" aria-modal="true" aria-labelledby="work-item-overlay-title">
        <h2 id="work-item-overlay-title" className="sr-only">{item.key}: {item.summary}</h2>
        <button ref={closeButtonRef} type="button" className={`${closeClass} icon-button`} aria-label="Close" onClick={onClose}>
          <X size={18} />
        </button>
        <WorkItemDetailView
          item={item}
          project={project}
          workItems={workItems}
          profile={profile}
          members={members}
          priorities={priorities}
          sprints={sprints}
          onBack={onClose}
          onStatusChange={onStatusChange}
          onOpenWorkItem={onOpenWorkItem}
          onManageWorkTypes={onManageWorkTypes}
        />
      </div>
    </div>
  )
}
