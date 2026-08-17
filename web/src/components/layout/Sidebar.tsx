import {
  FolderKanban,
  Home,
  Settings,
  Plus,
  Users,
  Shield,
} from 'lucide-react'
import type { Project } from '../../api/types'
import type { SettingsSection } from '../../features/settings/SettingsView'

export function Sidebar({
  mobileMenuOpen,
  setMobileMenuOpen,
  projects = [],
  selectedProjectId,
  onSelectProject,
  activeView,
  onHomeClick,
  onOpenSettings,
  onCreateProject,
}: {
  mobileMenuOpen: boolean
  setMobileMenuOpen: (open: boolean) => void
  projects?: Project[]
  selectedProjectId?: string
  onSelectProject?: (id: string) => void
  activeView?: string
  onHomeClick?: () => void
  onOpenSettings?: (section: SettingsSection) => void
  onCreateProject?: () => void
}) {
  return (
    <>
      <aside
        className={`region-left fixed inset-y-0 left-0 pt-14 w-[240px] bg-white border-r border-gray-200 flex flex-col z-10 transition-transform ${
          mobileMenuOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'
        }`}
      >
        <div className="flex-1 overflow-y-auto py-4 custom-scrollbar">
          <nav className="px-3 space-y-1">
            <button
              type="button"
              onClick={() => {
                onHomeClick?.()
                setMobileMenuOpen(false)
              }}
              className={`w-full text-left flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                activeView === 'home'
                  ? 'bg-blue-50 text-blue-700 font-semibold'
                  : 'text-gray-700 hover:bg-gray-100'
              }`}
            >
              <Home size={18} className="text-gray-500" /> Home
            </button>
          </nav>

          <div className="mt-6">
            <div className="px-4 text-xs font-bold text-gray-500 uppercase tracking-wider mb-2 flex items-center justify-between">
              <span>Spaces</span>
              {onCreateProject && (
                <button
                  type="button"
                  onClick={onCreateProject}
                  className="p-1 hover:bg-gray-100 rounded text-gray-500 hover:text-gray-700"
                  title="Create Space"
                >
                  <Plus size={14} />
                </button>
              )}
            </div>
            <nav className="px-3 space-y-0.5">
              {projects.map((project) => {
                const isSelected = activeView === 'project' && selectedProjectId === project.id
                return (
                  <button
                    key={project.id}
                    type="button"
                    onClick={() => {
                      onSelectProject?.(project.id)
                      setMobileMenuOpen(false)
                    }}
                    className={`w-full text-left flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                      isSelected
                        ? 'bg-blue-50 text-blue-700 font-semibold'
                        : 'text-gray-700 hover:bg-gray-100'
                    }`}
                  >
                    <span className="w-6 h-6 bg-blue-500 text-white rounded flex items-center justify-center text-xs font-bold flex-shrink-0">
                      {project.key?.charAt(0) ?? 'P'}
                    </span>
                    <span className="truncate">{project.name}</span>
                  </button>
                )
              })}
              {projects.length === 0 && (
                <p className="px-3 py-2 text-xs text-gray-400 italic">No spaces yet</p>
              )}
            </nav>
          </div>

          <div className="mt-6">
            <div className="px-4 text-xs font-bold text-gray-500 uppercase tracking-wider mb-2">
              Workspace Settings
            </div>
            <nav className="px-3 space-y-0.5">
              <button
                type="button"
                onClick={() => {
                  onOpenSettings?.('members')
                  setMobileMenuOpen(false)
                }}
                className="w-full text-left flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium"
              >
                <Users size={18} className="text-purple-600" /> Members & Teams
              </button>
              <button
                type="button"
                onClick={() => {
                  onOpenSettings?.('workspace')
                  setMobileMenuOpen(false)
                }}
                className="w-full text-left flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium"
              >
                <FolderKanban size={18} className="text-blue-600" /> General Workspace
              </button>
              <button
                type="button"
                onClick={() => {
                  onOpenSettings?.('security')
                  setMobileMenuOpen(false)
                }}
                className="w-full text-left flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium"
              >
                <Shield size={18} className="text-emerald-600" /> Security
              </button>
            </nav>
          </div>
        </div>

        <div className="p-3 border-t border-gray-200">
          <button
            type="button"
            onClick={() => {
              onOpenSettings?.('profile')
              setMobileMenuOpen(false)
            }}
            className="w-full text-left flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium"
          >
            <Settings size={16} /> My Preferences
          </button>
        </div>
      </aside>

      {mobileMenuOpen && (
        <button
          className="fixed inset-0 bg-black/40 z-0 lg:hidden"
          aria-label="Close menu"
          onClick={() => setMobileMenuOpen(false)}
        />
      )}
    </>
  )
}
