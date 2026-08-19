import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Trash2, Edit2, CornerDownRight, Reply } from 'lucide-react'
import { orbitApi } from '../../api/client'
import { RichTextEditor } from '../../components/form/RichTextEditor'
import { isRichTextEmpty } from '../../components/form/editorConstants'
import { RichTextView } from '../../components/form/RichTextView'
import type { Profile, TenantMembership, WorkItem } from '../../api/types'

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
  workItems = [],
}: {
  workItemId: string
  profile?: Profile
  members?: TenantMembership[]
  workItems?: WorkItem[]
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
  const historyQuery = useQuery({
    queryKey: ['work-item-history', workItemId],
    queryFn: () => orbitApi.listWorkItemHistory(workItemId),
    enabled: activeTab === 'history' || activeTab === 'all',
  })

  const comments = commentsQuery.data ?? []
  const attachments = attachmentsQuery.data ?? []
  // API returns ascending; show newest first, like the comment feed.
  const history = [...(historyQuery.data ?? [])].reverse()

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

  // BP-01: Use membershipId exclusively for authorship checks — display names are not
  // unique, so a name-string comparison could surface edit/delete to the wrong user.
  const isAuthor = (comment: { authorMembershipId: string }) => {
    return currentMember !== undefined && comment.authorMembershipId === currentMember.id
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

  // Sort comments newest first
  const sortedComments = [...comments].sort(
    (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  )

  const handleReplyTo = (comment: { authorDisplayName?: string | null; body?: string | null }) => {
    const author = comment.authorDisplayName ?? 'Member'
    const cleanSnippet = comment.body
      ? comment.body.replace(/<[^>]*>/g, ' ').slice(0, 100).trim()
      : ''
    const quoteHtml = cleanSnippet
      ? `<blockquote><p><strong>${author}:</strong> "${cleanSnippet}..."</p></blockquote><p>@${author}&nbsp;</p>`
      : `<p>@${author}&nbsp;</p>`
    setNewCommentBody((prev) => (isRichTextEmpty(prev) ? quoteHtml : `${prev}${quoteHtml}`))
  }

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
          {activeTab === 'all' && history.length > 0 && <p className="activity-section-label">History</p>}
          {historyQuery.isPending ? (
            <p className="text-sm text-gray-500">Loading history...</p>
          ) : history.length === 0 ? (
            <p className="activity-empty-text">No history recorded yet.</p>
          ) : (
            <ul className="history-feed">
              {history.map((entry) => (
                <li key={entry.id} className="history-feed-item">
                  <div className="history-feed-avatar">
                    {entry.changedByDisplayName ? entry.changedByDisplayName.charAt(0).toUpperCase() : '?'}
                  </div>
                  <div className="history-feed-body">
                    <p className="history-feed-line">
                      <span className="history-feed-actor">{entry.changedByDisplayName}</span>{' '}
                      {entry.fieldName === 'Ticket' ? (
                        'created this ticket'
                      ) : (
                        <>
                          updated the <span className="history-feed-field">{entry.fieldName}</span>
                        </>
                      )}
                      <span className="history-feed-time">
                        {new Date(entry.changedAt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}
                      </span>
                    </p>
                    {entry.fieldName !== 'Ticket' && (
                      <p className="history-feed-diff">
                        <span className="history-feed-old">{entry.oldValue ?? 'None'}</span>
                        <span className="history-feed-arrow">→</span>
                        <span className="history-feed-new">{entry.newValue ?? 'None'}</span>
                      </p>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          )}
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

          {/* New comment composer AT THE TOP (Matching user request & Jira style) */}
          <div className="flex gap-3 items-start mb-6">
            <div className="flex-shrink-0 pt-1">
              {profile?.avatarUrl ? (
                <img src={profile.avatarUrl} alt={profile.displayName} className="w-8 h-8 rounded-full bg-gray-100 object-cover" />
              ) : (
                <div className="w-8 h-8 rounded-full bg-[#ffab00] text-white flex items-center justify-center font-bold text-xs">
                  {profile?.displayName ? profile.displayName.charAt(0).toUpperCase() : 'EU'}
                </div>
              )}
            </div>
            <div className="flex-1 min-w-0">
              <div className="rounded-lg border border-[#dfe1e6] bg-white shadow-sm overflow-hidden focus-within:border-[#829bf7] focus-within:ring-2 focus-within:ring-blue-100 transition-all">
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
                    placeholder="Add a comment... (Type @ to mention someone or a ticket key like TST-1 to auto-link)"
                    minHeight={70}
                    workItemId={workItemId}
                    members={members}
                    workItems={workItems}
                    onAttachmentUploaded={() => queryClient.invalidateQueries({ queryKey: ['work-item-attachments', workItemId] })}
                  />
                </div>

                {/* Suggestion Chips nested INSIDE the composer box */}
                <div className="px-3 py-2 border-t border-gray-100 bg-[#fafbfc] flex items-center justify-between gap-2 flex-wrap">
                  <div className="flex items-center gap-1.5 flex-wrap overflow-x-auto">
                    {commentSuggestions.map((suggestion) => (
                      <button
                        key={suggestion}
                        type="button"
                        className="px-2.5 py-1 rounded-full bg-white hover:bg-[#f0f4ff] text-[#42526e] hover:text-[#0052cc] border border-[#dfe1e6] hover:border-[#b3d4ff] text-xs font-medium cursor-pointer transition-colors whitespace-nowrap shadow-2xs"
                        onClick={() =>
                          setNewCommentBody(
                            isRichTextEmpty(newCommentBody)
                              ? `<p>${suggestion}</p>`
                              : `${newCommentBody}<p>${suggestion}</p>`
                          )
                        }
                      >
                        {suggestion}
                      </button>
                    ))}
                  </div>

                  <button
                    type="button"
                    onClick={() => addMutation.mutate(newCommentBody)}
                    disabled={isRichTextEmpty(newCommentBody) || addMutation.isPending}
                    className="bg-[#0052cc] hover:bg-[#0065ff] text-white font-semibold text-xs px-3.5 py-1.5 rounded-md disabled:opacity-40 disabled:cursor-not-allowed flex items-center gap-1.5 transition-colors ml-auto"
                  >
                    {addMutation.isPending ? 'Posting...' : 'Save'}
                  </button>
                </div>
              </div>

              {/* Outside Pro-tip beneath the box */}
              <div className="mt-1.5 flex items-center justify-between text-xs text-[#6b778c]">
                <span>
                  <span className="font-semibold text-gray-700">Pro tip:</span> press{' '}
                  <kbd className="px-1.5 py-0.5 bg-gray-100 border border-gray-300 rounded font-mono text-[10px] font-bold text-gray-700">
                    M
                  </kbd>{' '}
                  to comment
                </span>
                <span className="text-[11px] text-gray-400 font-medium flex items-center gap-1">
                  <CornerDownRight size={11} /> Ctrl+Enter to send
                </span>
              </div>
              {addMutation.isError && <p className="text-red-600 text-xs mt-1">{addMutation.error.message}</p>}
            </div>
          </div>

          {/* Existing comments list - Newest first */}
          <div className="space-y-4 mb-6">
            {commentsQuery.isPending ? (
              <p className="text-sm text-gray-500">Loading comments...</p>
            ) : sortedComments.length === 0 ? (
              <p className="activity-empty-text">No comments yet. Be the first to start the conversation.</p>
            ) : (
              sortedComments.map((comment) => (
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
                  
                  <div className="flex-1 min-w-0 bg-white border border-gray-100 rounded-lg p-3">
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
                        <RichTextEditor value={editBody} onChange={setEditBody} minHeight={80} workItemId={workItemId} attachments={attachments} members={members} workItems={workItems} onAttachmentUploaded={() => queryClient.invalidateQueries({ queryKey: ['work-item-attachments', workItemId] })} />
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
                        
                        {/* Action buttons: Reply, Edit, Delete */}
                        <div className="mt-2 flex items-center gap-3">
                          <button
                            type="button"
                            onClick={() => handleReplyTo(comment)}
                            className="flex items-center gap-1 text-xs font-medium text-gray-500 hover:text-[#0052cc] transition-colors"
                          >
                            <Reply size={12} /> Reply
                          </button>
                          {isAuthor(comment) && (
                            <>
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
                            </>
                          )}
                        </div>
                      </>
                    )}
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  )
}

