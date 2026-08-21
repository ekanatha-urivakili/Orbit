import type {
  Board,
  AccountWorkspace,
  BootstrapInput,
  BootstrapResult,
  BootstrapStatus,
  CreatedWorkspace,
  CreateMembershipInput,
  CreateWorkItemInput,
  UpdateWorkItemInput,
  ExternalIdentitySummary,
  PagedResult,
  Project,
  ProjectPermission,
  ProjectRoleAssignment,
  ProjectSetting,
  Role,
  Profile,
  NotificationPreference,
  SessionSummary,
  SiteCapabilities,
  Sprint,
  SprintReport,
  CumulativeFlowDiagram,
  CycleTimeReport,
  ControlChart,
  Team,
  TeamMembership,
  TenantMembership,
  TenantRole,
  TypographySetting,
  WorkspaceSetting,
  PresignedWorkspaceLogoUpload,
  WorkspaceInvitation,
  WorkspaceInvitationStatus,
  MembershipTier,
  SystemChoices,
  WorkItem,
  WorkItemAttachment,
  WorkItemLink,
  WorkItemLinkKind,
  PresignedAttachmentUpload,
  WorkItemComment,
  WorkItemTypeDefinition,
  WorkItemStatusDefinition,
  StatusCategory,
  WorkItemType,
  CustomFieldDefinition,
  CustomFieldChoiceOptionInput,
  CustomFieldType,
  WorkItemCustomFieldValue,
  UpdateSprintInput,
  BoardViewPreference,
  SprintInsights,
} from './types'
import { tenantStorageKey, withAuthHeader } from './auth'

const apiUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5014/api/v1'

function getTenantId(): string {
  const existing = localStorage.getItem(tenantStorageKey)
  if (existing) return existing

  const created = crypto.randomUUID()
  localStorage.setItem(tenantStorageKey, created)
  return created
}

interface ProblemDetails {
  title?: string
  detail?: string
}

// §4.5: carries the X-Correlation-Id response header (set by CorrelationIdMiddleware) so a
// user-reported bug can be joined to the exact backend trace/log lines.
export class ApiError extends Error {
  readonly status: number
  readonly correlationId?: string

  constructor(message: string, status: number, correlationId?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.correlationId = correlationId
  }
}

async function request<T>(path: string, init?: RequestInit, tenantScoped = true): Promise<T> {
  const headers = new Headers(init?.headers)
  headers.set('Accept', 'application/json')
  if (init?.body) headers.set('Content-Type', 'application/json')
  if (tenantScoped) headers.set('X-Tenant-Id', getTenantId())
  await withAuthHeader(headers)

  const response = await fetch(`${apiUrl}${path}`, { ...init, headers })
  if (!response.ok) {
    const correlationId = response.headers.get('X-Correlation-Id') ?? undefined
    const problem = (await response.json().catch(() => ({}))) as ProblemDetails
    throw new ApiError(
      problem.detail ?? problem.title ?? `Request failed (${response.status})`,
      response.status,
      correlationId,
    )
  }

  if (response.status === 204) return undefined as T

  return (await response.json()) as T
}

export const orbitApi = {
  getBootstrapStatus: () => request<BootstrapStatus>('/bootstrap/status', undefined, false),
  bootstrap: async (input: BootstrapInput) => {
    const result = await request<BootstrapResult>('/bootstrap', {
      method: 'POST',
      body: JSON.stringify(input),
    }, false)
    localStorage.setItem(tenantStorageKey, result.workspaceId)
    return result
  },
  listProjects: (skip = 0, take = 200) =>
    request<PagedResult<Project>>(`/projects?skip=${skip}&take=${take}`),
  createProject: (input: { key: string; name: string }) =>
    request<Project>('/projects', { method: 'POST', body: JSON.stringify(input) }),
  getChoices: () => request<SystemChoices>('/choices', undefined, false),
  listWorkItemTypes: () => request<WorkItemTypeDefinition[]>('/work-item-types'),
  updateWorkItemType: (input: WorkItemTypeDefinition) =>
    request<WorkItemTypeDefinition>(`/work-item-types/${encodeURIComponent(input.id)}`, {
      method: 'PATCH',
      headers: { 'If-Match': `"${input.version}"` },
      body: JSON.stringify(input),
    }),
  listCustomFields: (projectId: string) =>
    request<CustomFieldDefinition[]>(`/projects/${encodeURIComponent(projectId)}/custom-fields`),
  createCustomField: (
    projectId: string,
    input: {
      key: string
      label: string
      fieldType: CustomFieldType
      required: boolean
      order: number
      choiceOptions: CustomFieldChoiceOptionInput[]
      applicableTypes: WorkItemType[]
    },
  ) =>
    request<CustomFieldDefinition>(`/projects/${encodeURIComponent(projectId)}/custom-fields`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),
  updateCustomField: (
    projectId: string,
    input: Pick<CustomFieldDefinition, 'id' | 'label' | 'required' | 'order' | 'enabled' | 'version' | 'applicableTypes'> & {
      choiceOptions: CustomFieldChoiceOptionInput[]
    },
  ) =>
    request<CustomFieldDefinition>(
      `/projects/${encodeURIComponent(projectId)}/custom-fields/${encodeURIComponent(input.id)}`,
      {
        method: 'PATCH',
        headers: { 'If-Match': `"${input.version}"` },
        body: JSON.stringify({
          label: input.label,
          required: input.required,
          order: input.order,
          enabled: input.enabled,
          choiceOptions: input.choiceOptions,
          applicableTypes: input.applicableTypes,
        }),
      },
    ),
  listWorkItemCustomFieldValues: (workItemId: string) =>
    request<WorkItemCustomFieldValue[]>(`/work-items/${encodeURIComponent(workItemId)}/custom-field-values`),
  setWorkItemCustomFieldValues: (workItemId: string, values: WorkItemCustomFieldValue[]) =>
    request<WorkItemCustomFieldValue[]>(`/work-items/${encodeURIComponent(workItemId)}/custom-field-values`, {
      method: 'PUT',
      body: JSON.stringify(values),
    }),
  listWorkItems: (projectId: string, skip = 0, take = 200) =>
    request<PagedResult<WorkItem>>(
      `/work-items?projectId=${encodeURIComponent(projectId)}&skip=${skip}&take=${take}`,
    ),
  createWorkItem: (input: CreateWorkItemInput) =>
    request<WorkItem>('/work-items', { method: 'POST', body: JSON.stringify(input) }),
  updateWorkItem: (workItem: WorkItem, input: UpdateWorkItemInput) =>
    request<WorkItem>(`/work-items/${workItem.id}`, {
      method: 'PATCH',
      headers: { 'If-Match': `"${workItem.version}"` },
      body: JSON.stringify(input),
    }),
  changeWorkItemAssignee: (workItem: WorkItem, assigneeUserId: string | null) =>
    request<WorkItem>(`/work-items/${workItem.id}/assignee`, {
      method: 'PATCH',
      headers: { 'If-Match': `"${workItem.version}"` },
      body: JSON.stringify({ assigneeUserId }),
    }),
  changeStatus: (workItem: WorkItem, statusId: string) =>
    request<WorkItem>(`/work-items/${workItem.id}/status`, {
      method: 'PATCH',
      headers: { 'If-Match': `"${workItem.version}"` },
      body: JSON.stringify({ statusId }),
    }),
  reorderWorkItem: (workItem: WorkItem, neighbors: { beforeId: string | null; afterId: string | null }) =>
    request<WorkItem>(`/work-items/${workItem.id}/rank`, {
      method: 'PATCH',
      headers: { 'If-Match': `"${workItem.version}"` },
      body: JSON.stringify({ beforeWorkItemId: neighbors.beforeId, afterWorkItemId: neighbors.afterId }),
    }),
  changeWorkItemType: (workItem: WorkItem, type: WorkItemType) =>
    request<WorkItem>(`/work-items/${workItem.id}/type`, {
      method: 'PATCH',
      headers: { 'If-Match': `"${workItem.version}"` },
      body: JSON.stringify({ type }),
    }),
  listWorkItemLinks: (workItemId: string) =>
    request<WorkItemLink[]>(`/work-items/${encodeURIComponent(workItemId)}/links`),
  addWorkItemLink: (workItemId: string, input: { kind: WorkItemLinkKind; targetWorkItemId: string; inverse: boolean }) =>
    request<WorkItemLink>(`/work-items/${encodeURIComponent(workItemId)}/links`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),
  removeWorkItemLink: (workItemId: string, linkId: string) =>
    request<void>(`/work-items/${encodeURIComponent(workItemId)}/links/${encodeURIComponent(linkId)}`, {
      method: 'DELETE',
    }),
  listWorkItemComments: (workItemId: string) =>
    request<WorkItemComment[]>(`/work-items/${encodeURIComponent(workItemId)}/comments`),
  addWorkItemComment: (workItemId: string, body: string) =>
    request<WorkItemComment>(`/work-items/${encodeURIComponent(workItemId)}/comments`, {
      method: 'POST',
      body: JSON.stringify({ body }),
    }),
  editWorkItemComment: (workItemId: string, commentId: string, body: string, version: number) =>
    request<WorkItemComment>(`/work-items/${encodeURIComponent(workItemId)}/comments/${encodeURIComponent(commentId)}`, {
      method: 'PATCH',
      headers: { 'If-Match': `"${version}"` },
      body: JSON.stringify({ body }),
    }),
  deleteWorkItemComment: (workItemId: string, commentId: string, version: number) =>
    request<void>(`/work-items/${encodeURIComponent(workItemId)}/comments/${encodeURIComponent(commentId)}`, {
      method: 'DELETE',
      headers: { 'If-Match': `"${version}"` },
    }),
  listWorkItemAttachments: (workItemId: string) =>
    request<WorkItemAttachment[]>(`/work-items/${encodeURIComponent(workItemId)}/attachments`),
  presignWorkItemAttachmentUpload: (workItemId: string, fileName: string, contentType: string, sizeBytes: number) =>
    request<PresignedAttachmentUpload>(`/work-items/${encodeURIComponent(workItemId)}/attachments/presign`, {
      method: 'POST',
      body: JSON.stringify({ fileName, contentType, sizeBytes }),
    }),
  confirmWorkItemAttachment: (
    workItemId: string,
    input: { fileName: string; contentType: string; sizeBytes: number; objectKey: string },
  ) =>
    request<WorkItemAttachment>(`/work-items/${encodeURIComponent(workItemId)}/attachments`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),
  deleteWorkItemAttachment: (workItemId: string, attachmentId: string) =>
    request<void>(`/work-items/${encodeURIComponent(workItemId)}/attachments/${encodeURIComponent(attachmentId)}`, {
      method: 'DELETE',
    }),
  // Rejects with 409 while the attachment is still being scanned, or 404 if it was flagged
  // Infected/Failed - see the malware-scanning gate on the download path.
  getWorkItemAttachmentDownloadUrl: (workItemId: string, attachmentId: string) =>
    request<WorkItemAttachment>(
      `/work-items/${encodeURIComponent(workItemId)}/attachments/${encodeURIComponent(attachmentId)}/download`,
    ),
  getWorkItemWatchers: (workItemId: string) =>
    request<import('./types').WorkItemWatchers>(`/work-items/${encodeURIComponent(workItemId)}/watchers`),
  watchWorkItem: (workItemId: string) =>
    request<void>(`/work-items/${encodeURIComponent(workItemId)}/watchers/me`, { method: 'PUT' }),
  unwatchWorkItem: (workItemId: string) =>
    request<void>(`/work-items/${encodeURIComponent(workItemId)}/watchers/me`, { method: 'DELETE' }),
  toggleWorkItemFlag: (workItem: WorkItem, flagged: boolean) =>
    request<WorkItem>(`/work-items/${workItem.id}/flag`, {
      method: 'PATCH',
      headers: { 'If-Match': `"${workItem.version}"` },
      body: JSON.stringify({ flagged }),
    }),
  setWorkItemCover: (workItem: WorkItem, attachmentId: string | null) =>
    request<WorkItem>(`/work-items/${workItem.id}/cover`, {
      method: 'PATCH',
      headers: { 'If-Match': `"${workItem.version}"` },
      body: JSON.stringify({ attachmentId }),
    }),
  getWorkItemVotes: (workItemId: string) =>
    request<import('./types').WorkItemVotes>(`/work-items/${encodeURIComponent(workItemId)}/votes`),
  addWorkItemVote: (workItemId: string) =>
    request<void>(`/work-items/${encodeURIComponent(workItemId)}/votes/me`, { method: 'PUT' }),
  removeWorkItemVote: (workItemId: string) =>
    request<void>(`/work-items/${encodeURIComponent(workItemId)}/votes/me`, { method: 'DELETE' }),
  listWorkItemHistory: (workItemId: string) =>
    request<PagedResult<import('./types').WorkItemHistoryEntry>>(
      `/work-items/${encodeURIComponent(workItemId)}/history`,
    ),
  listWorklogs: (workItemId: string) =>
    request<PagedResult<import('./types').WorkItemWorklog>>(
      `/work-items/${encodeURIComponent(workItemId)}/worklogs`,
    ),
  addWorklog: (workItemId: string, input: { minutesSpent: number; workDate: string; description: string | null }) =>
    request<import('./types').WorkItemWorklog>(`/work-items/${encodeURIComponent(workItemId)}/worklogs`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),
  deleteWorklog: (workItemId: string, worklogId: string) =>
    request<void>(`/work-items/${encodeURIComponent(workItemId)}/worklogs/${encodeURIComponent(worklogId)}`, {
      method: 'DELETE',
    }),
  cloneWorkItem: (workItemId: string) =>
    request<WorkItem>(`/work-items/${encodeURIComponent(workItemId)}/clone`, { method: 'POST' }),
  moveWorkItem: (workItem: WorkItem, targetProjectId: string) =>
    request<WorkItem>(`/work-items/${workItem.id}/move`, {
      method: 'POST',
      headers: { 'If-Match': `"${workItem.version}"` },
      body: JSON.stringify({ targetProjectId }),
    }),
  archiveWorkItem: (workItem: WorkItem) =>
    request<WorkItem>(`/work-items/${workItem.id}/archive`, {
      method: 'POST',
      headers: { 'If-Match': `"${workItem.version}"` },
    }),
  unarchiveWorkItem: (workItem: WorkItem) =>
    request<WorkItem>(`/work-items/${workItem.id}/unarchive`, {
      method: 'POST',
      headers: { 'If-Match': `"${workItem.version}"` },
    }),
  deleteWorkItem: (workItem: WorkItem) =>
    request<void>(`/work-items/${workItem.id}`, {
      method: 'DELETE',
      headers: { 'If-Match': `"${workItem.version}"` },
    }),
  shareWorkItem: (workItemId: string, input: { membershipIds: string[]; teamIds: string[]; message: string | null }) =>
    request<void>(`/work-items/${encodeURIComponent(workItemId)}/share`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),
  postWorkItemToSlack: (workItemId: string, message: string | null) =>
    request<void>(`/work-items/${encodeURIComponent(workItemId)}/slack-share`, {
      method: 'POST',
      body: JSON.stringify({ message }),
    }),
  startSlackConnect: (projectId: string) =>
    request<{ url: string }>('/integrations/slack/authorize-url', {
      method: 'POST',
      body: JSON.stringify({ projectId }),
    }),
  completeSlackOAuth: (code: string, state: string) =>
    request<{ id: string; projectId: string; teamName: string; channelName: string; createdAt: string }>(
      '/integrations/slack/complete',
      { method: 'POST', body: JSON.stringify({ code, state }) },
    ),
  getSlackConnection: (projectId: string) =>
    request<{ id: string; projectId: string; teamName: string; channelName: string; createdAt: string } | null>(
      `/integrations/slack/connection?projectId=${encodeURIComponent(projectId)}`,
    ),
  disconnectSlack: (connectionId: string) =>
    request<void>(`/integrations/slack/connections/${encodeURIComponent(connectionId)}`, { method: 'DELETE' }),
  // Direct fetch (not the JSON `request` helper) — export returns a downloadable file, not JSON.
  exportWorkItem: async (workItem: WorkItem, format: import('./types').WorkItemExportFormat) => {
    const headers = new Headers({ 'X-Tenant-Id': getTenantId() })
    await withAuthHeader(headers)
    const response = await fetch(
      `${apiUrl}/work-items/${workItem.id}/export?format=${format}`,
      { headers },
    )
    if (!response.ok) {
      const problem = (await response.json().catch(() => ({}))) as ProblemDetails
      throw new Error(problem.detail ?? problem.title ?? `Export failed (${response.status})`)
    }
    return { blob: await response.blob(), fileName: `${workItem.key}.${format.toLowerCase()}` }
  },
  // Direct PUT to the presigned object-storage URL — bypasses the Orbit API wrapper entirely,
  // since the file bytes go straight to MinIO/S3, not through the Orbit backend.
  uploadAttachmentFile: async (uploadUrl: string, file: File) => {
    const response = await fetch(uploadUrl, {
      method: 'PUT',
      headers: { 'Content-Type': file.type },
      body: file,
    })
    if (!response.ok) throw new Error(`Upload failed (${response.status})`)
  },
  getProfile: () => request<Profile>('/me'),
  listAccountWorkspaces: () => request<AccountWorkspace[]>('/me/workspaces'),
  getSiteCapabilities: () => request<SiteCapabilities>('/me/site-capabilities'),
  createWorkspace: (name: string) =>
    request<CreatedWorkspace>('/workspaces', {
      method: 'POST',
      body: JSON.stringify({ name }),
    }),
  createWorkspaceInOrganization: (name: string) =>
    request<CreatedWorkspace>('/organization/workspaces', {
      method: 'POST',
      body: JSON.stringify({ name }),
    }),
  updateProfile: (profile: Profile, input: Pick<Profile, 'displayName' | 'avatarUrl'>) =>
    request<Profile>('/me/profile', {
      method: 'PATCH',
      headers: { 'If-Match': `"${profile.version}"` },
      body: JSON.stringify(input),
    }),
  updatePreferences: (
    profile: Profile,
    input: Pick<Profile, 'locale' | 'timeZone' | 'theme' | 'density' | 'reduceMotion' | 'highContrast'>,
  ) =>
    request<Profile>('/me/preferences', {
      method: 'PATCH',
      headers: { 'If-Match': `"${profile.preferenceVersion}"` },
      body: JSON.stringify(input),
    }),
  getNotificationPreferences: () =>
    request<NotificationPreference>('/me/notification-preferences'),
  updateNotificationPreferences: (input: NotificationPreference) =>
    request<NotificationPreference>('/me/notification-preferences', {
      method: 'PATCH',
      headers: { 'If-Match': `"${input.version}"` },
      body: JSON.stringify(input),
    }),
  getWorkspaceSettings: () => request<WorkspaceSetting>('/workspaces/current/settings'),
  updateWorkspaceSettings: (input: WorkspaceSetting) =>
    request<WorkspaceSetting>('/workspaces/current/settings', {
      method: 'PATCH',
      headers: { 'If-Match': `"${input.version}"` },
      body: JSON.stringify(input),
    }),
  presignWorkspaceLogoUpload: (fileName: string, contentType: string, sizeBytes: number) =>
    request<PresignedWorkspaceLogoUpload>('/workspaces/current/settings/logo/presign', {
      method: 'POST',
      body: JSON.stringify({ fileName, contentType, sizeBytes }),
    }),
  confirmWorkspaceLogoUpload: (objectKey: string, version: number) =>
    request<WorkspaceSetting>('/workspaces/current/settings/logo', {
      method: 'PUT',
      headers: { 'If-Match': `"${version}"` },
      body: JSON.stringify({ objectKey }),
    }),
  // Direct PUT to the presigned object-storage URL, same as uploadAttachmentFile.
  uploadWorkspaceLogoFile: async (uploadUrl: string, file: File) => {
    const response = await fetch(uploadUrl, {
      method: 'PUT',
      headers: { 'Content-Type': file.type },
      body: file,
    })
    if (!response.ok) throw new Error(`Upload failed (${response.status})`)
  },
  getTypographySettings: () => request<TypographySetting>('/workspaces/current/typography-settings'),
  updateTypographySettings: (input: TypographySetting) =>
    request<TypographySetting>('/workspaces/current/typography-settings', {
      method: 'PATCH',
      headers: { 'If-Match': `"${input.version}"` },
      body: JSON.stringify(input),
    }),
  getProjectSettings: (projectId: string) =>
    request<ProjectSetting>(`/projects/${encodeURIComponent(projectId)}/settings`),
  updateProjectSettings: (input: ProjectSetting) =>
    request<ProjectSetting>(`/projects/${encodeURIComponent(input.projectId)}/settings`, {
      method: 'PATCH',
      headers: { 'If-Match': `"${input.version}"` },
      body: JSON.stringify(input),
    }),
  listMemberships: () => request<TenantMembership[]>('/memberships'),
  createMembership: (input: CreateMembershipInput) =>
    request<TenantMembership>('/memberships', { method: 'POST', body: JSON.stringify(input) }),
  listProjectRoles: (projectId: string) =>
    request<ProjectRoleAssignment[]>(`/projects/${encodeURIComponent(projectId)}/roles`),
  assignProjectRole: (projectId: string, membershipId: string, roleId: string) =>
    request<ProjectRoleAssignment>(
      `/projects/${encodeURIComponent(projectId)}/roles/${encodeURIComponent(membershipId)}`,
      { method: 'PUT', body: JSON.stringify({ roleId }) },
    ),
  listRoles: () => request<Role[]>('/roles'),
  createRole: (name: string, permissions: ProjectPermission[]) =>
    request<Role>('/roles', { method: 'POST', body: JSON.stringify({ name, permissions }) }),
  renameRole: (roleId: string, name: string) =>
    request<Role>(`/roles/${encodeURIComponent(roleId)}`, { method: 'PATCH', body: JSON.stringify({ name }) }),
  updateRolePermissions: (roleId: string, permissions: ProjectPermission[]) =>
    request<Role>(`/roles/${encodeURIComponent(roleId)}/permissions`, {
      method: 'PUT',
      body: JSON.stringify({ permissions }),
    }),
  deleteRole: (roleId: string) => request<void>(`/roles/${encodeURIComponent(roleId)}`, { method: 'DELETE' }),
  changeMembershipRole: (membershipId: string, role: TenantRole) =>
    request<TenantMembership>(`/memberships/${encodeURIComponent(membershipId)}/role`, {
      method: 'PUT',
      body: JSON.stringify({ role }),
    }),
  deactivateMembership: (membershipId: string) =>
    request<void>(`/memberships/${encodeURIComponent(membershipId)}`, { method: 'DELETE' }),
  getBoard: (projectId: string) => request<Board>(`/projects/${encodeURIComponent(projectId)}/board`),
  updateBoard: (input: Board) =>
    request<Board>(`/projects/${encodeURIComponent(input.projectId)}/board`, {
      method: 'PATCH',
      headers: { 'If-Match': `"${input.version}"` },
      body: JSON.stringify({
        name: input.name,
        type: input.type,
        columns: input.columns.map((column) => ({
          statusId: column.statusId,
          wipLimit: column.wipLimit,
          wipLimitMode: column.wipLimitMode,
        })),
      }),
    }),
  listWorkItemStatuses: (projectId: string) =>
    request<WorkItemStatusDefinition[]>(`/projects/${encodeURIComponent(projectId)}/statuses`),
  createWorkItemStatus: (
    projectId: string,
    input: { key: string; name: string; category: StatusCategory; order: number; colorToken: string },
  ) =>
    request<WorkItemStatusDefinition>(`/projects/${encodeURIComponent(projectId)}/statuses`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),
  updateWorkItemStatus: (
    projectId: string,
    status: WorkItemStatusDefinition,
    input: { name: string; category: StatusCategory; order: number; colorToken: string },
  ) =>
    request<WorkItemStatusDefinition>(
      `/projects/${encodeURIComponent(projectId)}/statuses/${encodeURIComponent(status.id)}`,
      {
        method: 'PATCH',
        headers: { 'If-Match': `"${status.version}"` },
        body: JSON.stringify(input),
      },
    ),
  deleteWorkItemStatus: (projectId: string, statusId: string) =>
    request<void>(`/projects/${encodeURIComponent(projectId)}/statuses/${encodeURIComponent(statusId)}`, {
      method: 'DELETE',
    }),
  setDefaultWorkItemStatus: (projectId: string, statusId: string) =>
    request<WorkItemStatusDefinition>(
      `/projects/${encodeURIComponent(projectId)}/statuses/${encodeURIComponent(statusId)}/default`,
      { method: 'POST' },
    ),
  getBoardViewPreference: (projectId: string) =>
    request<BoardViewPreference>(`/projects/${encodeURIComponent(projectId)}/board-view-preference`),
  updateBoardViewPreference: (projectId: string, preference: BoardViewPreference) =>
    request<BoardViewPreference>(`/projects/${encodeURIComponent(projectId)}/board-view-preference`, {
      method: 'PATCH',
      headers: { 'If-Match': `"${preference.version}"` },
      body: JSON.stringify({
        hideDoneItemsAfter: preference.hideDoneItemsAfter,
        columnSizeMode: preference.columnSizeMode,
        hiddenFields: preference.hiddenFields,
      }),
    }),
  listSprints: (projectId: string) =>
    request<Sprint[]>(`/projects/${encodeURIComponent(projectId)}/sprints`),
  createSprint: (projectId: string, name: string) =>
    request<Sprint>(`/projects/${encodeURIComponent(projectId)}/sprints`, {
      method: 'POST',
      body: JSON.stringify({ name }),
    }),
  updateSprint: (sprint: Sprint, input: UpdateSprintInput) =>
    request<Sprint>(`/sprints/${encodeURIComponent(sprint.id)}`, {
      method: 'PATCH',
      headers: { 'If-Match': `"${sprint.version}"` },
      body: JSON.stringify(input),
    }),
  startSprint: (sprint: Sprint, input: { goal: string | null; startDate: string | null; endDate: string | null }) =>
    request<Sprint>(`/sprints/${encodeURIComponent(sprint.id)}/start`, {
      method: 'POST',
      headers: { 'If-Match': `"${sprint.version}"` },
      body: JSON.stringify(input),
    }),
  completeSprint: (sprint: Sprint, rolloverTargetSprintId: string | null = null) =>
    request<Sprint>(`/sprints/${encodeURIComponent(sprint.id)}/complete`, {
      method: 'POST',
      headers: { 'If-Match': `"${sprint.version}"` },
      body: JSON.stringify({ rolloverTargetSprintId }),
    }),
  reopenSprint: (sprint: Sprint) =>
    request<Sprint>(`/sprints/${encodeURIComponent(sprint.id)}/reopen`, {
      method: 'POST',
      headers: { 'If-Match': `"${sprint.version}"` },
    }),
  assignWorkItemToSprint: (workItemId: string, sprintId: string) =>
    request<Sprint>(`/work-items/${encodeURIComponent(workItemId)}/sprint`, {
      method: 'PUT',
      body: JSON.stringify({ sprintId }),
    }),
  removeWorkItemFromSprint: (workItemId: string) =>
    request<Sprint>(`/work-items/${encodeURIComponent(workItemId)}/sprint`, { method: 'DELETE' }),
  getSprintReport: (sprintId: string) =>
    request<SprintReport>(`/sprints/${encodeURIComponent(sprintId)}/report`),
  getSprintInsights: (sprintId: string) =>
    request<SprintInsights>(`/sprints/${encodeURIComponent(sprintId)}/insights`),
  getSprintCumulativeFlowDiagram: (sprintId: string) =>
    request<CumulativeFlowDiagram>(`/sprints/${encodeURIComponent(sprintId)}/reports/cumulative-flow`),
  getSprintCycleTimeReport: (sprintId: string) =>
    request<CycleTimeReport>(`/sprints/${encodeURIComponent(sprintId)}/reports/cycle-time`),
  getSprintControlChart: (sprintId: string) =>
    request<ControlChart>(`/sprints/${encodeURIComponent(sprintId)}/reports/control-chart`),
  listTeams: () => request<Team[]>('/teams'),
  createTeam: (name: string) =>
    request<Team>('/teams', { method: 'POST', body: JSON.stringify({ name }) }),
  renameTeam: (teamId: string, name: string) =>
    request<Team>(`/teams/${encodeURIComponent(teamId)}`, {
      method: 'PUT',
      body: JSON.stringify({ name }),
    }),
  listTeamMembers: (teamId: string) =>
    request<TeamMembership[]>(`/teams/${encodeURIComponent(teamId)}/members`),
  addTeamMember: (teamId: string, membershipId: string) =>
    request<TeamMembership>(`/teams/${encodeURIComponent(teamId)}/members`, {
      method: 'POST',
      body: JSON.stringify({ membershipId }),
    }),
  removeTeamMember: (teamId: string, membershipId: string) =>
    request<void>(
      `/teams/${encodeURIComponent(teamId)}/members/${encodeURIComponent(membershipId)}`,
      { method: 'DELETE' },
    ),
  listInvitations: (filter?: { email?: string; status?: WorkspaceInvitationStatus }) => {
    const params = new URLSearchParams()
    if (filter?.email) params.set('email', filter.email)
    if (filter?.status) params.set('status', filter.status)
    const query = params.toString()
    return request<WorkspaceInvitation[]>(`/invitations${query ? `?${query}` : ''}`)
  },
  createInvitation: (input: { email: string; role: TenantRole; teamId: string | null; tier?: MembershipTier }) =>
    request<WorkspaceInvitation>('/invitations', { method: 'POST', body: JSON.stringify(input) }),
  revokeInvitation: (invitationId: string) =>
    request<void>(`/invitations/${encodeURIComponent(invitationId)}`, { method: 'DELETE' }),
  acceptInvitation: (
    tenantId: string,
    input: { token: string; displayName: string; password: string },
  ) =>
    request<TenantMembership>(
      `/workspaces/${encodeURIComponent(tenantId)}/invitations/accept`,
      { method: 'POST', body: JSON.stringify(input) },
      false,
    ),
  acceptInvitationWithExternalIdentity: (
    tenantId: string,
    input: { token: string; externalIdToken: string; displayName: string },
  ) =>
    request<TenantMembership>(
      `/workspaces/${encodeURIComponent(tenantId)}/invitations/accept-external`,
      { method: 'POST', body: JSON.stringify(input) },
      false,
    ),
  listSessions: () => request<SessionSummary[]>('/me/sessions'),
  revokeSession: (sessionId: string) =>
    request<void>(`/me/sessions/${encodeURIComponent(sessionId)}`, { method: 'DELETE' }),
  revokeOtherSessions: () => request<{ revokedCount: number }>('/me/sessions', { method: 'DELETE' }),
  listLinkedIdentities: () => request<ExternalIdentitySummary[]>('/me/external-identities'),
  linkExternalIdentity: (identityToken: string) =>
    request<ExternalIdentitySummary>('/me/external-identities', {
      method: 'POST',
      body: JSON.stringify({ identityToken }),
    }),
  unlinkExternalIdentity: (identityId: string) =>
    request<void>(`/me/external-identities/${encodeURIComponent(identityId)}`, { method: 'DELETE' }),
  startGoogleAccountLink: (returnUrl?: string) =>
    request<{ authorizeUrl: string }>('/me/external-identities/google/link-url', {
      method: 'POST',
      body: JSON.stringify({ returnUrl }),
    }),
}
