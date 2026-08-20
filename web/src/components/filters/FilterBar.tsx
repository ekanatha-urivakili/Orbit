import { useEffect, useRef, useState } from 'react'
import { Filter, Search, X } from 'lucide-react'
import type { WorkItemFilterFieldState } from '../../hooks/useWorkItemFilters'

interface FilterBarProps {
  searchTerm: string
  onSearchChange: (term: string) => void
  searchPlaceholder: string
  fields: WorkItemFilterFieldState[]
  activeCount: number
  onClearAll: () => void
}

export function FilterBar({ searchTerm, onSearchChange, searchPlaceholder, fields, activeCount, onClearAll }: FilterBarProps) {
  const [open, setOpen] = useState(false)
  const [activeFieldKey, setActiveFieldKey] = useState<WorkItemFilterFieldState['key']>(fields[0]?.key ?? 'status')
  const [fieldSearch, setFieldSearch] = useState('')
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const handleClickOutside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [open])

  const activeField = fields.find((field) => field.key === activeFieldKey) ?? fields[0]
  const visibleOptions = activeField
    ? activeField.options.filter((option) => option.label.toLowerCase().includes(fieldSearch.toLowerCase()))
    : []

  return (
    <div className="flex items-center gap-4" data-testid="filter-bar">
      <div className="relative">
        <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-500" />
        <input
          type="text"
          value={searchTerm}
          onChange={(event) => onSearchChange(event.target.value)}
          placeholder={searchPlaceholder}
          className="pl-9 pr-4 py-1.5 border border-gray-300 rounded hover:bg-gray-50 focus:outline-none focus:border-blue-500 text-sm w-64 bg-white dark:bg-[#22272b] dark:border-gray-600 dark:text-white"
        />
      </div>

      <div className="relative" ref={containerRef}>
        <button
          onClick={() => setOpen((current) => !current)}
          className="flex items-center gap-2 px-3 py-1.5 hover:bg-gray-100 rounded border border-transparent hover:border-gray-200 text-sm font-medium text-gray-700"
          aria-label="Filter"
        >
          <Filter size={16} />
          Filter
          {activeCount > 0 && (
            <span className="flex items-center justify-center min-w-[18px] h-[18px] px-1 rounded-full bg-blue-600 text-white text-[10px] font-bold">
              {activeCount}
            </span>
          )}
        </button>

        {open && (
          <div className="absolute left-0 top-full mt-1 flex w-[420px] bg-white border border-gray-200 shadow-xl rounded-lg z-50 overflow-hidden">
            <div className="w-40 border-r border-gray-100 py-2">
              {fields.map((field) => (
                <button
                  key={field.key}
                  onClick={() => { setActiveFieldKey(field.key); setFieldSearch('') }}
                  className={`w-full text-left px-3 py-2 text-sm flex items-center justify-between gap-1 ${
                    activeFieldKey === field.key ? 'bg-blue-50 text-blue-700 font-medium' : 'text-gray-700 hover:bg-gray-50'
                  }`}
                >
                  {field.label}
                  {field.selected.length > 0 && (
                    <span className="text-[10px] font-bold text-blue-600">{field.selected.length}</span>
                  )}
                </button>
              ))}
              <div className="border-t border-gray-100 mt-2 pt-2">
                <button
                  onClick={onClearAll}
                  disabled={activeCount === 0}
                  className="w-full text-left px-3 py-1.5 text-xs font-medium text-gray-500 hover:text-blue-700 disabled:opacity-40 disabled:hover:text-gray-500"
                >
                  Clear all
                </button>
              </div>
            </div>

            <div className="flex-1 py-2 px-1">
              <div className="px-2 pb-2">
                <input
                  type="text"
                  autoFocus
                  value={fieldSearch}
                  onChange={(event) => setFieldSearch(event.target.value)}
                  placeholder={`Search ${activeField?.label.toLowerCase() ?? ''}`}
                  className="w-full border border-gray-200 rounded px-2 py-1 text-sm focus:outline-none focus:border-blue-500"
                />
              </div>
              <div className="max-h-64 overflow-y-auto">
                {visibleOptions.map((option) => {
                  const checked = activeField?.selected.includes(option.value) ?? false
                  return (
                    <label
                      key={option.value}
                      className="flex items-center gap-2 px-3 py-1.5 text-sm hover:bg-gray-50 cursor-pointer"
                    >
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() => activeField?.toggle(option.value)}
                        className="rounded border-gray-300"
                      />
                      <span className="truncate">{option.label}</span>
                    </label>
                  )
                })}
                {visibleOptions.length === 0 && (
                  <p className="px-3 py-4 text-sm text-gray-400 text-center">No matches</p>
                )}
              </div>
              {activeField && activeField.options.length > 0 && (
                <div className="flex items-center justify-between px-3 pt-2 mt-1 border-t border-gray-100">
                  <button
                    onClick={activeField.clear}
                    disabled={activeField.selected.length === 0}
                    className="text-xs font-medium text-gray-500 hover:text-blue-700 disabled:opacity-40"
                  >
                    Clear
                  </button>
                  <span className="text-xs text-gray-400">
                    {activeField.selected.length} of {activeField.options.length}
                  </span>
                </div>
              )}
            </div>
          </div>
        )}
      </div>

      {activeCount > 0 && (
        <button
          onClick={onClearAll}
          className="flex items-center gap-1 text-xs font-semibold text-blue-600 dark:text-blue-400 hover:underline"
        >
          <X size={12} /> Clear filters
        </button>
      )}
    </div>
  )
}
