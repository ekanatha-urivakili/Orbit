import { useState, useRef, useEffect, type FormEvent } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  ChevronLeft,
  ChevronDown,
  Check,
  Settings,
  Edit,
  Eye,
  EyeOff,
  Link as LinkIcon,
  Share2,
} from 'lucide-react'
import { orbitApi } from '../../api/client'
import { useUpdateWorkItem } from '../../hooks/useUpdateWorkItem'
import { Field, Hint } from '../../components/form/Field'
import { SearchableSelect } from '../../components/form/SearchableSelect'
import { LabelsInput } from '../../components/form/LabelsInput'
import { WorkItemComments } from './WorkItemComments'
import { WorkItemAttachments } from './WorkItemAttachments'
import { WorkItemSubtasks } from './WorkItemSubtasks'
import { WorkItemLinkedItems } from './WorkItemLinkedItems'
import { WorkItemWorklogSection } from './WorkItemWorklogSection'
import { WorkItemShareMenu } from './WorkItemShareMenu'
import { WorkItemActionsMenu } from './WorkItemActionsMenu'
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

// Types that cannot be converted to/from another type (see WorkItem.ChangeType in the backend).
const structuralTypes: WorkItemType[] = ['Initiative', 'Epic', 'Subtask']

// Empty initial value for rich text acceptance criteria
const emptyAcceptanceCriteria = ''

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
  onNavigateHome,
  onManageWorkTypes,
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
  onNavigateHome?: () => void
  onManageWorkTypes?: () => void
  sprints?: Sprint[]
}) {
  const queryClient = useQueryClient()
  const [currentType, setCurrentType] = useState<WorkItemType>(item.type)
  useEffect(() => {
    setCurrentType(item.type)
  }, [item.type])
  const [typeMenuOpen, setTypeMenuOpen] = useState(false)
  const [epicPopupOpen, setEpicPopupOpen] = useState(false)
  const [epicMenuOpen, setEpicMenuOpen] = useState(false)
  const [epicSearch, setEpicSearch] = useState('')
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
    acceptanceCriteria: item.acceptanceCriteria ?? emptyAcceptanceCriteria,
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
  const [labels, setLabels] = useState<string[]>(item.labels)
  const [selectedSprintId, setSelectedSprintId] = useState(() => {
    const s = sprints.find((sp) => sp.name === item.sprintName)
    return s?.id ?? ''
  })
  const [newSprintName, setNewSprintName] = useState('')
  const [saveSuccess, setSaveSuccess] = useState(false)
  const successTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const [linkCopied, setLinkCopied] = useState(false)
  const [shareOpen, setShareOpen] = useState(false)
  const linkCopiedTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    return () => {
      if (successTimerRef.current) clearTimeout(successTimerRef.current)
      if (linkCopiedTimerRef.current) clearTimeout(linkCopiedTimerRef.current)
    }
  }, [])

  const handleCopyLink = async () => {
    await navigator.clipboard.writeText(`${window.location.origin}/browse/${item.key}`)
    setLinkCopied(true)
    if (linkCopiedTimerRef.current) clearTimeout(linkCopiedTimerRef.current)
    linkCopiedTimerRef.current = setTimeout(() => setLinkCopied(false), 2000)
  }

  const patch = (change: Partial<typeof details>) => {
    setSaveSuccess(false)
    setDetails((current) => ({ ...current, ...change }))
  }
  const mutation = useUpdateWorkItem(item.projectId)

  const epics = workItems.filter((w) => w.type === 'Epic' && w.id !== item.id)
  const parentEpic = workItems.find((w) => w.id === details.parentId && w.type === 'Epic')
  const filteredEpics = epics.filter((e) =>
    e.summary.toLowerCase().includes(epicSearch.toLowerCase()) ||
    e.key.toLowerCase().includes(epicSearch.toLowerCase())
  )

  const handleSelectEpic = (epicId: string | null) => {
    const selectedEpic = epicId ? epics.find((e) => e.id === epicId) : null
    patch({
      parentId: epicId,
      epicName: selectedEpic ? selectedEpic.summary : null,
    })
  }

  const attachmentsQuery = useQuery({
    queryKey: ['work-item-attachments', item.id],
    queryFn: () => orbitApi.listWorkItemAttachments(item.id),
  })
  const attachments = attachmentsQuery.data ?? []

  const typeDefinitionsQuery = useQuery({
    queryKey: ['work-item-types'],
    queryFn: () => orbitApi.listWorkItemTypes(),
  })
  const availableWorkTypes = (typeDefinitionsQuery.data ?? [])
    .filter((definition) => definition.enabled && !structuralTypes.includes(definition.id))
    .sort((a, b) => a.order - b.order)
    .map((definition) => ({ type: definition.id, label: definition.label }))

  const changeTypeMutation = useMutation({
    mutationFn: (newType: WorkItemType) => orbitApi.changeWorkItemType(item, newType),
    onSuccess: (updated) => {
      setCurrentType(updated.type)
      queryClient.invalidateQueries({ queryKey: ['work-items', item.projectId] })
    },
  })

  const handleChangeType = (newType: WorkItemType) => {
    setTypeMenuOpen(false)
    if (newType === currentType) return
    changeTypeMutation.mutate(newType)
  }

  const watchersQuery = useQuery({
    queryKey: ['work-item-watchers', item.id],
    queryFn: () => orbitApi.getWorkItemWatchers(item.id),
  })
  const watchers = watchersQuery.data ?? { isWatching: false, count: 0 }

  const watchMutation = useMutation({
    mutationFn: () => (watchers.isWatching ? orbitApi.unwatchWorkItem(item.id) : orbitApi.watchWorkItem(item.id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['work-item-watchers', item.id] })
    },
  })

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
          labels,
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
      {/* Breadcrumb row (Clickable navigation & Add Epic popup like Jira) */}
      <div className="work-item-detail-breadcrumb flex items-center gap-2">
        <button type="button" className="icon-button" onClick={onBack} aria-label="Back">
          <ChevronLeft size={18} />
        </button>
        <button
          type="button"
          onClick={onNavigateHome ?? onBack}
          className="hover:underline text-gray-600 dark:text-gray-400 font-medium cursor-pointer"
        >
          Spaces
        </button>
        <span className="work-item-detail-breadcrumb-sep">/</span>
        <button
          type="button"
          onClick={onBack}
          className="hover:underline text-gray-800 dark:text-gray-200 font-semibold flex items-center gap-1.5 cursor-pointer"
        >
          <span className="flex h-4 w-4 items-center justify-center rounded bg-blue-600 text-[10px] font-bold text-white">
            {project?.key ? project.key.slice(0, 1) : 'P'}
          </span>
          {project?.name ?? 'Space'}
        </button>
        <span className="work-item-detail-breadcrumb-sep">/</span>

        {/* Breadcrumb Add Epic / Epic badge (Matching Jira Screenshot 3) */}
        {currentType !== 'Initiative' && currentType !== 'Epic' && (
          <>
            <div className="relative inline-flex items-center gap-0.5">
              {parentEpic ? (
                <>
                  <button
                    type="button"
                    onClick={() => setEpicMenuOpen(!epicMenuOpen)}
                    className="flex items-center gap-1 px-1 py-0.5 rounded hover:bg-gray-100 dark:hover:bg-[#2c333a] transition-colors"
                    title="Epic - Change epic"
                  >
                    <WorkItemTypeIcon type="Epic" size={13} />
                    <ChevronDown size={11} className="text-gray-400" />
                  </button>
                  <button
                    type="button"
                    onClick={() => onOpenWorkItem(parentEpic)}
                    className="truncate max-w-[140px] text-xs font-semibold text-purple-700 dark:text-purple-400 hover:underline"
                    title={`${parentEpic.key}: ${parentEpic.summary}`}
                  >
                    {parentEpic.summary}
                  </button>

                  {epicMenuOpen && (
                    <div className="absolute left-0 top-full mt-1.5 w-44 bg-white dark:bg-[#1d2125] border border-[#dfe1e6] dark:border-[#394047] shadow-2xl rounded-xl py-1.5 z-50 animate-in fade-in">
                      <button
                        type="button"
                        onClick={() => {
                          handleSelectEpic(null)
                          setEpicMenuOpen(false)
                        }}
                        className="w-full text-left px-3 py-1.5 text-xs text-gray-700 dark:text-gray-200 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a]"
                      >
                        Unlink parent
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          setEpicMenuOpen(false)
                          setEpicPopupOpen(true)
                        }}
                        className="w-full text-left px-3 py-1.5 text-xs text-gray-700 dark:text-gray-200 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a]"
                      >
                        View all epics
                      </button>
                    </div>
                  )}
                </>
              ) : (
                <button
                  type="button"
                  onClick={() => setEpicPopupOpen(!epicPopupOpen)}
                  className="flex items-center gap-1.5 px-2 py-0.5 rounded hover:bg-gray-100 dark:hover:bg-[#2c333a] text-xs font-semibold text-gray-700 dark:text-gray-200 transition-colors"
                  title="Add epic"
                >
                  <Edit size={12} className="text-gray-400" />
                  <span>Add epic</span>
                </button>
              )}

              {/* Epic Search / Selection floating popup ("View all epics") */}
              {epicPopupOpen && (
                <div className="absolute left-0 top-full mt-1.5 w-72 bg-white dark:bg-[#1d2125] border border-[#dfe1e6] dark:border-[#394047] shadow-2xl rounded-xl p-2.5 z-50 animate-in fade-in">
                  <div className="flex items-center justify-between mb-2">
                    <span className="text-[11px] font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                      Select Epic
                    </span>
                    {parentEpic && (
                      <button
                        type="button"
                        onClick={() => {
                          handleSelectEpic(null)
                          setEpicPopupOpen(false)
                        }}
                        className="text-[11px] text-red-600 hover:underline font-medium"
                      >
                        Remove epic
                      </button>
                    )}
                  </div>
                  <input
                    type="text"
                    autoFocus
                    placeholder="Search epics..."
                    value={epicSearch}
                    onChange={(e) => setEpicSearch(e.target.value)}
                    className="w-full text-xs border border-gray-300 dark:border-gray-600 rounded px-2.5 py-1.5 mb-2 focus:outline-none focus:border-blue-500 dark:bg-[#22272b] dark:text-white"
                  />
                  <div className="max-h-48 overflow-y-auto space-y-1">
                    {filteredEpics.slice(0, 5).map((epic) => (
                      <button
                        key={epic.id}
                        type="button"
                        onClick={() => {
                          handleSelectEpic(epic.id)
                          setEpicPopupOpen(false)
                        }}
                        className={`w-full text-left px-2 py-1.5 rounded text-xs flex items-center gap-2 hover:bg-purple-50 dark:hover:bg-purple-950/30 transition-colors ${
                          details.parentId === epic.id
                            ? 'bg-purple-100 dark:bg-purple-900/40 font-semibold text-purple-900 dark:text-purple-300'
                            : 'text-gray-800 dark:text-gray-200'
                        }`}
                      >
                        <WorkItemTypeIcon type="Epic" size={13} />
                        <span className="font-semibold text-gray-600 dark:text-gray-400">{epic.key}</span>
                        <span className="truncate flex-1">{epic.summary}</span>
                        {details.parentId === epic.id && <Check size={13} className="text-purple-600 ml-auto" />}
                      </button>
                    ))}
                    {filteredEpics.length === 0 && (
                      <p className="text-xs text-gray-400 text-center py-2">No epics found</p>
                    )}
                  </div>
                </div>
              )}
            </div>
            <span className="work-item-detail-breadcrumb-sep">/</span>
          </>
        )}

        {/* Breadcrumb Interactive Type Icon */}
        <div className="relative inline-flex items-center">
          <button
            type="button"
            onClick={() => setTypeMenuOpen(!typeMenuOpen)}
            className="flex items-center gap-1.5 p-1 rounded hover:bg-gray-100 dark:hover:bg-[#2c333a] font-semibold text-gray-700 dark:text-gray-200 text-xs transition-colors"
            title={`${currentType} - Click to change work type`}
          >
            <WorkItemTypeIcon type={currentType} size={16} />
            <span>{item.key}</span>
            <ChevronDown size={12} className="text-gray-400" />
          </button>
          <button
            type="button"
            onClick={handleCopyLink}
            className="relative flex items-center p-1 rounded hover:bg-gray-100 dark:hover:bg-[#2c333a] text-gray-400 transition-colors"
            title={`${window.location.origin}/browse/${item.key}`}
            aria-label="Copy link to this ticket"
          >
            {linkCopied ? <Check size={13} className="text-green-600" /> : <LinkIcon size={13} />}
            {linkCopied && (
              <span className="absolute left-1/2 top-full mt-1.5 -translate-x-1/2 whitespace-nowrap rounded bg-gray-900 px-2 py-1 text-[11px] font-medium text-white shadow-lg animate-in fade-in">
                Copied!
              </span>
            )}
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
              {onManageWorkTypes && (
                <>
                  <div className="my-1 border-t border-gray-100" />
                  <button
                    type="button"
                    onClick={() => {
                      setTypeMenuOpen(false)
                      onManageWorkTypes()
                    }}
                    className="w-full text-left px-3.5 py-1.5 text-xs text-gray-600 hover:bg-[#f4f5f7] flex items-center gap-2"
                  >
                    <Settings size={13} className="text-gray-400" /> Manage work types
                  </button>
                </>
              )}
            </div>
          )}
        </div>

        {/* Watcher action toggle */}
        <div className="ml-auto flex items-center gap-2">
          <button
            type="button"
            onClick={() => watchMutation.mutate()}
            disabled={watchMutation.isPending}
            className={`flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-medium border transition-colors ${
              watchers.isWatching
                ? 'bg-blue-50 text-blue-700 border-blue-200 hover:bg-blue-100'
                : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'
            }`}
            title={watchers.isWatching ? 'Stop watching this work item' : 'Watch this work item for updates'}
          >
            {watchers.isWatching ? <EyeOff size={13} /> : <Eye size={13} />}
            <span>{watchers.isWatching ? 'Watching' : 'Watch'}</span>
            <span className="ml-0.5 rounded-full bg-gray-100 px-1.5 py-0.5 text-[10px] font-bold text-gray-600">
              {watchers.count}
            </span>
          </button>

          <div className="relative inline-flex items-center">
            <button
              type="button"
              onClick={() => setShareOpen((open) => !open)}
              className="flex items-center justify-center p-2 rounded-md border border-gray-200 text-gray-600 hover:bg-gray-50 transition-colors"
              title="Share"
              aria-label="Share"
            >
              <Share2 size={16} />
            </button>
            {shareOpen && <WorkItemShareMenu item={item} onClose={() => setShareOpen(false)} />}
          </div>

          <WorkItemActionsMenu
            item={item}
            onOpenWorkItem={onOpenWorkItem}
            onFocusParentField={() => {
              const field = document.getElementById('work-item-parent-field')
              field?.scrollIntoView({ behavior: 'smooth', block: 'center' })
              field?.querySelector('input, button')?.dispatchEvent(new Event('focus'))
            }}
            onDeleted={onBack}
          />
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

          {/* Acceptance Criteria Section */}
          <section className="work-item-detail-section">
            <h2>Acceptance criteria</h2>
            <RichTextEditor
              value={details.acceptanceCriteria ?? ''}
              onChange={(html) => patch({ acceptanceCriteria: html || null })}
              placeholder="Define the acceptance criteria, scenarios and expected outcomes. Use the table button (⊞) in the toolbar to insert a table."
              workItemId={item.id}
              attachments={attachments}
              onAttachmentUploaded={() =>
                queryClient.invalidateQueries({
                  queryKey: ['work-item-attachments', item.id],
                })
              }
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
              onStatusChange={onStatusChange}
            />
          )}
          <WorkItemLinkedItems workItemId={item.id} workItems={workItems} />
          <WorkItemWorklogSection
            workItemId={item.id}
            members={members}
            currentMembershipId={members.find((member) => member.userId === profile?.userId)?.id}
          />
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
              <div id="work-item-parent-field">
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
              </div>
            )}

            {currentType !== 'Initiative' && currentType !== 'Epic' && (
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
              <LabelsInput value={labels} onChange={setLabels} />
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
