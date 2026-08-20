import { useMemo, useState } from 'react'
import type { TenantMembership, WorkItem, WorkItemStatus, WorkItemType } from '../api/types'

export const UNASSIGNED = '__unassigned__'
export const NO_PARENT = '__no_parent__'
export const NO_LABEL = '__no_label__'

export interface WorkItemFilterOption {
  value: string
  label: string
}

export interface WorkItemFilterFieldState {
  key: 'status' | 'assignee' | 'type' | 'label' | 'parent'
  label: string
  options: WorkItemFilterOption[]
  selected: string[]
  toggle: (value: string) => void
  clear: () => void
}

export interface UseWorkItemFiltersResult {
  searchTerm: string
  setSearchTerm: (term: string) => void
  fields: WorkItemFilterFieldState[]
  activeCount: number
  clearAll: () => void
  matches: (item: WorkItem) => boolean
  filteredItems: WorkItem[]
}

function matchesSearch(item: WorkItem, term: string): boolean {
  if (!term.trim()) return true
  const haystack = `${item.key} ${item.summary}`.toLowerCase()
  return haystack.includes(term.trim().toLowerCase())
}

function useMultiSelect() {
  const [selected, setSelected] = useState<string[]>([])
  const toggle = (value: string) =>
    setSelected((current) => (current.includes(value) ? current.filter((v) => v !== value) : [...current, value]))
  const clear = () => setSelected([])
  return { selected, toggle, clear }
}

export function useWorkItemFilters(
  workItems: WorkItem[],
  members: TenantMembership[],
  statusLabels: Readonly<Record<WorkItemStatus, string>>,
  typeLabels: Partial<Record<WorkItemType, string>>,
): UseWorkItemFiltersResult {
  const [searchTerm, setSearchTerm] = useState('')
  const status = useMultiSelect()
  const assignee = useMultiSelect()
  const type = useMultiSelect()
  const label = useMultiSelect()
  const parent = useMultiSelect()

  const workItemsById = useMemo(() => new Map(workItems.map((item) => [item.id, item])), [workItems])

  const statusOptions = useMemo(
    () =>
      (Object.keys(statusLabels) as WorkItemStatus[])
        .filter((value) => workItems.some((item) => item.status === value))
        .map((value) => ({ value, label: statusLabels[value] })),
    [workItems, statusLabels],
  )

  const assigneeOptions = useMemo(() => {
    const activeMembers = members.filter((member): member is TenantMembership & { userId: string } => Boolean(member.userId))
    const options: WorkItemFilterOption[] = [{ value: UNASSIGNED, label: 'Unassigned' }]
    for (const member of activeMembers) {
      if (workItems.some((item) => item.assigneeUserId === member.userId)) {
        options.push({ value: member.userId, label: member.displayName ?? 'Unnamed member' })
      }
    }
    return options
  }, [members, workItems])

  const typeOptions = useMemo(() => {
    const present = Array.from(new Set(workItems.map((item) => item.type)))
    return present.map((value) => ({ value, label: typeLabels[value] ?? value }))
  }, [workItems, typeLabels])

  const labelOptions = useMemo(() => {
    const present = Array.from(new Set(workItems.flatMap((item) => item.labels))).sort()
    const options: WorkItemFilterOption[] = [{ value: NO_LABEL, label: 'No label' }]
    return options.concat(present.map((value) => ({ value, label: value })))
  }, [workItems])

  const parentOptions = useMemo(() => {
    const parentIds = new Set(workItems.map((item) => item.parentId).filter((id): id is string => Boolean(id)))
    const options: WorkItemFilterOption[] = [{ value: NO_PARENT, label: 'No parent' }]
    for (const id of parentIds) {
      const parentItem = workItemsById.get(id)
      if (parentItem) options.push({ value: id, label: `${parentItem.key} ${parentItem.summary}` })
    }
    return options
  }, [workItems, workItemsById])

  const matches = (item: WorkItem): boolean => {
    if (!matchesSearch(item, searchTerm)) return false
    if (status.selected.length > 0 && !status.selected.includes(item.status)) return false
    if (assignee.selected.length > 0) {
      const key = item.assigneeUserId ?? UNASSIGNED
      if (!assignee.selected.includes(key)) return false
    }
    if (type.selected.length > 0 && !type.selected.includes(item.type)) return false
    if (label.selected.length > 0) {
      const hasNoLabel = item.labels.length === 0
      const matchesLabel = label.selected.includes(NO_LABEL) && hasNoLabel
        ? true
        : item.labels.some((value) => label.selected.includes(value))
      if (!matchesLabel) return false
    }
    if (parent.selected.length > 0) {
      const key = item.parentId ?? NO_PARENT
      if (!parent.selected.includes(key)) return false
    }
    return true
  }

  const fields: WorkItemFilterFieldState[] = [
    { key: 'parent', label: 'Parent', options: parentOptions, selected: parent.selected, toggle: parent.toggle, clear: parent.clear },
    { key: 'assignee', label: 'Assignee', options: assigneeOptions, selected: assignee.selected, toggle: assignee.toggle, clear: assignee.clear },
    { key: 'status', label: 'Status', options: statusOptions, selected: status.selected, toggle: status.toggle, clear: status.clear },
    { key: 'type', label: 'Work type', options: typeOptions, selected: type.selected, toggle: type.toggle, clear: type.clear },
    { key: 'label', label: 'Labels', options: labelOptions, selected: label.selected, toggle: label.toggle, clear: label.clear },
  ]

  const activeCount = fields.reduce((sum, field) => sum + field.selected.length, 0) + (searchTerm.trim() ? 1 : 0)

  const clearAll = () => {
    setSearchTerm('')
    for (const field of fields) field.clear()
  }

  const filteredItems = useMemo(
    () => workItems.filter(matches),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [workItems, searchTerm, status.selected, assignee.selected, type.selected, label.selected, parent.selected],
  )

  return { searchTerm, setSearchTerm, fields, activeCount, clearAll, matches, filteredItems }
}
