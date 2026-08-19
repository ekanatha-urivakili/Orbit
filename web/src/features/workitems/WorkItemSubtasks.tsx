import { useState } from 'react'
import { ChevronDown, Plus, Search } from 'lucide-react'
import { WorkItemTypeIcon } from './typeIcons'
import { allStatuses, statusMeta } from '../board/constants'
import { useCreateWorkItem } from '../../hooks/useCreateWorkItem'
import { orbitApi } from '../../api/client'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { Project, TenantMembership, WorkItem, WorkItemStatus, WorkItemType } from '../../api/types'

export function WorkItemSubtasks({
  parent,
  workItems,
  project,
  members,
  onStatusChange,
  onOpenWorkItem,
}: {
  parent: WorkItem
  workItems: WorkItem[]
  project: Project
  members: TenantMembership[]
  onStatusChange: (workItem: WorkItem, status: WorkItemStatus) => void
  onOpenWorkItem: (workItem: WorkItem) => void
}) {
  const queryClient = useQueryClient()
  const [collapsed, setCollapsed] = useState(false)
  const [inlineCreateOpen, setInlineCreateOpen] = useState(false)
  const [chooseExistingOpen, setChooseExistingOpen] = useState(false)
  const [subtaskSummary, setSubtaskSummary] = useState('')
  const [subtaskType, setSubtaskType] = useState<WorkItemType>('Subtask')
  const [typeDropdownOpen, setTypeDropdownOpen] = useState(false)
  const [selectedExistingId, setSelectedExistingId] = useState('')

  const subtasks = workItems.filter((candidate) => candidate.parentId === parent.id)
  const doneCount = subtasks.filter((candidate) => candidate.status === 'Done').length
  const percentDone = subtasks.length ? Math.round((doneCount / subtasks.length) * 100) : 0
  const membersById = new Map(members.map((member) => [member.userId, member]))

  const createMutation = useCreateWorkItem(project.id)

  const handleCreateSubtask = () => {
    if (!subtaskSummary.trim() || createMutation.isPending) return

    createMutation.mutate(
      {
        projectId: project.id,
        summary: subtaskSummary.trim(),
        description: null,
        type: subtaskType,
        priority: 'Medium',
        parentId: parent.id,
      },
      {
        onSuccess: () => {
          setSubtaskSummary('')
          setInlineCreateOpen(false)
          queryClient.invalidateQueries({ queryKey: ['work-items', project.id] })
        },
      }
    )
  }

  const linkExistingMutation = useMutation({
    mutationFn: (existingId: string) => {
      const existing = workItems.find((w) => w.id === existingId)
      if (!existing) return Promise.reject(new Error('Work item not found.'))
      return orbitApi.updateWorkItem(existing, {
        summary: existing.summary,
        description: existing.description,
        priority: existing.priority,
        parentId: parent.id,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['work-items', project.id] })
      setChooseExistingOpen(false)
      setSelectedExistingId('')
    },
  })

  // Work items that can be linked as subtasks (exclude self, already subtasks of self, and parents)
  const linkableCandidates = workItems.filter(
    (w) => w.id !== parent.id && w.parentId !== parent.id && w.type !== 'Initiative'
  )

  return (
    <section className="mt-8 border-t border-gray-200 pt-6">
      {/* Subtasks Collapsible Header */}
      <div className="flex items-center justify-between mb-3">
        <button
          type="button"
          onClick={() => setCollapsed(!collapsed)}
          className="flex items-center gap-2 text-sm font-bold text-[#172b4d] hover:text-[#0052cc] transition-colors"
        >
          <ChevronDown
            size={18}
            className={`text-gray-500 transition-transform ${collapsed ? '-rotate-90' : ''}`}
          />
          <span>Subtasks</span>
          {subtasks.length > 0 && (
            <span className="text-xs text-gray-500 font-normal">({subtasks.length})</span>
          )}
        </button>

        <div className="flex items-center gap-3">
          {subtasks.length > 0 && (
            <span className="subtasks-progress">
              <span className="subtasks-progress-track">
                <span className="subtasks-progress-fill" style={{ width: `${percentDone}%` }} />
              </span>
              {percentDone}% Done
            </span>
          )}
          <button
            type="button"
            className="p-1 hover:bg-gray-100 rounded text-gray-500 hover:text-gray-900 transition-colors"
            aria-label="Add subtask"
            onClick={() => {
              setInlineCreateOpen(true)
              setCollapsed(false)
            }}
          >
            <Plus size={18} />
          </button>
        </div>
      </div>

      {!collapsed && (
        <div className="space-y-3">
          {/* Subtasks Table */}
          {subtasks.length > 0 && (
            <table className="subtasks-table">
              <thead>
                <tr>
                  <th>Work</th>
                  <th>Priority</th>
                  <th>Story pts</th>
                  <th>Assignee</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {subtasks.map((subtask) => (
                  <tr key={subtask.id}>
                    <td>
                      <button
                        type="button"
                        onClick={() => onOpenWorkItem(subtask)}
                        className="subtasks-row-link"
                      >
                        <WorkItemTypeIcon type={subtask.type} size={15} />
                        <span className="subtasks-row-key">{subtask.key}</span>
                        <span className="subtasks-row-summary">{subtask.summary}</span>
                      </button>
                    </td>
                    <td>{subtask.priority}</td>
                    <td>{subtask.storyPoints ?? '—'}</td>
                    <td>
                      {membersById.get(subtask.assigneeUserId ?? '')?.displayName ?? 'Unassigned'}
                    </td>
                    <td>
                      <select
                        className="subtasks-status-select"
                        value={subtask.status}
                        onChange={(event) =>
                          onStatusChange(subtask, event.target.value as WorkItemStatus)
                        }
                      >
                        {allStatuses.map((status) => (
                          <option key={status} value={status}>
                            {statusMeta[status].label}
                          </option>
                        ))}
                      </select>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {/* Inline Create Subtask Box (Matching Screenshot 1) */}
          {inlineCreateOpen ? (
            <div className="mt-2 rounded-lg border border-[#dfe1e6] bg-white p-2.5 shadow-sm space-y-2">
              <div className="flex items-center gap-2">
                <input
                  autoFocus
                  type="text"
                  value={subtaskSummary}
                  onChange={(e) => setSubtaskSummary(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') {
                      e.preventDefault()
                      handleCreateSubtask()
                    }
                  }}
                  placeholder="What needs to be done?"
                  className="flex-1 text-sm text-[#172b4d] dark:text-gray-100 placeholder-gray-400 dark:placeholder-gray-500 bg-transparent focus:outline-none px-2 py-1"
                />

                {/* Subtask Type Selector Chip */}
                <div className="relative">
                  <button
                    type="button"
                    onClick={() => setTypeDropdownOpen(!typeDropdownOpen)}
                    className="flex items-center gap-1.5 px-2.5 py-1 rounded bg-[#f4f5f7] dark:bg-[#22272b] hover:bg-[#ebecf0] dark:hover:bg-[#2a2f35] text-xs font-semibold text-[#42526e] dark:text-gray-300 border border-[#dfe1e6] dark:border-[#394047] transition-colors"
                  >
                    <WorkItemTypeIcon type={subtaskType} size={14} />
                    <span>{subtaskType}</span>
                    <ChevronDown size={12} className="text-gray-500" />
                  </button>

                  {typeDropdownOpen && (
                    <div className="absolute right-0 top-full mt-1 w-36 bg-white dark:bg-[#1e2327] border border-[#dfe1e6] dark:border-[#394047] shadow-xl rounded-lg py-1 z-50 animate-in fade-in">
                      {(['Subtask', 'Task', 'Bug'] as WorkItemType[]).map((t) => (
                        <button
                          key={t}
                          type="button"
                          onClick={() => {
                            setSubtaskType(t)
                            setTypeDropdownOpen(false)
                          }}
                          className="w-full text-left px-3 py-1.5 text-xs hover:bg-[#f4f5f7] dark:hover:bg-[#2a2f35] flex items-center gap-2 text-[#172b4d] dark:text-gray-200"
                        >
                          <WorkItemTypeIcon type={t} size={14} />
                          <span>{t}</span>
                        </button>
                      ))}
                    </div>
                  )}
                </div>

                {/* Submit button */}
                <button
                  type="button"
                  onClick={handleCreateSubtask}
                  disabled={!subtaskSummary.trim() || createMutation.isPending}
                  className="px-3 py-1 bg-[#0052cc] hover:bg-[#0065ff] text-white text-xs font-semibold rounded disabled:opacity-40 disabled:cursor-not-allowed transition-colors flex items-center gap-1 shrink-0"
                  title="Create subtask (Enter)"
                >
                  <Plus size={13} />
                  <span>{createMutation.isPending ? 'Creating…' : 'Create'}</span>
                </button>
              </div>

              {createMutation.isError && (
                <p className="text-xs text-red-600 dark:text-red-400">{createMutation.error.message}</p>
              )}

              {/* Bottom Action Row */}
              <div className="flex items-center justify-between pt-1 border-t border-gray-100 text-xs">
                <button
                  type="button"
                  onClick={() => setChooseExistingOpen(!chooseExistingOpen)}
                  className="flex items-center gap-1.5 text-[#0052cc] hover:underline font-medium"
                >
                  <Search size={13} />
                  Choose existing
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setInlineCreateOpen(false)
                    setChooseExistingOpen(false)
                    setSubtaskSummary('')
                  }}
                  className="text-gray-500 hover:text-gray-800 font-medium"
                >
                  Cancel
                </button>
              </div>

              {/* Choose Existing Dropdown Panel */}
              {chooseExistingOpen && (
                <div className="p-2 bg-[#f4f5f7] rounded-md border border-[#dfe1e6] mt-2 flex items-center gap-2">
                  <select
                    value={selectedExistingId}
                    onChange={(e) => setSelectedExistingId(e.target.value)}
                    className="flex-1 text-xs border border-gray-300 rounded px-2 py-1.5 bg-white text-gray-800 focus:outline-none"
                  >
                    <option value="">Select an existing work item...</option>
                    {linkableCandidates.map((cand) => (
                      <option key={cand.id} value={cand.id}>
                        {cand.key} — {cand.summary} ({cand.type})
                      </option>
                    ))}
                  </select>
                  <button
                    type="button"
                    disabled={!selectedExistingId || linkExistingMutation.isPending}
                    onClick={() => linkExistingMutation.mutate(selectedExistingId)}
                    className="px-3 py-1 bg-[#0052cc] hover:bg-[#0065ff] text-white text-xs font-semibold rounded disabled:opacity-50"
                  >
                    {linkExistingMutation.isPending ? 'Linking…' : 'Link'}
                  </button>
                </div>
              )}

              {linkExistingMutation.isError && (
                <p className="text-xs text-red-600">{linkExistingMutation.error.message}</p>
              )}
            </div>
          ) : (
            subtasks.length === 0 && (
              <button
                type="button"
                onClick={() => setInlineCreateOpen(true)}
                className="w-full py-2 px-3 border border-dashed border-gray-300 hover:border-blue-400 rounded-lg text-xs font-semibold text-gray-500 hover:text-blue-600 flex items-center justify-center gap-1.5 transition-colors"
              >
                <Plus size={14} /> Add subtask
              </button>
            )
          )}
        </div>
      )}
    </section>
  )
}
