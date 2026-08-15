import { useState, type FormEvent } from 'react'
import { X } from 'lucide-react'
import { Field, Hint } from '../../components/form/Field'

export function CreateWorkspaceDialog({
  pending,
  error,
  onCreate,
  onClose,
}: {
  pending: boolean
  error: Error | null
  onCreate: (name: string) => void
  onClose: () => void
}) {
  const [name, setName] = useState('')

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    onCreate(name)
  }

  return (
    <div className="dialog-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <section className="dialog max-w-lg" role="dialog" aria-modal="true" aria-labelledby="create-workspace-title">
        <header>
          <div>
            <h2 id="create-workspace-title">Create workspace</h2>
            <p className="mt-1 text-xs text-gray-500">The workspace becomes a separate security and data boundary.</p>
          </div>
          <button className="icon-button" type="button" aria-label="Close" onClick={onClose}><X size={20} /></button>
        </header>
        <form onSubmit={submit}>
          <Field label="Workspace name *">
            <input autoFocus required minLength={2} maxLength={120} value={name} onChange={(event) => setName(event.target.value)} />
            <Hint>A URL slug is generated from this name and must be unique.</Hint>
          </Field>
          {error && <p className="form-error">{error.message}</p>}
          <footer>
            <button type="button" className="secondary-button" onClick={onClose}>Cancel</button>
            <button className="primary-button" disabled={pending}>{pending ? 'Creating…' : 'Create workspace'}</button>
          </footer>
        </form>
      </section>
    </div>
  )
}
