import { useEffect, useMemo, useState } from 'react'
import { Search, LayoutGrid, ListTodo, BarChart3, Settings as SettingsIcon, FolderOpen } from 'lucide-react'
import { WorkItemTypeIcon } from '../features/workitems/typeIcons'
import type { Project, WorkItem } from '../api/types'
import type { TabType } from './layout/SubNavigation'

type Command = {
  id: string
  label: string
  hint?: string
  icon: React.ReactNode
  run: () => void
}

export function CommandPalette({
  projects,
  workItems,
  hasSelectedProject,
  onNavigateToProject,
  onOpenWorkItem,
  onNavigateTab,
  onOpenSettings,
}: {
  projects: Project[]
  workItems: WorkItem[]
  hasSelectedProject: boolean
  onNavigateToProject: (projectId: string) => void
  onOpenWorkItem: (workItem: WorkItem) => void
  onNavigateTab: (tab: TabType) => void
  onOpenSettings: () => void
}) {
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')

  const closePalette = () => {
    setOpen(false)
    setQuery('')
  }

  useEffect(() => {
    const handleShortcut = (event: KeyboardEvent) => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        setOpen(true)
      }
      if (event.key === 'Escape') {
        closePalette()
      }
    }
    const handleOpenEvent = () => setOpen(true)
    window.addEventListener('keydown', handleShortcut)
    window.addEventListener('orbit:open-command-palette', handleOpenEvent)
    return () => {
      window.removeEventListener('keydown', handleShortcut)
      window.removeEventListener('orbit:open-command-palette', handleOpenEvent)
    }
  }, [])

  const commands = useMemo<Command[]>(() => {
    const navigationCommands: Command[] = hasSelectedProject
      ? [
          { id: 'nav-summary', label: 'Go to Summary', icon: <BarChart3 size={15} />, run: () => onNavigateTab('Summary') },
          { id: 'nav-backlog', label: 'Go to Backlog', icon: <ListTodo size={15} />, run: () => onNavigateTab('Backlog') },
          { id: 'nav-board', label: 'Go to Board', icon: <LayoutGrid size={15} />, run: () => onNavigateTab('Board') },
          { id: 'nav-settings', label: 'Go to Settings', icon: <SettingsIcon size={15} />, run: onOpenSettings },
        ]
      : []

    const projectCommands: Command[] = projects.map((project) => ({
      id: `project-${project.id}`,
      label: project.name,
      hint: project.key,
      icon: <FolderOpen size={15} />,
      run: () => onNavigateToProject(project.id),
    }))

    const workItemCommands: Command[] = workItems.map((item) => ({
      id: `item-${item.id}`,
      label: item.summary,
      hint: item.key,
      icon: <WorkItemTypeIcon type={item.type} size={15} />,
      run: () => onOpenWorkItem(item),
    }))

    return [...navigationCommands, ...projectCommands, ...workItemCommands]
  }, [projects, workItems, hasSelectedProject, onNavigateToProject, onOpenWorkItem, onNavigateTab, onOpenSettings])

  const filtered = query.trim()
    ? commands.filter(
        (command) =>
          command.label.toLowerCase().includes(query.toLowerCase()) ||
          command.hint?.toLowerCase().includes(query.toLowerCase()),
      )
    : commands.slice(0, 20)

  if (!open) return null

  const runAndClose = (command: Command) => {
    command.run()
    closePalette()
  }

  return (
    <div
      className="fixed inset-0 z-[100] flex items-start justify-center bg-black/40 px-4 pt-24"
      role="dialog"
      onClick={closePalette}
    >
      <div
        className="w-full max-w-lg rounded-xl bg-white dark:bg-[#1d2125] shadow-2xl overflow-hidden"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="flex items-center gap-2 px-4 py-3 border-b border-gray-100 dark:border-[#394047]">
          <Search size={16} className="text-gray-400" />
          <input
            autoFocus
            type="text"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Search projects, work items, or jump to a page…"
            className="flex-1 text-sm bg-transparent focus:outline-none dark:text-white"
          />
          <kbd className="text-[10px] text-gray-400 border border-gray-200 dark:border-gray-600 rounded px-1.5 py-0.5">Esc</kbd>
        </div>
        <div className="max-h-80 overflow-y-auto py-1.5">
          {filtered.length === 0 && <p className="px-4 py-3 text-xs text-gray-400">No matches</p>}
          {filtered.map((command) => (
            <button
              key={command.id}
              type="button"
              onClick={() => runAndClose(command)}
              className="w-full flex items-center gap-2.5 px-4 py-2 text-sm text-left hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200"
            >
              <span className="text-gray-400 shrink-0">{command.icon}</span>
              <span className="flex-1 truncate">{command.label}</span>
              {command.hint && <span className="text-xs text-gray-400 shrink-0">{command.hint}</span>}
            </button>
          ))}
        </div>
      </div>
    </div>
  )
}
