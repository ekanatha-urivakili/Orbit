import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ListChecks } from 'lucide-react'
import { orbitApi } from '../../api/client'
import type { WorkItemCustomFieldValue, WorkItemType } from '../../api/types'

const inputClassName =
  'w-full rounded-lg border border-gray-300 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500'

export function WorkItemCustomFields({
  workItemId,
  projectId,
  workItemType,
}: {
  workItemId: string
  projectId: string
  workItemType: WorkItemType
}) {
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState<Record<string, string[]>>({})
  const [error, setError] = useState<string | null>(null)

  const definitionsQuery = useQuery({
    queryKey: ['custom-fields', projectId],
    queryFn: () => orbitApi.listCustomFields(projectId),
  })
  const valuesQuery = useQuery({
    queryKey: ['work-item-custom-field-values', workItemId],
    queryFn: () => orbitApi.listWorkItemCustomFieldValues(workItemId),
  })

  const definitions = [...(definitionsQuery.data ?? [])]
    .filter((definition) => definition.enabled)
    .filter((definition) => definition.applicableTypes.length === 0 || definition.applicableTypes.includes(workItemType))
    .sort((a, b) => a.order - b.order)
  const savedValuesByDefinitionId = new Map(
    (valuesQuery.data ?? []).map((value) => [value.customFieldDefinitionId, value.values]),
  )
  const getValue = (definitionId: string): string[] =>
    draft[definitionId] ?? savedValuesByDefinitionId.get(definitionId) ?? []
  const setValue = (definitionId: string, values: string[]) =>
    setDraft((previous) => ({ ...previous, [definitionId]: values }))

  const mutation = useMutation({
    mutationFn: (values: WorkItemCustomFieldValue[]) => orbitApi.setWorkItemCustomFieldValues(workItemId, values),
    onSuccess: () => {
      setDraft({})
      setError(null)
      queryClient.invalidateQueries({ queryKey: ['work-item-custom-field-values', workItemId] })
    },
    onError: (mutationError) =>
      setError(mutationError instanceof Error ? mutationError.message : 'Failed to save custom fields.'),
  })

  if (definitions.length === 0) return null

  const handleSave = () => {
    const missingRequired = definitions.find(
      (definition) => definition.required && getValue(definition.id).length === 0,
    )
    if (missingRequired) {
      setError(`'${missingRequired.label}' is required.`)
      return
    }

    setError(null)
    mutation.mutate(
      definitions.map((definition) => ({ customFieldDefinitionId: definition.id, values: getValue(definition.id) })),
    )
  }

  const hasDraft = Object.keys(draft).length > 0

  return (
    <div className="mt-8 border-t border-gray-200 pt-6">
      <h3 className="flex items-center gap-2 text-sm font-semibold text-gray-900 mb-4">
        <ListChecks size={16} className="text-gray-500" /> Custom fields
      </h3>

      <div className="space-y-3">
        {definitions.map((definition) => {
          const current = getValue(definition.id)
          return (
            <div key={definition.id}>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                {definition.label}
                {definition.required && <span className="text-red-500"> *</span>}
              </label>

              {definition.fieldType === 'Text' && (
                <input
                  type="text"
                  className={inputClassName}
                  value={current[0] ?? ''}
                  onChange={(event) => setValue(definition.id, event.target.value ? [event.target.value] : [])}
                />
              )}

              {definition.fieldType === 'Number' && (
                <input
                  type="number"
                  className={inputClassName}
                  value={current[0] ?? ''}
                  onChange={(event) => setValue(definition.id, event.target.value ? [event.target.value] : [])}
                />
              )}

              {definition.fieldType === 'Date' && (
                <input
                  type="date"
                  lang="en-GB"
                  className={inputClassName}
                  value={current[0] ?? ''}
                  onChange={(event) => setValue(definition.id, event.target.value ? [event.target.value] : [])}
                />
              )}

              {definition.fieldType === 'Checkbox' && (
                <input
                  type="checkbox"
                  checked={current[0] === 'true'}
                  onChange={(event) => setValue(definition.id, event.target.checked ? ['true'] : [])}
                />
              )}

              {definition.fieldType === 'SingleChoice' && (
                <select
                  className={inputClassName}
                  value={current[0] ?? ''}
                  onChange={(event) => setValue(definition.id, event.target.value ? [event.target.value] : [])}
                >
                  <option value="">—</option>
                  {definition.choiceOptions.map((option) => (
                    <option key={option.id} value={option.id}>
                      {option.label}
                    </option>
                  ))}
                </select>
              )}

              {definition.fieldType === 'MultiChoice' && (
                <div className="flex flex-wrap gap-3">
                  {definition.choiceOptions.map((option) => (
                    <label key={option.id} className="inline-flex items-center gap-1.5 text-sm text-gray-700">
                      <input
                        type="checkbox"
                        checked={current.includes(option.id)}
                        onChange={() =>
                          setValue(
                            definition.id,
                            current.includes(option.id)
                              ? current.filter((value) => value !== option.id)
                              : [...current, option.id],
                          )
                        }
                      />
                      {option.label}
                    </label>
                  ))}
                </div>
              )}
            </div>
          )
        })}
      </div>

      {error && <p className="text-red-600 text-xs mt-2">{error}</p>}

      {hasDraft && (
        <button
          type="button"
          onClick={handleSave}
          disabled={mutation.isPending}
          className="mt-3 text-sm font-medium text-blue-700 hover:text-blue-800 disabled:opacity-50"
        >
          {mutation.isPending ? 'Saving…' : 'Save custom fields'}
        </button>
      )}
    </div>
  )
}
