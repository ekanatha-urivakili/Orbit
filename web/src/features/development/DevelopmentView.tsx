import { useState } from 'react'
import { ExternalLink, GitBranch } from 'lucide-react'
import type { ProjectSetting } from '../../api/types'

interface MutationShape {
  isPending: boolean
  isError: boolean
  error: Error | null
}

export function DevelopmentView({
  projectSetting,
  loading,
  mutation,
  onSaveRepositoryUrl,
}: {
  projectSetting?: ProjectSetting
  loading: boolean
  mutation: MutationShape
  onSaveRepositoryUrl: (url: string | null) => void
}) {
  const [draftUrl, setDraftUrl] = useState('')

  if (loading) {
    return <div className="p-8 max-w-5xl text-sm text-gray-500">Loading…</div>
  }

  const repositoryUrl = projectSetting?.repositoryUrl

  return (
    <div className="p-8 max-w-5xl">
      <div className="flex flex-col items-center justify-center py-16 border border-gray-100 rounded-lg bg-white">
        <div className="p-4 bg-gray-100 rounded-full mb-6 text-gray-500">
          <GitBranch size={28} />
        </div>

        {repositoryUrl ? (
          <>
            <h3 className="text-xl font-bold text-gray-900 mb-2">Repository linked</h3>
            <a
              href={repositoryUrl}
              target="_blank"
              rel="noreferrer"
              className="flex items-center gap-2 text-blue-600 font-medium text-sm hover:underline mb-6"
            >
              {repositoryUrl} <ExternalLink size={14} />
            </a>
            <button
              onClick={() => onSaveRepositoryUrl(null)}
              disabled={mutation.isPending}
              className="secondary-button"
            >
              {mutation.isPending ? 'Removing…' : 'Disconnect'}
            </button>
          </>
        ) : (
          <>
            <h3 className="text-xl font-bold text-gray-900 mb-2">Link a repository</h3>
            <p className="text-gray-500 text-center max-w-md text-sm mb-6">
              Add a repository URL so your team can jump from this project straight to its source. Orbit does not
              yet sync commits, pull requests, or deployments — this just keeps a link handy.
            </p>
            <form
              className="flex items-center gap-2 w-full max-w-md"
              onSubmit={(event) => {
                event.preventDefault()
                if (draftUrl.trim()) onSaveRepositoryUrl(draftUrl.trim())
              }}
            >
              <input
                type="url"
                required
                placeholder="https://github.com/your-org/your-repo"
                value={draftUrl}
                onChange={(event) => setDraftUrl(event.target.value)}
                className="flex-1 border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:border-blue-500"
              />
              <button type="submit" disabled={mutation.isPending} className="primary-button">
                {mutation.isPending ? 'Saving…' : 'Link repository'}
              </button>
            </form>
          </>
        )}

        {mutation.isError && <p className="form-error mt-3">{mutation.error?.message}</p>}
      </div>
    </div>
  )
}
