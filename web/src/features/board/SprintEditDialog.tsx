import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Calendar, X } from 'lucide-react'
import { orbitApi } from '../../api/client'
import type { Sprint } from '../../api/types'

export function SprintEditDialog({ sprint, onClose }: { sprint: Sprint; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [name, setName] = useState(sprint.name)
  const [goal, setGoal] = useState(sprint.goal ?? '')
  const [startDate, setStartDate] = useState(sprint.startDate ?? '')
  const [endDate, setEndDate] = useState(sprint.endDate ?? '')

  const mutation = useMutation({
    mutationFn: () =>
      orbitApi.updateSprint(sprint, {
        name: name.trim(),
        goal: goal.trim() || null,
        startDate: startDate || null,
        endDate: endDate || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sprints', sprint.projectId] })
      onClose()
    },
  })

  return (
    <div className="dialog-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <section className="dialog sprint-edit-dialog" role="dialog" aria-modal="true" aria-labelledby="sprint-edit-title">
        <header>
          <h2 id="sprint-edit-title">Edit sprint</h2>
          <button className="icon-button" type="button" aria-label="Close" onClick={onClose}><X size={20} /></button>
        </header>
        <form
          className="sprint-edit-form"
          onSubmit={(event) => {
            event.preventDefault()
            mutation.mutate()
          }}
        >
          <p className="sprint-edit-required-hint">* Required fields are marked with an asterisk</p>

          <label className="sprint-edit-field">
            <span>Sprint name *</span>
            <input
              type="text"
              required
              minLength={2}
              maxLength={120}
              value={name}
              onChange={(event) => setName(event.target.value)}
            />
          </label>

          <div className="sprint-edit-date-row">
            <label className="sprint-edit-field">
              <span>Start date *</span>
              <div className="sprint-edit-date-input">
                <Calendar size={14} />
                <input type="datetime-local" value={startDate} onChange={(event) => setStartDate(event.target.value)} />
              </div>
            </label>
            <label className="sprint-edit-field">
              <span>End date *</span>
              <div className="sprint-edit-date-input">
                <Calendar size={14} />
                <input type="datetime-local" value={endDate} onChange={(event) => setEndDate(event.target.value)} />
              </div>
            </label>
          </div>

          <label className="sprint-edit-checkbox toggle-switch" title="Not available yet">
            <input type="checkbox" disabled />
            <span>Automatically complete sprint</span>
          </label>

          <label className="sprint-edit-field">
            <span>Sprint goal</span>
            <textarea
              value={goal}
              onChange={(event) => setGoal(event.target.value)}
              rows={3}
              placeholder=""
            />
          </label>

          {mutation.isError && <p className="form-error">{mutation.error.message}</p>}
          <div className="flex justify-end gap-2 pt-1">
            <button type="button" onClick={onClose} className="secondary-button">Cancel</button>
            <button type="submit" disabled={mutation.isPending} className="primary-button">
              {mutation.isPending ? 'Updating…' : 'Update'}
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}
