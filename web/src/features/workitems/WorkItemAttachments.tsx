import { useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Paperclip, Trash2, Download, Image as ImageIcon, File as FileIcon } from 'lucide-react'
import { orbitApi } from '../../api/client'
import type { TenantMembership } from '../../api/types'

function formatSize(sizeBytes: number): string {
  if (sizeBytes < 1024) return `${sizeBytes} B`
  if (sizeBytes < 1024 * 1024) return `${(sizeBytes / 1024).toFixed(1)} KB`
  return `${(sizeBytes / (1024 * 1024)).toFixed(1)} MB`
}

export function WorkItemAttachments({
  workItemId,
  members,
}: {
  workItemId: string
  members: TenantMembership[]
}) {
  const queryClient = useQueryClient()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [uploadError, setUploadError] = useState<string | null>(null)
  const [isUploading, setIsUploading] = useState(false)

  const attachmentsQuery = useQuery({
    queryKey: ['work-item-attachments', workItemId],
    queryFn: () => orbitApi.listWorkItemAttachments(workItemId),
  })
  const attachments = attachmentsQuery.data ?? []
  const membersById = new Map(members.map((member) => [member.id, member]))

  const deleteMutation = useMutation({
    mutationFn: (attachmentId: string) => orbitApi.deleteWorkItemAttachment(workItemId, attachmentId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['work-item-attachments', workItemId] }),
  })

  const handleFileSelected = async (file: File | undefined) => {
    if (!file) return
    setUploadError(null)
    setIsUploading(true)
    try {
      const presigned = await orbitApi.presignWorkItemAttachmentUpload(workItemId, file.name, file.type, file.size)
      await orbitApi.uploadAttachmentFile(presigned.uploadUrl, file)
      await orbitApi.confirmWorkItemAttachment(workItemId, {
        fileName: file.name,
        contentType: file.type,
        sizeBytes: file.size,
        objectKey: presigned.objectKey,
      })
      await queryClient.invalidateQueries({ queryKey: ['work-item-attachments', workItemId] })
    } catch (error) {
      setUploadError(error instanceof Error ? error.message : 'Upload failed.')
    } finally {
      setIsUploading(false)
      if (fileInputRef.current) fileInputRef.current.value = ''
    }
  }

  return (
    <div className="mt-8 border-t border-gray-200 pt-6">
      <h3 className="flex items-center gap-2 text-sm font-semibold text-gray-900 mb-4">
        <Paperclip size={16} className="text-gray-500" /> Attachments
      </h3>

      <div className="space-y-2 mb-4">
        {attachmentsQuery.isPending ? (
          <p className="text-sm text-gray-500">Loading attachments...</p>
        ) : attachments.length === 0 ? (
          <p className="text-sm text-gray-500">No attachments yet.</p>
        ) : (
          attachments.map((attachment) => (
            <div key={attachment.id} className="flex items-center gap-3 text-sm bg-gray-50 rounded-lg p-3">
              {attachment.contentType.startsWith('image/') ? (
                <ImageIcon size={18} className="text-gray-400 flex-shrink-0" />
              ) : (
                <FileIcon size={18} className="text-gray-400 flex-shrink-0" />
              )}
              <div className="flex-1 min-w-0">
                {attachment.downloadUrl ? (
                  <a href={attachment.downloadUrl} target="_blank" rel="noreferrer" className="font-medium text-blue-700 hover:underline truncate block">
                    {attachment.fileName}
                  </a>
                ) : (
                  <span className="font-medium text-gray-500 truncate block">{attachment.fileName} (scanning...)</span>
                )}
                <div className="text-xs text-gray-500">
                  {formatSize(attachment.sizeBytes)}
                  {' · '}
                  {membersById.get(attachment.uploadedByMembershipId)?.displayName ?? 'Unknown member'}
                  {' · '}
                  {new Date(attachment.uploadedAt).toLocaleDateString()}
                </div>
              </div>
              {attachment.downloadUrl && (
                <a href={attachment.downloadUrl} target="_blank" rel="noreferrer" className="p-1 text-gray-500 hover:text-gray-900" aria-label="Download">
                  <Download size={16} />
                </a>
              )}
              <button
                type="button"
                onClick={() => deleteMutation.mutate(attachment.id)}
                disabled={deleteMutation.isPending}
                className="p-1 text-red-500 hover:text-red-700 disabled:opacity-50"
                aria-label="Delete attachment"
              >
                <Trash2 size={16} />
              </button>
            </div>
          ))
        )}
      </div>

      <label className="inline-flex items-center gap-2 text-sm font-medium text-blue-700 hover:text-blue-800 cursor-pointer">
        <Paperclip size={14} />
        {isUploading ? 'Uploading…' : 'Attach a file'}
        <input
          ref={fileInputRef}
          type="file"
          className="hidden"
          disabled={isUploading}
          onChange={(event) => handleFileSelected(event.target.files?.[0])}
        />
      </label>
      {uploadError && <p className="text-red-600 text-xs mt-1">{uploadError}</p>}
    </div>
  )
}
