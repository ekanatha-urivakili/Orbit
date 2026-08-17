import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Trash2, Edit2, CornerDownRight } from 'lucide-react'
import { orbitApi } from '../../api/client'
import { RichTextEditor, isRichTextEmpty } from '../../components/form/RichTextEditor'
import { RichTextView } from '../../components/form/RichTextView'
import type { Profile, TenantMembership } from '../../api/types'

type ActivityTab = 'all' | 'comments' | 'history' | 'log'

const commentSuggestions = [
  '🎉 Looks good!',
  '👋 Need help?',
  '🚫 This is blocked…',
  '🔍 Can you clarify…?',
  '✅ This is on track',
]

export function WorkItemComments({
  workItemId,
  profile,
  members = [],
}: {
  workItemId: string
  profile?: Profile
  members?: TenantMembership[]
}) {
  const queryClient = useQueryClient()
  const [activeTab, setActiveTab] = useState<ActivityTab>('comments')
  const [newCommentBody, setNewCommentBody] = useState('')
  const [editingCommentId, setEditingCommentId] = useState<string | null>(null)
  const [editBody, setEditBody] = useState('')

  const currentMember = members.find((m) => m.userId === profile?.userId)

  const commentsQuery = useQuery({
    queryKey: ['work-item-comments', workItemId],
    queryFn: () => orbitApi.listWorkItemComments(workItemId),
  })
  const attachmentsQuery = useQuery({
    queryKey: ['work-item-attachments', workItemId],
    queryFn: () => orbitApi.listWorkItemAttachments(workItemId),
  })

  const comments = commentsQuery.data ?? []
  const attachments = attachmentsQuery.data ?? []

  const addMutation = useMutation({
    mutationFn: (body: string) => orbitApi.addWorkItemComment(workItemId, body),
    onSuccess: () => {
      setNewCommentBody('')
      queryClient.invalidateQueries({ queryKey: ['work-item-comments', workItemId] })
    },
  })

  const editMutation = useMutation({
    mutationFn: ({ commentId, body, version }: { commentId: string; body: string; version: number }) =>
      orbitApi.editWorkItemComment(workItemId, commentId, body, version),
    onSuccess: () => {
      setEditingCommentId(null)
      queryClient.invalidateQueries({ queryKey: ['work-item-comments', workItemId] })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: ({ commentId, version }: { commentId: string; version: number }) =>
      orbitApi.deleteWorkItemComment(workItemId, commentId, version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['work-item-comments', workItemId] })
    },
  })

  const isAuthor = (comment: { authorMembershipId: string; authorDisplayName?: string }) => {
    if (currentMember) {
      return comment.authorMembershipId === currentMember.id
    }
    return Boolean(profile && comment.authorDisplayName === profile.displayName)
  }

  const tabs: { id: ActivityTab; label: string }[] = [
    { id: 'all', label: 'All' },
    { id: 'comments', label: 'Comments' },
    { id: 'history', label: 'History' },
    { id: 'log', label: 'Log' },
  ]

  const showComments = activeTab === 'comments' || activeTab === 'all'
  const showHistory = activeTab === 'history' || activeTab === 'all'
  const showLog = activeTab === 'log' || activeTab === 'all'

  return (
    <div className="mt-8 border-t border-gray-200 pt-5">
      {/* Activity header + tabs */}
      <div className="activity-tabs-header">
        <span className="activity-tabs-title">Activity</span>
        <nav className="activity-tabs">
          {tabs.map((tab) => (
            <button
              key={tab.id}
              type="button"
              onClick={() => setActiveTab(tab.id)}
              className={`activity-tab${activeTab === tab.id ? ' activity-tab--active' : ''}`}
            >
              {tab.label}
            </button>
          ))}
        </nav>
      </div>

      {/* History section */}
      {showHistory && (
        <div className="activity-section">
          {activeTab === 'all' && <p className="activity-section-label">History</p>}
          <p className="activity-empty-text">No history recorded yet.</p>
        </div>
      )}

      {/* Log section */}
      {showLog && (
        <div className="activity-section">
          {activeTab === 'all' && <p className="activity-section-label">Log</p>}
          <p className="activity-empty-text">No work log entries yet.</p>
        </div>
      )}

      {/* Comments section */}
      {showComments && (
        <div className="activity-section">
          {activeTab === 'all' && comments.length > 0 && <p className="activity-section-label">Comments</p>}

          <div className="space-y-4 mb-6">
            {commentsQuery.isPending ? (
              <p className="text-sm text-gray-500">Loading comments...</p>
            ) : comments.length === 0 ? (
              <p className="activity-empty-text">No comments yet. Be the first to start the conversation.</p>
            ) : (
              comments.map((comment) => (
                <div key={comment.id} className={`flex gap-3 text-sm ${comment.isDeleted ? 'opacity-50' : ''}`}>
                  <div className="flex-shrink-0 pt-1">
                    {comment.authorAvatarUrl ? (
                      <img src={comment.authorAvatarUrl} alt={comment.authorDisplayName ?? 'Author'} className="w-8 h-8 rounded-full bg-gray-100 object-cover" />
                    ) : (
                      <div className="w-8 h-8 rounded-full bg-blue-100 text-blue-700 flex items-center justify-center font-bold text-xs uppercase">
                        {comment.authorDisplayName ? comment.authorDisplayName.charAt(0) : '?'}
                      </div>
                    )}
                  </div>
                  
                  <div className="flex-1 min-w-0 bg-gray-50 rounded-lg p-3">
                    <div className="flex items-center justify-between gap-2 mb-1">
                      <div className="font-medium text-gray-900 truncate">
                        {comment.authorDisplayName ?? 'Member'}
                      </div>
                      <div className="text-xs text-gray-500 flex-shrink-0 flex items-center gap-2">
                        <span>{new Date(comment.createdAt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}</span>
                        {comment.lastEditedAt && !comment.isDeleted && <span className="text-gray-400 font-medium">(edited)</span>}
                      </div>
                    </div>

                    {comment.isDeleted ? (
                      <p className="text-gray-500 italic flex items-center gap-1.5"><Trash2 size={12} /> This comment was deleted.</p>
                    ) : editingCommentId === comment.id ? (
                      <div className="mt-2">
                        <RichTextEditor value={editBody} onChange={setEditBody} minHeight={80} workItemId={workItemId} attachments={attachments} onAttachmentUploaded={() => queryClient.invalidateQueries({ queryKey: ['work-item-attachments', workItemId] })} />
                        <div className="mt-2 flex items-center justify-end gap-2">
                          <button type="button" onClick={() => setEditingCommentId(null)} className="text-xs font-medium text-gray-600 hover:text-gray-900 px-2 py-1">Cancel</button>
                          <button 
                            type="button" 
                            onClick={() => editMutation.mutate({ commentId: comment.id, body: editBody, version: comment.version })}
                            disabled={editMutation.isPending || isRichTextEmpty(editBody)}
                            className="text-xs font-medium bg-blue-600 text-white rounded px-3 py-1.5 hover:bg-blue-700 disabled:opacity-50"
                          >
                            {editMutation.isPending ? 'Saving...' : 'Save'}
                          </button>
                        </div>
                      </div>
                    ) : (
                      <>
                        <RichTextView className="text-gray-700 break-words" html={comment.body ?? ''} attachments={attachments} />
                        
                        {/* Actions - only visible to author */}
                        {isAuthor(comment) && (
                          <div className="mt-2 flex items-center gap-3">
                            <button 
                              type="button" 
                              onClick={() => { setEditingCommentId(comment.id); setEditBody(comment.body ?? ''); }}
                              className="flex items-center gap-1 text-xs font-medium text-gray-500 hover:text-gray-900"
                            >
                              <Edit2 size={12} /> Edit
                            </button>
                            <button 
                              type="button" 
                              onClick={() => {
                                if (window.confirm('Are you sure you want to delete this comment?')) {
                                  deleteMutation.mutate({ commentId: comment.id, version: comment.version })
                                }
                              }}
                              className="flex items-center gap-1 text-xs font-medium text-red-500 hover:text-red-700"
                            >
                              <Trash2 size={12} /> Delete
                            </button>
                          </div>
                        )}
                      </>
                    )}
                  </div>
                </div>
              ))
            )}
          </div>

          {/* New comment composer */}
          <div className="flex gap-3 items-start">
            <div className="flex-shrink-0 pt-1">
              {profile?.avatarUrl ? (
                <img src={profile.avatarUrl} alt={profile.displayName} className="w-8 h-8 rounded-full bg-gray-100 object-cover" />
              ) : (
                <div className="w-8 h-8 rounded-full bg-gray-200 text-gray-700 flex items-center justify-center font-bold text-xs uppercase">
                  {profile?.displayName?.charAt(0) ?? '?'}
                </div>
              )}
            </div>
            <div className="flex-1 min-w-0">
              <div className="comment-suggestions">
                {commentSuggestions.map((suggestion) => (
                  <button
                    key={suggestion}
                    type="button"
                    className="comment-suggestion-chip"
                    onClick={() => setNewCommentBody(isRichTextEmpty(newCommentBody) ? `<p>${suggestion}</p>` : `${newCommentBody}<p>${suggestion}</p>`)}
                  >
                    {suggestion}
                  </button>
                ))}
              </div>
              <div
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
                    e.preventDefault()
                    if (!isRichTextEmpty(newCommentBody) && !addMutation.isPending) {
                      addMutation.mutate(newCommentBody)
                    }
                  }
                }}
              >
                <RichTextEditor
                  value={newCommentBody}
                  onChange={setNewCommentBody}
                  placeholder="Add a comment... (use @ to mention)"
                  minHeight={80}
                  workItemId={workItemId}
                  onAttachmentUploaded={() => queryClient.invalidateQueries({ queryKey: ['work-item-attachments', workItemId] })}
                />
              </div>
              <div className="mt-2 flex items-center justify-between">
                <div className="flex items-center text-xs text-gray-400 font-medium">
                  <CornerDownRight size={12} className="mr-1" /> Ctrl+Enter to send
                </div>
                <button
                  type="button"
                  onClick={() => addMutation.mutate(newCommentBody)}
                  disabled={isRichTextEmpty(newCommentBody) || addMutation.isPending}
                  className="bg-blue-600 hover:bg-blue-700 text-white font-medium text-xs px-3 py-1.5 rounded disabled:opacity-50 flex items-center gap-1.5 transition-colors"
                >
                  {addMutation.isPending ? 'Posting...' : 'Comment'}
                </button>
              </div>
              {addMutation.isError && <p className="text-red-600 text-xs mt-1">{addMutation.error.message}</p>}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

