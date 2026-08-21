import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowLeft, ArrowRight, ArrowUp, Plus, Star, Trash2, X } from 'lucide-react'
import { orbitApi } from '../../api/client'
import type { StatusCategory, WorkItemStatusDefinition } from '../../api/types'

const CATEGORY_OPTIONS: { value: StatusCategory; label: string }[] = [
  { value: 'ToDo', label: 'To do' },
  { value: 'InProgress', label: 'In progress' },
  { value: 'Done', label: 'Done' },
]

const COLOR_OPTIONS = ['slate', 'cyan', 'blue', 'amber', 'green', 'red', 'purple', 'orange']

type ViewMode = 'Diagram' | 'Text'

export function WorkflowEditorDialog({ projectId, boardName, onClose }: { projectId: string; boardName: string; onClose: () => void }) {
  const queryClient = useQueryClient()
  const statusesQuery = useQuery({
    queryKey: ['work-item-statuses', projectId],
    queryFn: () => orbitApi.listWorkItemStatuses(projectId),
  })
  const statuses = [...(statusesQuery.data ?? [])].sort((a, b) => a.order - b.order)
  const [error, setError] = useState<string | null>(null)
  const [addingStatus, setAddingStatus] = useState(false)
  const [view, setView] = useState<ViewMode>('Diagram')
  const [selectedStatusId, setSelectedStatusId] = useState<string | null>(null)

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['work-item-statuses', projectId] })

  const updateMutation = useMutation({
    mutationFn: (input: { status: WorkItemStatusDefinition; patch: { name: string; category: StatusCategory; order: number; colorToken: string } }) =>
      orbitApi.updateWorkItemStatus(projectId, input.status, input.patch),
    onSuccess: invalidate,
    onError: (err: Error) => setError(err.message),
  })

  const setDefaultMutation = useMutation({
    mutationFn: (statusId: string) => orbitApi.setDefaultWorkItemStatus(projectId, statusId),
    onSuccess: invalidate,
    onError: (err: Error) => setError(err.message),
  })

  const deleteMutation = useMutation({
    mutationFn: (statusId: string) => orbitApi.deleteWorkItemStatus(projectId, statusId),
    onSuccess: () => {
      invalidate()
      setSelectedStatusId(null)
    },
    onError: (err: Error) => setError(err.message),
  })

  const createMutation = useMutation({
    mutationFn: (input: { key: string; name: string; category: StatusCategory; colorToken: string }) =>
      orbitApi.createWorkItemStatus(projectId, { ...input, order: (statuses.at(-1)?.order ?? 0) + 10 }),
    onSuccess: () => {
      invalidate()
      setAddingStatus(false)
    },
    onError: (err: Error) => setError(err.message),
  })

  function move(status: WorkItemStatusDefinition, direction: -1 | 1) {
    const index = statuses.findIndex((candidate) => candidate.id === status.id)
    const target = statuses[index + direction]
    if (!target) return
    setError(null)
    updateMutation.mutate({ status, patch: { name: status.name, category: status.category, order: target.order, colorToken: status.colorToken } })
    updateMutation.mutate({ status: target, patch: { name: target.name, category: target.category, order: status.order, colorToken: target.colorToken } })
  }

  const selectedStatus = statuses.find((status) => status.id === selectedStatusId) ?? null

  return (
    <div className="dialog-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <section className="dialog workflow-editor-dialog" role="dialog" aria-modal="true" aria-labelledby="workflow-editor-title">
        <header>
          <div>
            <h2 id="workflow-editor-title">Workflow for {boardName}</h2>
            <p className="mt-1 text-xs text-gray-500">Add, rename, recolor, and reorder this project's statuses.</p>
          </div>
          <button className="icon-button" type="button" aria-label="Close" onClick={onClose}><X size={20} /></button>
        </header>

        <div className="workflow-toolbar">
          <button type="button" className="workflow-toolbar-button" onClick={() => setAddingStatus(true)}>
            <Plus size={14} /> Add status
          </button>
          <div className="workflow-view-toggle">
            <button
              type="button"
              className={view === 'Diagram' ? 'is-active' : ''}
              onClick={() => setView('Diagram')}
            >
              Diagram
            </button>
            <button
              type="button"
              className={view === 'Text' ? 'is-active' : ''}
              onClick={() => setView('Text')}
            >
              Text
            </button>
          </div>
        </div>

        <div className="px-6 pb-6">
          {error && <p className="form-error mb-3">{error}</p>}
          {statusesQuery.isPending && <p className="text-sm text-gray-500">Loading…</p>}

          {view === 'Diagram' ? (
            <>
              <div className="workflow-diagram">
                <div className="workflow-node workflow-node--start">START</div>
                <div className="workflow-arrow" />
                <div className="workflow-node workflow-node--create">Create</div>
                {statuses.map((status, index) => (
                  <div key={status.id} className="workflow-diagram-step">
                    <div className="workflow-arrow">
                      <span className="workflow-arrow-label">Any</span>
                    </div>
                    <button
                      type="button"
                      className={`workflow-node status-dot-${status.colorToken}${selectedStatusId === status.id ? ' is-selected' : ''}`}
                      onClick={() => setSelectedStatusId(status.id === selectedStatusId ? null : status.id)}
                    >
                      {status.name}
                      {status.isDefault && <Star size={11} className="workflow-node-default-star" />}
                    </button>
                    {index === statuses.length - 1 && (
                      <>
                        <div className="workflow-arrow"><span className="workflow-arrow-label">Any</span></div>
                        <div className="workflow-node workflow-node--start">{status.name.toUpperCase()}</div>
                      </>
                    )}
                  </div>
                ))}
              </div>

              {selectedStatus ? (
                <StatusEditPanel
                  status={selectedStatus}
                  statuses={statuses}
                  onMove={move}
                  onUpdate={(patch) => {
                    setError(null)
                    updateMutation.mutate({ status: selectedStatus, patch })
                  }}
                  onSetDefault={() => {
                    setError(null)
                    setDefaultMutation.mutate(selectedStatus.id)
                  }}
                  onDelete={() => {
                    setError(null)
                    deleteMutation.mutate(selectedStatus.id)
                  }}
                  deletePending={deleteMutation.isPending}
                  canDelete={statuses.length > 1}
                />
              ) : (
                <p className="workflow-diagram-hint">Click a status above to rename, recolor, recategorize, reorder, or set it as the default for new work items.</p>
              )}
            </>
          ) : (
            <ul className="flex flex-col gap-2">
              {statuses.map((status, index) => (
                <li
                  key={`${status.id}-${status.version}`}
                  className="flex items-center gap-2 border border-gray-200 dark:border-[#394047] rounded-lg px-2.5 py-1.5 bg-white dark:bg-[#1e2327]"
                >
                  <div className="flex flex-col">
                    <button type="button" disabled={index === 0} onClick={() => move(status, -1)} aria-label={`Move ${status.name} up`}>
                      <ArrowUp size={12} />
                    </button>
                    <button type="button" disabled={index === statuses.length - 1} onClick={() => move(status, 1)} aria-label={`Move ${status.name} down`}>
                      <ArrowDown size={12} />
                    </button>
                  </div>
                  <span className={`status-dot ${status.colorToken}`} />
                  <input
                    type="text"
                    className="flex-1 border border-gray-200 dark:border-gray-600 rounded px-2 py-1 text-sm bg-white dark:bg-[#22272b]"
                    defaultValue={status.name}
                    onBlur={(event) => {
                      const name = event.target.value.trim()
                      if (name && name !== status.name) {
                        setError(null)
                        updateMutation.mutate({ status, patch: { name, category: status.category, order: status.order, colorToken: status.colorToken } })
                      }
                    }}
                  />
                  <select
                    className="border border-gray-200 dark:border-gray-600 rounded px-2 py-1 text-xs bg-white dark:bg-[#22272b]"
                    value={status.category}
                    onChange={(event) => {
                      setError(null)
                      updateMutation.mutate({
                        status,
                        patch: { name: status.name, category: event.target.value as StatusCategory, order: status.order, colorToken: status.colorToken },
                      })
                    }}
                  >
                    {CATEGORY_OPTIONS.map((option) => (
                      <option key={option.value} value={option.value}>{option.label}</option>
                    ))}
                  </select>
                  <select
                    className="border border-gray-200 dark:border-gray-600 rounded px-2 py-1 text-xs bg-white dark:bg-[#22272b]"
                    value={status.colorToken}
                    onChange={(event) => {
                      setError(null)
                      updateMutation.mutate({
                        status,
                        patch: { name: status.name, category: status.category, order: status.order, colorToken: event.target.value },
                      })
                    }}
                  >
                    {COLOR_OPTIONS.map((color) => (
                      <option key={color} value={color}>{color}</option>
                    ))}
                  </select>
                  <button
                    type="button"
                    className={status.isDefault ? 'workflow-default-star is-default' : 'workflow-default-star'}
                    disabled={status.isDefault}
                    onClick={() => {
                      setError(null)
                      setDefaultMutation.mutate(status.id)
                    }}
                    aria-label={status.isDefault ? `${status.name} is the default status` : `Set ${status.name} as default`}
                    title={status.isDefault ? 'Default status for new work items' : 'Set as default for new work items'}
                  >
                    <Star size={14} />
                  </button>
                  <button
                    type="button"
                    disabled={statuses.length === 1 || status.isDefault || deleteMutation.isPending}
                    onClick={() => {
                      setError(null)
                      deleteMutation.mutate(status.id)
                    }}
                    aria-label={`Delete ${status.name}`}
                    title={
                      statuses.length === 1
                        ? "A project's workflow needs at least one status"
                        : status.isDefault
                          ? 'Set another status as default before deleting this one'
                          : 'Delete status'
                    }
                  >
                    <Trash2 size={14} />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <footer className="sticky bottom-0 -mx-6 -mb-6 border-t border-gray-200 bg-white px-6 py-4">
          <button type="button" className="secondary-button" onClick={onClose}>Close</button>
        </footer>
      </section>

      {addingStatus && (
        <AddStatusDialog
          pending={createMutation.isPending}
          onCancel={() => setAddingStatus(false)}
          onSubmit={(input) => createMutation.mutate(input)}
        />
      )}
    </div>
  )
}

function StatusEditPanel({
  status,
  statuses,
  onMove,
  onUpdate,
  onSetDefault,
  onDelete,
  deletePending,
  canDelete,
}: {
  status: WorkItemStatusDefinition
  statuses: WorkItemStatusDefinition[]
  onMove: (status: WorkItemStatusDefinition, direction: -1 | 1) => void
  onUpdate: (patch: { name: string; category: StatusCategory; order: number; colorToken: string }) => void
  onSetDefault: () => void
  onDelete: () => void
  deletePending: boolean
  canDelete: boolean
}) {
  const index = statuses.findIndex((candidate) => candidate.id === status.id)

  return (
    <div className="workflow-edit-panel">
      <div className="flex items-center gap-2">
        <button type="button" disabled={index === 0} onClick={() => onMove(status, -1)} aria-label={`Move ${status.name} earlier`}>
          <ArrowLeft size={14} />
        </button>
        <input
          type="text"
          key={`${status.id}-${status.version}`}
          className="flex-1 border border-gray-200 dark:border-gray-600 rounded px-2 py-1.5 text-sm bg-white dark:bg-[#22272b] font-semibold"
          defaultValue={status.name}
          onBlur={(event) => {
            const name = event.target.value.trim()
            if (name && name !== status.name) {
              onUpdate({ name, category: status.category, order: status.order, colorToken: status.colorToken })
            }
          }}
        />
        <button type="button" disabled={index === statuses.length - 1} onClick={() => onMove(status, 1)} aria-label={`Move ${status.name} later`}>
          <ArrowRight size={14} />
        </button>
      </div>
      <div className="workflow-edit-panel-row">
        <label>
          Category
          <select
            value={status.category}
            onChange={(event) => onUpdate({ name: status.name, category: event.target.value as StatusCategory, order: status.order, colorToken: status.colorToken })}
          >
            {CATEGORY_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </label>
        <label>
          Color
          <select
            value={status.colorToken}
            onChange={(event) => onUpdate({ name: status.name, category: status.category, order: status.order, colorToken: event.target.value })}
          >
            {COLOR_OPTIONS.map((color) => (
              <option key={color} value={color}>{color}</option>
            ))}
          </select>
        </label>
      </div>
      <div className="flex items-center justify-between pt-2">
        <button type="button" className="secondary-button" disabled={status.isDefault} onClick={onSetDefault}>
          <Star size={13} /> {status.isDefault ? 'Default status' : 'Set as default'}
        </button>
        <button
          type="button"
          className="text-red-600 text-xs font-semibold flex items-center gap-1 disabled:opacity-40"
          disabled={!canDelete || status.isDefault || deletePending}
          onClick={onDelete}
          title={status.isDefault ? 'Set another status as default before deleting this one' : undefined}
        >
          <Trash2 size={13} /> Delete status
        </button>
      </div>
    </div>
  )
}

function AddStatusDialog({
  pending,
  onCancel,
  onSubmit,
}: {
  pending: boolean
  onCancel: () => void
  onSubmit: (input: { key: string; name: string; category: StatusCategory; colorToken: string }) => void
}) {
  const [name, setName] = useState('')
  const [category, setCategory] = useState<StatusCategory>('ToDo')

  return (
    <div className="dialog-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && onCancel()}>
      <section className="dialog add-status-dialog" role="dialog" aria-modal="true" aria-labelledby="add-status-title">
        <header>
          <h2 id="add-status-title">Add status</h2>
          <button className="icon-button" type="button" aria-label="Close" onClick={onCancel}><X size={20} /></button>
        </header>
        <form
          className="sprint-edit-form"
          onSubmit={(event) => {
            event.preventDefault()
            const trimmed = name.trim()
            if (!trimmed) return
            const key = trimmed.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '')
            onSubmit({ key, name: trimmed, category, colorToken: COLOR_OPTIONS[Math.floor(Math.random() * COLOR_OPTIONS.length)] })
          }}
        >
          <p className="sprint-edit-required-hint">A status shows the progression of work.</p>
          <label className="sprint-edit-field">
            <span>Name *</span>
            <input
              type="text"
              required
              autoFocus
              placeholder="e.g. Ready for QA"
              value={name}
              onChange={(event) => setName(event.target.value)}
            />
          </label>
          <label className="sprint-edit-field">
            <span>Category *</span>
            <select value={category} onChange={(event) => setCategory(event.target.value as StatusCategory)}>
              {CATEGORY_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </label>
          <div className="flex justify-end gap-2 pt-1">
            <button type="button" className="secondary-button" onClick={onCancel}>Cancel</button>
            <button type="submit" disabled={pending} className="primary-button">
              {pending ? 'Adding…' : 'Submit'}
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}
