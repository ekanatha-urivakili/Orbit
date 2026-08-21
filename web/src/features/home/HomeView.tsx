import { ArrowRight, Building2, CheckCircle2, CircleDot, FolderKanban, Plus } from 'lucide-react'
import type { AccountWorkspace, Profile, Project, WorkItem, WorkItemStatusDefinition } from '../../api/types'

export function HomeView({
  profile,
  projects,
  workItems,
  statuses,
  workspaceName,
  workspaces,
  currentWorkspaceId,
  onWorkspaceChange,
  onCreateWorkspace,
  onOpenProject,
  onCreate,
}: {
  profile?: Profile
  projects: Project[]
  workItems: WorkItem[]
  statuses: WorkItemStatusDefinition[]
  workspaceName?: string
  workspaces?: AccountWorkspace[]
  currentWorkspaceId?: string
  onWorkspaceChange?: (id: string) => void
  onCreateWorkspace?: () => void
  onOpenProject: (projectId: string) => void
  onCreate: () => void
}) {
  const categoryByStatusId = new Map(statuses.map((status) => [status.id, status.category]))
  const completed = workItems.filter((item) => categoryByStatusId.get(item.statusId) === 'Done').length
  const active = workItems.filter((item) => categoryByStatusId.get(item.statusId) === 'InProgress').length

  return (
    <div className="min-h-[calc(100vh-56px)] bg-[#f7f8fa] dark:bg-[#101214] p-6 lg:p-10">
      <div className="mx-auto max-w-6xl">
        {/* Workspace Banner */}
        <div className="mb-6 flex items-center justify-between bg-white dark:bg-[#1d2125] border border-gray-200 dark:border-[#394047] rounded-xl px-5 py-3.5 shadow-sm flex-wrap gap-3">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-lg bg-blue-50 dark:bg-blue-950/40 text-blue-600 dark:text-blue-400 flex items-center justify-center font-bold">
              <Building2 size={20} />
            </div>
            <div>
              <p className="text-[11px] font-bold uppercase tracking-wider text-gray-500 dark:text-gray-400">Current Workspace</p>
              <h2 className="text-base font-bold text-gray-900 dark:text-white">{workspaceName ?? 'Orbit Workspace'}</h2>
            </div>
          </div>
          <div className="flex items-center gap-2.5">
            {workspaces && workspaces.length > 1 && onWorkspaceChange && (
              <select
                value={currentWorkspaceId ?? ''}
                onChange={(e) => onWorkspaceChange(e.target.value)}
                className="text-xs font-semibold border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-1.5 bg-white dark:bg-[#22272b] text-gray-800 dark:text-gray-200 focus:outline-none focus:border-blue-500"
              >
                {workspaces.map((ws) => (
                  <option key={ws.id} value={ws.id}>
                    {ws.name}
                  </option>
                ))}
              </select>
            )}
            {onCreateWorkspace && (
              <button
                type="button"
                onClick={onCreateWorkspace}
                className="text-xs font-semibold px-2.5 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800 text-gray-700 dark:text-gray-300 flex items-center gap-1"
              >
                <Plus size={13} /> New workspace
              </button>
            )}
          </div>
        </div>

        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <p className="text-sm text-gray-500 dark:text-gray-400">Your work</p>
            <h1 className="mt-1 text-3xl font-semibold text-gray-900 dark:text-white">
              Welcome back{profile?.displayName ? `, ${profile.displayName.split(' ')[0]}` : ''}
            </h1>
            <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">Continue delivery work or create a new item.</p>
          </div>
          <button onClick={onCreate} className="primary-button">
            <Plus size={17} /> Create work item
          </button>
        </div>

        <div className="mt-8 grid gap-4 sm:grid-cols-3">
          <Stat icon={<FolderKanban size={20} />} label="Projects" value={projects.length} />
          <Stat icon={<CircleDot size={20} />} label="Active work" value={active} />
          <Stat icon={<CheckCircle2 size={20} />} label="Completed" value={completed} />
        </div>

        <section className="mt-8 rounded-xl border border-gray-200 dark:border-[#394047] bg-white dark:bg-[#1d2125] p-5 shadow-sm">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Recent spaces</h2>
          <div className="mt-4 divide-y divide-gray-100 dark:divide-gray-800">
            {projects.map((project) => (
              <button
                key={project.id}
                onClick={() => onOpenProject(project.id)}
                className="flex w-full items-center gap-4 py-4 text-left hover:bg-gray-50 dark:hover:bg-[#22272b] transition-colors rounded-lg px-2"
              >
                <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-100 dark:bg-blue-900 text-sm font-bold text-blue-700 dark:text-blue-300">
                  {project.key.slice(0, 2)}
                </span>
                <span className="flex-1">
                  <span className="block font-medium text-gray-900 dark:text-white">{project.name}</span>
                  <span className="text-xs text-gray-500 dark:text-gray-400">{project.key}</span>
                </span>
                <ArrowRight size={18} className="text-gray-400" />
              </button>
            ))}
          </div>
        </section>
      </div>
    </div>
  )
}

function Stat({ icon, label, value }: { icon: React.ReactNode; label: string; value: number }) {
  return (
    <div className="rounded-xl border border-gray-200 dark:border-[#394047] bg-white dark:bg-[#1d2125] p-5 shadow-sm">
      <span className="text-blue-600 dark:text-blue-400">{icon}</span>
      <p className="mt-4 text-2xl font-semibold text-gray-900 dark:text-white">{value}</p>
      <p className="text-sm text-gray-500 dark:text-gray-400">{label}</p>
    </div>
  )
}
