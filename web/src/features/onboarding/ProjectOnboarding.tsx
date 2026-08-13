import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { orbitApi } from '../../api/client'
import { OnboardingShell } from './OnboardingShell'

export function ProjectOnboarding() {
  const queryClient = useQueryClient()
  const [key, setKey] = useState('ORB')
  const [name, setName] = useState('Orbit delivery')
  const mutation = useMutation({
    mutationFn: orbitApi.createProject,
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: ['projects'] }),
  })
  return (
    <OnboardingShell
      eyebrow="Start shipping"
      title="Create your first project"
      description="A project owns its board, backlog, and work item keys."
    >
      <form onSubmit={(event) => { event.preventDefault(); mutation.mutate({ key, name }) }}>
        <label>Project key<input required pattern="[A-Za-z0-9]{2,10}" value={key} onChange={(event) => setKey(event.target.value.toUpperCase())} /></label>
        <label>Project name<input required minLength={2} maxLength={120} value={name} onChange={(event) => setName(event.target.value)} /></label>
        {mutation.isError && <p className="form-error">{mutation.error.message}</p>}
        <button className="primary-button" disabled={mutation.isPending}>{mutation.isPending ? 'Creating…' : 'Create project'}</button>
      </form>
    </OnboardingShell>
  )
}
