import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Search, X } from 'lucide-react'
import { orbitApi } from '../../api/client'
import type { BoardColumnSizeMode, HideDoneItemsAfter } from '../../api/types'

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
      <aside className="w-[360px] shrink-0 border-l border-gray-200 dark:border-[#394047] bg-white dark:bg-[#1d2125] h-full overflow-y-auto z-20">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100 dark:border-[#394047]">
          <h2 className="text-base font-bold text-[#172b4d] dark:text-gray-100">View settings</h2>
          <button className="icon-button" type="button" aria-label="Close" onClick={onClose}>
            <X size={18} />
          </button>
        </div>
        <p className="px-5 py-6 text-sm text-gray-500">{query.isError ? query.error.message : 'Loading…'}</p>
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
    <aside className="w-[360px] shrink-0 border-l border-gray-200 dark:border-[#394047] bg-white dark:bg-[#1d2125] h-full overflow-y-auto z-20">
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100 dark:border-[#394047]">
        <h2 className="text-base font-bold text-[#172b4d] dark:text-gray-100">View settings</h2>
        <button className="icon-button text-gray-500 hover:text-gray-700" type="button" aria-label="Close" onClick={onClose}>
          <X size={18} />
        </button>
      </div>

      <div className="p-5 space-y-6">
        {pendingError && <p className="form-error text-xs">{pendingError}</p>}

        {/* Hide done work items */}
        <div className="space-y-1.5">
          <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300">
            Hide done work items after:
          </label>
          <select
            className="w-full border border-gray-300 dark:border-[#394047] rounded-md px-3 py-2 text-xs bg-white dark:bg-[#22272b] text-gray-800 dark:text-gray-200 focus:outline-none focus:ring-2 focus:ring-blue-500"
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
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>

        {/* Column size */}
        <div className="space-y-2">
          <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300">Column size</label>
          <p className="text-[11px] text-gray-500 dark:text-gray-400">
            Choose whether columns keep a fixed width or grow to fill available space.
          </p>
          <div className="grid grid-cols-2 gap-3 pt-1">
            {/* Fixed Option */}
            <button
              type="button"
              onClick={() =>
                mutation.mutate({
                  hideDoneItemsAfter: preference.hideDoneItemsAfter,
                  columnSizeMode: 'Fixed',
                  hiddenFields: preference.hiddenFields,
                })
              }
              className={`flex flex-col items-center justify-center p-3 rounded-lg border text-xs transition-all ${
                preference.columnSizeMode === 'Fixed'
                  ? 'border-blue-600 bg-blue-50/50 dark:bg-blue-950/30 text-blue-700 dark:text-blue-400 font-semibold ring-1 ring-blue-600'
                  : 'border-gray-200 dark:border-[#394047] hover:bg-gray-50 dark:hover:bg-[#22272b] text-gray-700 dark:text-gray-300'
              }`}
            >
              <div className="flex gap-1 mb-2 items-center justify-center h-7">
                <div className="w-2.5 h-6 rounded-sm bg-gray-300 dark:bg-gray-600" />
                <div className="w-2.5 h-6 rounded-sm bg-gray-300 dark:bg-gray-600" />
                <div className="w-2.5 h-6 rounded-sm bg-gray-300 dark:bg-gray-600" />
              </div>
              <span>Fixed</span>
            </button>

            {/* Flexible Option */}
            <button
              type="button"
              onClick={() =>
                mutation.mutate({
                  hideDoneItemsAfter: preference.hideDoneItemsAfter,
                  columnSizeMode: 'Flexible',
                  hiddenFields: preference.hiddenFields,
                })
              }
              className={`flex flex-col items-center justify-center p-3 rounded-lg border text-xs transition-all ${
                preference.columnSizeMode === 'Flexible'
                  ? 'border-blue-600 bg-blue-50/50 dark:bg-blue-950/30 text-blue-700 dark:text-blue-400 font-semibold ring-1 ring-blue-600'
                  : 'border-gray-200 dark:border-[#394047] hover:bg-gray-50 dark:hover:bg-[#22272b] text-gray-700 dark:text-gray-300'
              }`}
            >
              <div className="flex gap-1 mb-2 items-center justify-center h-7 w-full px-2">
                <div className="flex-1 h-6 rounded-sm bg-gray-300 dark:bg-gray-600" />
                <div className="flex-1 h-6 rounded-sm bg-gray-300 dark:bg-gray-600" />
                <div className="flex-1 h-6 rounded-sm bg-gray-300 dark:bg-gray-600" />
              </div>
              <span>Flexible</span>
            </button>
          </div>
        </div>

        {/* Show fields */}
        <div className="space-y-3 pt-2">
          <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300">Show fields</label>
          <div className="relative">
            <Search size={14} className="absolute left-2.5 top-2.5 text-gray-400" />
            <input
              type="text"
              placeholder="Search fields"
              value={fieldSearch}
              onChange={(event) => setFieldSearch(event.target.value)}
              className="w-full pl-8 pr-3 py-1.5 border border-gray-300 dark:border-[#394047] rounded-md text-xs bg-white dark:bg-[#22272b] text-gray-800 dark:text-gray-200 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          <div className="divide-y divide-gray-100 dark:divide-[#394047] max-h-[360px] overflow-y-auto">
            {filteredFields.map((field) => {
              const disabled = field.key === null
              const isChecked = field.alwaysOn || (field.key !== null && !preference.hiddenFields.includes(field.key))

              return (
                <div key={field.label} className="flex items-center justify-between py-2 text-xs">
                  <span className={disabled ? 'text-gray-400 dark:text-gray-500' : 'text-gray-700 dark:text-gray-200 font-medium'}>
                    {field.label}
                  </span>
                  <button
                    type="button"
                    role="switch"
                    aria-checked={isChecked}
                    disabled={disabled || field.alwaysOn}
                    onClick={() => field.key && toggleField(field.key)}
                    className={`relative inline-flex h-4 w-7 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${
                      disabled
                        ? 'opacity-30 cursor-not-allowed bg-gray-200'
                        : isChecked
                        ? 'bg-green-600'
                        : 'bg-gray-300 dark:bg-gray-600'
                    }`}
                  >
                    <span
                      aria-hidden="true"
                      className={`pointer-events-none inline-block h-3 w-3 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${
                        isChecked ? 'translate-x-3' : 'translate-x-0'
                      }`}
                    />
                  </button>
                </div>
              )
            })}
          </div>
        </div>
      </div>
    </aside>
  )
}
