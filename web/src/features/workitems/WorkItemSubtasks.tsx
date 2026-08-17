import { useState } from 'react'
import { Plus } from 'lucide-react'
import { WorkItemTypeIcon } from './typeIcons'
import { CreateWorkItemDialog } from './CreateWorkItemDialog'
import { allStatuses, statusMeta } from '../board/constants'
import type { Priority, Profile, Project, TenantMembership, WorkItem, WorkItemStatus, WorkItemTypeDefinition } from '../../api/types'

export function WorkItemSubtasks({
  parent,
  workItems,
  project,
  profile,
  members,
  types,
  priorities,
  onOpenWorkItem,
  onStatusChange,
}: {
  parent: WorkItem
  workItems: WorkItem[]
  project: Project
  profile?: Profile
  members: TenantMembership[]
  types: WorkItemTypeDefinition[]
  priorities: Priority[]
  onOpenWorkItem: (workItem: WorkItem) => void
  onStatusChange: (workItem: WorkItem, status: WorkItemStatus) => void
}) {
  const [creating, setCreating] = useState(false)
  const subtasks = workItems.filter((candidate) => candidate.parentId === parent.id)
  const doneCount = subtasks.filter((candidate) => candidate.status === 'Done').length
  const percentDone = subtasks.length ? Math.round((doneCount / subtasks.length) * 100) : 0
  const membersById = new Map(members.map((member) => [member.userId, member]))

  return (
    <section className="mt-8 border-t border-gray-200 pt-6">
      <div className="subtasks-header">
        <h2>Subtasks</h2>
        <div className="subtasks-header-actions">
          {subtasks.length > 0 && (
            <span className="subtasks-progress">
              <span className="subtasks-progress-track"><span className="subtasks-progress-fill" style={{ width: `${percentDone}%` }} /></span>
              {percentDone}% Done
            </span>
          )}
          <button type="button" className="icon-button" aria-label="Add subtask" onClick={() => setCreating(true)}><Plus size={16} /></button>
        </div>
      </div>

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
                  <button type="button" className="subtasks-row-link" onClick={() => onOpenWorkItem(subtask)}>
                    <WorkItemTypeIcon type={subtask.type} size={15} />
                    <span className="subtasks-row-key">{subtask.key}</span>
                    <span className="subtasks-row-summary">{subtask.summary}</span>
                  </button>
                </td>
                <td>{subtask.priority}</td>
                <td>{subtask.storyPoints ?? '—'}</td>
                <td>{membersById.get(subtask.assigneeUserId ?? '')?.displayName ?? 'Unassigned'}</td>
                <td>
                  <select
                    className="subtasks-status-select"
                    value={subtask.status}
                    onChange={(event) => onStatusChange(subtask, event.target.value as WorkItemStatus)}
                  >
                    {allStatuses.map((status) => (
                      <option key={status} value={status}>{statusMeta[status].label}</option>
                    ))}
                  </select>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {creating && (
        <CreateWorkItemDialog
          project={project}
          workItems={workItems}
          profile={profile}
          members={members}
          types={types}
          priorities={priorities}
          parent={parent}
          onClose={() => setCreating(false)}
        />
      )}
    </section>
  )
}
