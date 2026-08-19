import { Rocket, Zap, CheckSquare, Bookmark, Compass, FlaskConical, Star, Inbox, Bug, ListTree, type LucideIcon } from 'lucide-react'
import type { WorkItemType } from '../../api/types'

export const workItemTypeIcons: Record<WorkItemType, { icon: LucideIcon; color: string }> = {
  Initiative: { icon: Rocket, color: '#8b5cf6' },
  Epic: { icon: Zap, color: '#8b5cf6' },
  Feature: { icon: Star, color: '#f59e0b' },
  Story: { icon: Bookmark, color: '#22a06b' },
  Task: { icon: CheckSquare, color: '#2f7fe0' },
  Subtask: { icon: ListTree, color: '#2f7fe0' },
  Bug: { icon: Bug, color: '#e5484d' },
  Spike: { icon: Compass, color: '#d97706' },
  Test: { icon: FlaskConical, color: '#0891b2' },
  Request: { icon: Inbox, color: '#64748b' },
}
