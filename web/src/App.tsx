import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { orbitApi } from './api/client'
import * as auth from './api/auth'
import { completeOidcCallback } from './features/auth/oidcPkce'
import { ResetPasswordView } from './features/auth/ResetPasswordView'
import { AcceptInvitationView } from './features/auth/AcceptInvitationView'
import { LoginView } from './features/auth/LoginView'
import { RegisterView } from './features/auth/RegisterView'

import type { Board, BoardColumn, BoardType, PagedResult, Priority, Sprint, ThemePreference, WorkItem, WorkItemStatus } from './api/types'
import { getStoredLogoUrl, setStoredLogoUrl } from './lib/branding'

import './App.css'

// Layout Components
import { Header } from './components/layout/Header'
import { Sidebar } from './components/layout/Sidebar'
import { SubNavigation, type TabType } from './components/layout/SubNavigation'
import { LoadingScreen, ErrorScreen } from './components/layout/FeedbackScreens'

// Feature Components
import { BoardView } from './features/board/BoardView'
import { BacklogView } from './features/backlog/BacklogView'
import { DevelopmentView } from './features/development/DevelopmentView'
import { SummaryView } from './features/summary/SummaryView'
import { CreateWorkItemDialog } from './features/workitems/CreateWorkItemDialog'
import { WorkItemDetailView } from './features/workitems/WorkItemDetailView'
import { BootstrapOnboarding } from './features/onboarding/BootstrapOnboarding'
import { ProjectOnboarding } from './features/onboarding/ProjectOnboarding'
import { SettingsView } from './features/settings/SettingsView'
import type { SettingsSection } from './features/settings/SettingsView'
import { HomeView } from './features/home/HomeView'
import { CreateWorkspaceDialog } from './features/workspaces/CreateWorkspaceDialog'
import { applyTypographySetting } from './typography'

type ActiveView = 'home' | 'project' | 'settings' | 'workitem'

function App() {
  const queryClient = useQueryClient()
  const [authSession, setAuthSession] = useState(auth.getCurrentSession())
  const [selectedProjectId, setSelectedProjectId] = useState<string | null>(null)
  const [createOpen, setCreateOpen] = useState(false)
  const [createWorkspaceOpen, setCreateWorkspaceOpen] = useState(false)
  const [editingWorkItemId, setEditingWorkItemId] = useState<string | null>(null)
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)
  const [online, setOnline] = useState(navigator.onLine)
  const [activeTab, setActiveTab] = useState<TabType>('Backlog')
  const [activeView, setActiveView] = useState<ActiveView>('project')
  const [settingsSection, setSettingsSection] = useState<SettingsSection>('profile')
  const [oidcError, setOidcError] = useState<string | null>(null)

  const [registerRequested, setRegisterRequested] = useState(() => {
    const url = new URL(window.location.href)
    const requested = new URLSearchParams(url.hash.slice(1)).get('register') !== null
    if (requested) {
      url.hash = ''
      window.history.replaceState(window.history.state, '', `${url.pathname}${url.search}`)
    }
    return requested
  })
  const [resetToken] = useState(() => {
    const url = new URL(window.location.href)
    const token = new URLSearchParams(url.hash.slice(1)).get('resetToken') ?? url.searchParams.get('resetToken')
    if (token) {
      url.hash = ''
      url.searchParams.delete('resetToken')
      window.history.replaceState(window.history.state, '', `${url.pathname}${url.search}`)
    }
    return token
  })
  const [invitation] = useState(() => {
    const url = new URL(window.location.href)
    const fragment = new URLSearchParams(url.hash.slice(1))
    const token = fragment.get('invitationToken')
    const tenantId = fragment.get('invitationTenantId')
    if (token || tenantId) {
      url.hash = ''
      window.history.replaceState(window.history.state, '', `${url.pathname}${url.search}`)
    }
    return token && tenantId ? { token, tenantId } : null
  })

  useEffect(() => {
    const updateOnlineState = () => setOnline(navigator.onLine)
    window.addEventListener('online', updateOnlineState)
    window.addEventListener('offline', updateOnlineState)
    return () => {
      window.removeEventListener('online', updateOnlineState)
      window.removeEventListener('offline', updateOnlineState)
    }
  }, [])

  useEffect(() => auth.subscribe(() => setAuthSession(auth.getCurrentSession())), [])

  // Runs once on every load, including immediately after an OIDC redirect back to the app root -
  // there is no client-side router to restore the view the user started from, so the callback is
  // handled globally here rather than inside whichever panel initiated it.
  useEffect(() => {
    completeOidcCallback()
      .then((result) => {
        if (!result) return
        if (result.mode === 'login') {
          auth.setExternalAccessToken(result.accessToken)
          void queryClient.resetQueries()
          return
        }

        if (!result.idToken) {
          setOidcError('The identity provider did not return an identity proof.')
          return
        }

        if (result.mode === 'accept-invitation') {
          if (!result.pendingInvitation) {
            setOidcError('The pending invitation could not be recovered after sign-in.')
            return
          }
          const { token, tenantId, displayName } = result.pendingInvitation
          orbitApi
            .acceptInvitationWithExternalIdentity(tenantId, { token, externalIdToken: result.idToken, displayName })
            .then(async () => {
              auth.setExternalAccessToken(result.accessToken)
              await queryClient.resetQueries()
            })
            .catch((acceptError: Error) => setOidcError(acceptError.message))
          return
        }

        orbitApi
          .linkExternalIdentity(result.idToken)
          .then(() => queryClient.invalidateQueries({ queryKey: ['linked-identities'] }))
          .catch((linkError: Error) => setOidcError(linkError.message))
      })
      .catch((callbackError: Error) => setOidcError(callbackError.message))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // "Sign in with Google" is a server-brokered redirect (Program.cs's /auth/google/start ->
  // Google -> /auth/google/callback), so it lands back on the app root the same way the OIDC
  // callback above does, carrying a one-time handoff code rather than tokens directly.
  useEffect(() => {
    const url = new URL(window.location.href)
    const handoffCode = url.searchParams.get('googleAuth')
    const googleError = url.searchParams.get('googleAuthError')
    if (!handoffCode && !googleError) return

    url.searchParams.delete('googleAuth')
    url.searchParams.delete('googleAuthError')
    window.history.replaceState(window.history.state, '', `${url.pathname}${url.search}`)

    if (googleError) {
      setOidcError(googleError)
      return
    }

    auth.exchangeGoogleHandoff(handoffCode!)
      .then(() => queryClient.resetQueries())
      .catch((exchangeError: Error) => setOidcError(exchangeError.message))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const bootstrapQuery = useQuery({ queryKey: ['bootstrap-status'], queryFn: orbitApi.getBootstrapStatus })
  const projectsQuery = useQuery({
    queryKey: ['projects'],
    queryFn: () => orbitApi.listProjects(),
    enabled: bootstrapQuery.data?.initializationRequired === false,
  })
  const projects = projectsQuery.data?.items ?? []
  const choicesQuery = useQuery({ queryKey: ['choices'], queryFn: orbitApi.getChoices, staleTime: Infinity })
  const itemTypesQuery = useQuery({
    queryKey: ['work-item-types'],
    queryFn: orbitApi.listWorkItemTypes,
    enabled: bootstrapQuery.data?.initializationRequired === false,
  })
  const profileQuery = useQuery({
    queryKey: ['profile'],
    queryFn: orbitApi.getProfile,
    enabled: bootstrapQuery.data?.initializationRequired === false,
  })
  const typographyQuery = useQuery({
    queryKey: ['typography-settings'],
    queryFn: orbitApi.getTypographySettings,
    enabled: bootstrapQuery.data?.initializationRequired === false,
    staleTime: Infinity,
  })
  const workspaceSettingsQuery = useQuery({
    queryKey: ['workspace-settings'],
    queryFn: orbitApi.getWorkspaceSettings,
    enabled: bootstrapQuery.data?.initializationRequired === false,
  })
  useEffect(() => {
    if (workspaceSettingsQuery.data?.logoUrl !== undefined) {
      setStoredLogoUrl(workspaceSettingsQuery.data.logoUrl)
    }
  }, [workspaceSettingsQuery.data?.logoUrl])
  useEffect(() => {
    if (typographyQuery.data) {
      applyTypographySetting(typographyQuery.data)
    }
  }, [typographyQuery.data])
  const membersQuery = useQuery({
    queryKey: ['memberships'],
    queryFn: orbitApi.listMemberships,
    enabled: bootstrapQuery.data?.initializationRequired === false,
  })
  const members = membersQuery.data ?? []
  const accountWorkspacesQuery = useQuery({
    queryKey: ['account-workspaces'],
    queryFn: orbitApi.listAccountWorkspaces,
    enabled: authSession !== null,
  })
  const siteCapabilitiesQuery = useQuery({
    queryKey: ['site-capabilities'],
    queryFn: orbitApi.getSiteCapabilities,
    enabled: authSession !== null,
  })
  const workspaceSwitchMutation = useMutation({
    mutationFn: auth.switchWorkspace,
    onSuccess: async () => {
      setSelectedProjectId(null)
      setActiveView('project')
      await queryClient.resetQueries()
    },
  })
  const createWorkspaceMutation = useMutation({
    mutationFn: orbitApi.createWorkspace,
    onSuccess: async (workspace) => {
      setCreateWorkspaceOpen(false)
      setSelectedProjectId(null)
      setActiveView('project')
      await queryClient.invalidateQueries({ queryKey: ['account-workspaces'] })
      workspaceSwitchMutation.mutate(workspace.id)
    },
  })

  useEffect(() => {
    if (profileQuery.data) document.documentElement.dataset.theme = profileQuery.data.theme.toLowerCase()
  }, [profileQuery.data])

  useEffect(() => {
    if (!selectedProjectId && projectsQuery.data?.items[0]) setSelectedProjectId(projectsQuery.data.items[0].id)
  }, [projectsQuery.data, selectedProjectId])

  const selectedProject = projects.find((project) => project.id === selectedProjectId)
  const workItemsQuery = useQuery({
    queryKey: ['work-items', selectedProjectId],
    queryFn: () => orbitApi.listWorkItems(selectedProjectId ?? ''),
    enabled: Boolean(selectedProjectId),
  })
  const workItems = workItemsQuery.data?.items ?? []
  const workItemsTruncated = (workItemsQuery.data?.totalCount ?? 0) > workItems.length
  const projectSettingQuery = useQuery({
    queryKey: ['project-settings', selectedProjectId],
    queryFn: () => orbitApi.getProjectSettings(selectedProjectId ?? ''),
    enabled: Boolean(selectedProjectId),
  })
  const boardQuery = useQuery({
    queryKey: ['board', selectedProjectId],
    queryFn: () => orbitApi.getBoard(selectedProjectId ?? ''),
    enabled: Boolean(selectedProjectId),
  })
  const sprintsQuery = useQuery({
    queryKey: ['sprints', selectedProjectId],
    queryFn: () => orbitApi.listSprints(selectedProjectId ?? ''),
    enabled: Boolean(selectedProjectId),
  })
  const sprints = sprintsQuery.data ?? []

  const statusMutation = useMutation({
    mutationFn: ({ workItem, status }: { workItem: WorkItem; status: WorkItemStatus }) =>
      orbitApi.changeStatus(workItem, status),
    onSuccess: (updated) => {
      queryClient.setQueryData<PagedResult<WorkItem>>(['work-items', updated.projectId], (current) =>
        current && { ...current, items: current.items.map((item) => (item.id === updated.id ? updated : item)) },
      )
    },
  })
  const reorderMutation = useMutation({
    mutationFn: ({
      workItem,
      neighbors,
    }: {
      workItem: WorkItem
      neighbors: { beforeId: string | null; afterId: string | null }
    }) => orbitApi.reorderWorkItem(workItem, neighbors),
    onSuccess: (updated) => {
      queryClient.setQueryData<PagedResult<WorkItem>>(['work-items', updated.projectId], (current) => {
        if (!current) return current
        const items = current.items.map((item) => (item.id === updated.id ? updated : item))
        items.sort((a, b) => a.rank - b.rank)
        return { ...current, items }
      })
    },
  })
  const repositoryUrlMutation = useMutation({
    mutationFn: (repositoryUrl: string | null) => {
      const setting = projectSettingQuery.data
      if (!setting) throw new Error('Project settings are still loading.')
      return orbitApi.updateProjectSettings({ ...setting, repositoryUrl })
    },
    onSuccess: (updated) => queryClient.setQueryData(['project-settings', selectedProjectId], updated),
  })
  const boardMutation = useMutation({
    mutationFn: (input: { name: string; type: BoardType; columns: BoardColumn[] }) => {
      const board = boardQuery.data
      if (!board) throw new Error('Board is still loading.')
      return orbitApi.updateBoard({ ...board, ...input })
    },
    onSuccess: (updated: Board) => queryClient.setQueryData(['board', selectedProjectId], updated),
  })
  const patchSprint = (updated: Sprint) => {
    queryClient.setQueryData<Sprint[]>(['sprints', updated.projectId], (current) =>
      current
        ? current.some((sprint) => sprint.id === updated.id)
          ? current.map((sprint) => (sprint.id === updated.id ? updated : sprint))
          : [...current, updated]
        : [updated],
    )
  }
  const createSprintMutation = useMutation({
    mutationFn: (name: string) => orbitApi.createSprint(selectedProjectId ?? '', name),
    onSuccess: patchSprint,
  })
  const startSprintMutation = useMutation({
    mutationFn: ({ sprint, goal, startDate, endDate }: { sprint: Sprint; goal: string | null; startDate: string | null; endDate: string | null }) =>
      orbitApi.startSprint(sprint, { goal, startDate, endDate }),
    onSuccess: patchSprint,
  })
  const completeSprintMutation = useMutation({
    mutationFn: ({ sprint, rolloverTargetSprintId }: { sprint: Sprint; rolloverTargetSprintId: string | null }) =>
      orbitApi.completeSprint(sprint, rolloverTargetSprintId),
    onSuccess: patchSprint,
  })
  const reopenSprintMutation = useMutation({
    mutationFn: (sprint: Sprint) => orbitApi.reopenSprint(sprint),
    onSuccess: patchSprint,
  })
  const assignToSprintMutation = useMutation({
    mutationFn: ({ workItemId, sprintId }: { workItemId: string; sprintId: string }) =>
      orbitApi.assignWorkItemToSprint(workItemId, sprintId),
    onSuccess: patchSprint,
  })
  const removeFromSprintMutation = useMutation({
    mutationFn: (workItemId: string) => orbitApi.removeWorkItemFromSprint(workItemId),
    onSuccess: patchSprint,
  })
  const themeMutation = useMutation({
    mutationFn: (theme: ThemePreference) => {
      const profile = profileQuery.data
      if (!profile) throw new Error('Profile is still loading.')
      return orbitApi.updatePreferences(profile, {
        locale: profile.locale,
        timeZone: profile.timeZone,
        theme,
        density: profile.density,
        reduceMotion: profile.reduceMotion,
        highContrast: profile.highContrast,
      })
    },
    onSuccess: (profile) => {
      queryClient.setQueryData(['profile'], profile)
      document.documentElement.dataset.theme = profile.theme.toLowerCase()
    },
  })

  const currentLogoUrl = workspaceSettingsQuery.data?.logoUrl ?? getStoredLogoUrl()

  if (resetToken) return <ResetPasswordView token={resetToken} logoUrl={currentLogoUrl} />
  if (invitation) return <AcceptInvitationView token={invitation.token} tenantId={invitation.tenantId} logoUrl={currentLogoUrl} />

  if (bootstrapQuery.isPending || choicesQuery.isPending) return <LoadingScreen />
  if (bootstrapQuery.isError || choicesQuery.isError) {
    return <ErrorScreen message={(bootstrapQuery.error ?? choicesQuery.error)?.message ?? 'Unable to load Orbit.'} />
  }

  if (bootstrapQuery.data.initializationRequired) return <BootstrapOnboarding />

  if (!authSession && registerRequested) {
    return (
      <RegisterView
        logoUrl={currentLogoUrl}
        onSuccess={() => void queryClient.resetQueries()}
        onBack={() => setRegisterRequested(false)}
      />
    )
  }

  if (!authSession) return <LoginView logoUrl={currentLogoUrl} onRegister={() => setRegisterRequested(true)} />

  if (projectsQuery.isPending) return <LoadingScreen />
  if (projectsQuery.isError) return <ErrorScreen message={projectsQuery.error.message} />

  return (
    <div className="min-h-screen bg-white">
      <Header
        online={online}
        profile={profileQuery.data}
        logoUrl={workspaceSettingsQuery.data?.logoUrl}
        onCreateClick={selectedProject ? () => setCreateOpen(true) : undefined}
        onHomeClick={() => setActiveView('home')}
        onOpenSettings={(section) => { setSettingsSection(section); setActiveView('settings') }}
        onThemeChange={(theme) => themeMutation.mutate(theme)}
        workspaces={accountWorkspacesQuery.data}
        currentWorkspaceId={authSession?.workspaceId}
        switchingWorkspace={workspaceSwitchMutation.isPending}
        onWorkspaceChange={(workspaceId) => workspaceSwitchMutation.mutate(workspaceId)}
        onCreateWorkspace={siteCapabilitiesQuery.data?.canCreateWorkspace ? () => setCreateWorkspaceOpen(true) : undefined}
      />
      {workspaceSwitchMutation.isError && (
        <div className="error-banner m-4">{workspaceSwitchMutation.error.message}</div>
      )}
      {oidcError && (
        <div className="error-banner m-4 flex items-center justify-between">
          <span>{oidcError}</span>
          <button onClick={() => setOidcError(null)} className="ml-4 text-sm font-medium underline">Dismiss</button>
        </div>
      )}
      {createWorkspaceOpen && (
        <CreateWorkspaceDialog
          pending={createWorkspaceMutation.isPending}
          error={createWorkspaceMutation.error}
          onCreate={(name) => createWorkspaceMutation.mutate(name)}
          onClose={() => setCreateWorkspaceOpen(false)}
        />
      )}

      <div className="flex">
        <Sidebar
          mobileMenuOpen={mobileMenuOpen}
          setMobileMenuOpen={setMobileMenuOpen}
          projects={projects}
          selectedProjectId={selectedProjectId ?? undefined}
          onSelectProject={(projectId) => {
            setSelectedProjectId(projectId)
            setActiveView('project')
          }}
          activeView={activeView}
          onHomeClick={() => setActiveView('home')}
          onOpenSettings={(section) => {
            setSettingsSection(section)
            setActiveView('settings')
          }}
        />
        
        <main className="region-middle flex-1 lg:ml-[240px] min-h-[calc(100vh-48px)] bg-white relative">
          {projects.length === 0 ? <ProjectOnboarding /> : <>
          {activeView === 'home' && <HomeView profile={profileQuery.data} projects={projects} workItems={workItems} onCreate={() => setCreateOpen(true)} onOpenProject={(projectId) => { setSelectedProjectId(projectId); setActiveView('project') }} />}
          {activeView === 'settings' && selectedProject && (
            <SettingsView key={settingsSection} project={selectedProject} initialSection={settingsSection} onClose={() => setActiveView('project')} />
          )}
          {activeView === 'workitem' && (() => {
            const openWorkItem = workItems.find((item) => item.id === editingWorkItemId)
            return openWorkItem ? (
              <WorkItemDetailView
                item={openWorkItem}
                project={selectedProject}
                workItems={workItems}
                profile={profileQuery.data}
                members={members}
                priorities={(choicesQuery.data?.priorities ?? []).map((choice) => choice.value as Priority)}
                onBack={() => { setActiveView('project'); setEditingWorkItemId(null) }}
                onStatusChange={(workItem, status) => statusMutation.mutate({ workItem, status })}
                onOpenWorkItem={(workItem) => setEditingWorkItemId(workItem.id)}
                sprints={sprints}
              />
            ) : null
          })()}
          {activeView === 'project' && <>
            <SubNavigation
              project={selectedProject}
              activeTab={activeTab}
              setActiveTab={setActiveTab}
            />
            <div className="relative">
            {workItemsTruncated && (
              <div className="mx-8 mt-4 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                Showing {workItems.length} of {workItemsQuery.data?.totalCount} work items — narrow with a filter to see the rest.
              </div>
            )}
            {activeTab === 'Summary' && (
              <SummaryView
                workItems={workItems}
                profile={profileQuery.data}
                members={members}
                onOpenWorkItem={(workItem) => {
                  setEditingWorkItemId(workItem.id)
                  setActiveView('workitem')
                }}
                onSwitchTab={(tab) => setActiveTab(tab)}
              />
            )}

            {activeTab === 'Backlog' && (
              <BacklogView
                workItems={workItems}
                projectId={selectedProjectId ?? ''}
                members={members}
                sprints={sprints}
                sprintsLoading={sprintsQuery.isPending}
                onCreateSprint={(name) => createSprintMutation.mutate(name)}
                onStartSprint={(sprint) => startSprintMutation.mutate({ sprint, goal: null, startDate: null, endDate: null })}
                onCompleteSprint={(sprint, rolloverTargetSprintId) => completeSprintMutation.mutate({ sprint, rolloverTargetSprintId })}
                onReopenSprint={(sprint) => reopenSprintMutation.mutate(sprint)}
                onAssignToSprint={(workItemId, sprintId) => assignToSprintMutation.mutate({ workItemId, sprintId })}
                onRemoveFromSprint={(workItemId) => removeFromSprintMutation.mutate(workItemId)}
                onOpenWorkItem={(workItem) => { setEditingWorkItemId(workItem.id); setActiveView('workitem') }}
                error={
                  createSprintMutation.error?.message ??
                  startSprintMutation.error?.message ??
                  completeSprintMutation.error?.message ??
                  reopenSprintMutation.error?.message ??
                  assignToSprintMutation.error?.message ??
                  removeFromSprintMutation.error?.message ??
                  null
                }
              />
            )}

            {activeTab === 'Board' && (
              <div className="p-8">
                {statusMutation.isError && <div className="error-banner">{statusMutation.error.message}</div>}
                {reorderMutation.isError && <div className="error-banner">{reorderMutation.error.message}</div>}
                <BoardView
                  projectName={selectedProject?.name ?? ''}
                  board={boardQuery.data}
                  loading={boardQuery.isPending}
                  mutation={boardMutation}
                  onSave={(input) => boardMutation.mutate(input)}
                  workItems={workItems}
                  workItemsLoading={workItemsQuery.isPending}
                  onStatusChange={(workItem, status) => statusMutation.mutate({ workItem, status })}
                  onReorder={(workItem, neighbors) => reorderMutation.mutate({ workItem, neighbors })}
                  onOpen={(workItem) => { setEditingWorkItemId(workItem.id); setActiveView('workitem') }}
                />
              </div>
            )}

            {activeTab === 'Development' && (
              <DevelopmentView
                projectSetting={projectSettingQuery.data}
                loading={projectSettingQuery.isPending}
                mutation={repositoryUrlMutation}
                onSaveRepositoryUrl={(url) => repositoryUrlMutation.mutate(url)}
              />
            )}
            </div>
          </>}
          </>}
        </main>
      </div>

      {createOpen && selectedProject && (
        <CreateWorkItemDialog
          project={selectedProject}
          workItems={workItems}
          profile={profileQuery.data}
          members={members}
          types={(itemTypesQuery.data ?? []).filter((itemType) => itemType.enabled)}
          priorities={(choicesQuery.data?.priorities ?? []).map((choice) => choice.value as Priority)}
          sprints={sprints}
          onClose={() => setCreateOpen(false)}
        />
      )}

    </div>
  )
}

export default App
