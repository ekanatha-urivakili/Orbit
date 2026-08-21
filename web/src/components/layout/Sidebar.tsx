import {
  FolderKanban,
  Home,
  Settings,
  Plus,
  Users,
  Shield,
  ChevronLeft,
  ChevronRight,
  KanbanSquare,
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
  activeTab,
  onHomeClick,
  onOpenSettings,
  onCreateProject,
  workspaceName,
  boardName,
  onSelectBoard,
  width = 240,
  collapsed = false,
  onToggleCollapse,
  onStartResize,
  isResizing = false,
}: {
  mobileMenuOpen: boolean
  setMobileMenuOpen: (open: boolean) => void
  projects?: Project[]
  selectedProjectId?: string
  onSelectProject?: (id: string) => void
  activeView?: string
  activeTab?: string
  onHomeClick?: () => void
  onOpenSettings?: (section: SettingsSection) => void
  onCreateProject?: () => void
  workspaceName?: string
  boardName?: string
  onSelectBoard?: () => void
  width?: number
  collapsed?: boolean
  onToggleCollapse?: () => void
  onStartResize?: (event: React.PointerEvent) => void
  isResizing?: boolean
}) {
  return (
    <>
      <aside
        style={{
          width: collapsed ? 0 : `${width}px`,
        }}
        className={`region-left fixed inset-y-0 left-0 pt-14 bg-white dark:bg-[#181b1f] border-r border-gray-200 dark:border-[#394047] flex flex-col z-20 transition-[width] duration-150 ease-out ${
          isResizing ? '!transition-none' : ''
        } ${
          mobileMenuOpen
            ? 'translate-x-0 !w-[240px]'
            : collapsed
              ? '-translate-x-full lg:translate-x-0 !border-r-0 overflow-hidden'
              : '-translate-x-full lg:translate-x-0'
        }`}
      >
        {workspaceName && !collapsed && (
          <div className="px-4 py-3 border-b border-gray-200 dark:border-[#394047] flex-shrink-0">
            <p className="text-[11px] font-bold uppercase tracking-wider text-gray-400">Workspace</p>
            <p className="truncate text-sm font-semibold text-gray-800 dark:text-gray-200">{workspaceName}</p>
          </div>
        )}
        <div className={`flex-1 overflow-y-auto py-4 custom-scrollbar ${collapsed ? 'hidden' : ''}`}>
          <nav className="px-3 space-y-1">
            <button
              type="button"
              onClick={() => {
                onHomeClick?.()
                setMobileMenuOpen(false)
              }}
              className={`w-full text-left flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                activeView === 'home'
                  ? 'bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400 font-semibold'
                  : 'text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-[#22272b]'
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
                  className="p-1 hover:bg-gray-100 dark:hover:bg-[#22272b] rounded text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
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
                        ? 'bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400 font-semibold'
                        : 'text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-[#22272b]'
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

          {boardName && (
            <div className="mt-6">
              <div className="px-4 text-xs font-bold text-gray-500 uppercase tracking-wider mb-2">
                Boards
              </div>
              <nav className="px-3 space-y-0.5">
                <button
                  type="button"
                  onClick={() => {
                    onSelectBoard?.()
                    setMobileMenuOpen(false)
                  }}
                  className={`w-full text-left flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                    activeView === 'project' && activeTab === 'Board'
                      ? 'bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400 font-semibold'
                      : 'text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-[#22272b]'
                  }`}
                >
                  <KanbanSquare size={18} className="text-gray-500 flex-shrink-0" />
                  <span className="truncate">{boardName}</span>
                </button>
              </nav>
            </div>
          )}

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
                className="w-full text-left flex items-center gap-3 px-3 py-2 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-[#22272b] rounded-md text-sm font-medium"
              >
                <Users size={18} className="text-purple-600" /> Members & Teams
              </button>
              <button
                type="button"
                onClick={() => {
                  onOpenSettings?.('workspace')
                  setMobileMenuOpen(false)
                }}
                className="w-full text-left flex items-center gap-3 px-3 py-2 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-[#22272b] rounded-md text-sm font-medium"
              >
                <FolderKanban size={18} className="text-blue-600" /> General Workspace
              </button>
              <button
                type="button"
                onClick={() => {
                  onOpenSettings?.('security')
                  setMobileMenuOpen(false)
                }}
                className="w-full text-left flex items-center gap-3 px-3 py-2 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-[#22272b] rounded-md text-sm font-medium"
              >
                <Shield size={18} className="text-emerald-600" /> Security
              </button>
            </nav>
          </div>
        </div>

        {!collapsed && (
          <div className="p-3 border-t border-gray-200 dark:border-[#394047] flex-shrink-0">
            <button
              type="button"
              onClick={() => {
                onOpenSettings?.('profile')
                setMobileMenuOpen(false)
              }}
              className="w-full text-left flex items-center gap-3 px-3 py-2 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-[#22272b] rounded-md text-sm font-medium"
            >
              <Settings size={16} /> My Preferences
            </button>
          </div>
        )}

        {/* Draggable Separation Bar on right border of sidebar */}
        <div
          className={`sidebar-resizer hidden lg:flex ${isResizing ? 'is-resizing' : ''}`}
          onPointerDown={onStartResize}
          onDoubleClick={onToggleCollapse}
          title="Double click to collapse Ctrl + ["
        >
          <div className="sidebar-resizer-line" />
          <button
            type="button"
            onClick={(event) => {
              event.stopPropagation()
              onToggleCollapse?.()
            }}
            className="sidebar-resizer-btn"
            aria-label="Collapse sidebar"
            title="Double click to collapse Ctrl + ["
          >
            <ChevronLeft size={12} />
          </button>
        </div>
      </aside>

      {/* Floating expand tab when left sidebar is collapsed */}
      {collapsed && (
        <button
          type="button"
          onClick={onToggleCollapse}
          className="fixed top-16 left-0 z-20 hidden lg:flex items-center justify-center w-6 h-10 bg-white dark:bg-[#1d2125] border border-l-0 border-gray-300 dark:border-gray-600 rounded-r-md shadow-md text-gray-600 dark:text-gray-300 hover:text-blue-600 hover:bg-blue-50 transition-colors"
          title="Expand sidebar Ctrl + ["
          aria-label="Expand sidebar"
        >
          <ChevronRight size={14} />
        </button>
      )}

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
