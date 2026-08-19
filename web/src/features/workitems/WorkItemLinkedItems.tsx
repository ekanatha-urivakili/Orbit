import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, X } from 'lucide-react'
import { orbitApi } from '../../api/client'
import { SearchableSelect } from '../../components/form/SearchableSelect'
import { WorkItemTypeIcon } from './typeIcons'
import type { WorkItem, WorkItemLink } from '../../api/types'

const relationshipOptions: { value: string; label: string }[] = [
  { value: 'Blocks:true', label: 'is blocked by' },
  { value: 'Blocks:false', label: 'blocks' },
  { value: 'RelatesTo:false', label: 'relates to' },
  { value: 'Duplicates:false', label: 'duplicates' },
  { value: 'Duplicates:true', label: 'is duplicated by' },
]

function relationshipLabel(link: WorkItemLink): string {
  if (link.kind === 'RelatesTo') return 'relates to'
  const isInverse = link.direction === 'Incoming'
  const option = relationshipOptions.find((candidate) => candidate.value === `${link.kind}:${isInverse}`)
  return option?.label ?? link.kind
}

export function WorkItemLinkedItems({
  workItemId,
  workItems,
}: {
  workItemId: string
  workItems: WorkItem[]
}) {
  const queryClient = useQueryClient()
  const [relationship, setRelationship] = useState(relationshipOptions[0].value)
  const [targetId, setTargetId] = useState('')

  const linksQuery = useQuery({
    queryKey: ['work-item-links', workItemId],
    queryFn: () => orbitApi.listWorkItemLinks(workItemId),
  })
  const links = linksQuery.data ?? []

  const addMutation = useMutation({
    mutationFn: () => {
      const [kind, inverse] = relationship.split(':') as [WorkItemLink['kind'], string]
      return orbitApi.addWorkItemLink(workItemId, {
        kind,
        targetWorkItemId: targetId,
        inverse: inverse === 'true',
      })
    },
    onSuccess: () => {
      setTargetId('')
      queryClient.invalidateQueries({ queryKey: ['work-item-links', workItemId] })
    },
  })

  const removeMutation = useMutation({
    mutationFn: (linkId: string) => orbitApi.removeWorkItemLink(workItemId, linkId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['work-item-links', workItemId] }),
  })

  const targetOptions = workItems
    .filter((item) => item.id !== workItemId && !links.some((link) => link.workItemId === item.id))
    .map((item) => ({ value: item.id, label: `${item.key} — ${item.summary}`, badge: item.type }))

  const groups = new Map<string, WorkItemLink[]>()
  for (const link of links) {
    const label = relationshipLabel(link)
    groups.set(label, [...(groups.get(label) ?? []), link])
  }

  return (
    <section className="mt-8 border-t border-gray-200 pt-6">
      <div className="flex items-center justify-between mb-3">
        <h2 className="text-sm font-bold text-[#172b4d]">Linked work items</h2>
      </div>

      {/* Linked Work Items List */}
      {[...groups.entries()].map(([label, groupLinks]) => (
        <div key={label} className="linked-items-group mb-3">
          <h3 className="text-xs font-semibold uppercase text-gray-500 mb-1.5">{label}</h3>
          {groupLinks.map((link) => (
            <div
              key={link.id}
              className="flex items-center gap-2.5 py-1 px-2 rounded-md hover:bg-gray-50 text-sm border border-transparent hover:border-gray-200 group"
            >
              <WorkItemTypeIcon type={link.type} size={15} />
              <span className="font-semibold text-gray-600 text-xs">{link.key}</span>
              <a
                href={`/browse/${link.key}`}
                target="_blank"
                rel="noopener noreferrer"
                className="flex-1 text-[#172b4d] truncate hover:underline"
              >
                {link.summary}
              </a>
              <button
                type="button"
                className="p-1 text-gray-400 hover:text-red-600 opacity-0 group-hover:opacity-100 transition-opacity"
                aria-label={`Remove link to ${link.key}`}
                onClick={() => removeMutation.mutate(link.id)}
              >
                <X size={14} />
              </button>
            </div>
          ))}
        </div>
      ))}

      {/* Inline Link Creator (Matching Screenshot 1) */}
      <div className="mt-2 rounded-lg border border-[#dfe1e6] bg-white p-2.5 shadow-sm space-y-2">
        <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-2">
          {/* Relationship selector */}
          <div className="w-full sm:w-44 shrink-0">
            <select
              value={relationship}
              onChange={(e) => setRelationship(e.target.value)}
              className="w-full border border-gray-300 rounded px-2.5 py-1.5 text-xs text-gray-700 bg-[#f4f5f7] hover:bg-[#ebecf0] font-medium focus:outline-none focus:border-blue-500"
            >
              {relationshipOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>

          {/* Search / Target selector */}
          <div className="flex-1">
            <SearchableSelect
              size="sm"
              value={targetId}
              onChange={(val) => setTargetId(val)}
              options={targetOptions}
              placeholder="Type, search or paste URL"
              searchPlaceholder="Search work items…"
            />
          </div>
        </div>

        {/* Bottom Action Row */}
        <div className="flex items-center justify-between pt-1 border-t border-gray-100 text-xs">
          <button
            type="button"
            className="flex items-center gap-1 text-gray-500 hover:text-gray-900 font-medium"
            onClick={() => {
              // Focus or open link selection
            }}
          >
            <Plus size={13} />
            Create linked work item
          </button>

          <div className="flex items-center gap-2">
            <button
              type="button"
              disabled={!targetId || addMutation.isPending}
              onClick={() => addMutation.mutate()}
              className="px-3 py-1 bg-[#0052cc] hover:bg-[#0065ff] text-white font-semibold rounded disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              {addMutation.isPending ? 'Linking…' : 'Link'}
            </button>
            <button
              type="button"
              onClick={() => setTargetId('')}
              className="text-gray-500 hover:text-gray-800 font-medium px-2 py-1"
            >
              Cancel
            </button>
          </div>
        </div>
      </div>

      {addMutation.isError && <p className="form-error mt-2">{addMutation.error.message}</p>}
    </section>
  )
}
