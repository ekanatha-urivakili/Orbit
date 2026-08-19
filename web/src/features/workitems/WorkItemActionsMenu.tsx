import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  MoreHorizontal,
  Clock,
  Command,
  Flag,
  ThumbsUp,
  ImageIcon,
  GitBranch,
  Copy,
  FolderInput,
  Archive,
  ArchiveRestore,
  Trash2,
  Printer,
  FileSpreadsheet,
  FileCode,
  FileJson,
  MessageSquareShare,
} from 'lucide-react'
import { orbitApi } from '../../api/client'
import type { WorkItem, WorkItemAttachment, WorkItemExportFormat, WorkItemVotes } from '../../api/types'

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

export function WorkItemActionsMenu({
  item,
  onOpenWorkItem,
  onFocusParentField,
  onDeleted,
}: {
  item: WorkItem
  onOpenWorkItem: (workItem: WorkItem) => void
  onFocusParentField: () => void
  onDeleted: () => void
}) {
  const queryClient = useQueryClient()
  const [menuOpen, setMenuOpen] = useState(false)
  const [logWorkOpen, setLogWorkOpen] = useState(false)
  const [coverPickerOpen, setCoverPickerOpen] = useState(false)
  const [movePickerOpen, setMovePickerOpen] = useState(false)
  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false)
  const [deleteError, setDeleteError] = useState<string | null>(null)

  const votesQuery = useQuery({
    queryKey: ['work-item-votes', item.id],
    queryFn: () => orbitApi.getWorkItemVotes(item.id),
    enabled: menuOpen,
  })
  const votes: WorkItemVotes = votesQuery.data ?? { hasVoted: false, count: 0 }

  const invalidateItem = () => {
    queryClient.invalidateQueries({ queryKey: ['work-items', item.projectId] })
  }

  const flagMutation = useMutation({
    mutationFn: () => orbitApi.toggleWorkItemFlag(item, !item.isFlagged),
    onSuccess: invalidateItem,
  })

  const voteMutation = useMutation({
    mutationFn: () => (votes.hasVoted ? orbitApi.removeWorkItemVote(item.id) : orbitApi.addWorkItemVote(item.id)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['work-item-votes', item.id] }),
  })

  const cloneMutation = useMutation({
    mutationFn: () => orbitApi.cloneWorkItem(item.id),
    onSuccess: (clone) => {
      invalidateItem()
      setMenuOpen(false)
      onOpenWorkItem(clone)
    },
  })

  const archiveMutation = useMutation({
    mutationFn: () => (item.isArchived ? orbitApi.unarchiveWorkItem(item) : orbitApi.archiveWorkItem(item)),
    onSuccess: () => {
      invalidateItem()
      setMenuOpen(false)
    },
  })

  const deleteMutation = useMutation({
    mutationFn: () => orbitApi.deleteWorkItem(item),
    onSuccess: () => {
      invalidateItem()
      setDeleteConfirmOpen(false)
      setMenuOpen(false)
      onDeleted()
    },
    onError: (error: Error) => setDeleteError(error.message),
  })

  const exportMutation = useMutation({
    mutationFn: (format: WorkItemExportFormat) => orbitApi.exportWorkItem(item, format),
    onSuccess: ({ blob, fileName }) => downloadBlob(blob, fileName),
  })

  const connectSlackMutation = useMutation({
    mutationFn: () => orbitApi.startSlackConnect(item.projectId),
    onSuccess: ({ url }) => {
      sessionStorage.setItem('slack-connect-return-path', window.location.pathname)
      window.location.href = url
    },
  })

  const closeAndRun = (fn: () => void) => {
    setMenuOpen(false)
    fn()
  }

  return (
    <div className="relative inline-flex items-center">
      <button
        type="button"
        onClick={() => setMenuOpen((open) => !open)}
        className="flex items-center justify-center p-2 rounded-md border border-gray-200 text-gray-600 hover:bg-gray-50 transition-colors"
        title="Actions"
        aria-label="Actions"
      >
        <MoreHorizontal size={16} />
      </button>

      {menuOpen && (
        <div className="absolute right-0 top-full mt-1.5 w-64 bg-white dark:bg-[#1d2125] border border-[#dfe1e6] dark:border-[#394047] shadow-2xl rounded-xl py-1.5 z-50 animate-in fade-in text-sm">
          <button
            type="button"
            className="w-full text-left px-3.5 py-2 flex items-center gap-2.5 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200"
            onClick={() => closeAndRun(() => setLogWorkOpen(true))}
          >
            <Clock size={15} className="text-gray-400" /> Log work
          </button>
          <button
            type="button"
            className="w-full text-left px-3.5 py-2 flex items-center gap-2.5 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200"
            onClick={() => closeAndRun(() => window.dispatchEvent(new CustomEvent('orbit:open-command-palette')))}
          >
            <Command size={15} className="text-gray-400" /> Open command palette
          </button>

          <div className="my-1 border-t border-gray-100 dark:border-[#394047]" />

          <button
            type="button"
            className="w-full text-left px-3.5 py-2 flex items-center gap-2.5 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200"
            onClick={() => flagMutation.mutate()}
            disabled={flagMutation.isPending}
          >
            <Flag size={15} className={item.isFlagged ? 'text-orange-500' : 'text-gray-400'} />
            {item.isFlagged ? 'Remove flag' : 'Add flag'}
          </button>
          <button
            type="button"
            className="w-full text-left px-3.5 py-2 flex items-center gap-2.5 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200"
            onClick={() => voteMutation.mutate()}
            disabled={voteMutation.isPending}
          >
            <ThumbsUp size={15} className={votes.hasVoted ? 'text-blue-600' : 'text-gray-400'} />
            {votes.hasVoted ? 'Remove vote' : 'Add vote'} ({votes.count})
          </button>

          <div className="my-1 border-t border-gray-100 dark:border-[#394047]" />

          <button
            type="button"
            className="w-full text-left px-3.5 py-2 flex items-center gap-2.5 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200"
            onClick={() => closeAndRun(() => setCoverPickerOpen(true))}
          >
            <ImageIcon size={15} className="text-gray-400" /> Select cover
          </button>
          <button
            type="button"
            className="w-full text-left px-3.5 py-2 flex items-center gap-2.5 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200"
            onClick={() => closeAndRun(onFocusParentField)}
          >
            <GitBranch size={15} className="text-gray-400" /> Change parent
          </button>
          <button
            type="button"
            className="w-full text-left px-3.5 py-2 flex items-center gap-2.5 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200 disabled:opacity-50"
            onClick={() => cloneMutation.mutate()}
            disabled={cloneMutation.isPending}
          >
            <Copy size={15} className="text-gray-400" /> {cloneMutation.isPending ? 'Cloning…' : 'Clone'}
          </button>
          <button
            type="button"
            className="w-full text-left px-3.5 py-2 flex items-center gap-2.5 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200"
            onClick={() => closeAndRun(() => setMovePickerOpen(true))}
          >
            <FolderInput size={15} className="text-gray-400" /> Move
          </button>
          <button
            type="button"
            className="w-full text-left px-3.5 py-2 flex items-center gap-2.5 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200 disabled:opacity-50"
            onClick={() => archiveMutation.mutate()}
            disabled={archiveMutation.isPending}
          >
            {item.isArchived ? (
              <ArchiveRestore size={15} className="text-gray-400" />
            ) : (
              <Archive size={15} className="text-gray-400" />
            )}
            {item.isArchived ? 'Unarchive' : 'Archive'}
          </button>
          <button
            type="button"
            className="w-full text-left px-3.5 py-2 flex items-center gap-2.5 hover:bg-red-50 dark:hover:bg-red-950/30 text-red-600"
            onClick={() => closeAndRun(() => setDeleteConfirmOpen(true))}
          >
            <Trash2 size={15} /> Delete
          </button>

          <div className="my-1 border-t border-gray-100 dark:border-[#394047]" />

          <button
            type="button"
            className="w-full text-left px-3.5 py-2 flex items-center gap-2.5 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200"
            onClick={() => closeAndRun(() => window.print())}
          >
            <Printer size={15} className="text-gray-400" /> Print
          </button>
          <button
            type="button"
            className="w-full text-left px-3.5 py-2 flex items-center gap-2.5 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200 disabled:opacity-50"
            onClick={() => closeAndRun(() => exportMutation.mutate('Csv'))}
          >
            <FileSpreadsheet size={15} className="text-gray-400" /> Export Excel (CSV)
          </button>
          <button
            type="button"
            className="w-full text-left px-3.5 py-2 flex items-center gap-2.5 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200 disabled:opacity-50"
            onClick={() => closeAndRun(() => exportMutation.mutate('Xml'))}
          >
            <FileCode size={15} className="text-gray-400" /> Export XML
          </button>
          <button
            type="button"
            className="w-full text-left px-3.5 py-2 flex items-center gap-2.5 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200 disabled:opacity-50"
            onClick={() => closeAndRun(() => exportMutation.mutate('Json'))}
          >
            <FileJson size={15} className="text-gray-400" /> Export JSON
          </button>

          <div className="my-1 border-t border-gray-100 dark:border-[#394047]" />

          <button
            type="button"
            className="w-full text-left px-3.5 py-2 flex items-center gap-2.5 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200 disabled:opacity-50"
            onClick={() => connectSlackMutation.mutate()}
            disabled={connectSlackMutation.isPending}
          >
            <MessageSquareShare size={15} className="text-gray-400" />
            {connectSlackMutation.isPending ? 'Connecting…' : 'Connect Slack channel'}
          </button>
        </div>
      )}

      {logWorkOpen && <LogWorkDialog workItemId={item.id} onClose={() => setLogWorkOpen(false)} />}
      {coverPickerOpen && (
        <CoverPickerDialog item={item} onClose={() => setCoverPickerOpen(false)} onSaved={invalidateItem} />
      )}
      {movePickerOpen && (
        <MoveDialog
          item={item}
          onClose={() => setMovePickerOpen(false)}
          onMoved={(moved) => {
            invalidateItem()
            setMovePickerOpen(false)
            onOpenWorkItem(moved)
          }}
        />
      )}
      {deleteConfirmOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40" role="dialog">
          <div className="w-full max-w-sm rounded-xl bg-white dark:bg-[#1d2125] p-5 shadow-2xl">
            <h3 className="text-sm font-bold text-[#172b4d] dark:text-gray-100 mb-2">Delete {item.key}?</h3>
            <p className="text-xs text-gray-500 dark:text-gray-400 mb-4">
              This permanently deletes the work item. It cannot be undone.
            </p>
            {deleteError && <p className="text-xs text-red-600 mb-3">{deleteError}</p>}
            <div className="flex justify-end gap-2">
              <button
                type="button"
                className="px-3 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-100 rounded"
                onClick={() => setDeleteConfirmOpen(false)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="px-3 py-1.5 text-xs font-semibold text-white bg-red-600 hover:bg-red-700 rounded disabled:opacity-50"
                onClick={() => deleteMutation.mutate()}
                disabled={deleteMutation.isPending}
              >
                {deleteMutation.isPending ? 'Deleting…' : 'Delete'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

function LogWorkDialog({ workItemId, onClose }: { workItemId: string; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [hours, setHours] = useState('')
  const [minutes, setMinutes] = useState('')
  const [workDate, setWorkDate] = useState(() => new Date().toISOString().slice(0, 10))
  const [description, setDescription] = useState('')

  const mutation = useMutation({
    mutationFn: () =>
      orbitApi.addWorklog(workItemId, {
        minutesSpent: (Number(hours) || 0) * 60 + (Number(minutes) || 0),
        workDate,
        description: description.trim() || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['work-item-worklogs', workItemId] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40" role="dialog">
      <form
        className="w-full max-w-sm rounded-xl bg-white dark:bg-[#1d2125] p-5 shadow-2xl space-y-3"
        onSubmit={(event) => {
          event.preventDefault()
          mutation.mutate()
        }}
      >
        <h3 className="text-sm font-bold text-[#172b4d] dark:text-gray-100">Log work</h3>
        <div className="flex gap-2">
          <label className="flex-1 text-xs text-gray-600 dark:text-gray-300">
            Hours
            <input
              type="number"
              min="0"
              max="24"
              value={hours}
              onChange={(event) => setHours(event.target.value)}
              className="mt-1 w-full border border-gray-300 dark:border-gray-600 rounded px-2 py-1.5 text-sm bg-white dark:bg-[#22272b] dark:text-white"
            />
          </label>
          <label className="flex-1 text-xs text-gray-600 dark:text-gray-300">
            Minutes
            <input
              type="number"
              min="0"
              max="59"
              value={minutes}
              onChange={(event) => setMinutes(event.target.value)}
              className="mt-1 w-full border border-gray-300 dark:border-gray-600 rounded px-2 py-1.5 text-sm bg-white dark:bg-[#22272b] dark:text-white"
            />
          </label>
        </div>
        <label className="block text-xs text-gray-600 dark:text-gray-300">
          Date
          <input
            type="date"
            required
            value={workDate}
            onChange={(event) => setWorkDate(event.target.value)}
            className="mt-1 w-full border border-gray-300 dark:border-gray-600 rounded px-2 py-1.5 text-sm bg-white dark:bg-[#22272b] dark:text-white"
          />
        </label>
        <label className="block text-xs text-gray-600 dark:text-gray-300">
          Description (optional)
          <textarea
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            rows={2}
            className="mt-1 w-full border border-gray-300 dark:border-gray-600 rounded px-2 py-1.5 text-sm bg-white dark:bg-[#22272b] dark:text-white"
          />
        </label>
        {mutation.isError && <p className="text-xs text-red-600">{mutation.error.message}</p>}
        <div className="flex justify-end gap-2 pt-1">
          <button type="button" onClick={onClose} className="px-3 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-100 rounded">
            Cancel
          </button>
          <button
            type="submit"
            disabled={mutation.isPending || ((Number(hours) || 0) * 60 + (Number(minutes) || 0)) < 1}
            className="px-3 py-1.5 text-xs font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded disabled:opacity-50"
          >
            {mutation.isPending ? 'Saving…' : 'Save'}
          </button>
        </div>
      </form>
    </div>
  )
}

function CoverPickerDialog({
  item,
  onClose,
  onSaved,
}: {
  item: WorkItem
  onClose: () => void
  onSaved: () => void
}) {
  const attachmentsQuery = useQuery({
    queryKey: ['work-item-attachments', item.id],
    queryFn: () => orbitApi.listWorkItemAttachments(item.id),
  })
  const images = (attachmentsQuery.data ?? []).filter((attachment: WorkItemAttachment) =>
    attachment.contentType.startsWith('image/'),
  )

  const mutation = useMutation({
    mutationFn: (attachmentId: string | null) => orbitApi.setWorkItemCover(item, attachmentId),
    onSuccess: () => {
      onSaved()
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40" role="dialog">
      <div className="w-full max-w-sm rounded-xl bg-white dark:bg-[#1d2125] p-5 shadow-2xl">
        <h3 className="text-sm font-bold text-[#172b4d] dark:text-gray-100 mb-3">Select cover</h3>
        {images.length === 0 ? (
          <p className="text-xs text-gray-500 dark:text-gray-400 mb-4">
            Upload an image attachment first, then it can be used as a cover.
          </p>
        ) : (
          <div className="grid grid-cols-3 gap-2 mb-4 max-h-48 overflow-y-auto">
            {images.map((image) => (
              <button
                key={image.id}
                type="button"
                onClick={() => mutation.mutate(image.id)}
                className={`aspect-video rounded border text-[10px] p-1 truncate ${
                  item.coverAttachmentId === image.id ? 'border-blue-500 ring-2 ring-blue-200' : 'border-gray-200'
                }`}
                title={image.fileName}
              >
                {image.fileName}
              </button>
            ))}
          </div>
        )}
        {mutation.isError && <p className="text-xs text-red-600 mb-2">{mutation.error.message}</p>}
        <div className="flex justify-between gap-2">
          {item.coverAttachmentId && (
            <button
              type="button"
              onClick={() => mutation.mutate(null)}
              className="px-3 py-1.5 text-xs font-medium text-red-600 hover:bg-red-50 rounded"
            >
              Remove cover
            </button>
          )}
          <button type="button" onClick={onClose} className="ml-auto px-3 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-100 rounded">
            Close
          </button>
        </div>
      </div>
    </div>
  )
}

function MoveDialog({
  item,
  onClose,
  onMoved,
}: {
  item: WorkItem
  onClose: () => void
  onMoved: (moved: WorkItem) => void
}) {
  const [targetProjectId, setTargetProjectId] = useState('')
  const projectsQuery = useQuery({
    queryKey: ['projects-for-move'],
    queryFn: () => orbitApi.listProjects(),
  })
  const projects = (projectsQuery.data?.items ?? []).filter((project) => project.id !== item.projectId)

  const mutation = useMutation({
    mutationFn: () => orbitApi.moveWorkItem(item, targetProjectId),
    onSuccess: (moved) => onMoved(moved),
  })

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40" role="dialog">
      <div className="w-full max-w-sm rounded-xl bg-white dark:bg-[#1d2125] p-5 shadow-2xl space-y-3">
        <h3 className="text-sm font-bold text-[#172b4d] dark:text-gray-100">Move {item.key}</h3>
        <select
          value={targetProjectId}
          onChange={(event) => setTargetProjectId(event.target.value)}
          className="w-full border border-gray-300 dark:border-gray-600 rounded px-2 py-1.5 text-sm bg-white dark:bg-[#22272b] dark:text-white"
        >
          <option value="">Select a project…</option>
          {projects.map((project) => (
            <option key={project.id} value={project.id}>
              {project.key} — {project.name}
            </option>
          ))}
        </select>
        {mutation.isError && <p className="text-xs text-red-600">{mutation.error.message}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="px-3 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-100 rounded">
            Cancel
          </button>
          <button
            type="button"
            disabled={!targetProjectId || mutation.isPending}
            onClick={() => mutation.mutate()}
            className="px-3 py-1.5 text-xs font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded disabled:opacity-50"
          >
            {mutation.isPending ? 'Moving…' : 'Move'}
          </button>
        </div>
      </div>
    </div>
  )
}
