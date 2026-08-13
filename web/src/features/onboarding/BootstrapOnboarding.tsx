import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { orbitApi } from '../../api/client'
import { OnboardingShell } from './OnboardingShell'

export function BootstrapOnboarding() {
  const queryClient = useQueryClient()
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [workspaceName, setWorkspaceName] = useState('')
  const mutation = useMutation({
    mutationFn: orbitApi.bootstrap,
    onSuccess: async () => {
      queryClient.setQueryData(['bootstrap-status'], { initializationRequired: false })
      await queryClient.invalidateQueries({ queryKey: ['projects'] })
    },
  })

  return (
    <OnboardingShell
      eyebrow="First-run setup"
      title="Initialize Orbit"
      description="Create the first site administrator and workspace. This setup can run only once."
    >
      <form onSubmit={(event) => {
        event.preventDefault()
        mutation.mutate({ displayName, email, password, workspaceName })
      }}>
        <label>Display name<input required minLength={2} maxLength={120} autoComplete="name" value={displayName} onChange={(event) => setDisplayName(event.target.value)} /></label>
        <label>Email<input required type="email" maxLength={320} autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} /></label>
        <label>Password<input required type="password" minLength={12} maxLength={128} autoComplete="new-password" value={password} onChange={(event) => setPassword(event.target.value)} /></label>
        <label>Workspace name<input required minLength={2} maxLength={120} value={workspaceName} onChange={(event) => setWorkspaceName(event.target.value)} /></label>
        {mutation.isError && <p className="form-error">{mutation.error.message}</p>}
        <button className="primary-button" disabled={mutation.isPending}>{mutation.isPending ? 'Initializing…' : 'Initialize Orbit'}</button>
      </form>
    </OnboardingShell>
  )
}
