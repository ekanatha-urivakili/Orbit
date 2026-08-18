import { useState, useRef, useEffect, type FormEvent } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ChevronLeft,
  ChevronDown,
  Check,
  Table,
  Plus,
  Settings,
  Edit,
} from 'lucide-react'
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
import type {
  Priority,
  Profile,
  Project,
  TenantMembership,
  WorkItem,
  WorkItemStatus,
  WorkItemType,
  Sprint,
} from '../../api/types'

const countries = [
  'Global',
  'Argentina',
  'Brasil',
  'Nigeria',
  'South Africa',
  'US',
  'Saudi Arabia',
  'Turkey',
]

const availableWorkTypes: Array<{ type: WorkItemType; label: string }> = [
  { type: 'Story', label: 'Story' },
  { type: 'Task', label: 'Task' },
  { type: 'Initiative', label: 'Feature' },
  { type: 'Spike', label: 'Request' },
  { type: 'Bug', label: 'Bug' },
]

const default5x3TableMarkdown = `| Scenario | Given | When | Then | Expected Result |
| --- | --- | --- | --- | --- |
| 1. User Authentication | Valid credentials provided | User submits login | Token issued | Dashboard opens |
| 2. Input Validation | Required field missing | User saves changes | Form validates | Warning displayed |
| 3. State Persistence | Item fields modified | User clicks Save changes | Mutation completes | Saved successfully badge shown |`

export function WorkItemDetailView({
  item,
  project,
  workItems,
  profile,
  members,
  priorities,
  onBack,
  onStatusChange,
  onOpenWorkItem,
  sprints = [],
}: {
  item: WorkItem
  project?: Project
  workItems: WorkItem[]
  profile?: Profile
  members: TenantMembership[]
  priorities: Priority[]
  onBack: () => void
  onStatusChange: (workItem: WorkItem, status: WorkItemStatus) => void
  onOpenWorkItem: (workItem: WorkItem) => void
  sprints?: Sprint[]
}) {
  const queryClient = useQueryClient()
  const [currentType, setCurrentType] = useState<WorkItemType>(item.type)
  const [typeMenuOpen, setTypeMenuOpen] = useState(false)
  const [summary, setSummary] = useState(item.summary)
  const [editingSummary, setEditingSummary] = useState(false)
  const [description, setDescription] = useState(item.description ?? '')
  const [priority, setPriority] = useState<Priority>(item.priority)
  const [details, setDetails] = useState<{
    parentId: string | null
    epicName: string | null
    acceptanceCriteria: string | null
    stepsToConduct: string | null
    assigneeUserId: string | null
    developerUserId: string | null
    productOwnerUserId: string | null
    sprintName: string | null
    identifiedOn: string | null
    storyPoints: number | null
    countries: string[]
    attachmentNames: string[]
  }>({
    parentId: item.parentId,
    epicName: item.epicName,
    acceptanceCriteria: item.acceptanceCriteria ?? default5x3TableMarkdown,
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
  const [selectedSprintId, setSelectedSprintId] = useState(() => {
    const s = sprints.find((sp) => sp.name === item.sprintName)
    return s?.id ?? ''
  })
  const [newSprintName, setNewSprintName] = useState('')
  const [saveSuccess, setSaveSuccess] = useState(false)
  const successTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    return () => {
      if (successTimerRef.current) clearTimeout(successTimerRef.current)
    }
  }, [])

  const patch = (change: Partial<typeof details>) => {
    setSaveSuccess(false)
    setDetails((current) => ({ ...current, ...change }))
  }
  const mutation = useUpdateWorkItem(item.projectId)

  const attachmentsQuery = useQuery({
    queryKey: ['work-item-attachments', item.id],
    queryFn: () => orbitApi.listWorkItemAttachments(item.id),
  })
  const attachments = attachmentsQuery.data ?? []

  const handleChangeType = (newType: WorkItemType) => {
    setCurrentType(newType)
    setTypeMenuOpen(false)
    setSaveSuccess(false)

    mutation.mutate(
      {
        workItem: item,
        input: {
          summary,
          description: description || null,
          priority,
          ...details,
          labels: labelsText
            .split(',')
            .map((label) => label.trim())
            .filter(Boolean),
        },
      },
      {
        onSuccess: () => {
          queryClient.invalidateQueries({ queryKey: ['work-items', item.projectId] })
          setSaveSuccess(true)
          if (successTimerRef.current) clearTimeout(successTimerRef.current)
          successTimerRef.current = setTimeout(() => {
            setSaveSuccess(false)
          }, 4000)
        },
      }
    )
  }

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSaveSuccess(false)

    let targetSprintId = selectedSprintId
    let targetSprintName = details.sprintName

    if (selectedSprintId === '__new_sprint__') {
      const newSprint = await orbitApi.createSprint(item.projectId, newSprintName)
      targetSprintId = newSprint.id
      targetSprintName = newSprint.name
    } else if (selectedSprintId) {
      const s = sprints.find((sp) => sp.id === selectedSprintId)
      targetSprintName = s ? s.name : null
    } else {
      targetSprintName = null
    }

    mutation.mutate(
      {
        workItem: item,
        input: {
          summary,
          description: description || null,
          priority,
          ...details,
          sprintName: targetSprintName,
          labels: labelsText
            .split(',')
            .map((label) => label.trim())
            .filter(Boolean),
        },
      },
      {
        onSuccess: async () => {
          if (targetSprintId === '') {
            await orbitApi.removeWorkItemFromSprint(item.id)
          } else if (targetSprintId) {
            await orbitApi.assignWorkItemToSprint(item.id, targetSprintId)
          }
          queryClient.invalidateQueries({ queryKey: ['sprints', item.projectId] })
          queryClient.invalidateQueries({ queryKey: ['work-items', item.projectId] })
          setNewSprintName('')
          setSaveSuccess(true)
          if (successTimerRef.current) clearTimeout(successTimerRef.current)
          successTimerRef.current = setTimeout(() => {
            setSaveSuccess(false)
          }, 4000)
        },
      }
    )
  }

  const parentOptions = workItems.filter((candidate) => {
    if (candidate.id === item.id) return false
    if (currentType === 'Initiative') return false
    if (currentType === 'Epic') return candidate.type === 'Initiative'
    if (currentType === 'Subtask') return candidate.type !== 'Initiative'
    return candidate.type === 'Epic' || candidate.type === 'Initiative'
  })
  const membersById = new Map(members.map((member) => [member.userId, member]))

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
    <div className="work-item-detail">
      {/* Breadcrumb row */}
      <div className="work-item-detail-breadcrumb flex items-center gap-2">
        <button type="button" className="icon-button" onClick={onBack} aria-label="Back">
          <ChevronLeft size={18} />
        </button>
        <span className="text-gray-600 font-medium">Spaces</span>
        <span className="work-item-detail-breadcrumb-sep">/</span>
        <span>{project?.name ?? 'Space'}</span>
        <span className="work-item-detail-breadcrumb-sep">/</span>

        {/* Breadcrumb Interactive Type Icon */}
        <div className="relative inline-flex items-center">
          <button
            type="button"
            onClick={() => setTypeMenuOpen(!typeMenuOpen)}
            className="flex items-center gap-1.5 p-1 rounded hover:bg-gray-100 font-semibold text-gray-700 text-xs transition-colors"
            title={`${currentType} - Click to change work type`}
          >
            <WorkItemTypeIcon type={currentType} size={16} />
            <span>{item.key}</span>
            <ChevronDown size={12} className="text-gray-400" />
          </button>

          {/* Type switcher floating menu (Matching Screenshot 2) */}
          {typeMenuOpen && (
            <div className="absolute left-0 top-full mt-1.5 w-52 bg-white border border-[#dfe1e6] shadow-2xl rounded-xl py-2 z-50 animate-in fade-in">
              <div className="px-3.5 py-1 text-[11px] font-bold text-gray-500 uppercase tracking-wider">
                Change work type
              </div>
              <div className="my-1">
                {availableWorkTypes.map(({ type: t, label }) => (
                  <button
                    key={t}
                    type="button"
                    onClick={() => handleChangeType(t)}
                    className={`w-full text-left px-3.5 py-2 text-sm flex items-center gap-2.5 transition-colors ${
                      currentType === t
                        ? 'bg-[#deebff] text-[#0052cc] font-semibold'
                        : 'hover:bg-[#f4f5f7] text-[#172b4d]'
                    }`}
                  >
                    <WorkItemTypeIcon type={t} size={16} />
                    <span>{label}</span>
                    {currentType === t && <Check size={14} className="ml-auto text-[#0052cc]" />}
                  </button>
                ))}
              </div>
              <div className="my-1 border-t border-gray-100" />
              <button
                type="button"
                onClick={() => setTypeMenuOpen(false)}
                className="w-full text-left px-3.5 py-1.5 text-xs text-gray-600 hover:bg-[#f4f5f7] flex items-center gap-2"
              >
                <Plus size={13} className="text-gray-400" /> Add work type
              </button>
              <button
                type="button"
                onClick={() => setTypeMenuOpen(false)}
                className="w-full text-left px-3.5 py-1.5 text-xs text-gray-600 hover:bg-[#f4f5f7] flex items-center gap-2"
              >
                <Edit size={13} className="text-gray-400" /> Edit work type
              </button>
              <button
                type="button"
                onClick={() => setTypeMenuOpen(false)}
                className="w-full text-left px-3.5 py-1.5 text-xs text-gray-600 hover:bg-[#f4f5f7] flex items-center gap-2"
              >
                <Settings size={13} className="text-gray-400" /> Manage work types
              </button>
            </div>
          )}
        </div>
      </div>

      <form className="work-item-detail-grid" onSubmit={submit}>
        <div className="work-item-detail-main">
          {/* Title Row with Type Switcher */}
          <div className="work-item-detail-title-row relative flex items-center gap-2">
            <button
              type="button"
              onClick={() => setTypeMenuOpen(!typeMenuOpen)}
              className="p-1 rounded hover:bg-gray-100 transition-colors"
              title="Click to change work type"
            >
              <WorkItemTypeIcon type={currentType} size={22} />
            </button>
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
            <h1 className="work-item-detail-title" onClick={() => setEditingSummary(true)}>
              {summary}
            </h1>
          )}

          {currentType === 'Epic' && (
            <Field label="Epic name *">
              <input
                required
                maxLength={255}
                value={details.epicName ?? ''}
                onChange={(event) => patch({ epicName: event.target.value || null })}
              />
            </Field>
          )}

          {/* Description Section */}
          <section className="work-item-detail-section">
            <h2>Description</h2>
            <RichTextEditor
              value={description}
              onChange={setDescription}
              placeholder="Describe the outcome, context, and expected behaviour."
              workItemId={item.id}
              attachments={attachments}
              onAttachmentUploaded={() =>
                queryClient.invalidateQueries({
                  queryKey: ['work-item-attachments', item.id],
                })
              }
            />
          </section>

          {/* Acceptance Criteria Section with 5 Columns x 3 Rows Table */}
          <section className="work-item-detail-section">
            <div className="flex items-center justify-between mb-2">
              <h2>Acceptance criteria</h2>
              <button
                type="button"
                onClick={() =>
                  patch({
                    acceptanceCriteria: default5x3TableMarkdown,
                  })
                }
                className="text-xs font-semibold text-[#0052cc] hover:underline flex items-center gap-1"
                title="Insert 5x3 standard acceptance criteria table"
              >
                <Table size={13} /> Insert 5x3 Table
              </button>
            </div>
            <textarea
              value={details.acceptanceCriteria ?? ''}
              onChange={(event) =>
                patch({ acceptanceCriteria: event.target.value || null })
              }
              placeholder="Provide scenario-based criteria or table..."
              maxLength={32000}
              rows={6}
              className="font-mono text-[13px] leading-relaxed"
            />
          </section>

          {currentType === 'Bug' && (
            <section className="work-item-detail-section">
              <h2>Steps to conduct action</h2>
              <textarea
                value={details.stepsToConduct ?? ''}
                onChange={(event) =>
                  patch({ stepsToConduct: event.target.value || null })
                }
                maxLength={32000}
                rows={4}
              />
            </section>
          )}

          {currentType === 'Bug' && (
            <fieldset className="rounded-lg border border-gray-200 p-3 mt-4">
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
          )}

          {mutation.isError && <p className="form-error">{mutation.error.message}</p>}
          <div className="work-item-detail-save-row">
            {saveSuccess && (
              <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm font-semibold animate-in fade-in duration-200">
                <Check size={16} className="text-green-600 shrink-0" />
                Saved successfully!
              </span>
            )}
            <button
              type="submit"
              className="primary-button"
              disabled={mutation.isPending}
            >
              {mutation.isPending ? 'Saving…' : 'Save changes'}
            </button>
          </div>

          <WorkItemAttachments workItemId={item.id} members={members} />
          {project && (
            <WorkItemSubtasks
              parent={item}
              workItems={workItems}
              project={project}
              members={members}
              onOpenWorkItem={onOpenWorkItem}
              onStatusChange={onStatusChange}
            />
          )}
          <WorkItemLinkedItems workItemId={item.id} workItems={workItems} />
          <WorkItemComments workItemId={item.id} profile={profile} members={members} />
        </div>

        {/* Sidebar Details Panel (Pure White background) */}
        <aside className="work-item-detail-sidebar">
          <label className="work-item-detail-status">
            <span className="sr-only">Status</span>
            <select
              value={item.status}
              onChange={(event) =>
                onStatusChange(item, event.target.value as WorkItemStatus)
              }
            >
              {allStatuses.map((status) => (
                <option key={status} value={status}>
                  {statusMeta[status].label}
                </option>
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
                  ...members
                    .filter((member) => member.userId)
                    .map((member) => ({
                      value: member.userId ?? '',
                      label: `${member.displayName ?? 'Unnamed member'}${
                        profile && member.userId === profile.userId ? ' (me)' : ''
                      }`,
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

            {currentType !== 'Initiative' && (
              <Field variant="panel" label="Parent">
                <SearchableSelect
                  size="xl"
                  value={details.parentId ?? ''}
                  onChange={(val) => patch({ parentId: val || null })}
                  options={[
                    { value: '', label: 'No parent' },
                    ...parentOptions.map((candidate) => ({
                      value: candidate.id,
                      label: `${candidate.key} — ${candidate.summary}`,
                      badge: candidate.type,
                    })),
                  ]}
                  placeholder="No parent"
                  searchPlaceholder="Search parent work items…"
                />
              </Field>
            )}

            {(currentType === 'Task' || currentType === 'Story' || currentType === 'Bug') && (
              <Field variant="panel" label="Story points">
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
            )}

            {currentType === 'Bug' && (
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
                <Field variant="panel" label="Sprint">
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
                <Field variant="panel" label="Identified on">
                  <input
                    value={details.identifiedOn ?? ''}
                    onChange={(event) => patch({ identifiedOn: event.target.value || null })}
                    maxLength={255}
                    placeholder="Production, staging, device…"
                  />
                </Field>
              </>
            )}

            {currentType === 'Spike' && (
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

            <Field variant="panel" label="Labels">
              <input
                value={labelsText}
                onChange={(event) => setLabelsText(event.target.value)}
                placeholder="frontend, customer-impact"
              />
            </Field>

            <div className="work-item-detail-meta">
              <div>
                <span>Reporter</span>
                <span className="wid-reporter">
                  <span className="wid-reporter-avatar">
                    {(
                      membersById.get(profile?.userId ?? '')?.displayName ??
                      profile?.displayName ??
                      '?'
                    )
                      .charAt(0)
                      .toUpperCase()}
                  </span>
                  {membersById.get(profile?.userId ?? '')?.displayName ??
                    profile?.displayName ??
                    'Unknown'}
                </span>
              </div>
              <div>
                <span>Created</span>
                <span>{new Date(item.createdAt).toLocaleDateString()}</span>
              </div>
              <div>
                <span>Updated</span>
                <span>{new Date(item.updatedAt).toLocaleDateString()}</span>
              </div>
            </div>
            <Hint variant="panel">Changes to details are saved with the Save changes button above.</Hint>
          </div>
        </aside>
      </form>
    </div>
  )
}
