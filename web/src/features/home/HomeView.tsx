import { ArrowRight, CheckCircle2, CircleDot, FolderKanban, Plus } from 'lucide-react'
import type { Profile, Project, WorkItem } from '../../api/types'

export function HomeView({ profile, projects, workItems, onOpenProject, onCreate }: { profile?: Profile; projects: Project[]; workItems: WorkItem[]; onOpenProject: (projectId: string) => void; onCreate: () => void }) {
  const completed = workItems.filter((item) => item.status === 'Done').length
  const active = workItems.filter((item) => item.status === 'InProgress' || item.status === 'InReview').length

  return <div className="min-h-[calc(100vh-56px)] bg-[#f7f8fa] p-6 lg:p-10">
    <div className="mx-auto max-w-6xl">
      <div className="flex flex-wrap items-end justify-between gap-4"><div><p className="text-sm text-gray-500">Your work</p><h1 className="mt-1 text-3xl font-semibold text-gray-900">Welcome back{profile?.displayName ? `, ${profile.displayName.split(' ')[0]}` : ''}</h1><p className="mt-2 text-sm text-gray-500">Continue delivery work or create a new item.</p></div><button onClick={onCreate} className="primary-button"><Plus size={17} /> Create work item</button></div>
      <div className="mt-8 grid gap-4 sm:grid-cols-3"><Stat icon={<FolderKanban size={20} />} label="Projects" value={projects.length} /><Stat icon={<CircleDot size={20} />} label="Active work" value={active} /><Stat icon={<CheckCircle2 size={20} />} label="Completed" value={completed} /></div>
      <section className="mt-8 rounded-xl border border-gray-200 bg-white p-5 shadow-sm"><h2 className="text-lg font-semibold text-gray-900">Recent spaces</h2><div className="mt-4 divide-y divide-gray-100">{projects.map((project) => <button key={project.id} onClick={() => onOpenProject(project.id)} className="flex w-full items-center gap-4 py-4 text-left hover:bg-gray-50"><span className="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-100 text-sm font-bold text-blue-700">{project.key.slice(0, 2)}</span><span className="flex-1"><span className="block font-medium text-gray-900">{project.name}</span><span className="text-xs text-gray-500">{project.key}</span></span><ArrowRight size={18} className="text-gray-400" /></button>)}</div></section>
    </div>
  </div>
}

function Stat({ icon, label, value }: { icon: React.ReactNode; label: string; value: number }) { return <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm"><span className="text-blue-600">{icon}</span><p className="mt-4 text-2xl font-semibold text-gray-900">{value}</p><p className="text-sm text-gray-500">{label}</p></div> }
