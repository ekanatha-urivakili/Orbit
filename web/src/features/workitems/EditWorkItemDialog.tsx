import { useState, type FormEvent } from 'react'
import { X } from 'lucide-react'
import { useUpdateWorkItem } from '../../hooks/useUpdateWorkItem'
import { Field } from '../../components/form/Field'
import { WorkItemComments } from './WorkItemComments'
import { WorkItemAttachments } from './WorkItemAttachments'
import type { Priority, Profile, TenantMembership, WorkItem, WorkItemLinkType } from '../../api/types'

const countries = ['Global', 'Argentina', 'Brasil', 'Nigeria', 'South Africa', 'US', 'Saudi Arabia', 'Turkey']

export function EditWorkItemDialog({
  item,
  workItems,
  profile,
  members,
  priorities,
  onClose,
}: {
  item: WorkItem
  workItems: WorkItem[]
  profile?: Profile
  members: TenantMembership[]
  priorities: Priority[]
  onClose: () => void
}) {
  const [summary, setSummary] = useState(item.summary)
  const [description, setDescription] = useState(item.description ?? '')
  const [priority, setPriority] = useState<Priority>(item.priority)
  const [details, setDetails] = useState({
    parentId: item.parentId,
    epicName: item.epicName,
    acceptanceCriteria: item.acceptanceCriteria,
    stepsToConduct: item.stepsToConduct,
    assigneeUserId: item.assigneeUserId,
    developerUserId: item.developerUserId,
    productOwnerUserId: item.productOwnerUserId,
    sprintName: item.sprintName,
    identifiedOn: item.identifiedOn,
    storyPoints: item.storyPoints,
    linkType: item.linkType,
    linkedWorkItemId: item.linkedWorkItemId,
    countries: item.countries,
    attachmentNames: item.attachmentNames,
  })
  const [labelsText, setLabelsText] = useState(item.labels.join(', '))

  const patch = (change: Partial<typeof details>) => setDetails((current) => ({ ...current, ...change }))
  const mutation = useUpdateWorkItem(item.projectId)
  const type = item.type

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    mutation.mutate(
      {
        workItem: item,
        input: {
          summary,
          description: description || null,
          priority,
          ...details,
          labels: labelsText.split(',').map((label) => label.trim()).filter(Boolean),
        },
      },
      { onSuccess: () => onClose() },
    )
  }

  const parentOptions = workItems.filter((candidate) => {
    if (candidate.id === item.id) return false
    if (type === 'Initiative') return false
    if (type === 'Epic') return candidate.type === 'Initiative'
    if (type === 'Subtask') return candidate.type !== 'Initiative'
    return candidate.type === 'Epic' || candidate.type === 'Initiative'
  })
  const linkOptions = workItems.filter((candidate) => candidate.id !== item.id)

  return (
    <div className="dialog-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <section className="dialog create-work-dialog" role="dialog" aria-modal="true" aria-labelledby="edit-title">
        <header>
          <div><h2 id="edit-title">Edit {item.key}</h2><p className="mt-1 text-xs text-gray-500">Required fields are marked with an asterisk.</p></div>
          <button className="icon-button" type="button" aria-label="Close" onClick={onClose}><X size={20} /></button>
        </header>

        <form onSubmit={submit}>
          <Field label="Work type"><input value={type} disabled /></Field>

          {type === 'Epic' && <Field label="Epic name *"><input required maxLength={255} value={details.epicName ?? ''} onChange={(event) => patch({ epicName: event.target.value || null })} /></Field>}
          <Field label="Summary *"><input autoFocus required minLength={3} maxLength={255} value={summary} onChange={(event) => setSummary(event.target.value)} /></Field>
          <Field label="Description"><textarea value={description} onChange={(event) => setDescription(event.target.value)} maxLength={32000} rows={5} /></Field>
          {type === 'Epic' && <Field label="Acceptance criteria"><textarea value={details.acceptanceCriteria ?? ''} onChange={(event) => patch({ acceptanceCriteria: event.target.value || null })} maxLength={32000} rows={4} /></Field>}
          {type === 'Bug' && <Field label="Steps to conduct action"><textarea value={details.stepsToConduct ?? ''} onChange={(event) => patch({ stepsToConduct: event.target.value || null })} maxLength={32000} rows={4} /></Field>}

          <div className="form-row">
            <Field label="Assignee">
              <select value={details.assigneeUserId ?? ''} onChange={(event) => patch({ assigneeUserId: event.target.value || null })}>
                <option value="">Unassigned</option>
                {members.filter((member) => member.userId).map((member) => (
                  <option key={member.id} value={member.userId ?? ''}>
                    {member.displayName ?? 'Unnamed member'}{profile && member.userId === profile.userId ? ' (me)' : ''}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Priority"><select value={priority} onChange={(event) => setPriority(event.target.value as Priority)}>{priorities.map((value) => <option key={value}>{value}</option>)}</select></Field>
          </div>

          {type !== 'Initiative' && <Field label="Parent"><select value={details.parentId ?? ''} onChange={(event) => patch({ parentId: event.target.value || null })}><option value="">No parent</option>{parentOptions.map((candidate) => <option key={candidate.id} value={candidate.id}>{candidate.key} — {candidate.summary}</option>)}</select></Field>}

          {(type === 'Task' || type === 'Story') && (
            <Field label="Story points"><input type="number" min="0" max="10000" step="0.5" value={details.storyPoints ?? ''} onChange={(event) => patch({ storyPoints: event.target.value ? Number(event.target.value) : null })} /></Field>
          )}

          {type === 'Bug' && <>
            <div className="form-row">
              <Field label="Sprint"><input value={details.sprintName ?? ''} onChange={(event) => patch({ sprintName: event.target.value || null })} maxLength={255} placeholder="Sprint name" /></Field>
              <Field label="Identified on"><input value={details.identifiedOn ?? ''} onChange={(event) => patch({ identifiedOn: event.target.value || null })} maxLength={255} placeholder="Production, staging, device…" /></Field>
            </div>
            <div className="form-row">
              <Field label="Developer"><select value={details.developerUserId ?? ''} onChange={(event) => patch({ developerUserId: event.target.value || null })}><option value="">Unassigned</option>{profile && <option value={profile.userId}>{profile.displayName}</option>}</select></Field>
              <Field label="Story points"><input type="number" min="0" max="10000" step="0.5" value={details.storyPoints ?? ''} onChange={(event) => patch({ storyPoints: event.target.value ? Number(event.target.value) : null })} /></Field>
            </div>
            <fieldset className="rounded-lg border border-gray-200 p-3"><legend className="px-1 text-xs font-semibold text-gray-600">Countries</legend><div className="grid grid-cols-2 gap-2 sm:grid-cols-3">{countries.map((country) => <label key={country} className="country-option"><input type="checkbox" checked={details.countries.includes(country)} onChange={(event) => patch({ countries: event.target.checked ? [...details.countries, country] : details.countries.filter((value) => value !== country) })} /> {country}</label>)}</div></fieldset>
          </>}

          {type === 'Spike' && <Field label="Product owner"><select value={details.productOwnerUserId ?? ''} onChange={(event) => patch({ productOwnerUserId: event.target.value || null })}><option value="">Unassigned</option>{profile && <option value={profile.userId}>{profile.displayName}</option>}</select></Field>}

          <Field label="Labels"><input value={labelsText} onChange={(event) => setLabelsText(event.target.value)} placeholder="frontend, customer-impact (comma separated)" /></Field>

          <div className="form-row">
            <Field label="Linked work items"><select value={details.linkType ?? ''} onChange={(event) => patch({ linkType: event.target.value ? event.target.value as WorkItemLinkType : null, linkedWorkItemId: event.target.value ? details.linkedWorkItemId : null })}><option value="">No relationship</option><option value="DependsOn">Depends on</option><option value="Blocks">Blocks</option><option value="RelatesTo">Relates to</option></select></Field>
            <Field label="Target work item"><select disabled={!details.linkType} value={details.linkedWorkItemId ?? ''} onChange={(event) => patch({ linkedWorkItemId: event.target.value || null })}><option value="">Select work item</option>{linkOptions.map((candidate) => <option key={candidate.id} value={candidate.id}>{candidate.key} — {candidate.summary}</option>)}</select></Field>
          </div>

          <WorkItemAttachments workItemId={item.id} members={members} />

          <WorkItemComments workItemId={item.id} profile={profile} />

          {mutation.isError && <p className="form-error">{mutation.error.message}</p>}
          <footer className="sticky bottom-0 -mx-6 -mb-6 border-t border-gray-200 bg-white px-6 py-4">
            <button type="button" className="secondary-button" onClick={onClose}>Cancel</button>
            <button className="primary-button" disabled={mutation.isPending}>{mutation.isPending ? 'Saving…' : 'Save'}</button>
          </footer>
        </form>
      </section>
    </div>
  )
}
