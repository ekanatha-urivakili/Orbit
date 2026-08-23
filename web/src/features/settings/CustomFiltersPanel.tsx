import { useState } from 'react'
import { Field } from '../../components/form/Field'
import { HelpCircle, Sparkles, BookOpen, X, Check, Code2, Layers, Filter } from 'lucide-react'
import { Panel } from './SettingsView'

interface QueryPreset {
  name: string
  description: string
  query: string
  category: string
}

const QUERY_PRESETS: QueryPreset[] = [
  {
    name: 'My Open Items',
    description: 'Work items assigned to me that are not yet marked as Done.',
    query: "assignee = currentUser() AND status != 'Done'",
    category: 'Personal',
  },
  {
    name: 'High Priority Bugs',
    description: 'Critical and high priority bugs requiring immediate attention.',
    query: "type = 'Bug' AND priority IN ('Highest', 'High') AND status != 'Done'",
    category: 'Triage',
  },
  {
    name: 'Current Sprint Backlog',
    description: 'All work scheduled in the active sprint ordered by rank.',
    query: 'sprint = currentSprint() ORDER BY rank ASC',
    category: 'Sprint',
  },
  {
    name: 'Unassigned To-Do',
    description: 'Items ready to be picked up with no assigned developer.',
    query: "assignee IS EMPTY AND status = 'To Do'",
    category: 'Triage',
  },
  {
    name: 'Overdue Items',
    description: 'Work items where the due date has elapsed and work is still open.',
    query: "dueDate < now() AND status != 'Done' ORDER BY dueDate ASC",
    category: 'Planning',
  },
]

const SYNTAX_GUIDE = [
  {
    category: 'Fields & Attributes',
    items: [
      { code: 'status', desc: "Status name, e.g. status = 'In Progress'" },
      { code: 'assignee', desc: "Assignee user or function, e.g. assignee = currentUser()" },
      { code: 'type', desc: "Work item type (Task, Bug, Story, Epic)" },
      { code: 'priority', desc: "Priority level (Lowest, Low, Medium, High, Highest)" },
      { code: 'sprint', desc: "Sprint name or function, e.g. sprint = currentSprint()" },
      { code: 'labels', desc: "Label tags, e.g. labels IN ('frontend', 'security')" },
    ],
  },
  {
    category: 'Operators & Functions',
    items: [
      { code: '= , !=', desc: 'Equality and inequality comparisons' },
      { code: 'IN (a, b)', desc: 'Matches any item in the specified list' },
      { code: 'IS EMPTY', desc: 'Checks for unassigned or unset fields' },
      { code: 'currentUser()', desc: 'Resolves dynamically to your authenticated account' },
      { code: 'currentSprint()', desc: 'Resolves to the active sprint in this project' },
      { code: 'now()', desc: 'Current UTC timestamp for date comparisons' },
    ],
  },
]

// No custom-filters backend exists yet (WQL is unshipped — see
// OBSERVABILITY-CACHING-ARCHITECTURE.md's row 3 note). This form intentionally does not persist:
// wire it to a real create mutation once the API exists instead of faking storage with local state.
export function CustomFiltersPanel() {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [query, setQuery] = useState('')
  const [showSyntaxHelp, setShowSyntaxHelp] = useState(false)
  const [copiedPreset, setCopiedPreset] = useState<string | null>(null)

  const handleApplyPreset = (preset: QueryPreset) => {
    setName(preset.name)
    setDescription(preset.description)
    setQuery(preset.query)
    setCopiedPreset(preset.name)
    setTimeout(() => setCopiedPreset(null), 2000)
  }

  const handleInsertSyntax = (snippet: string) => {
    setQuery((prev) => (prev ? `${prev} AND ${snippet}` : snippet))
  }

  const handleReset = () => {
    setName('')
    setDescription('')
    setQuery('')
  }

  const handleCreate = (event: React.FormEvent) => {
    event.preventDefault()
  }

  return (
    <div className="space-y-6">
      {/* Informational Feature Preview Banner */}
      <div className="rounded-xl border border-blue-100 dark:border-blue-900/40 bg-gradient-to-r from-blue-50/80 via-indigo-50/50 to-purple-50/40 dark:from-blue-950/30 dark:via-indigo-950/20 dark:to-purple-950/20 p-4 shadow-sm">
        <div className="flex items-start gap-3">
          <div className="rounded-lg bg-blue-600/10 dark:bg-blue-400/10 p-2 text-blue-600 dark:text-blue-400 shrink-0 mt-0.5">
            <Sparkles size={18} />
          </div>
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2 flex-wrap">
              <h3 className="text-sm font-semibold text-gray-900 dark:text-gray-100">
                Work Query Language (WQL) Engine
              </h3>
              <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium bg-blue-100 dark:bg-blue-900/60 text-blue-700 dark:text-blue-300">
                Preview
              </span>
            </div>
            <p className="mt-1 text-xs text-gray-600 dark:text-gray-300 leading-relaxed">
              Define reusable WQL filter presets to search and slice work items across boards, backlogs, and timelines.
              Query execution will be activated in an upcoming platform release. Explore sample queries and syntax below.
            </p>
          </div>
        </div>
      </div>

      <Panel
        title="Custom filters"
        description="Create a reusable filter to quickly isolate work on your board, backlog, and timeline."
      >
        {/* Quick Query Templates */}
        <div className="mb-6 pb-5 border-b border-gray-100 dark:border-[#2d343c]">
          <div className="flex items-center justify-between gap-2 mb-2.5">
            <span className="text-xs font-semibold text-gray-700 dark:text-gray-300 flex items-center gap-1.5">
              <Filter size={13} className="text-blue-500" />
              Quick Templates
            </span>
            <span className="text-[11px] text-gray-400 dark:text-gray-500">Click to apply to form</span>
          </div>
          <div className="flex flex-wrap gap-2">
            {QUERY_PRESETS.map((preset) => {
              const isSelected = name === preset.name && query === preset.query
              return (
                <button
                  key={preset.name}
                  type="button"
                  onClick={() => handleApplyPreset(preset)}
                  className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-all duration-150 border ${
                    isSelected
                      ? 'bg-blue-50 dark:bg-blue-950/60 border-blue-300 dark:border-blue-700 text-blue-700 dark:text-blue-300 shadow-sm'
                      : 'bg-white dark:bg-[#22272b] border-gray-200 dark:border-[#394047] text-gray-700 dark:text-gray-300 hover:border-blue-300 dark:hover:border-blue-500 hover:bg-gray-50 dark:hover:bg-[#282e34]'
                  }`}
                >
                  {copiedPreset === preset.name ? (
                    <Check size={12} className="text-green-600 dark:text-green-400 shrink-0" />
                  ) : (
                    <Code2 size={12} className="text-gray-400 dark:text-gray-500 shrink-0" />
                  )}
                  <span>{preset.name}</span>
                </button>
              )
            })}
          </div>
        </div>

        <form onSubmit={handleCreate} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field variant="panel" label="Name *">
              <input
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="e.g. My Open Bugs"
                className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 dark:border-[#4b5563] bg-white dark:bg-[#22272b] text-gray-900 dark:text-gray-100 placeholder-gray-400 dark:placeholder-gray-500 focus:outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 transition-all"
              />
            </Field>
            <Field variant="panel" label="Description">
              <input
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Describe the scope of this filter"
                className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 dark:border-[#4b5563] bg-white dark:bg-[#22272b] text-gray-900 dark:text-gray-100 placeholder-gray-400 dark:placeholder-gray-500 focus:outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 transition-all"
              />
            </Field>
          </div>

          <Field variant="panel" label="Filter query *">
            <div className="relative">
              <textarea
                required
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                rows={3}
                placeholder="status = 'To Do' AND assignee = currentUser()"
                className="w-full px-3 py-2 pr-10 text-sm font-mono rounded-lg border border-gray-300 dark:border-[#4b5563] bg-white dark:bg-[#22272b] text-gray-900 dark:text-gray-100 placeholder-gray-400 dark:placeholder-gray-500 focus:outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 transition-all leading-relaxed"
              />
              <button
                type="button"
                onClick={() => setShowSyntaxHelp((prev) => !prev)}
                className={`absolute top-2.5 right-2.5 p-1 rounded-md transition-colors ${
                  showSyntaxHelp
                    ? 'bg-blue-100 text-blue-700 dark:bg-blue-900 dark:text-blue-300'
                    : 'text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-800'
                }`}
                title="Toggle WQL Syntax Help"
                aria-label="Toggle WQL Syntax Help"
              >
                <HelpCircle size={17} />
              </button>
            </div>
          </Field>

          {/* Syntax Help Drawer */}
          {showSyntaxHelp && (
            <div className="rounded-xl border border-gray-200 dark:border-[#394047] bg-gray-50/70 dark:bg-[#1a1e22] p-4 text-xs space-y-4 animate-in fade-in zoom-in-95 duration-150">
              <div className="flex items-center justify-between gap-2 pb-2 border-b border-gray-200 dark:border-[#2d343c]">
                <div className="flex items-center gap-2 font-semibold text-gray-800 dark:text-gray-200">
                  <BookOpen size={15} className="text-blue-500" />
                  <span>WQL Syntax Reference</span>
                </div>
                <button
                  type="button"
                  onClick={() => setShowSyntaxHelp(false)}
                  className="p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 rounded"
                  aria-label="Close syntax guide"
                >
                  <X size={14} />
                </button>
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                {SYNTAX_GUIDE.map((section) => (
                  <div key={section.category} className="space-y-2">
                    <div className="font-semibold text-gray-700 dark:text-gray-300 flex items-center gap-1.5">
                      <Layers size={13} className="text-indigo-500" />
                      {section.category}
                    </div>
                    <ul className="space-y-1.5">
                      {section.items.map((item) => (
                        <li
                          key={item.code}
                          className="flex flex-col gap-0.5 p-1.5 rounded bg-white dark:bg-[#22272b] border border-gray-100 dark:border-[#2d343c]"
                        >
                          <div className="flex items-center justify-between gap-2">
                            <code className="font-mono text-[11px] font-semibold text-blue-600 dark:text-blue-400">
                              {item.code}
                            </code>
                            <button
                              type="button"
                              onClick={() => handleInsertSyntax(item.code)}
                              className="text-[10px] text-gray-400 hover:text-blue-600 dark:hover:text-blue-400 font-medium"
                              title="Append to query"
                            >
                              + Insert
                            </button>
                          </div>
                          <span className="text-[11px] text-gray-500 dark:text-gray-400">{item.desc}</span>
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>
            </div>
          )}

          <div className="flex items-center justify-between gap-2 pt-3 border-t border-gray-100 dark:border-[#2d343c]">
            <span className="text-xs text-gray-400 dark:text-gray-500 italic">
              Custom filter persistence will be wired upon API completion.
            </span>
            <div className="flex items-center gap-2">
              <button type="button" className="secondary-button" onClick={handleReset}>
                Reset
              </button>
              <button
                type="submit"
                className="primary-button"
                disabled
                title="Custom filters are currently in preview mode"
              >
                Create Filter
              </button>
            </div>
          </div>
        </form>
      </Panel>
    </div>
  )
}
