import { useState, type FormEvent } from 'react'
import { Paperclip, X } from 'lucide-react'
import { useCreateWorkItem } from '../../hooks/useCreateWorkItem'
import { Field, Hint } from '../../components/form/Field'
import type {
  CreateWorkItemInput,
  Priority,
  Profile,
  Project,
  WorkItem,
  WorkItemLinkType,
  WorkItemType,
} from '../../api/types'

const countries = ['Global', 'Argentina', 'Brasil', 'Nigeria', 'South Africa', 'US', 'Saudi Arabia', 'Turkey']

const blankDetails: Required<Omit<CreateWorkItemInput, 'projectId' | 'summary' | 'description' | 'type' | 'priority'>> = {
  parentId: null,
  epicName: null,
  acceptanceCriteria: null,
  stepsToConduct: null,
  assigneeUserId: null,
  developerUserId: null,
  productOwnerUserId: null,
  sprintName: null,
  identifiedOn: null,
  storyPoints: null,
  linkType: null,
  linkedWorkItemId: null,
  labels: [],
  countries: [],
  attachmentNames: [],
}

export function CreateWorkItemDialog({
  project,
  workItems,
  profile,
  types,
  priorities,
  onClose,
}: {
  project: Project
  workItems: WorkItem[]
  profile?: Profile
  types: WorkItemType[]
  priorities: Priority[]
  onClose: () => void
}) {
  const [summary, setSummary] = useState('')
  const [description, setDescription] = useState('')
  const [type, setType] = useState<WorkItemType>('Task')
  const [priority, setPriority] = useState<Priority>('Medium')
  const [details, setDetails] = useState(blankDetails)
  const [labelsText, setLabelsText] = useState('')
  const [createAnother, setCreateAnother] = useState(false)

  const patch = (change: Partial<typeof blankDetails>) => setDetails((current) => ({ ...current, ...change }))
  const mutation = useCreateWorkItem(project.id)

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    mutation.mutate(
      {
        projectId: project.id,
        summary,
        description: description || null,
        type,
        priority,
        ...details,
        labels: labelsText.split(',').map((label) => label.trim()).filter(Boolean),
      },
      {
        onSuccess: () => {
          if (!createAnother) {
            onClose()
            return
          }
          setSummary('')
          setDescription('')
          setDetails(blankDetails)
          setLabelsText('')
        },
      },
    )
  }

  const parentOptions = workItems.filter((item) => {
    if (type === 'Initiative') return false
    if (type === 'Epic') return item.type === 'Initiative'
    if (type === 'Subtask') return item.type !== 'Initiative'
    return item.type === 'Epic' || item.type === 'Initiative'
  })

  return (
    <div className="dialog-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <section className="dialog create-work-dialog" role="dialog" aria-modal="true" aria-labelledby="create-title">
        <header>
          <div><h2 id="create-title">Create {type}</h2><p className="mt-1 text-xs text-gray-500">Required fields are marked with an asterisk.</p></div>
          <button className="icon-button" type="button" aria-label="Close" onClick={onClose}><X size={20} /></button>
        </header>

        <form onSubmit={submit}>
          <div className="form-row">
            <Field label="Space *"><select value={project.id} disabled><option value={project.id}>{project.name} ({project.key})</option></select></Field>
            <Field label="Work type *"><select value={type} onChange={(event) => { setType(event.target.value as WorkItemType); patch({ parentId: null }) }}>{types.map((value) => <option key={value}>{value}</option>)}</select></Field>
          </div>

          <Field label="Status"><select value="Backlog" disabled><option>Backlog</option></select><Hint>This is the initial status upon creation.</Hint></Field>

          {type === 'Epic' && <Field label="Epic name *"><input required maxLength={255} value={details.epicName ?? ''} onChange={(event) => patch({ epicName: event.target.value || null })} /><Hint>Provide a short name to identify this epic.</Hint></Field>}
          <Field label="Summary *"><input autoFocus required minLength={3} maxLength={255} value={summary} onChange={(event) => setSummary(event.target.value)} /></Field>
          <Field label="Description"><textarea value={description} onChange={(event) => setDescription(event.target.value)} maxLength={32000} rows={5} placeholder="Describe the outcome, context, and expected behaviour." /></Field>
          {type === 'Epic' && <Field label="Acceptance criteria"><textarea value={details.acceptanceCriteria ?? ''} onChange={(event) => patch({ acceptanceCriteria: event.target.value || null })} maxLength={32000} rows={4} /></Field>}
          {type === 'Bug' && <Field label="Steps to conduct action"><textarea value={details.stepsToConduct ?? ''} onChange={(event) => patch({ stepsToConduct: event.target.value || null })} maxLength={32000} rows={4} placeholder="Numbered reproduction steps and expected versus actual result." /></Field>}

          <div className="form-row">
            {(type === 'Bug' || type === 'Spike') && <Field label="Assignee"><select value={details.assigneeUserId ?? ''} onChange={(event) => patch({ assigneeUserId: event.target.value || null })}><option value="">Automatic</option>{profile && <option value={profile.userId}>Assign to me — {profile.displayName}</option>}</select></Field>}
            <Field label="Priority"><select value={priority} onChange={(event) => setPriority(event.target.value as Priority)}>{priorities.map((value) => <option key={value}>{value}</option>)}</select></Field>
          </div>

          {type !== 'Initiative' && <Field label="Parent"><select value={details.parentId ?? ''} onChange={(event) => patch({ parentId: event.target.value || null })}><option value="">No parent</option>{parentOptions.map((item) => <option key={item.id} value={item.id}>{item.key} — {item.summary}</option>)}</select><Hint>Hierarchy rules restrict which parents can be selected.</Hint></Field>}

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
            <Field label="Target work item"><select disabled={!details.linkType} value={details.linkedWorkItemId ?? ''} onChange={(event) => patch({ linkedWorkItemId: event.target.value || null })}><option value="">Select work item</option>{workItems.map((item) => <option key={item.id} value={item.id}>{item.key} — {item.summary}</option>)}</select></Field>
          </div>

          <Field label="Attachments"><span className="attachment-picker"><Paperclip size={17} /><span>{details.attachmentNames.length ? details.attachmentNames.join(', ') : 'Browse files'}</span><input className="sr-only" type="file" multiple onChange={(event) => patch({ attachmentNames: Array.from(event.target.files ?? []).map((file) => file.name) })} /></span><Hint>Orbit stores safe file metadata in this increment. Binary upload follows quarantine scanning.</Hint></Field>

          {mutation.isError && <p className="form-error">{mutation.error.message}</p>}
          <footer className="sticky bottom-0 -mx-6 -mb-6 border-t border-gray-200 bg-white px-6 py-4">
            <label className="mr-auto flex-row items-center gap-2"><input className="create-another-check" type="checkbox" checked={createAnother} onChange={(event) => setCreateAnother(event.target.checked)} /> Create another</label>
            <button type="button" className="secondary-button" onClick={onClose}>Cancel</button>
            <button className="primary-button" disabled={mutation.isPending}>{mutation.isPending ? 'Creating…' : 'Create'}</button>
          </footer>
        </form>
      </section>
    </div>
  )
}
