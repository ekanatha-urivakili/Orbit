import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Search, X } from 'lucide-react'
import { orbitApi } from '../../api/client'
import type { BoardColumnSizeMode, HideDoneItemsAfter } from '../../api/types'

// Matches the reference "Show fields" list. `key: null` means the toggle has no rendering effect
// in Orbit yet (Orbit has no equivalent data - e.g. no "Reporter" concept, no Confluence
// integration) and is shown disabled rather than as a live-but-inert control, so a checked toggle
// always means something real is happening on the card.
const CARD_FIELDS: { key: string | null; label: string; alwaysOn?: boolean }[] = [
  { key: 'assignee', label: 'Assignee' },
  { key: null, label: 'Card cover' },
  { key: null, label: 'Confluence items' },
  { key: 'created', label: 'Created' },
  { key: null, label: 'Development' },
  { key: 'dueDate', label: 'Due date' },
  { key: 'flagged', label: 'Flagged' },
  { key: 'labels', label: 'Labels' },
  { key: null, label: 'Linked work items' },
  { key: 'parent', label: 'Parent' },
  { key: 'priority', label: 'Priority' },
  { key: null, label: 'Reporter' },
  { key: null, label: 'Resolved' },
  { key: 'startDate', label: 'Start date' },
  { key: 'status', label: 'Status' },
  { key: 'storyPointEstimate', label: 'Story point estimate' },
  { key: 'subtaskSummary', label: 'Subtask summary' },
  { key: 'summary', label: 'Summary', alwaysOn: true },
  { key: null, label: 'Team' },
  { key: 'updated', label: 'Updated' },
  { key: 'workItemKey', label: 'Work item key' },
]

const HIDE_DONE_OPTIONS: { value: HideDoneItemsAfter; label: string }[] = [
  { value: 'Never', label: 'Never' },
  { value: 'OneDay', label: '1 day' },
  { value: 'OneWeek', label: '1 week' },
  { value: 'TwoWeeks', label: '2 weeks' },
  { value: 'OneMonth', label: '1 month' },
]

export function BoardViewSettingsPanel({ projectId, onClose }: { projectId: string; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [fieldSearch, setFieldSearch] = useState('')
  const query = useQuery({
    queryKey: ['board-view-preference', projectId],
    queryFn: () => orbitApi.getBoardViewPreference(projectId),
  })
  const preference = query.data
  const [pendingError, setPendingError] = useState<string | null>(null)

  const mutation = useMutation({
    mutationFn: (input: { hideDoneItemsAfter: HideDoneItemsAfter; columnSizeMode: BoardColumnSizeMode; hiddenFields: string[] }) =>
      orbitApi.updateBoardViewPreference(projectId, {
        projectId,
        version: preference?.version ?? 0,
        ...input,
      }),
    onSuccess: () => {
      setPendingError(null)
      queryClient.invalidateQueries({ queryKey: ['board-view-preference', projectId] })
    },
    onError: (error: Error) => setPendingError(error.message),
  })

  const filteredFields = useMemo(
    () => CARD_FIELDS.filter((field) => field.label.toLowerCase().includes(fieldSearch.trim().toLowerCase())),
    [fieldSearch],
  )

  if (!preference) {
    return (
      <aside className="view-settings-panel">
        <div className="view-settings-panel-header">
          <h2>View settings</h2>
          <button className="icon-button" type="button" aria-label="Close" onClick={onClose}><X size={16} /></button>
        </div>
        <p className="px-4 py-4 text-sm text-gray-500">{query.isError ? query.error.message : 'Loading…'}</p>
      </aside>
    )
  }

  const toggleField = (key: string) => {
    const hiddenFields = preference.hiddenFields.includes(key)
      ? preference.hiddenFields.filter((field) => field !== key)
      : [...preference.hiddenFields, key]
    mutation.mutate({ hideDoneItemsAfter: preference.hideDoneItemsAfter, columnSizeMode: preference.columnSizeMode, hiddenFields })
  }

  return (
    <aside className="view-settings-panel">
      <div className="view-settings-panel-header">
        <h2>View settings</h2>
        <button className="icon-button" type="button" aria-label="Close" onClick={onClose}><X size={16} /></button>
      </div>
      <div className="view-settings-panel-body">
        {pendingError && <p className="form-error">{pendingError}</p>}

        <label className="view-settings-field">
          <span className="view-settings-label">Hide done work items after:</span>
          <select
            className="view-settings-select"
            value={preference.hideDoneItemsAfter}
            onChange={(event) =>
              mutation.mutate({
                hideDoneItemsAfter: event.target.value as HideDoneItemsAfter,
                columnSizeMode: preference.columnSizeMode,
                hiddenFields: preference.hiddenFields,
              })
            }
          >
            {HIDE_DONE_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </label>

        <div className="view-settings-field">
          <span className="view-settings-label">Column size</span>
          <p className="view-settings-hint">Choose whether columns keep a fixed width or grow to fill available space.</p>
          <div className="view-settings-toggle-group">
            {(['Fixed', 'Flexible'] as BoardColumnSizeMode[]).map((mode) => (
              <button
                key={mode}
                type="button"
                className={`view-settings-toggle-button${preference.columnSizeMode === mode ? ' is-active' : ''}`}
                onClick={() =>
                  mutation.mutate({ hideDoneItemsAfter: preference.hideDoneItemsAfter, columnSizeMode: mode, hiddenFields: preference.hiddenFields })
                }
              >
                {mode}
              </button>
            ))}
          </div>
        </div>

        <div className="view-settings-field">
          <span className="view-settings-label">Show fields</span>
          <div className="view-settings-search">
            <Search size={13} />
            <input
              type="text"
              placeholder="Search fields"
              value={fieldSearch}
              onChange={(event) => setFieldSearch(event.target.value)}
            />
          </div>
          <ul className="view-settings-field-list">
            {filteredFields.map((field) => {
              const disabled = field.key === null
              const checked = field.alwaysOn || (field.key !== null && !preference.hiddenFields.includes(field.key))
              return (
                <li key={field.label} className={disabled ? 'is-unavailable' : undefined}>
                  <span>{field.label}</span>
                  <button
                    type="button"
                    role="switch"
                    aria-checked={checked}
                    disabled={disabled || field.alwaysOn}
                    title={disabled ? 'Not available yet' : undefined}
                    className={`view-settings-switch${checked ? ' is-on' : ''}`}
                    onClick={() => field.key && toggleField(field.key)}
                  >
                    <span className="view-settings-switch-knob" />
                  </button>
                </li>
              )
            })}
          </ul>
        </div>
      </div>
    </aside>
  )
}
