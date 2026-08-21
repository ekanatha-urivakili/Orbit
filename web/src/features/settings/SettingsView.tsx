import { useState, type ReactNode } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Bell, Building2, FolderCog, LockKeyhole, Paintbrush, Tags, UserRound, Users, UsersRound, X } from 'lucide-react'
import { orbitApi } from '../../api/client'
import * as auth from '../../api/auth'
import { useIsAuthenticated } from '../../hooks/useIsAuthenticated'
import { LoginForm } from '../auth/LoginView'
import { setStoredLogoUrl } from '../../lib/branding'
import { applyTheme } from '../../lib/theme'
import { getOidcConfig, startOidcLogin } from '../auth/oidcPkce'
import { Field, Hint, SubmitRow } from '../../components/form/Field'
import { SearchableSelect } from '../../components/form/SearchableSelect'
import type {
  CreateMembershipInput,
  CustomFieldChoiceOptionInput,
  CustomFieldDefinition,
  CustomFieldType,
  DensityPreference,
  DigestCadence,
  NotificationPreference,
  PrincipalType,
  Priority,
  Profile,
  Project,
  ProjectRole,
  ProjectSetting,
  Team,
  TenantMembership,
  TenantRole,
  ThemePreference,
  TypographySetting,
  WorkItemType,
  WorkItemTypeDefinition,
  WorkspaceInvitationStatus,
  WorkspaceSetting,
} from '../../api/types'
import { applyTypographySetting } from '../../typography'

export type SettingsSection = 'profile' | 'notifications' | 'workspace' | 'project' | 'item-types' | 'custom-fields' | 'members' | 'teams' | 'security' | 'appearance'

const sections: Array<{ id: SettingsSection; label: string; icon: typeof UserRound }> = [
  { id: 'profile', label: 'Profile and preferences', icon: UserRound },
  { id: 'notifications', label: 'Notifications', icon: Bell },
  { id: 'members', label: 'Members & roles', icon: UsersRound },
  { id: 'teams', label: 'Teams', icon: Users },
  { id: 'security', label: 'Account security', icon: LockKeyhole },
  { id: 'workspace', label: 'Workspace', icon: Building2 },
  { id: 'appearance', label: 'Appearance', icon: Paintbrush },
  { id: 'project', label: 'Project defaults', icon: FolderCog },
  { id: 'item-types', label: 'Work item types', icon: Tags },
  { id: 'custom-fields', label: 'Custom fields', icon: Tags },
]

export function SettingsView({ project, initialSection = 'profile', onClose }: { project: Project; initialSection?: SettingsSection; onClose: () => void }) {
  const [activeSection, setActiveSection] = useState<SettingsSection>(initialSection)
  const profileQuery = useQuery({ queryKey: ['profile'], queryFn: orbitApi.getProfile })
  const notificationsQuery = useQuery({
    queryKey: ['notification-preferences'],
    queryFn: orbitApi.getNotificationPreferences,
  })
  const workspaceQuery = useQuery({ queryKey: ['workspace-settings'], queryFn: orbitApi.getWorkspaceSettings })
  const typographyQuery = useQuery({ queryKey: ['typography-settings'], queryFn: orbitApi.getTypographySettings })
  const projectQuery = useQuery({
    queryKey: ['project-settings', project.id],
    queryFn: () => orbitApi.getProjectSettings(project.id),
  })
  const itemTypesQuery = useQuery({ queryKey: ['work-item-types'], queryFn: orbitApi.listWorkItemTypes })
  const customFieldsQuery = useQuery({
    queryKey: ['custom-fields', project.id],
    queryFn: () => orbitApi.listCustomFields(project.id),
  })

  return (
    <div className="min-h-[calc(100vh-48px)] bg-[#f7f8fa] w-full">
      <div className="border-b border-gray-200 bg-white px-6 py-4 lg:px-8">
        <div className="w-full flex items-center justify-between gap-4">
          <div>
            <p className="mb-0.5 text-xs font-semibold uppercase tracking-wider text-gray-500">Orbit administration</p>
            <h1 className="text-2xl font-semibold text-gray-900">Settings</h1>
          </div>
          <button onClick={onClose} className="rounded-md p-2 text-gray-500 hover:bg-gray-100" aria-label="Close settings">
            <X size={20} />
          </button>
        </div>
      </div>

      <div className="w-full grid gap-6 px-6 py-6 md:grid-cols-[240px_minmax(0,1fr)] lg:px-8">
        <nav aria-label="Settings sections" className="h-fit rounded-xl border border-gray-200 bg-white p-2 shadow-sm">
          {sections.map(({ id, label, icon: Icon }) => (
            <button
              key={id}
              onClick={() => setActiveSection(id)}
              className={`flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left text-sm font-medium ${activeSection === id ? 'bg-blue-50 text-blue-700' : 'text-gray-700 hover:bg-gray-50'}`}
            >
              <Icon size={18} /> {label}
            </button>
          ))}
        </nav>

        <section className="min-w-0">
          {activeSection === 'profile' && <QueryState query={profileQuery} render={(profile) => <ProfileForm profile={profile} />} />}
          {activeSection === 'notifications' && <QueryState query={notificationsQuery} render={(preference) => <NotificationForm preference={preference} />} />}
          {activeSection === 'workspace' && <QueryState query={workspaceQuery} render={(setting) => <WorkspaceForm setting={setting} />} />}
          {activeSection === 'appearance' && <QueryState query={typographyQuery} render={(setting) => <AppearanceForm setting={setting} />} />}
          {activeSection === 'project' && <QueryState query={projectQuery} render={(setting) => <ProjectForm project={project} setting={setting} itemTypes={itemTypesQuery.data ?? []} />} />}
          {activeSection === 'item-types' && <QueryState query={itemTypesQuery} render={(definitions) => <ItemTypesPanel definitions={definitions} />} />}
          {activeSection === 'custom-fields' && <QueryState query={customFieldsQuery} render={(fields) => <CustomFieldsPanel projectId={project.id} fields={fields} />} />}
          {activeSection === 'members' && <MembersPanel project={project} />}
          {activeSection === 'teams' && <TeamsPanel />}
          {activeSection === 'security' && <SecurityPanel />}
        </section>
      </div>
    </div>
  )
}

interface QueryShape<T> {
  data?: T
  isPending: boolean
  isError: boolean
  error: Error | null
}

function QueryState<T>({ query, render }: { query: QueryShape<T>; render: (data: T) => ReactNode }) {
  if (query.isPending) return <Panel title="Loading settings…"><p className="text-sm text-gray-500">Fetching the latest version.</p></Panel>
  if (query.isError || !query.data) return <Panel title="Unable to load settings"><p className="text-sm text-red-700">{query.error?.message ?? 'Settings are unavailable.'}</p></Panel>
  return render(query.data)
}

function ProfileForm({ profile }: { profile: Profile }) {
  const client = useQueryClient()
  const [displayName, setDisplayName] = useState(profile.displayName)
  const [avatarUrl, setAvatarUrl] = useState(profile.avatarUrl ?? '')
  const [locale, setLocale] = useState(profile.locale)
  const [timeZone, setTimeZone] = useState(profile.timeZone)
  const [theme, setTheme] = useState<ThemePreference>(profile.theme)
  const [density, setDensity] = useState<DensityPreference>(profile.density)
  const [reduceMotion, setReduceMotion] = useState(profile.reduceMotion)
  const [highContrast, setHighContrast] = useState(profile.highContrast)
  const profileMutation = useMutation({
    mutationFn: () => orbitApi.updateProfile(profile, { displayName, avatarUrl: avatarUrl || null }),
    onSuccess: (updated) => client.setQueryData(['profile'], updated),
  })
  const preferenceMutation = useMutation({
    mutationFn: () => orbitApi.updatePreferences(profile, { locale, timeZone, theme, density, reduceMotion, highContrast }),
    onSuccess: (updated) => {
      client.setQueryData(['profile'], updated)
      applyTheme(updated.theme.toLowerCase())
      document.documentElement.dataset.density = updated.density.toLowerCase()
    },
  })

  return (
    <div className="space-y-5">
      <Panel title="Profile" description="Your global Orbit identity across every workspace.">
        <form onSubmit={(event) => { event.preventDefault(); profileMutation.mutate() }} className="space-y-4">
          <Field variant="panel" label="Display name"><input value={displayName} onChange={(event) => setDisplayName(event.target.value)} required minLength={2} maxLength={120} /></Field>
          <Field variant="panel" label="Email"><input value={profile.email} disabled /><Hint variant="panel">Verified email changes require the authentication increment.</Hint></Field>
          <Field variant="panel" label="Avatar URL"><input value={avatarUrl} onChange={(event) => setAvatarUrl(event.target.value)} type="url" placeholder="https://…" /></Field>
          <SubmitRow mutation={profileMutation} />
        </form>
      </Panel>

      <Panel title="Appearance and region" description="These preferences follow your account between workspaces.">
        <form onSubmit={(event) => { event.preventDefault(); preferenceMutation.mutate() }} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field variant="panel" label="Locale"><input value={locale} onChange={(event) => setLocale(event.target.value)} /></Field>
            <Field variant="panel" label="Time zone"><input value={timeZone} onChange={(event) => setTimeZone(event.target.value)} /></Field>
            <Field variant="panel" label="Theme">
              <SearchableSelect
                value={theme}
                onChange={(val) => setTheme(val as ThemePreference)}
                options={['System', 'Light', 'Dark']}
                searchable={false}
              />
            </Field>
            <Field variant="panel" label="Density">
              <SearchableSelect
                value={density}
                onChange={(val) => setDensity(val as DensityPreference)}
                options={['Comfortable', 'Compact']}
                searchable={false}
              />
            </Field>
          </div>
          <Toggle label="Reduce motion" checked={reduceMotion} onChange={setReduceMotion} />
          <Toggle label="Increase interface contrast" checked={highContrast} onChange={setHighContrast} />
          <SubmitRow mutation={preferenceMutation} />
        </form>
      </Panel>
    </div>
  )
}

function NotificationForm({ preference }: { preference: NotificationPreference }) {
  const client = useQueryClient()
  const [draft, setDraft] = useState(preference)
  const mutation = useMutation({
    mutationFn: () => orbitApi.updateNotificationPreferences(draft),
    onSuccess: (updated) => { setDraft(updated); client.setQueryData(['notification-preferences'], updated) },
  })
  const patch = (change: Partial<NotificationPreference>) => setDraft((current) => ({ ...current, ...change }))

  return (
    <Panel title="Notifications" description="Choose how Orbit delivers activity you are allowed to see.">
      <form onSubmit={(event) => { event.preventDefault(); mutation.mutate() }} className="space-y-5">
        <Toggle label="In-app notifications" checked={draft.inAppEnabled} onChange={(checked) => patch({ inAppEnabled: checked })} />
        <Toggle label="Email notifications" checked={draft.emailEnabled} onChange={(checked) => patch({ emailEnabled: checked })} />
        <Toggle label="Notify me about my own changes" checked={draft.selfNotify} onChange={(checked) => patch({ selfNotify: checked })} />
        <Field variant="panel" label="Digest cadence">
          <SearchableSelect
            value={draft.digestCadence}
            onChange={(val) => patch({ digestCadence: val as DigestCadence })}
            options={['None', 'Daily', 'Weekly']}
            searchable={false}
          />
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field variant="panel" label="Quiet hours start"><input type="time" value={draft.quietHoursStart?.slice(0, 5) ?? ''} onChange={(event) => patch({ quietHoursStart: event.target.value || null })} /></Field>
          <Field variant="panel" label="Quiet hours end"><input type="time" value={draft.quietHoursEnd?.slice(0, 5) ?? ''} onChange={(event) => patch({ quietHoursEnd: event.target.value || null })} /></Field>
        </div>
        <SubmitRow mutation={mutation} />
      </form>
    </Panel>
  )
}

function WorkspaceLogoField({ setting }: { setting: WorkspaceSetting }) {
  const client = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const mutation = useMutation({
    mutationFn: async (file: File) => {
      const presigned = await orbitApi.presignWorkspaceLogoUpload(file.name, file.type, file.size)
      await orbitApi.uploadWorkspaceLogoFile(presigned.uploadUrl, file)
      return orbitApi.confirmWorkspaceLogoUpload(presigned.objectKey, setting.version)
    },
    onSuccess: (updated) => {
      setError(null)
      setStoredLogoUrl(updated.logoUrl)
      client.setQueryData(['workspace-settings'], updated)
    },
    onError: (uploadError: Error) => setError(uploadError.message),
  })

  return (
    <Field variant="panel" label="Workspace logo">
      <div className="flex items-center gap-3">
        {setting.logoUrl
          ? <img src={setting.logoUrl} alt="Workspace logo" className="h-10 w-10 rounded object-contain" />
          : <div className="flex h-10 w-10 items-center justify-center rounded bg-[#0052cc] text-sm font-bold text-white">O</div>}
        {setting.canAdminister && (
          <label className="cursor-pointer rounded-md border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50">
            {mutation.isPending ? 'Uploading…' : 'Upload logo'}
            <input
              type="file"
              accept="image/png,image/jpeg,image/gif,image/webp,image/svg+xml"
              className="hidden"
              disabled={mutation.isPending}
              onChange={(event) => {
                const file = event.target.files?.[0]
                if (file) mutation.mutate(file)
                event.target.value = ''
              }}
            />
          </label>
        )}
      </div>
      {error && <p className="mt-1 text-sm text-red-700">{error}</p>}
    </Field>
  )
}

function WorkspaceForm({ setting }: { setting: WorkspaceSetting }) {
  const client = useQueryClient()
  const [draft, setDraft] = useState(setting)
  const mutation = useMutation({
    mutationFn: () => orbitApi.updateWorkspaceSettings(draft),
    onSuccess: (updated) => { setDraft(updated); client.setQueryData(['workspace-settings'], updated) },
  })
  const patch = (change: Partial<WorkspaceSetting>) => setDraft((current) => ({ ...current, ...change }))

  return (
    <Panel title={setting.workspaceName} description="Workspace-wide defaults and member capabilities.">
      <div className="space-y-4">
        <WorkspaceLogoField setting={setting} />
        <form onSubmit={(event) => { event.preventDefault(); mutation.mutate() }} className="space-y-4">
          <Field variant="panel" label="Description"><textarea value={draft.description ?? ''} onChange={(event) => patch({ description: event.target.value || null })} rows={4} maxLength={1000} disabled={!setting.canAdminister} /></Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field variant="panel" label="Default locale"><input value={draft.defaultLocale} onChange={(event) => patch({ defaultLocale: event.target.value })} disabled={!setting.canAdminister} /></Field>
            <Field variant="panel" label="Default time zone"><input value={draft.defaultTimeZone} onChange={(event) => patch({ defaultTimeZone: event.target.value })} disabled={!setting.canAdminister} /></Field>
          </div>
          <Toggle label="Allow members to create projects" checked={draft.allowMemberProjectCreation} onChange={(checked) => patch({ allowMemberProjectCreation: checked })} disabled={!setting.canAdminister} />
          {setting.canAdminister ? <SubmitRow mutation={mutation} /> : <Hint variant="panel">You need workspace administrator permission to edit these settings.</Hint>}
        </form>
      </div>
    </Panel>
  )
}

const fontFamilyOptions = [
  { value: 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif', label: 'Inter (default)' },
  { value: 'ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif', label: 'System UI' },
  { value: 'Georgia, "Times New Roman", serif', label: 'Georgia (serif)' },
  { value: '"Courier New", ui-monospace, monospace', label: 'Courier (monospace)' },
]

function RegionFontFields({
  legend,
  family,
  color,
  sizePx,
  disabled,
  onFamilyChange,
  onColorChange,
  onSizeChange,
}: {
  legend: string
  family: string
  color: string
  sizePx: number
  disabled: boolean
  onFamilyChange: (value: string) => void
  onColorChange: (value: string) => void
  onSizeChange: (value: number) => void
}) {
  return (
    <fieldset className="rounded-lg border border-gray-200 p-4">
      <legend className="px-1 text-sm font-semibold text-gray-800">{legend}</legend>
      <div className="mt-2 grid gap-4 sm:grid-cols-3">
        <Field variant="panel" label="Font family">
          <SearchableSelect
            value={family}
            onChange={onFamilyChange}
            options={fontFamilyOptions}
            searchable={false}
            disabled={disabled}
          />
        </Field>
        <Field variant="panel" label="Text color">
          <input type="color" value={color} disabled={disabled} onChange={(event) => onColorChange(event.target.value)} className="h-10 w-full cursor-pointer p-1" />
        </Field>
        <Field variant="panel" label="Font size (px)">
          <input type="number" min={10} max={24} value={sizePx} disabled={disabled} onChange={(event) => onSizeChange(Number(event.target.value))} />
        </Field>
      </div>
    </fieldset>
  )
}

function AppearanceForm({ setting }: { setting: TypographySetting }) {
  const client = useQueryClient()
  const [draft, setDraft] = useState(setting)
  const disabled = !setting.canAdminister
  const mutation = useMutation({
    mutationFn: () => orbitApi.updateTypographySettings(draft),
    onSuccess: (updated) => {
      setDraft(updated)
      client.setQueryData(['typography-settings'], updated)
      applyTypographySetting(updated)
    },
  })
  const patch = (change: Partial<TypographySetting>) => setDraft((current) => ({ ...current, ...change }))

  return (
    <div className="space-y-5">
      <Panel title="Appearance" description="Font family, color, and size for each area of Orbit, plus the size used by every textbox and dropdown.">
        <form onSubmit={(event) => { event.preventDefault(); mutation.mutate() }} className="space-y-4">
          <RegionFontFields
            legend="Left navigation"
            family={draft.leftFontFamily}
            color={draft.leftFontColor}
            sizePx={draft.leftFontSizePx}
            disabled={disabled}
            onFamilyChange={(value) => patch({ leftFontFamily: value })}
            onColorChange={(value) => patch({ leftFontColor: value })}
            onSizeChange={(value) => patch({ leftFontSizePx: value })}
          />
          <RegionFontFields
            legend="Main content"
            family={draft.middleFontFamily}
            color={draft.middleFontColor}
            sizePx={draft.middleFontSizePx}
            disabled={disabled}
            onFamilyChange={(value) => patch({ middleFontFamily: value })}
            onColorChange={(value) => patch({ middleFontColor: value })}
            onSizeChange={(value) => patch({ middleFontSizePx: value })}
          />
          <RegionFontFields
            legend="Detail panel"
            family={draft.rightFontFamily}
            color={draft.rightFontColor}
            sizePx={draft.rightFontSizePx}
            disabled={disabled}
            onFamilyChange={(value) => patch({ rightFontFamily: value })}
            onColorChange={(value) => patch({ rightFontColor: value })}
            onSizeChange={(value) => patch({ rightFontSizePx: value })}
          />
          <fieldset className="rounded-lg border border-gray-200 p-4">
            <legend className="px-1 text-sm font-semibold text-gray-800">Textboxes & dropdowns</legend>
            <div className="mt-2 grid gap-4 sm:grid-cols-2">
              <Field variant="panel" label="Control height (px)">
                <input type="number" min={24} max={56} value={draft.controlHeightPx} disabled={disabled} onChange={(event) => patch({ controlHeightPx: Number(event.target.value) })} />
              </Field>
              <Field variant="panel" label="Text size inside controls (px)">
                <input type="number" min={10} max={24} value={draft.controlFontSizePx} disabled={disabled} onChange={(event) => patch({ controlFontSizePx: Number(event.target.value) })} />
              </Field>
            </div>
          </fieldset>
          {setting.canAdminister ? <SubmitRow mutation={mutation} /> : <Hint variant="panel">You need workspace administrator permission to edit appearance settings.</Hint>}
        </form>
      </Panel>
    </div>
  )
}

function ProjectForm({ project, setting, itemTypes }: { project: Project; setting: ProjectSetting; itemTypes: WorkItemTypeDefinition[] }) {
  const client = useQueryClient()
  const [draft, setDraft] = useState(setting)
  const mutation = useMutation({
    mutationFn: () => orbitApi.updateProjectSettings(draft),
    onSuccess: (updated) => { setDraft(updated); client.setQueryData(['project-settings', project.id], updated) },
  })
  const patch = (change: Partial<ProjectSetting>) => setDraft((current) => ({ ...current, ...change }))
  const types = itemTypes.filter((itemType) => itemType.enabled && itemType.id !== 'Subtask')
  const priorities: Priority[] = ['Lowest', 'Low', 'Medium', 'High', 'Highest']

  return (
    <Panel title={`${project.name} defaults`} description="Defaults applied when the project creates new work.">
      <form onSubmit={(event) => { event.preventDefault(); mutation.mutate() }} className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field variant="panel" label="Default work item type">
            <SearchableSelect
              value={draft.defaultWorkItemType}
              onChange={(val) => patch({ defaultWorkItemType: val as WorkItemType })}
              options={types.map((type) => ({ value: type.id, label: type.label }))}
              searchPlaceholder="Search work types…"
            />
          </Field>
          <Field variant="panel" label="Default priority">
            <SearchableSelect
              value={draft.defaultPriority}
              onChange={(val) => patch({ defaultPriority: val as Priority })}
              options={priorities.map((priority) => ({ value: priority, label: priority }))}
              searchPlaceholder="Search priority…"
            />
          </Field>
        </div>
        <Toggle label="Enable releases" checked={draft.enableReleases} onChange={(checked) => patch({ enableReleases: checked })} />
        <Toggle label="Enable time tracking" checked={draft.enableTimeTracking} onChange={(checked) => patch({ enableTimeTracking: checked })} />
        <SubmitRow mutation={mutation} />
      </form>
    </Panel>
  )
}

function ItemTypesPanel({ definitions }: { definitions: WorkItemTypeDefinition[] }) {
  return (
    <Panel title="Work item types" description="Workspace labels and availability. Stable IDs preserve existing work when labels change.">
      <div className="space-y-3">
        {definitions.map((definition) => <ItemTypeRow key={definition.id} definition={definition} />)}
      </div>
    </Panel>
  )
}

function ItemTypeRow({ definition }: { definition: WorkItemTypeDefinition }) {
  const client = useQueryClient()
  const [draft, setDraft] = useState(definition)
  const mutation = useMutation({
    mutationFn: () => orbitApi.updateWorkItemType(draft),
    onSuccess: (updated) => {
      setDraft(updated)
      client.setQueryData<WorkItemTypeDefinition[]>(['work-item-types'], (current) =>
        current?.map((itemType) => itemType.id === updated.id ? updated : itemType),
      )
    },
  })

  return (
    <form onSubmit={(event) => { event.preventDefault(); mutation.mutate() }} className="rounded-lg border border-gray-200 p-4">
      <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_100px_auto] sm:items-end">
        <Field variant="panel" label={`${definition.id} label`}><input value={draft.label} maxLength={80} disabled={!definition.canAdminister} onChange={(event) => setDraft((current) => ({ ...current, label: event.target.value }))} /></Field>
        <Field variant="panel" label="Order"><input type="number" min={0} max={10000} value={draft.order} disabled={!definition.canAdminister} onChange={(event) => setDraft((current) => ({ ...current, order: Number(event.target.value) }))} /></Field>
        <Toggle label="Enabled" checked={draft.enabled} disabled={!definition.canAdminister} onChange={(enabled) => setDraft((current) => ({ ...current, enabled }))} />
      </div>
      <Field variant="panel" label="Description"><input value={draft.description} maxLength={500} disabled={!definition.canAdminister} onChange={(event) => setDraft((current) => ({ ...current, description: event.target.value }))} /></Field>
      {definition.canAdminister && <SubmitRow mutation={mutation} />}
    </form>
  )
}

const customFieldTypes: CustomFieldType[] = ['Text', 'Number', 'Date', 'SingleChoice', 'MultiChoice', 'Checkbox']
const choiceFieldTypes: CustomFieldType[] = ['SingleChoice', 'MultiChoice']
const workItemTypesForScreens: WorkItemType[] =
  ['Initiative', 'Epic', 'Task', 'Story', 'Spike', 'Test', 'Feature', 'Request', 'Bug', 'Subtask']
const blankCustomField = {
  key: '',
  label: '',
  fieldType: 'Text' as CustomFieldType,
  required: false,
  order: 0,
  choiceOptionsText: '',
  applicableTypes: [] as WorkItemType[],
}

function ApplicableTypesPicker({
  value,
  onChange,
}: {
  value: WorkItemType[]
  onChange: (types: WorkItemType[]) => void
}) {
  return (
    <Field variant="panel" label="Applies to (all types if none selected)">
      <div className="flex flex-wrap gap-3">
        {workItemTypesForScreens.map((type) => (
          <label key={type} className="inline-flex items-center gap-1.5 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={value.includes(type)}
              onChange={() =>
                onChange(value.includes(type) ? value.filter((t) => t !== type) : [...value, type])
              }
            />
            {type}
          </label>
        ))}
      </div>
    </Field>
  )
}

function parseChoiceOptionsText(
  text: string,
  existing: CustomFieldDefinition['choiceOptions'],
): CustomFieldChoiceOptionInput[] {
  const existingByLabel = new Map(existing.map((option) => [option.label.toLowerCase(), option.id]))
  const seen = new Set<string>()
  return text
    .split(',')
    .map((label) => label.trim())
    .filter((label) => {
      if (label.length === 0) return false
      const lower = label.toLowerCase()
      if (seen.has(lower)) return false
      seen.add(lower)
      return true
    })
    .map((label) => ({ id: existingByLabel.get(label.toLowerCase()) ?? null, label }))
}

function CustomFieldsPanel({ projectId, fields }: { projectId: string; fields: CustomFieldDefinition[] }) {
  const client = useQueryClient()
  const [draft, setDraft] = useState(blankCustomField)
  const isChoiceType = choiceFieldTypes.includes(draft.fieldType)
  const createMutation = useMutation({
    mutationFn: () =>
      orbitApi.createCustomField(projectId, {
        key: draft.key,
        label: draft.label,
        fieldType: draft.fieldType,
        required: draft.required,
        order: draft.order,
        choiceOptions: isChoiceType ? parseChoiceOptionsText(draft.choiceOptionsText, []) : [],
        applicableTypes: draft.applicableTypes,
      }),
    onSuccess: (created) => {
      setDraft(blankCustomField)
      client.setQueryData<CustomFieldDefinition[]>(['custom-fields', projectId], (current) => [...(current ?? []), created])
    },
  })

  return (
    <div className="space-y-5">
      <Panel title="Add a custom field" description="Shows on the work item detail view for whichever types it applies to.">
        <form onSubmit={(event) => { event.preventDefault(); createMutation.mutate() }} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field variant="panel" label="Key">
              <input
                required
                maxLength={64}
                pattern="[a-zA-Z0-9-]+"
                value={draft.key}
                onChange={(event) => setDraft((current) => ({ ...current, key: event.target.value }))}
              />
            </Field>
            <Field variant="panel" label="Label">
              <input
                required
                maxLength={80}
                value={draft.label}
                onChange={(event) => setDraft((current) => ({ ...current, label: event.target.value }))}
              />
            </Field>
            <Field variant="panel" label="Type">
              <SearchableSelect
                value={draft.fieldType}
                onChange={(val) => setDraft((current) => ({ ...current, fieldType: val as CustomFieldType }))}
                options={customFieldTypes.map((type) => ({ value: type, label: type }))}
                searchable={false}
              />
            </Field>
            <Toggle
              label="Required"
              checked={draft.required}
              onChange={(required) => setDraft((current) => ({ ...current, required }))}
            />
            {isChoiceType && (
              <Field variant="panel" label="Options (comma-separated)">
                <input
                  required
                  value={draft.choiceOptionsText}
                  onChange={(event) => setDraft((current) => ({ ...current, choiceOptionsText: event.target.value }))}
                />
              </Field>
            )}
          </div>
          <ApplicableTypesPicker
            value={draft.applicableTypes}
            onChange={(applicableTypes) => setDraft((current) => ({ ...current, applicableTypes }))}
          />
          <SubmitRow mutation={createMutation} />
        </form>
      </Panel>
      <Panel title="Custom fields" description="Project-defined fields, ordered for display.">
        <div className="space-y-3">
          {fields.map((field) => <CustomFieldRow key={field.id} projectId={projectId} field={field} />)}
          {fields.length === 0 && <p className="text-sm text-gray-500">No custom fields yet.</p>}
        </div>
      </Panel>
    </div>
  )
}

function CustomFieldRow({ projectId, field }: { projectId: string; field: CustomFieldDefinition }) {
  const client = useQueryClient()
  const [draft, setDraft] = useState(field)
  const isChoiceType = choiceFieldTypes.includes(field.fieldType)
  const [choiceOptionsText, setChoiceOptionsText] = useState(
    field.choiceOptions.map((option) => option.label).join(', '),
  )
  const mutation = useMutation({
    mutationFn: () =>
      orbitApi.updateCustomField(projectId, {
        ...draft,
        choiceOptions: isChoiceType ? parseChoiceOptionsText(choiceOptionsText, field.choiceOptions) : [],
      }),
    onSuccess: (updated) => {
      setDraft(updated)
      setChoiceOptionsText(updated.choiceOptions.map((option) => option.label).join(', '))
      client.setQueryData<CustomFieldDefinition[]>(['custom-fields', projectId], (current) =>
        current?.map((existing) => existing.id === updated.id ? updated : existing),
      )
    },
  })

  return (
    <form onSubmit={(event) => { event.preventDefault(); mutation.mutate() }} className="rounded-lg border border-gray-200 p-4">
      <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_100px_auto_auto] sm:items-end">
        <Field variant="panel" label={`${field.key} (${field.fieldType})`}>
          <input value={draft.label} maxLength={80} onChange={(event) => setDraft((current) => ({ ...current, label: event.target.value }))} />
        </Field>
        <Field variant="panel" label="Order">
          <input type="number" min={0} max={10000} value={draft.order} onChange={(event) => setDraft((current) => ({ ...current, order: Number(event.target.value) }))} />
        </Field>
        <Toggle label="Required" checked={draft.required} onChange={(required) => setDraft((current) => ({ ...current, required }))} />
        <Toggle label="Enabled" checked={draft.enabled} onChange={(enabled) => setDraft((current) => ({ ...current, enabled }))} />
      </div>
      {isChoiceType && (
        <div className="mt-3">
          <Field variant="panel" label="Options (comma-separated)">
            <input value={choiceOptionsText} onChange={(event) => setChoiceOptionsText(event.target.value)} />
          </Field>
        </div>
      )}
      <div className="mt-3">
        <ApplicableTypesPicker
          value={draft.applicableTypes}
          onChange={(applicableTypes) => setDraft((current) => ({ ...current, applicableTypes }))}
        />
      </div>
      <SubmitRow mutation={mutation} />
    </form>
  )
}

const blankMembership: CreateMembershipInput = { issuer: '', subject: '', principalType: 'User', role: 'Member' }

function MembersPanel({ project }: { project: Project }) {
  const client = useQueryClient()
  const membershipsQuery = useQuery({ queryKey: ['memberships'], queryFn: orbitApi.listMemberships })
  const [invitationSearch, setInvitationSearch] = useState('')
  const [invitationStatusFilter, setInvitationStatusFilter] = useState<WorkspaceInvitationStatus | ''>('')
  const invitationsQuery = useQuery({
    queryKey: ['invitations', invitationSearch, invitationStatusFilter],
    queryFn: () => orbitApi.listInvitations({
      email: invitationSearch || undefined,
      status: invitationStatusFilter || undefined,
    }),
  })
  const teamsQuery = useQuery({ queryKey: ['teams'], queryFn: orbitApi.listTeams })
  const projectRolesQuery = useQuery({
    queryKey: ['project-roles', project.id],
    queryFn: () => orbitApi.listProjectRoles(project.id),
  })
  const [draft, setDraft] = useState(blankMembership)
  const [invitationEmail, setInvitationEmail] = useState('')
  const [invitationRole, setInvitationRole] = useState<TenantRole>('Member')
  const [invitationTeamId, setInvitationTeamId] = useState('')
  const [invitationIsGuest, setInvitationIsGuest] = useState(false)
  const inviteMutation = useMutation({
    mutationFn: () => orbitApi.createInvitation({
      email: invitationEmail,
      role: invitationIsGuest ? 'Member' : invitationRole,
      teamId: invitationTeamId || null,
      tier: invitationIsGuest ? 'Guest' : 'Standard',
    }),
    onSuccess: () => {
      setInvitationEmail('')
      setInvitationTeamId('')
      setInvitationIsGuest(false)
      client.invalidateQueries({ queryKey: ['invitations'] })
    },
  })
  const revokeInvitationMutation = useMutation({
    mutationFn: orbitApi.revokeInvitation,
    onSuccess: () => client.invalidateQueries({ queryKey: ['invitations'] }),
  })
  const createMutation = useMutation({
    mutationFn: () => orbitApi.createMembership(draft),
    onSuccess: () => {
      setDraft(blankMembership)
      client.invalidateQueries({ queryKey: ['memberships'] })
    },
  })
  const assignMutation = useMutation({
    mutationFn: ({ membershipId, role }: { membershipId: string; role: ProjectRole }) =>
      orbitApi.assignProjectRole(project.id, membershipId, role),
    onSuccess: () => client.invalidateQueries({ queryKey: ['project-roles', project.id] }),
  })
  const roleMutation = useMutation({
    mutationFn: ({ membershipId, role }: { membershipId: string; role: TenantRole }) =>
      orbitApi.changeMembershipRole(membershipId, role),
    onSuccess: () => client.invalidateQueries({ queryKey: ['memberships'] }),
  })
  const removeMutation = useMutation({
    mutationFn: (membershipId: string) => orbitApi.deactivateMembership(membershipId),
    onSuccess: () => client.invalidateQueries({ queryKey: ['memberships'] }),
  })

  const projectRoleByMembership = new Map(
    (projectRolesQuery.data ?? []).map((assignment) => [assignment.membershipId, assignment.role]),
  )

  return (
    <div className="space-y-5">
      <Panel title="Invite a member" description="Send a single-use invitation that expires after seven days.">
        <form onSubmit={(event) => { event.preventDefault(); inviteMutation.mutate() }} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-3">
            <Field variant="panel" label="Email">
              <input required type="email" maxLength={320} value={invitationEmail} onChange={(event) => setInvitationEmail(event.target.value)} />
            </Field>
            <Field variant="panel" label="Workspace role">
              <SearchableSelect
                value={invitationIsGuest ? 'Member' : invitationRole}
                onChange={(val) => setInvitationRole(val as TenantRole)}
                options={['Member', 'Administrator']}
                searchable={false}
                disabled={invitationIsGuest}
              />
            </Field>
            <Field variant="panel" label="Team (optional)">
              <SearchableSelect
                value={invitationTeamId}
                onChange={(val) => setInvitationTeamId(val)}
                options={[
                  { value: '', label: 'No team' },
                  ...(teamsQuery.data ?? []).map((team) => ({ value: team.id, label: team.name })),
                ]}
                placeholder="No team"
                searchPlaceholder="Search teams…"
              />
            </Field>
          </div>
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={invitationIsGuest}
              onChange={(event) => setInvitationIsGuest(event.target.checked)}
              className="rounded border-gray-300"
            />
            Invite as guest — Member role only, sees just the projects they're explicitly added to
          </label>
          <SubmitRow mutation={inviteMutation} />
        </form>
        <div className="mt-5 grid gap-4 sm:grid-cols-3">
          <Field variant="panel" label="Search by email">
            <input
              type="search"
              placeholder="Search invitations…"
              value={invitationSearch}
              onChange={(event) => setInvitationSearch(event.target.value)}
            />
          </Field>
          <Field variant="panel" label="Status">
            <SearchableSelect
              value={invitationStatusFilter}
              onChange={(val) => setInvitationStatusFilter(val as WorkspaceInvitationStatus | '')}
              options={[
                { value: '', label: 'All statuses' },
                { value: 'Active', label: 'Active' },
                { value: 'Accepted', label: 'Accepted' },
                { value: 'Revoked', label: 'Revoked' },
              ]}
              searchable={false}
            />
          </Field>
        </div>
        {!!invitationsQuery.data?.length && (
          <ul className="mt-3 divide-y divide-gray-100 rounded-lg border border-gray-200">
            {invitationsQuery.data.map((invitation) => (
              <li key={invitation.id} className="flex items-center justify-between gap-4 p-3 text-sm">
                <span>
                  {invitation.email} · {invitation.role} · {invitation.status}
                  {invitation.acceptedAt && ` · accepted ${new Date(invitation.acceptedAt).toLocaleDateString()}`}
                </span>
                {invitation.status === 'Active' && (
                  <button onClick={() => revokeInvitationMutation.mutate(invitation.id)} className="text-xs font-medium text-red-600 hover:underline">Revoke</button>
                )}
              </li>
            ))}
          </ul>
        )}
        {invitationsQuery.isSuccess && invitationsQuery.data.length === 0 && (
          <p className="mt-3 text-sm text-gray-500">No invitations match this search.</p>
        )}
      </Panel>
      <Panel title="Members" description="Everyone with access to this workspace.">
        {membershipsQuery.isPending && <p className="text-sm text-gray-500">Loading members…</p>}
        {membershipsQuery.isError && <p className="text-sm text-red-700">{membershipsQuery.error.message}</p>}
        {membershipsQuery.data && (
          <div className="overflow-x-auto rounded-lg border border-gray-200">
            <table className="w-full text-left text-sm">
              <thead className="bg-gray-50 text-xs font-semibold uppercase text-gray-500">
                <tr>
                  <th className="px-4 py-2">Identity</th>
                  <th className="px-4 py-2">Type</th>
                  <th className="px-4 py-2">Workspace role</th>
                  <th className="px-4 py-2">This project</th>
                  <th className="px-4 py-2">Status</th>
                  <th className="px-4 py-2" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {membershipsQuery.data.map((membership) => (
                  <tr key={membership.id}>
                    <td className="px-4 py-2 text-gray-900">
                      {membership.userId ? 'Local account' : `${membership.issuer} / ${membership.subject}`}
                      {membership.tier === 'Guest' && (
                        <span className="ml-2 rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800">
                          Guest
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-2 text-gray-600">{membership.principalType}</td>
                    <td className="px-4 py-2">
                      <div className="w-36">
                        <SearchableSelect
                          size="sm"
                          value={membership.role}
                          disabled={!membership.isActive || roleMutation.isPending}
                          onChange={(val) =>
                            roleMutation.mutate({ membershipId: membership.id, role: val as TenantRole })
                          }
                          options={['Member', 'Administrator', 'Owner']}
                          searchable={false}
                        />
                      </div>
                    </td>
                    <td className="px-4 py-2">
                      <div className="w-36">
                        <SearchableSelect
                          size="sm"
                          value={projectRoleByMembership.get(membership.id) ?? ''}
                          disabled={assignMutation.isPending}
                          onChange={(val) =>
                            val &&
                            assignMutation.mutate({ membershipId: membership.id, role: val as ProjectRole })
                          }
                          options={[
                            { value: '', label: 'No role' },
                            { value: 'Viewer', label: 'Viewer' },
                            { value: 'Member', label: 'Member' },
                            { value: 'Administrator', label: 'Administrator' },
                          ]}
                          searchable={false}
                        />
                      </div>
                    </td>
                    <td className="px-4 py-2 text-gray-600">{membership.isActive ? 'Active' : 'Inactive'}</td>
                    <td className="px-4 py-2 text-right">
                      {membership.isActive && (
                        <button
                          onClick={() => removeMutation.mutate(membership.id)}
                          disabled={removeMutation.isPending}
                          className="text-xs font-medium text-red-600 hover:underline disabled:opacity-50"
                        >
                          Remove
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {projectRolesQuery.isError && (
          <p className="mt-3 text-xs text-gray-500">
            Per-project roles are only visible to project administrators: {projectRolesQuery.error.message}
          </p>
        )}
      </Panel>

      <Panel title="Add a member" description="Grant workspace access to a federated identity or service account.">
        <form onSubmit={(event) => { event.preventDefault(); createMutation.mutate() }} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field variant="panel" label="Issuer">
              <input required value={draft.issuer} onChange={(event) => setDraft({ ...draft, issuer: event.target.value })} placeholder="https://identity.example.com" />
            </Field>
            <Field variant="panel" label="Subject">
              <input required value={draft.subject} onChange={(event) => setDraft({ ...draft, subject: event.target.value })} placeholder="user or client id" />
            </Field>
            <Field variant="panel" label="Principal type">
              <SearchableSelect
                value={draft.principalType}
                onChange={(val) => setDraft({ ...draft, principalType: val as PrincipalType })}
                options={[
                  { value: 'User', label: 'User' },
                  { value: 'ServiceAccount', label: 'Service account' },
                ]}
                searchable={false}
              />
            </Field>
            <Field variant="panel" label="Workspace role">
              <SearchableSelect
                value={draft.role}
                onChange={(val) => setDraft({ ...draft, role: val as TenantRole })}
                options={['Member', 'Administrator', 'Owner']}
                searchable={false}
              />
            </Field>
          </div>
          <SubmitRow mutation={createMutation} />
        </form>
      </Panel>
    </div>
  )
}

function TeamsPanel() {
  const client = useQueryClient()
  const teamsQuery = useQuery({ queryKey: ['teams'], queryFn: orbitApi.listTeams })
  const membershipsQuery = useQuery({ queryKey: ['memberships'], queryFn: orbitApi.listMemberships })
  const [name, setName] = useState('')
  const [expandedTeamId, setExpandedTeamId] = useState<string | null>(null)
  const createMutation = useMutation({
    mutationFn: () => orbitApi.createTeam(name),
    onSuccess: () => {
      setName('')
      client.invalidateQueries({ queryKey: ['teams'] })
    },
  })

  return (
    <div className="space-y-5">
      <Panel title="Teams" description="Workspace-scoped groups of active members used for ownership and permissions.">
        {teamsQuery.isPending && <p className="text-sm text-gray-500">Loading teams…</p>}
        {teamsQuery.isError && <p className="text-sm text-red-700">{teamsQuery.error.message}</p>}
        {teamsQuery.data?.length === 0 && <p className="text-sm text-gray-500">No teams yet.</p>}
        {!!teamsQuery.data?.length && (
          <ul className="divide-y divide-gray-100 rounded-lg border border-gray-200">
            {teamsQuery.data.map((team) => (
              <li key={team.id}>
                <button
                  onClick={() => setExpandedTeamId((current) => (current === team.id ? null : team.id))}
                  className="flex w-full items-center justify-between px-4 py-3 text-left text-sm font-medium text-gray-900 hover:bg-gray-50"
                >
                  {team.name}
                  <span className="text-xs font-normal text-gray-400">
                    {expandedTeamId === team.id ? 'Hide members' : 'Show members'}
                  </span>
                </button>
                {expandedTeamId === team.id && (
                  <TeamMembersEditor team={team} memberships={membershipsQuery.data ?? []} />
                )}
              </li>
            ))}
          </ul>
        )}
      </Panel>

      <Panel title="Create a team" description="Name a new workspace team.">
        <form onSubmit={(event) => { event.preventDefault(); createMutation.mutate() }} className="space-y-4">
          <Field variant="panel" label="Team name">
            <input
              required
              minLength={2}
              maxLength={120}
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder="Platform Team"
            />
          </Field>
          <SubmitRow mutation={createMutation} />
        </form>
      </Panel>
    </div>
  )
}

function TeamMembersEditor({ team, memberships }: { team: Team; memberships: TenantMembership[] }) {
  const client = useQueryClient()
  const teamMembersQuery = useQuery({
    queryKey: ['team-members', team.id],
    queryFn: () => orbitApi.listTeamMembers(team.id),
  })
  const [selected, setSelected] = useState('')
  const addMutation = useMutation({
    mutationFn: (membershipId: string) => orbitApi.addTeamMember(team.id, membershipId),
    onSuccess: () => {
      setSelected('')
      client.invalidateQueries({ queryKey: ['team-members', team.id] })
    },
  })
  const removeMutation = useMutation({
    mutationFn: (membershipId: string) => orbitApi.removeTeamMember(team.id, membershipId),
    onSuccess: () => client.invalidateQueries({ queryKey: ['team-members', team.id] }),
  })

  const memberIds = new Set((teamMembersQuery.data ?? []).map((member) => member.membershipId))
  const membershipById = new Map(memberships.map((membership) => [membership.id, membership]))
  const candidates = memberships.filter((membership) => membership.isActive && !memberIds.has(membership.id))
  const describe = (membershipId: string) => {
    const membership = membershipById.get(membershipId)
    if (!membership) return membershipId
    return membership.userId ? 'Local account' : `${membership.issuer} / ${membership.subject}`
  }

  return (
    <div className="border-t border-gray-100 bg-gray-50 px-4 py-3">
      {teamMembersQuery.isPending && <p className="text-xs text-gray-500">Loading members…</p>}
      {teamMembersQuery.data?.length === 0 && <p className="text-xs text-gray-500">No members yet.</p>}
      {!!teamMembersQuery.data?.length && (
        <ul className="mb-3 space-y-1">
          {teamMembersQuery.data.map((member) => (
            <li key={member.id} className="flex items-center justify-between text-sm text-gray-700">
              <span>{describe(member.membershipId)}</span>
              <button
                onClick={() => removeMutation.mutate(member.membershipId)}
                disabled={removeMutation.isPending}
                className="text-xs font-medium text-red-600 hover:underline disabled:opacity-50"
              >
                Remove
              </button>
            </li>
          ))}
        </ul>
      )}
      {candidates.length > 0 && (
        <div className="flex items-center gap-2">
          <div className="w-56">
            <SearchableSelect
              size="sm"
              value={selected}
              onChange={(val) => setSelected(val)}
              options={[
                { value: '', label: 'Add a member…' },
                ...candidates.map((membership) => ({
                  value: membership.id,
                  label: describe(membership.id),
                })),
              ]}
              placeholder="Add a member…"
              searchPlaceholder="Search members…"
            />
          </div>
          <button
            onClick={() => selected && addMutation.mutate(selected)}
            disabled={!selected || addMutation.isPending}
            className="rounded-md bg-blue-600 px-3 py-1.5 text-xs font-medium text-white disabled:opacity-50 hover:bg-blue-700"
          >
            Add
          </button>
        </div>
      )}
    </div>
  )
}

function SecurityPanel() {
  const authenticated = useIsAuthenticated()
  const client = useQueryClient()
  const sessionsQuery = useQuery({ queryKey: ['sessions'], queryFn: orbitApi.listSessions, enabled: authenticated })
  const identitiesQuery = useQuery({
    queryKey: ['linked-identities'],
    queryFn: orbitApi.listLinkedIdentities,
    enabled: authenticated,
  })
  const revokeMutation = useMutation({
    mutationFn: (sessionId: string) => orbitApi.revokeSession(sessionId),
    onSuccess: () => client.invalidateQueries({ queryKey: ['sessions'] }),
  })
  const unlinkMutation = useMutation({
    mutationFn: (identityId: string) => orbitApi.unlinkExternalIdentity(identityId),
    onSuccess: () => client.invalidateQueries({ queryKey: ['linked-identities'] }),
  })
  const linkGoogleMutation = useMutation({
    mutationFn: () => orbitApi.startGoogleAccountLink(window.location.origin),
    onSuccess: ({ authorizeUrl }) => { window.location.href = authorizeUrl },
  })
  const oidcConfigured = getOidcConfig() !== null

  if (!authenticated) {
    return (
      <Panel
        title="Account security"
        description="Sign in with your local credentials to manage active sessions and linked identities."
      >
        <LoginForm />
      </Panel>
    )
  }

  return (
    <div className="space-y-5">
      <Panel title="Active sessions" description="Devices and browsers currently signed in to your account.">
        {sessionsQuery.isPending && <p className="text-sm text-gray-500">Loading sessions…</p>}
        {sessionsQuery.isError && <p className="text-sm text-red-700">{sessionsQuery.error.message}</p>}
        {sessionsQuery.data?.length === 0 && <p className="text-sm text-gray-500">No active sessions.</p>}
        {!!sessionsQuery.data?.length && (
          <ul className="divide-y divide-gray-100 rounded-lg border border-gray-200">
            {sessionsQuery.data.map((session) => (
              <li key={session.sessionId} className="flex items-center justify-between gap-4 p-4">
                <div>
                  <h3 className="text-sm font-semibold text-gray-900">
                    {session.userAgent ?? 'Unknown device'}
                    {session.isCurrent && (
                      <span className="ml-2 rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-700">
                        This device
                      </span>
                    )}
                  </h3>
                  <p className="mt-1 text-xs text-gray-500">
                    {session.workspaceName} · last used {new Date(session.lastUsedAt).toLocaleString()}
                  </p>
                </div>
                {!session.isCurrent && (
                  <button
                    onClick={() => revokeMutation.mutate(session.sessionId)}
                    disabled={revokeMutation.isPending}
                    className="text-xs font-medium text-red-600 hover:underline disabled:opacity-50"
                  >
                    Revoke
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}
        <button
          onClick={() => auth.logout()}
          className="mt-4 rounded-md border border-gray-200 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
        >
          Sign out
        </button>
      </Panel>
      <Panel title="Linked identities" description="External sign-in methods linked to your account.">
        {identitiesQuery.isPending && <p className="text-sm text-gray-500">Loading linked identities…</p>}
        {identitiesQuery.data?.length === 0 && <p className="text-sm text-gray-500">No linked identities.</p>}
        {!!identitiesQuery.data?.length && (
          <ul className="divide-y divide-gray-100 rounded-lg border border-gray-200">
            {identitiesQuery.data.map((identity) => (
              <li key={identity.id} className="flex items-center justify-between gap-4 p-4">
                <div>
                  <h3 className="text-sm font-semibold text-gray-900">{identity.issuer}</h3>
                  <p className="mt-1 text-xs text-gray-500">
                    Linked {new Date(identity.createdAt).toLocaleDateString()}
                  </p>
                </div>
                <button
                  onClick={() => unlinkMutation.mutate(identity.id)}
                  disabled={unlinkMutation.isPending}
                  className="text-xs font-medium text-red-600 hover:underline disabled:opacity-50"
                >
                  Unlink
                </button>
              </li>
            ))}
          </ul>
        )}
        <div className="mt-4 flex gap-2">
          <button
            onClick={() => linkGoogleMutation.mutate()}
            disabled={linkGoogleMutation.isPending}
            className="rounded-md border border-gray-200 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50 disabled:opacity-50"
          >
            Link Google account
          </button>
          {oidcConfigured && (
            <button
              onClick={() => startOidcLogin('link')}
              className="rounded-md border border-gray-200 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
            >
              Link SSO identity
            </button>
          )}
        </div>
        {linkGoogleMutation.isError && (
          <p className="mt-2 text-xs text-red-600">{linkGoogleMutation.error.message}</p>
        )}
      </Panel>
      <div className="divide-y divide-gray-200 rounded-lg border border-gray-200">
        {[
          ['Change email', 'Requires verified email-change tokens and global uniqueness transaction.'],
          ['Change password', 'Requires current-password verification and session-family revocation.'],
        ].map(([title, detail]) => (
          <div key={title} className="flex items-center justify-between gap-4 p-4">
            <div><h3 className="text-sm font-semibold text-gray-900">{title}</h3><p className="mt-1 text-xs text-gray-500">{detail}</p></div>
            <button disabled className="rounded-md border border-gray-200 px-3 py-1.5 text-sm text-gray-400">Unavailable</button>
          </div>
        ))}
      </div>
    </div>
  )
}

function Panel({ title, description, children }: { title: string; description?: string; children: ReactNode }) {
  return <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm sm:p-7"><h2 className="text-xl font-semibold text-gray-900">{title}</h2>{description && <p className="mt-1 mb-6 text-sm text-gray-500">{description}</p>}<div className={description ? '' : 'mt-5'}>{children}</div></div>
}

function Toggle({ label, checked, onChange, disabled = false }: { label: string; checked: boolean; onChange: (checked: boolean) => void; disabled?: boolean }) {
  return <label className="flex items-center justify-between gap-4 rounded-lg border border-gray-200 px-4 py-3 text-sm font-medium text-gray-800"><span>{label}</span><input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} disabled={disabled} className="h-4 w-4 accent-blue-600" /></label>
}
