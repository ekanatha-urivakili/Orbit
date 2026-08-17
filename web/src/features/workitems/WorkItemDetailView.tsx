import { useState, type FormEvent } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronLeft, ChevronDown } from 'lucide-react'
import { orbitApi } from '../../api/client'
import { useUpdateWorkItem } from '../../hooks/useUpdateWorkItem'
import { Field, Hint } from '../../components/form/Field'
import { SearchableSelect } from '../../components/form/SearchableSelect'
import { WorkItemComments } from './WorkItemComments'
import { WorkItemAttachments } from './WorkItemAttachments'
import { WorkItemSubtasks } from './WorkItemSubtasks'
import { WorkItemLinkedItems } from './WorkItemLinkedItems'
import { WorkItemTypeIcon } from './typeIcons'
import { RichTextEditor } from '../../components/form/RichTextEditor'
import { allStatuses, statusMeta } from '../board/constants'
import type { Priority, Profile, Project, TenantMembership, WorkItem, WorkItemStatus, WorkItemTypeDefinition } from '../../api/types'

const countries = ['Global', 'Argentina', 'Brasil', 'Nigeria', 'South Africa', 'US', 'Saudi Arabia', 'Turkey']

export function WorkItemDetailView({
  item,
  project,
  workItems,
  profile,
  members,
  types,
  priorities,
  onBack,
  onStatusChange,
  onOpenWorkItem,
}: {
  item: WorkItem
  project?: Project
  workItems: WorkItem[]
  profile?: Profile
  members: TenantMembership[]
  types: WorkItemTypeDefinition[]
  priorities: Priority[]
  onBack: () => void
  onStatusChange: (workItem: WorkItem, status: WorkItemStatus) => void
  onOpenWorkItem: (workItem: WorkItem) => void
}) {
  const queryClient = useQueryClient()
  const [summary, setSummary] = useState(item.summary)
  const [editingSummary, setEditingSummary] = useState(false)
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
    countries: item.countries,
    attachmentNames: item.attachmentNames,
  })
  const [labelsText, setLabelsText] = useState(item.labels.join(', '))

  const patch = (change: Partial<typeof details>) => setDetails((current) => ({ ...current, ...change }))
  const mutation = useUpdateWorkItem(item.projectId)
  const type = item.type

  const attachmentsQuery = useQuery({
    queryKey: ['work-item-attachments', item.id],
    queryFn: () => orbitApi.listWorkItemAttachments(item.id),
  })
  const attachments = attachmentsQuery.data ?? []

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    mutation.mutate({
      workItem: item,
      input: {
        summary,
        description: description || null,
        priority,
        ...details,
        labels: labelsText.split(',').map((label) => label.trim()).filter(Boolean),
      },
    })
  }

  const parentOptions = workItems.filter((candidate) => {
    if (candidate.id === item.id) return false
    if (type === 'Initiative') return false
    if (type === 'Epic') return candidate.type === 'Initiative'
    if (type === 'Subtask') return candidate.type !== 'Initiative'
    return candidate.type === 'Epic' || candidate.type === 'Initiative'
  })
  const membersById = new Map(members.map((member) => [member.userId, member]))

  return (
    <div className="work-item-detail">
      <div className="work-item-detail-breadcrumb">
        <button type="button" className="icon-button" onClick={onBack} aria-label="Back">
          <ChevronLeft size={18} />
        </button>
        <span>{project?.name ?? 'Space'}</span>
        <span className="work-item-detail-breadcrumb-sep">/</span>
        <span>{item.key}</span>
      </div>

      <form className="work-item-detail-grid" onSubmit={submit}>
        <div className="work-item-detail-main">
          <div className="work-item-detail-title-row">
            <WorkItemTypeIcon type={type} size={22} />
            <span className="text-sm font-semibold text-gray-500">{item.key}</span>
          </div>

          {editingSummary ? (
            <input
              autoFocus
              required
              minLength={3}
              maxLength={255}
              className="work-item-detail-title-input"
              value={summary}
              onChange={(event) => setSummary(event.target.value)}
              onBlur={() => setEditingSummary(false)}
            />
          ) : (
            <h1 className="work-item-detail-title" onClick={() => setEditingSummary(true)}>{summary}</h1>
          )}

          {type === 'Epic' && <Field label="Epic name *"><input required maxLength={255} value={details.epicName ?? ''} onChange={(event) => patch({ epicName: event.target.value || null })} /></Field>}

          <section className="work-item-detail-section">
            <h2>Description</h2>
            <RichTextEditor
              value={description}
              onChange={setDescription}
              placeholder="Describe the outcome, context, and expected behaviour."
              workItemId={item.id}
              attachments={attachments}
              onAttachmentUploaded={() => queryClient.invalidateQueries({ queryKey: ['work-item-attachments', item.id] })}
            />
          </section>

          {(type === 'Epic' || details.acceptanceCriteria) && (
            <section className="work-item-detail-section">
              <h2>Acceptance criteria</h2>
              <textarea value={details.acceptanceCriteria ?? ''} onChange={(event) => patch({ acceptanceCriteria: event.target.value || null })} maxLength={32000} rows={4} />
            </section>
          )}
          {type === 'Bug' && (
            <section className="work-item-detail-section">
              <h2>Steps to conduct action</h2>
              <textarea value={details.stepsToConduct ?? ''} onChange={(event) => patch({ stepsToConduct: event.target.value || null })} maxLength={32000} rows={4} />
            </section>
          )}

          {type === 'Bug' && (
            <fieldset className="rounded-lg border border-gray-200 p-3 mt-4">
              <legend className="px-1 text-xs font-semibold text-gray-600">Countries</legend>
              <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
                {countries.map((country) => (
                  <label key={country} className="country-option">
                    <input
                      type="checkbox"
                      checked={details.countries.includes(country)}
                      onChange={(event) => patch({ countries: event.target.checked ? [...details.countries, country] : details.countries.filter((value) => value !== country) })}
                    /> {country}
                  </label>
                ))}
              </div>
            </fieldset>
          )}

          {mutation.isError && <p className="form-error">{mutation.error.message}</p>}
          <div className="work-item-detail-save-row">
            <button className="primary-button" disabled={mutation.isPending}>{mutation.isPending ? 'Saving…' : 'Save changes'}</button>
          </div>

          <WorkItemAttachments workItemId={item.id} members={members} />
          {project && (
            <WorkItemSubtasks
              parent={item}
              workItems={workItems}
              project={project}
              profile={profile}
              members={members}
              types={types}
              priorities={priorities}
              onOpenWorkItem={onOpenWorkItem}
              onStatusChange={onStatusChange}
            />
          )}
          <WorkItemLinkedItems workItemId={item.id} workItems={workItems} />
          <WorkItemComments workItemId={item.id} profile={profile} members={members} />
        </div>

        <aside className="work-item-detail-sidebar">
          <label className="work-item-detail-status">
            <span className="sr-only">Status</span>
            <select value={item.status} onChange={(event) => onStatusChange(item, event.target.value as WorkItemStatus)}>
              {allStatuses.map((status) => (
                <option key={status} value={status}>{statusMeta[status].label}</option>
              ))}
            </select>
            <ChevronDown size={14} aria-hidden="true" />
          </label>

          <div className="work-item-detail-panel">
            <h3>Details</h3>

            <Field variant="panel" label="Assignee">
              <SearchableSelect
                size="xl"
                value={details.assigneeUserId ?? ''}
                onChange={(val) => patch({ assigneeUserId: val || null })}
                options={[
                  { value: '', label: 'Unassigned' },
                  ...members.filter((member) => member.userId).map((member) => ({
                    value: member.userId ?? '',
                    label: `${member.displayName ?? 'Unnamed member'}${profile && member.userId === profile.userId ? ' (me)' : ''}`,
                  })),
                ]}
                placeholder="Unassigned"
                searchPlaceholder="Search members…"
              />
            </Field>

            <Field variant="panel" label="Priority">
              <SearchableSelect
                size="xl"
                value={priority}
                onChange={(val) => setPriority(val as Priority)}
                options={priorities.map((value) => ({ value, label: value }))}
                searchPlaceholder="Search priority…"
              />
            </Field>

            {type !== 'Initiative' && (
              <Field variant="panel" label="Parent">
                <SearchableSelect
                  size="xl"
                  value={details.parentId ?? ''}
                  onChange={(val) => patch({ parentId: val || null })}
                  options={[
                    { value: '', label: 'No parent' },
                    ...parentOptions.map((candidate) => ({ value: candidate.id, label: `${candidate.key} — ${candidate.summary}`, badge: candidate.type })),
                  ]}
                  placeholder="No parent"
                  searchPlaceholder="Search parent work items…"
                />
              </Field>
            )}

            {(type === 'Task' || type === 'Story' || type === 'Bug') && (
              <Field variant="panel" label="Story points">
                <input type="number" min="0" max="10000" step="0.5" value={details.storyPoints ?? ''} onChange={(event) => patch({ storyPoints: event.target.value ? Number(event.target.value) : null })} />
              </Field>
            )}

            {type === 'Bug' && (
              <>
                <Field variant="panel" label="Developer">
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
                <Field variant="panel" label="Sprint"><input value={details.sprintName ?? ''} onChange={(event) => patch({ sprintName: event.target.value || null })} maxLength={255} placeholder="Sprint name" /></Field>
                <Field variant="panel" label="Identified on"><input value={details.identifiedOn ?? ''} onChange={(event) => patch({ identifiedOn: event.target.value || null })} maxLength={255} placeholder="Production, staging, device…" /></Field>
              </>
            )}

            {type === 'Spike' && (
              <Field variant="panel" label="Product owner">
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

            <Field variant="panel" label="Labels"><input value={labelsText} onChange={(event) => setLabelsText(event.target.value)} placeholder="frontend, customer-impact" /></Field>

            <div className="work-item-detail-meta">
              <div>
                <span>Reporter</span>
                <span className="wid-reporter">
                  <span className="wid-reporter-avatar">{(membersById.get(profile?.userId ?? '')?.displayName ?? profile?.displayName ?? '?').charAt(0).toUpperCase()}</span>
                  {membersById.get(profile?.userId ?? '')?.displayName ?? profile?.displayName ?? 'Unknown'}
                </span>
              </div>
              <div><span>Created</span><span>{new Date(item.createdAt).toLocaleDateString()}</span></div>
              <div><span>Updated</span><span>{new Date(item.updatedAt).toLocaleDateString()}</span></div>
            </div>
            <Hint variant="panel">Changes to details are saved with the Save changes button above.</Hint>
          </div>
        </aside>
      </form>
    </div>
  )
}
