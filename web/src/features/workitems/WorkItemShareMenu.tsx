import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Link as LinkIcon, X, Check, MessageSquareShare } from 'lucide-react'
import { orbitApi } from '../../api/client'
import type { WorkItem } from '../../api/types'

export function WorkItemShareMenu({ item, onClose }: { item: WorkItem; onClose: () => void }) {
  const [tab, setTab] = useState<'work-item' | 'slack'>('work-item')
  const [query, setQuery] = useState('')
  const [selectedMembershipIds, setSelectedMembershipIds] = useState<string[]>([])
  const [selectedTeamIds, setSelectedTeamIds] = useState<string[]>([])
  const [message, setMessage] = useState('')
  const [linkCopied, setLinkCopied] = useState(false)
  const [shared, setShared] = useState(false)

  const membersQuery = useQuery({ queryKey: ['memberships'], queryFn: () => orbitApi.listMemberships() })
  const teamsQuery = useQuery({ queryKey: ['teams'], queryFn: () => orbitApi.listTeams() })

  const memberOptions = (membersQuery.data ?? []).filter(
    (member) => member.displayName?.toLowerCase().includes(query.toLowerCase()),
  )
  const teamOptions = (teamsQuery.data ?? []).filter((team) =>
    team.name.toLowerCase().includes(query.toLowerCase()),
  )

  const shareMutation = useMutation({
    mutationFn: () =>
      orbitApi.shareWorkItem(item.id, {
        membershipIds: selectedMembershipIds,
        teamIds: selectedTeamIds,
        message: message.trim() || null,
      }),
    onSuccess: () => setShared(true),
  })

  const slackConnectionQuery = useQuery({
    queryKey: ['slack-connection', item.projectId],
    queryFn: () => orbitApi.getSlackConnection(item.projectId),
    enabled: tab === 'slack',
  })
  const [slackMessage, setSlackMessage] = useState('')
  const [slackShared, setSlackShared] = useState(false)
  const slackShareMutation = useMutation({
    mutationFn: () => orbitApi.postWorkItemToSlack(item.id, slackMessage.trim() || null),
    onSuccess: () => setSlackShared(true),
  })

  const handleCopyLink = async () => {
    await navigator.clipboard.writeText(`${window.location.origin}/browse/${item.key}`)
    setLinkCopied(true)
    setTimeout(() => setLinkCopied(false), 2000)
  }

  const toggleMembership = (id: string) =>
    setSelectedMembershipIds((current) =>
      current.includes(id) ? current.filter((value) => value !== id) : [...current, id],
    )
  const toggleTeam = (id: string) =>
    setSelectedTeamIds((current) =>
      current.includes(id) ? current.filter((value) => value !== id) : [...current, id],
    )

  const hasSelection = selectedMembershipIds.length > 0 || selectedTeamIds.length > 0

  return (
    <div className="absolute right-0 top-full mt-2 w-96 bg-white dark:bg-[#1d2125] border border-[#dfe1e6] dark:border-[#394047] shadow-2xl rounded-xl z-50 animate-in fade-in">
      <div className="flex items-center justify-between px-4 pt-3 border-b border-gray-100 dark:border-[#394047]">
        <div className="flex items-center gap-4">
          <button
            type="button"
            onClick={() => setTab('work-item')}
            className={`pb-2 text-sm font-semibold border-b-2 -mb-px ${
              tab === 'work-item'
                ? 'text-blue-700 border-blue-600'
                : 'text-gray-500 border-transparent hover:text-gray-700'
            }`}
          >
            Share work item
          </button>
          <button
            type="button"
            onClick={() => setTab('slack')}
            className={`pb-2 text-sm font-semibold border-b-2 -mb-px flex items-center gap-1.5 ${
              tab === 'slack'
                ? 'text-blue-700 border-blue-600'
                : 'text-gray-500 border-transparent hover:text-gray-700'
            }`}
          >
            <MessageSquareShare size={14} /> Share in Slack
          </button>
        </div>
        <button type="button" onClick={onClose} className="text-gray-400 hover:text-gray-700" aria-label="Close">
          <X size={16} />
        </button>
      </div>

      {tab === 'work-item' ? (
      <div className="p-4 space-y-3">
        <label className="block text-xs font-semibold text-gray-600 dark:text-gray-300">
          Names or teams
          <input
            type="text"
            placeholder="e.g. Maria, Team Orange"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            className="mt-1 w-full border border-gray-300 dark:border-gray-600 rounded px-2.5 py-1.5 text-sm bg-white dark:bg-[#22272b] dark:text-white"
          />
        </label>

        {query && (
          <div className="max-h-32 overflow-y-auto border border-gray-100 dark:border-[#394047] rounded-md divide-y divide-gray-50 dark:divide-[#2c333a]">
            {memberOptions.map((member) => (
              <button
                key={member.id}
                type="button"
                onClick={() => toggleMembership(member.id)}
                className="w-full flex items-center justify-between px-2.5 py-1.5 text-xs hover:bg-gray-50 dark:hover:bg-[#2c333a]"
              >
                <span>{member.displayName}</span>
                {selectedMembershipIds.includes(member.id) && <Check size={13} className="text-blue-600" />}
              </button>
            ))}
            {teamOptions.map((team) => (
              <button
                key={team.id}
                type="button"
                onClick={() => toggleTeam(team.id)}
                className="w-full flex items-center justify-between px-2.5 py-1.5 text-xs hover:bg-gray-50 dark:hover:bg-[#2c333a]"
              >
                <span>{team.name} (team)</span>
                {selectedTeamIds.includes(team.id) && <Check size={13} className="text-blue-600" />}
              </button>
            ))}
            {memberOptions.length === 0 && teamOptions.length === 0 && (
              <p className="px-2.5 py-2 text-xs text-gray-400">No matches</p>
            )}
          </div>
        )}

        <p className="text-[11px] text-gray-400">Recipients will see the name of the work item and your message</p>

        <label className="block text-xs font-semibold text-gray-600 dark:text-gray-300">
          Message (optional)
          <textarea
            value={message}
            onChange={(event) => setMessage(event.target.value)}
            placeholder="Anything they should know?"
            rows={3}
            className="mt-1 w-full border border-gray-300 dark:border-gray-600 rounded px-2.5 py-1.5 text-sm bg-white dark:bg-[#22272b] dark:text-white"
          />
        </label>

        {shareMutation.isError && <p className="text-xs text-red-600">{shareMutation.error.message}</p>}
        {shared && <p className="text-xs text-green-600 font-medium">Shared!</p>}

        <div className="flex items-center justify-between pt-1">
          <button
            type="button"
            onClick={handleCopyLink}
            className="flex items-center gap-1.5 text-xs font-medium text-gray-600 hover:text-gray-900"
          >
            <LinkIcon size={13} /> {linkCopied ? 'Copied!' : 'Copy link'}
          </button>
          <button
            type="button"
            disabled={!hasSelection || shareMutation.isPending}
            onClick={() => shareMutation.mutate()}
            className="px-4 py-1.5 text-xs font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded disabled:opacity-40"
          >
            {shareMutation.isPending ? 'Sharing…' : 'Share'}
          </button>
        </div>
      </div>
      ) : (
      <div className="p-4 space-y-3">
        {slackConnectionQuery.isLoading ? (
          <p className="text-xs text-gray-400">Checking connection…</p>
        ) : slackConnectionQuery.data ? (
          <>
            <p className="text-xs text-gray-500 dark:text-gray-400">
              Connected to <strong>#{slackConnectionQuery.data.channelName}</strong> in{' '}
              {slackConnectionQuery.data.teamName}.
            </p>
            <label className="block text-xs font-semibold text-gray-600 dark:text-gray-300">
              Message (optional)
              <textarea
                value={slackMessage}
                onChange={(event) => setSlackMessage(event.target.value)}
                rows={3}
                className="mt-1 w-full border border-gray-300 dark:border-gray-600 rounded px-2.5 py-1.5 text-sm bg-white dark:bg-[#22272b] dark:text-white"
              />
            </label>
            {slackShareMutation.isError && <p className="text-xs text-red-600">{slackShareMutation.error.message}</p>}
            {slackShared && <p className="text-xs text-green-600 font-medium">Posted to Slack!</p>}
            <div className="flex justify-end">
              <button
                type="button"
                disabled={slackShareMutation.isPending}
                onClick={() => slackShareMutation.mutate()}
                className="px-4 py-1.5 text-xs font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded disabled:opacity-40"
              >
                {slackShareMutation.isPending ? 'Posting…' : 'Post to Slack'}
              </button>
            </div>
          </>
        ) : (
          <p className="text-xs text-gray-500 dark:text-gray-400">
            No Slack channel is connected for this project yet. Use Actions → "Connect Slack channel" first.
          </p>
        )}
      </div>
      )}
    </div>
  )
}
