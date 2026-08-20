import { Globe, List, KanbanSquare, Code2, CalendarClock, FileText, FileSpreadsheet, Zap, Plus, Users, Share2, Maximize2, MoreHorizontal } from 'lucide-react'
import type { Project } from '../../api/types'

export type TabType = 'Summary' | 'Backlog' | 'Board' | 'Timeline' | 'Development'

export function SubNavigation({ 
  project, 
  activeTab, 
  setActiveTab 
}: { 
  project?: Project,
  activeTab: TabType,
  setActiveTab: (tab: TabType) => void
}) {
  return (
    <div className="bg-white border-b border-gray-200 pt-6 px-8 sticky top-14 z-10">
      <div className="text-xs text-gray-500 mb-2">Spaces</div>
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 bg-blue-500 text-white rounded flex items-center justify-center font-bold">O</div>
          <h1 className="text-2xl font-semibold text-gray-900">{project?.name || 'My Software Team'}</h1>
          <button className="text-gray-400 hover:text-gray-600"><Users size={20} /></button>
          <button className="text-gray-400 hover:text-gray-600"><MoreHorizontal size={20} /></button>
        </div>
        <div className="flex items-center gap-2">
          <button className="p-2 hover:bg-gray-100 rounded text-gray-600"><Share2 size={18} /></button>
          <button className="p-2 hover:bg-gray-100 rounded text-gray-600"><Zap size={18} /></button>
          <button className="p-2 hover:bg-gray-100 rounded text-gray-600"><Maximize2 size={18} /></button>
        </div>
      </div>
      
      <div className="flex gap-6 overflow-x-auto custom-scrollbar">
        <button 
          onClick={() => setActiveTab('Summary')}
          className={`flex items-center gap-2 pb-3 px-1 border-b-2 font-medium text-sm transition-colors whitespace-nowrap ${activeTab === 'Summary' ? 'border-blue-600 text-blue-600' : 'border-transparent text-gray-600 hover:text-gray-900'}`}
        >
          <Globe size={16} /> Summary
        </button>
        <button 
          onClick={() => setActiveTab('Backlog')}
          className={`flex items-center gap-2 pb-3 px-1 border-b-2 font-medium text-sm transition-colors whitespace-nowrap ${activeTab === 'Backlog' ? 'border-blue-600 text-blue-600' : 'border-transparent text-gray-600 hover:text-gray-900'}`}
        >
          <List size={16} /> Backlog
        </button>
        <button 
          onClick={() => setActiveTab('Board')}
          className={`flex items-center gap-2 pb-3 px-1 border-b-2 font-medium text-sm transition-colors whitespace-nowrap ${activeTab === 'Board' ? 'border-blue-600 text-blue-600' : 'border-transparent text-gray-600 hover:text-gray-900'}`}
        >
          <KanbanSquare size={16} /> Board
        </button>
        <button 
          onClick={() => setActiveTab('Development')}
          className={`flex items-center gap-2 pb-3 px-1 border-b-2 font-medium text-sm transition-colors whitespace-nowrap ${activeTab === 'Development' ? 'border-blue-600 text-blue-600' : 'border-transparent text-gray-600 hover:text-gray-900'}`}
        >
          <Code2 size={16} /> Code
        </button>
        <button
          onClick={() => setActiveTab('Timeline')}
          className={`flex items-center gap-2 pb-3 px-1 border-b-2 font-medium text-sm transition-colors whitespace-nowrap ${activeTab === 'Timeline' ? 'border-blue-600 text-blue-600' : 'border-transparent text-gray-600 hover:text-gray-900'}`}
        >
          <CalendarClock size={16} /> Timeline
        </button>
        <button className="flex items-center gap-2 pb-3 px-1 border-b-2 border-transparent font-medium text-sm text-gray-600 hover:text-gray-900 whitespace-nowrap">
          <FileText size={16} /> Docs
        </button>
        <button className="flex items-center gap-2 pb-3 px-1 border-b-2 border-transparent font-medium text-sm text-gray-600 hover:text-gray-900 whitespace-nowrap">
          <FileSpreadsheet size={16} /> Forms
        </button>
        <button 
          onClick={() => setActiveTab('Development')}
          className={`flex items-center gap-2 pb-3 px-1 border-b-2 font-medium text-sm transition-colors whitespace-nowrap ${activeTab === 'Development' ? 'border-blue-600 text-blue-600' : 'border-transparent text-gray-600 hover:text-gray-900'}`}
        >
          <Code2 size={16} /> Development
        </button>
        <button className="flex items-center gap-2 pb-3 px-1 border-b-2 border-transparent font-medium text-sm text-gray-600 hover:text-gray-900 whitespace-nowrap">
          <Plus size={16} />
        </button>
      </div>
    </div>
  )
}
