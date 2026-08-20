import { useState, type FormEvent } from 'react'
import { Paperclip, X } from 'lucide-react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useCreateWorkItem } from '../../hooks/useCreateWorkItem'
import { Field, Hint } from '../../components/form/Field'
import { SearchableSelect } from '../../components/form/SearchableSelect'
import { AssigneePicker } from '../../components/AssigneePicker'
import { WorkItemTypeIcon } from './typeIcons'
import { RichTextEditor } from '../../components/form/RichTextEditor'
import { orbitApi } from '../../api/client'
import type {
  CreateWorkItemInput,
  Priority,
  Profile,
  Project,
  TenantMembership,
  WorkItem,
  WorkItemType,
  WorkItemTypeDefinition,
  Sprint,
} from '../../api/types'

const countries = ['Global', 'Argentina', 'Brasil', 'Nigeria', 'South Africa', 'US', 'Saudi Arabia', 'Turkey']

const defaultAcceptanceCriteriaTable = '<table><thead><tr><th>As a</th><th>When</th><th>Then</th><th>Dev</th><th>UAT</th><th>Production</th><th>Comments</th></tr></thead><tbody><tr><td></td><td></td><td></td><td></td><td></td><td></td><td></td></tr><tr><td></td><td></td><td></td><td></td><td></td><td></td><td></td></tr></tbody></table>'

const blankDetails: Required<Omit<CreateWorkItemInput, 'projectId' | 'summary' | 'description' | 'type' | 'priority'>> = {
  parentId: null,
  epicName: null,
  acceptanceCriteria: defaultAcceptanceCriteriaTable,
  stepsToConduct: null,
  assigneeUserId: null,
  developerUserId: null,
  productOwnerUserId: null,
  sprintName: null,
  identifiedOn: null,
  startDate: null,
  dueDate: null,
  teamId: null,
  storyPoints: null,
  labels: [],
  countries: [],
  attachmentNames: [],
}

export function CreateWorkItemDialog({
  project,
  workItems,
  profile,
  members = [],
  types,
  priorities,
  parent,
  onClose,
  sprints = [],
}: {
  project: Project
  workItems: WorkItem[]
  profile?: Profile
  members?: TenantMembership[]
  types: WorkItemTypeDefinition[]
  priorities: Priority[]
  /** When set, this dialog creates a subtask locked under `parent` instead of a top-level item. */
  parent?: WorkItem
  onClose: () => void
  sprints?: Sprint[]
}) {
  const queryClient = useQueryClient()
  const [summary, setSummary] = useState('')
  const [description, setDescription] = useState('')
  const [type, setType] = useState<WorkItemType>(types.find((itemType) => itemType.id === 'Story')?.id ?? types[0]?.id ?? 'Story')
  const [priority, setPriority] = useState<Priority>('Medium')
  const [details, setDetails] = useState({ ...blankDetails, parentId: parent?.id ?? null })
  const [labelsText, setLabelsText] = useState('')
  const [createAnother, setCreateAnother] = useState(false)
  const [selectedSprintId, setSelectedSprintId] = useState('')
  const [newSprintName, setNewSprintName] = useState('')

  const teamsQuery = useQuery({
    queryKey: ['teams'],
    queryFn: () => orbitApi.listTeams(),
  })
  const teams = teamsQuery.data ?? []

  const patch = (change: Partial<typeof blankDetails>) => setDetails((current) => ({ ...current, ...change }))
  const mutation = useCreateWorkItem(project.id)
  const typeLabel = types.find((itemType) => itemType.id === type)?.label ?? type

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    
    let targetSprintId = selectedSprintId
    let targetSprintName = details.sprintName

    if (selectedSprintId === '__new_sprint__') {
      const newSprint = await orbitApi.createSprint(project.id, newSprintName)
      targetSprintId = newSprint.id
      targetSprintName = newSprint.name
    } else if (selectedSprintId) {
      const s = sprints.find(sp => sp.id === selectedSprintId)
      targetSprintName = s ? s.name : null
    } else {
      targetSprintName = null
    }

    mutation.mutate(
      {
        projectId: project.id,
        summary,
        description: description || null,
        type,
        priority,
        ...details,
        sprintName: targetSprintName,
        labels: labelsText.split(',').map((label) => label.trim()).filter(Boolean),
      },
      {
        onSuccess: async (newWorkItem) => {
          if (targetSprintId) {
            await orbitApi.assignWorkItemToSprint(newWorkItem.id, targetSprintId)
            queryClient.invalidateQueries({ queryKey: ['sprints', project.id] })
          }
          queryClient.invalidateQueries({ queryKey: ['work-items', project.id] })

          if (!createAnother) {
            onClose()
            return
          }
          setSummary('')
          setDescription('')
          setDetails({ ...blankDetails, parentId: parent?.id ?? null })
          setLabelsText('')
          setSelectedSprintId('')
          setNewSprintName('')
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

  const openSprints = sprints.filter((s) => s.state !== 'Closed')
  const closedSprints = sprints.filter((s) => s.state === 'Closed')
  const sprintOptions = [
    { value: '', label: 'No Sprint' },
    ...openSprints.map((s) => ({ value: s.id, label: s.name })),
    ...closedSprints.map((s) => ({ value: s.id, label: `${s.name} (Closed)` })),
  ]
  if (openSprints.length === 0) {
    sprintOptions.push({ value: '__new_sprint__', label: 'Create a new sprint...' })
  }

  return (
    <div className="dialog-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <section className="dialog create-work-dialog" role="dialog" aria-modal="true" aria-labelledby="create-title">
        <header>
          <div>
            <h2 id="create-title" className="flex items-center gap-2"><WorkItemTypeIcon type={type} size={20} /> Create {typeLabel}</h2>
            <p className="mt-1 text-xs text-gray-500">
              {parent ? <>Subtask of <strong>{parent.key}</strong> — {parent.summary}</> : 'Required fields are marked with an asterisk.'}
            </p>
          </div>
          <button className="icon-button" type="button" aria-label="Close" onClick={onClose}><X size={20} /></button>
        </header>

        <div className="dialog-scroll">
        <form onSubmit={submit}>
          <div className="form-row">
            <Field label="Space *">
              <SearchableSelect
                size="xl"
                value={project.id}
                options={[{ value: project.id, label: `${project.name} (${project.key})` }]}
                searchable={false}
              />
            </Field>
            <Field label="Work type *">
              <SearchableSelect
                size="xl"
                value={type}
                onChange={(val) => { setType(val as WorkItemType); patch({ parentId: parent?.id ?? null }) }}
                options={types.map((itemType) => ({ value: itemType.id, label: itemType.label, icon: <WorkItemTypeIcon type={itemType.id} /> }))}
                searchPlaceholder="Search work types…"
              />
            </Field>
          </div>

          <Field label="Status">
            <SearchableSelect
              size="xl"
              value="Backlog"
              options={[{ value: 'Backlog', label: 'Backlog' }]}
              searchable={false}
            />
            <Hint>This is the initial status upon creation.</Hint>
          </Field>

          {type === 'Epic' && <Field label="Epic name *"><input required maxLength={255} value={details.epicName ?? ''} onChange={(event) => patch({ epicName: event.target.value || null })} /><Hint>Provide a short name to identify this epic.</Hint></Field>}
          <Field label="Summary *"><input autoFocus required minLength={3} maxLength={255} value={summary} onChange={(event) => setSummary(event.target.value)} /></Field>
          <Field label="Description"><RichTextEditor value={description} onChange={setDescription} placeholder="Describe the outcome, context, and expected behaviour." /></Field>
          <Field label="Acceptance criteria">
            <RichTextEditor
              value={details.acceptanceCriteria ?? ''}
              onChange={(html) => patch({ acceptanceCriteria: html || null })}
              placeholder="Define the acceptance criteria. Use the table button (⊞) in the toolbar to insert a table."
            />
          </Field>
          {type === 'Bug' && <Field label="Steps to conduct action"><textarea value={details.stepsToConduct ?? ''} onChange={(event) => patch({ stepsToConduct: event.target.value || null })} maxLength={32000} rows={4} placeholder="Numbered reproduction steps and expected versus actual result." /></Field>}

          <div className="form-row">
            <Field label="Assignee">
              <div className="flex items-center gap-2.5 min-h-[38px] settings-control bg-white dark:bg-[#22272b] px-3 py-1.5 border border-gray-300 dark:border-gray-600 rounded-lg">
                <AssigneePicker
                  members={members}
                  value={details.assigneeUserId}
                  onChange={(assigneeUserId) => patch({ assigneeUserId })}
                  size="md"
                />
                <span className="text-sm text-gray-700 dark:text-gray-300">
                  {details.assigneeUserId
                    ? members.find((m) => m.userId === details.assigneeUserId)?.displayName ?? 'Unnamed member'
                    : 'Unassigned'}
                </span>
              </div>
            </Field>
            <Field label="Priority">
              <SearchableSelect
                size="xl"
                value={priority}
                onChange={(val) => setPriority(val as Priority)}
                options={priorities.map((value) => ({ value, label: value }))}
                searchPlaceholder="Search priority…"
              />
            </Field>
          </div>

          {parent ? (
            <Field label="Parent">
              <SearchableSelect size="xl" disabled value={parent.id} options={[{ value: parent.id, label: `${parent.key} — ${parent.summary}`, badge: parent.type }]} searchable={false} />
            </Field>
          ) : type !== 'Initiative' && (
            <Field label="Parent">
              <SearchableSelect
                size="xl"
                value={details.parentId ?? ''}
                onChange={(val) => patch({ parentId: val || null })}
                options={[
                  { value: '', label: 'No parent' },
                  ...parentOptions.map((item) => ({
                    value: item.id,
                    label: `${item.key} — ${item.summary}`,
                    badge: item.type,
                  })),
                ]}
                placeholder="No parent"
                searchPlaceholder="Search parent work items…"
              />
              <Hint>Hierarchy rules restrict which parents can be selected.</Hint>
            </Field>
          )}

          {/* Sprints and Estimation for all relevant work item types */}
          {type !== 'Initiative' && type !== 'Epic' && (
            <div className="form-row">
              <Field label="Sprint">
                <SearchableSelect
                  size="xl"
                  value={selectedSprintId}
                  onChange={(val) => setSelectedSprintId(val)}
                  options={sprintOptions}
                  placeholder="No Sprint"
                />
                {selectedSprintId === '__new_sprint__' && (
                  <div className="mt-2">
                    <input
                      type="text"
                      required
                      placeholder="New Sprint Name"
                      value={newSprintName}
                      onChange={(e) => setNewSprintName(e.target.value)}
                      className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm focus:outline-none focus:border-blue-500"
                    />
                  </div>
                )}
              </Field>
              <Field label="Story points">
                <input
                  type="number"
                  min="0"
                  max="10000"
                  step="0.5"
                  value={details.storyPoints ?? ''}
                  onChange={(event) =>
                    patch({
                      storyPoints: event.target.value ? Number(event.target.value) : null,
                    })
                  }
                />
              </Field>
            </div>
          )}

          <div className="form-row">
            <Field label="Team">
              <SearchableSelect
                size="xl"
                value={details.teamId ?? ''}
                onChange={(val) => patch({ teamId: val || null })}
                options={[
                  { value: '', label: 'No team' },
                  ...teams.map((team) => ({ value: team.id, label: team.name })),
                ]}
                placeholder="No team"
                searchPlaceholder="Search teams…"
              />
            </Field>
            <Field label="Start date">
              <input
                type="date"
                lang="en-GB"
                value={details.startDate ?? ''}
                onChange={(event) => patch({ startDate: event.target.value || null })}
              />
            </Field>
          </div>

          <div className="form-row">
            <Field label="Due date">
              <input
                type="date"
                lang="en-GB"
                min={details.startDate ?? undefined}
                value={details.dueDate ?? ''}
                onChange={(event) => patch({ dueDate: event.target.value || null })}
              />
            </Field>
          </div>

          {type === 'Bug' && (
            <>
              <div className="form-row">
                <Field label="Developer">
                  <SearchableSelect
                    size="xl"
                    value={details.developerUserId ?? ''}
                    onChange={(val) => patch({ developerUserId: val || null })}
                    options={[
                      { value: '', label: 'Unassigned' },
                      ...(profile ? [{ value: profile.userId, label: profile.displayName }] : []),
                    ]}
                    placeholder="Unassigned"
                    searchPlaceholder="Search developers…"
                  />
                </Field>
                <Field label="Identified on">
                  <input
                    value={details.identifiedOn ?? ''}
                    onChange={(event) => patch({ identifiedOn: event.target.value || null })}
                    maxLength={255}
                    placeholder="Production, staging, device…"
                  />
                </Field>
              </div>
              <fieldset className="rounded-lg border border-gray-200 p-3 mb-4">
                <legend className="px-1 text-xs font-semibold text-gray-600">Countries</legend>
                <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
                  {countries.map((country) => (
                    <label key={country} className="country-option">
                      <input
                        type="checkbox"
                        checked={details.countries.includes(country)}
                        onChange={(event) =>
                          patch({
                            countries: event.target.checked
                              ? [...details.countries, country]
                              : details.countries.filter((value) => value !== country),
                          })
                        }
                      />{' '}
                      {country}
                    </label>
                  ))}
                </div>
              </fieldset>
            </>
          )}

          {type === 'Spike' && (
            <Field label="Product owner">
              <SearchableSelect
                size="xl"
                value={details.productOwnerUserId ?? ''}
                onChange={(val) => patch({ productOwnerUserId: val || null })}
                options={[
                  { value: '', label: 'Unassigned' },
                  ...(profile ? [{ value: profile.userId, label: profile.displayName }] : []),
                ]}
                placeholder="Unassigned"
                searchPlaceholder="Search product owners…"
              />
            </Field>
          )}

          <Field label="Labels"><input value={labelsText} onChange={(event) => setLabelsText(event.target.value)} placeholder="frontend, customer-impact (comma separated)" /></Field>

          <Field label="Attachments"><span className="attachment-picker"><Paperclip size={17} /><span>{details.attachmentNames.length ? details.attachmentNames.join(', ') : 'Browse files'}</span><input className="sr-only" type="file" multiple onChange={(event) => patch({ attachmentNames: Array.from(event.target.files ?? []).map((file) => file.name) })} /></span><Hint>Orbit stores safe file metadata in this increment. Binary upload follows quarantine scanning.</Hint></Field>

          {mutation.isError && <p className="form-error">{mutation.error.message}</p>}
          <footer className="sticky bottom-0 -mx-6 -mb-6 rounded-b-2xl border-t border-gray-200 bg-white px-6 py-4">
            <label className="create-another-label"><input className="create-another-check" type="checkbox" checked={createAnother} onChange={(event) => setCreateAnother(event.target.checked)} /> Create another</label>
            <button type="button" className="secondary-button" onClick={onClose}>Cancel</button>
            <button className="primary-button" disabled={mutation.isPending}>{mutation.isPending ? 'Creating…' : 'Create'}</button>
          </footer>
        </form>
        </div>
      </section>
    </div>
  )
}
