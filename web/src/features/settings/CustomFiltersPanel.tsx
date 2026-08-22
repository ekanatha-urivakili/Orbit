import { useState } from 'react'
import { Field } from '../../components/form/Field'
import { HelpCircle } from 'lucide-react'
import { Panel } from './SettingsView'

// No custom-filters backend exists yet (WQL is unshipped — see
// OBSERVABILITY-CACHING-ARCHITECTURE.md's row 3 note). This form intentionally does not persist:
// wire it to a real create mutation once the API exists instead of faking storage with local state.
export function CustomFiltersPanel() {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [query, setQuery] = useState('')

  const handleCreate = (event: React.FormEvent) => {
    event.preventDefault()
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
              disabled
              title="Custom filters are not available yet"
            >
              Create
            </button>
          </div>
        </form>
      </Panel>
    </div>
  )
}
