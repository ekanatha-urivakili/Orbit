import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, X } from 'lucide-react'
import { orbitApi } from '../../api/client'
import { SearchableSelect } from '../../components/form/SearchableSelect'
import { WorkItemTypeIcon } from './typeIcons'
import type { WorkItem, WorkItemLink } from '../../api/types'

const relationshipOptions: { value: string; label: string }[] = [
  { value: 'Blocks:false', label: 'Blocks' },
  { value: 'Blocks:true', label: 'Is blocked by' },
  { value: 'RelatesTo:false', label: 'Relates to' },
  { value: 'Duplicates:false', label: 'Duplicates' },
  { value: 'Duplicates:true', label: 'Is duplicated by' },
]

function relationshipLabel(link: WorkItemLink): string {
  if (link.kind === 'RelatesTo') return 'Relates to'
  const isInverse = link.direction === 'Incoming'
  const option = relationshipOptions.find((candidate) => candidate.value === `${link.kind}:${isInverse}`)
  return option?.label ?? link.kind
}

export function WorkItemLinkedItems({ workItemId, workItems }: { workItemId: string; workItems: WorkItem[] }) {
  const queryClient = useQueryClient()
  const [adding, setAdding] = useState(false)
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
      return orbitApi.addWorkItemLink(workItemId, { kind, targetWorkItemId: targetId, inverse: inverse === 'true' })
    },
    onSuccess: () => {
      setAdding(false)
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
      <div className="subtasks-header">
        <h2>Linked work items</h2>
        <button type="button" className="icon-button" aria-label="Add linked work item" onClick={() => setAdding(true)}><Plus size={16} /></button>
      </div>

      {links.length === 0 && !adding && <p className="activity-empty-text">No linked work items yet.</p>}

      {[...groups.entries()].map(([label, groupLinks]) => (
        <div key={label} className="linked-items-group">
          <h3 className="linked-items-group-label">{label}</h3>
          {groupLinks.map((link) => (
            <div key={link.id} className="linked-items-row">
              <WorkItemTypeIcon type={link.type} size={15} />
              <span className="subtasks-row-key">{link.key}</span>
              <span className="subtasks-row-summary">{link.summary}</span>
              <button
                type="button"
                className="icon-button"
                aria-label={`Remove link to ${link.key}`}
                onClick={() => removeMutation.mutate(link.id)}
              ><X size={14} /></button>
            </div>
          ))}
        </div>
      ))}

      {adding && (
        <div className="linked-items-add-row">
          <SearchableSelect size="md" value={relationship} onChange={setRelationship} options={relationshipOptions} searchable={false} />
          <SearchableSelect
            size="md"
            value={targetId}
            onChange={setTargetId}
            options={targetOptions}
            placeholder="Select work item"
            searchPlaceholder="Search work items…"
          />
          <button type="button" className="secondary-button" onClick={() => setAdding(false)}>Cancel</button>
          <button type="button" className="primary-button" disabled={!targetId || addMutation.isPending} onClick={() => addMutation.mutate()}>
            {addMutation.isPending ? 'Adding…' : 'Add'}
          </button>
        </div>
      )}
      {addMutation.isError && <p className="form-error">{addMutation.error.message}</p>}
    </section>
  )
}
