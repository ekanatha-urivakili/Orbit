import { useState, ReactNode } from 'react'

function Panel({ title, description, children }: { title: string; description?: string; children: ReactNode }) {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm sm:p-7">
      <h2 className="text-xl font-semibold text-gray-900">{title}</h2>
      {description && <p className="mt-1 mb-6 text-sm text-gray-500">{description}</p>}
      <div className={description ? '' : 'mt-5'}>{children}</div>
    </div>
  )
}
import { Field } from '../../components/form/Field'
import { HelpCircle } from 'lucide-react'

interface CustomFilter {
  id: string
  name: string
  description: string
  query: string
}

export function CustomFiltersPanel() {
  const [filters, setFilters] = useState<CustomFilter[]>([])
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [query, setQuery] = useState('')

  const handleCreate = (event: React.FormEvent) => {
    event.preventDefault()
    if (!name || !query) return
    const newFilter: CustomFilter = {
      id: Date.now().toString(),
      name,
      description,
      query,
    }
    setFilters([...filters, newFilter])
    setName('')
    setDescription('')
    setQuery('')
  }

  return (
    <div className="space-y-5">
      <Panel title="Custom filters" description="Create a reusable filter to find work on your board, backlog, and timeline.">
        <form onSubmit={handleCreate} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field variant="panel" label="Name *">
              <input
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Name your filter"
                className="w-full"
              />
            </Field>
            <Field variant="panel" label="Description">
              <input
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Describe your filter"
                className="w-full"
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
                className="w-full font-mono text-sm pr-8"
              />
              <button 
                type="button" 
                className="absolute top-2 right-2 text-gray-400 hover:text-gray-600"
                title="WQL Syntax Help"
              >
                <HelpCircle size={16} />
              </button>
            </div>
          </Field>
          
          <div className="flex justify-end gap-2 pt-2">
            <button type="button" className="secondary-button" onClick={() => {
              setName('')
              setDescription('')
              setQuery('')
            }}>
              Cancel
            </button>
            <button 
              type="submit" 
              className="primary-button"
              disabled={!name.trim() || !query.trim()}
            >
              Create
            </button>
          </div>
        </form>
      </Panel>

      {filters.length > 0 && (
        <Panel title="Saved filters">
          <div className="space-y-3">
            {filters.map(filter => (
              <div key={filter.id} className="p-4 border border-gray-200 rounded-lg flex justify-between items-center bg-white">
                <div>
                  <h4 className="font-semibold text-gray-900">{filter.name}</h4>
                  <p className="text-sm text-gray-500">{filter.description}</p>
                  <code className="text-xs bg-gray-100 px-1.5 py-0.5 rounded text-gray-700 mt-1 block w-fit">
                    {filter.query}
                  </code>
                </div>
                <button 
                  type="button" 
                  className="text-red-600 hover:text-red-700 text-sm font-medium"
                  onClick={() => setFilters(filters.filter(f => f.id !== filter.id))}
                >
                  Delete
                </button>
              </div>
            ))}
          </div>
        </Panel>
      )}
    </div>
  )
}
