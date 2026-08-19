import { workItemTypeIcons } from './typeConstants'
import type { WorkItemType } from '../../api/types'

export function WorkItemTypeIcon({ type, size = 14 }: { type: WorkItemType; size?: number }) {
  const meta = workItemTypeIcons[type]
  if (!meta) return null
  const Icon = meta.icon
  return <Icon size={size} color={meta.color} strokeWidth={2.25} aria-hidden="true" />
}
