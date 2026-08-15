import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { MessageSquare, Trash2, Edit2, CornerDownRight } from 'lucide-react'
import { orbitApi } from '../../api/client'
import type { Profile, TenantMembership } from '../../api/types'

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
  const [newCommentBody, setNewCommentBody] = useState('')
  const [editingCommentId, setEditingCommentId] = useState<string | null>(null)
  const [editBody, setEditBody] = useState('')

  const currentMember = members.find((m) => m.userId === profile?.userId)

  const commentsQuery = useQuery({
    queryKey: ['work-item-comments', workItemId],
    queryFn: () => orbitApi.listWorkItemComments(workItemId),
  })

  const comments = commentsQuery.data ?? []

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

  return (
    <div className="mt-8 border-t border-gray-200 pt-6">
      <h3 className="flex items-center gap-2 text-sm font-semibold text-gray-900 mb-4">
        <MessageSquare size={16} className="text-gray-500" /> Comments
      </h3>

      <div className="space-y-4 mb-6">
        {commentsQuery.isPending ? (
          <p className="text-sm text-gray-500">Loading comments...</p>
        ) : comments.length === 0 ? (
          <p className="text-sm text-gray-500">No comments yet. Be the first to start the conversation.</p>
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
                    <textarea
                      autoFocus
                      className="w-full text-sm rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500 bg-white"
                      rows={3}
                      value={editBody}
                      onChange={(e) => setEditBody(e.target.value)}
                    />
                    <div className="mt-2 flex items-center justify-end gap-2">
                      <button type="button" onClick={() => setEditingCommentId(null)} className="text-xs font-medium text-gray-600 hover:text-gray-900 px-2 py-1">Cancel</button>
                      <button 
                        type="button" 
                        onClick={() => editMutation.mutate({ commentId: comment.id, body: editBody, version: comment.version })}
                        disabled={editMutation.isPending || !editBody.trim()}
                        className="text-xs font-medium bg-blue-600 text-white rounded px-3 py-1.5 hover:bg-blue-700 disabled:opacity-50"
                      >
                        {editMutation.isPending ? 'Saving...' : 'Save'}
                      </button>
                    </div>
                  </div>
                ) : (
                  <>
                    <div className="text-gray-700 whitespace-pre-wrap break-words">{comment.body}</div>
                    
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
          <div className="relative">
            <textarea
              className="w-full text-sm rounded-lg border border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500 min-h-[80px] p-3 pb-10"
              placeholder="Add a comment... (use @ to mention)"
              value={newCommentBody}
              onChange={(e) => setNewCommentBody(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
                  e.preventDefault()
                  if (newCommentBody.trim() && !addMutation.isPending) {
                    addMutation.mutate(newCommentBody)
                  }
                }
              }}
            />
            <div className="absolute bottom-2 right-2">
              <button
                type="button"
                onClick={() => addMutation.mutate(newCommentBody)}
                disabled={!newCommentBody.trim() || addMutation.isPending}
                className="bg-blue-600 hover:bg-blue-700 text-white font-medium text-xs px-3 py-1.5 rounded disabled:opacity-50 flex items-center gap-1.5 transition-colors"
              >
                {addMutation.isPending ? 'Posting...' : 'Comment'}
              </button>
            </div>
            <div className="absolute bottom-3 left-3 flex items-center text-xs text-gray-400 font-medium pointer-events-none">
              <CornerDownRight size={12} className="mr-1" /> Ctrl+Enter to send
            </div>
          </div>
          {addMutation.isError && <p className="text-red-600 text-xs mt-1">{addMutation.error.message}</p>}
        </div>
      </div>
    </div>
  )
}
