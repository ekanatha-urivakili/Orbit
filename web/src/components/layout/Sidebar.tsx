import { Clock, Star, LayoutGrid, Briefcase, Boxes, Filter, LayoutDashboard, FileText, Users, Target, FolderGit2, Settings } from 'lucide-react'

export function Sidebar({ 
  mobileMenuOpen, 
  setMobileMenuOpen 
}: { 
  mobileMenuOpen: boolean
  setMobileMenuOpen: (open: boolean) => void 
}) {
  return (
    <>
      <aside className={`fixed inset-y-0 left-0 pt-14 w-[240px] bg-white border-r border-gray-200 flex flex-col z-10 transition-transform ${mobileMenuOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'}`}>
        <div className="flex-1 overflow-y-auto py-4 custom-scrollbar">
          
          <nav className="px-3 space-y-0.5">
            <a href="#" className="flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium">
              <span className="w-6 flex justify-center"><div className="w-5 h-5 rounded-full border-2 border-gray-400 flex items-center justify-center"><div className="w-2.5 h-2.5 bg-gray-400 rounded-full"></div></div></span> For you
            </a>
            <a href="#" className="flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium">
              <span className="w-6 flex justify-center"><Clock size={18} /></span> Recent
            </a>
            <a href="#" className="flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium">
              <span className="w-6 flex justify-center"><Star size={18} /></span> Starred
            </a>
            <a href="#" className="flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium">
              <span className="w-6 flex justify-center"><LayoutGrid size={18} /></span> Apps
            </a>
            <a href="#" className="flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium">
              <span className="w-6 flex justify-center"><Briefcase size={18} /></span> Plans
            </a>
          </nav>

          <div className="mt-6">
            <div className="px-6 text-xs font-bold text-gray-500 uppercase tracking-wider mb-2 flex items-center justify-between">
              Spaces <span className="text-gray-400 font-normal">+</span>
            </div>
            <nav className="px-3 space-y-0.5">
              <a href="#" className="flex items-center gap-3 px-3 py-2 bg-blue-50 text-blue-700 rounded-md text-sm font-medium">
                <span className="w-6 flex justify-center"><div className="w-6 h-6 bg-blue-500 text-white rounded flex items-center justify-center text-xs">O</div></span> My Software Team
              </a>
              <a href="#" className="flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium">
                <span className="w-6 flex justify-center"><Boxes size={18} /></span> More spaces
              </a>
            </nav>
          </div>

          <div className="mt-6">
            <nav className="px-3 space-y-0.5">
              <a href="#" className="flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium">
                <span className="w-6 flex justify-center"><Filter size={18} /></span> Filters
              </a>
              <a href="#" className="flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium">
                <span className="w-6 flex justify-center"><LayoutDashboard size={18} /></span> Dashboards
              </a>
            </nav>
          </div>

          <div className="mt-6">
            <nav className="px-3 space-y-0.5">
              <a href="#" className="flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium">
                <span className="w-6 flex justify-center text-blue-600"><FileText size={18} /></span> Confluence <span className="ml-auto text-gray-400">↗</span>
              </a>
              <a href="#" className="flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium">
                <span className="w-6 flex justify-center text-purple-600"><Users size={18} /></span> Teams <span className="ml-auto text-gray-400">↗</span>
              </a>
              <a href="#" className="flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium">
                <span className="w-6 flex justify-center text-red-500"><Target size={18} /></span> Goals <span className="ml-auto text-gray-400">↗</span>
              </a>
              <a href="#" className="flex items-center gap-3 px-3 py-2 text-gray-700 hover:bg-gray-100 rounded-md text-sm font-medium">
                <span className="w-6 flex justify-center"><FolderGit2 size={18} /></span> Projects <span className="ml-auto text-gray-400">↗</span>
              </a>
            </nav>
          </div>
          
        </div>
        
        <div className="p-4 border-t border-gray-200">
          <a href="#" className="flex items-center gap-3 text-gray-700 hover:text-gray-900 text-sm font-medium">
            <Settings size={16} /> Customise sidebar
          </a>
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
