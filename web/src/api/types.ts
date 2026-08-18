export type WorkItemType = 'Initiative' | 'Epic' | 'Task' | 'Story' | 'Spike' | 'Test' | 'Feature' | 'Request' | 'Bug' | 'Subtask'
export type WorkItemLinkKind = 'Blocks' | 'RelatesTo' | 'Duplicates'
export type WorkItemLinkDirection = 'Outgoing' | 'Incoming'

export type WorkItemStatus =
  | 'Backlog'
  | 'Selected'
  | 'InProgress'
  | 'InReview'
  | 'Done'
  | 'Blocked'

export type Priority = 'Lowest' | 'Low' | 'Medium' | 'High' | 'Highest'

export interface PagedResult<T> {
  items: T[]
  totalCount: number
}

export interface Project {
  id: string
  key: string
  name: string
  version: number
  createdAt: string
}

export interface WorkItem {
  id: string
  projectId: string
  key: string
  summary: string
  description: string | null
  parentId: string | null
  epicName: string | null
  acceptanceCriteria: string | null
  stepsToConduct: string | null
  assigneeUserId: string | null
  developerUserId: string | null
  productOwnerUserId: string | null
  sprintName: string | null
  identifiedOn: string | null
  storyPoints: number | null
  labels: string[]
  countries: string[]
  attachmentNames: string[]
  type: WorkItemType
  status: WorkItemStatus
  priority: Priority
  rank: number
  version: number
  createdAt: string
  updatedAt: string
}

export interface WorkItemLink {
  id: string
  kind: WorkItemLinkKind
  direction: WorkItemLinkDirection
  workItemId: string
  key: string
  summary: string
  type: WorkItemType
  status: WorkItemStatus
}

export interface WorkItemComment {
  id: string
  tenantId: string
  workItemId: string
  authorMembershipId: string
  authorDisplayName: string
  authorAvatarUrl: string | null
  body: string | null
  mentionedUserIds: string[]
  version: number
  isDeleted: boolean
  createdAt: string
  updatedAt: string
  lastEditedAt: string | null
}

export interface WorkItemAttachment {
  id: string
  workItemId: string
  fileName: string
  contentType: string
  sizeBytes: number
  uploadedByMembershipId: string
  uploadedAt: string
  downloadUrl: string
}

export interface PresignedAttachmentUpload {
  uploadUrl: string
  objectKey: string
  expiresAt: string
}

export interface Choice {
  id: string
  value: string
  label: string
  description: string
  order: number
  colorToken: string
  enabled: boolean
}

export interface SystemChoices {
  workItemTypes: Choice[]
  workItemStatuses: Choice[]
  priorities: Choice[]
}

export interface WorkItemTypeDefinition {
  id: WorkItemType
  label: string
  description: string
  order: number
  colorToken: string
  enabled: boolean
  canAdminister: boolean
  version: number
}

export type CustomFieldType = 'Text' | 'Number' | 'Date' | 'Checkbox'

export interface CustomFieldDefinition {
  id: string
  key: string
  label: string
  fieldType: CustomFieldType
  required: boolean
  order: number
  enabled: boolean
  version: number
}

export interface CreateWorkItemInput {
  projectId: string
  summary: string
  description: string | null
  type: WorkItemType
  priority: Priority
  parentId?: string | null
  epicName?: string | null
  acceptanceCriteria?: string | null
  stepsToConduct?: string | null
  assigneeUserId?: string | null
  developerUserId?: string | null
  productOwnerUserId?: string | null
  sprintName?: string | null
  identifiedOn?: string | null
  storyPoints?: number | null
  labels?: string[]
  countries?: string[]
  attachmentNames?: string[]
}

export interface UpdateWorkItemInput {
  summary: string
  description: string | null
  priority: Priority
  parentId?: string | null
  epicName?: string | null
  acceptanceCriteria?: string | null
  stepsToConduct?: string | null
  assigneeUserId?: string | null
  developerUserId?: string | null
  productOwnerUserId?: string | null
  sprintName?: string | null
  identifiedOn?: string | null
  storyPoints?: number | null
  labels?: string[]
  countries?: string[]
  attachmentNames?: string[]
}

export interface BootstrapStatus {
  initializationRequired: boolean
}

export interface BootstrapInput {
  displayName: string
  email: string
  password: string
  workspaceName: string
}

export interface BootstrapResult {
  userId: string
  email: string
  displayName: string
  workspaceId: string
  workspaceSlug: string
  workspaceName: string
  membershipId: string
}

export interface RegisterInput {
  displayName: string
  email: string
  password: string
  organizationName: string
  workspaceName: string
}

export type ThemePreference = 'System' | 'Light' | 'Dark'
export type DensityPreference = 'Comfortable' | 'Compact'
export type DigestCadence = 'None' | 'Daily' | 'Weekly'

export interface Profile {
  userId: string
  email: string
  displayName: string
  avatarUrl: string | null
  version: number
  locale: string
  timeZone: string
  theme: ThemePreference
  density: DensityPreference
  reduceMotion: boolean
  highContrast: boolean
  preferenceVersion: number
}

export interface NotificationPreference {
  inAppEnabled: boolean
  emailEnabled: boolean
  digestCadence: DigestCadence
  quietHoursStart: string | null
  quietHoursEnd: string | null
  selfNotify: boolean
  version: number
}

export interface WorkspaceSetting {
  workspaceId: string
  workspaceName: string
  description: string | null
  defaultLocale: string
  defaultTimeZone: string
  allowMemberProjectCreation: boolean
  logoUrl: string | null
  canAdminister: boolean
  version: number
}

export interface PresignedWorkspaceLogoUpload {
  uploadUrl: string
  objectKey: string
  expiresAt: string
}

export interface ProjectSetting {
  projectId: string
  defaultWorkItemType: WorkItemType
  defaultPriority: Priority
  enableReleases: boolean
  enableTimeTracking: boolean
  repositoryUrl: string | null
  version: number
}

export interface TypographySetting {
  leftFontFamily: string
  leftFontColor: string
  leftFontSizePx: number
  middleFontFamily: string
  middleFontColor: string
  middleFontSizePx: number
  rightFontFamily: string
  rightFontColor: string
  rightFontSizePx: number
  controlHeightPx: number
  controlFontSizePx: number
  canAdminister: boolean
  version: number
}

export type PrincipalType = 'User' | 'ServiceAccount'
export type TenantRole = 'Owner' | 'Administrator' | 'Member'
export type ProjectRole = 'Administrator' | 'Member' | 'Viewer'
export type MembershipTier = 'Standard' | 'Guest'

export interface TenantMembership {
  id: string
  userId: string | null
  issuer: string | null
  subject: string | null
  principalType: PrincipalType
  role: TenantRole
  tier: MembershipTier
  isActive: boolean
  createdAt: string
  displayName: string | null
  avatarUrl: string | null
}

export interface CreateMembershipInput {
  issuer: string
  subject: string
  principalType: PrincipalType
  role: TenantRole
}

export interface ProjectRoleAssignment {
  id: string
  projectId: string
  membershipId: string
  role: ProjectRole
  createdAt: string
}

export interface Team {
  id: string
  name: string
  createdByMembershipId: string
  createdAt: string
}

export interface TeamMembership {
  id: string
  teamId: string
  membershipId: string
  createdAt: string
}

export type WorkspaceInvitationStatus = 'Active' | 'Accepted' | 'Revoked'

export interface WorkspaceInvitation {
  id: string
  email: string
  role: TenantRole
  tier: MembershipTier
  teamId: string | null
  status: WorkspaceInvitationStatus
  expiresAt: string
  createdAt: string
  acceptedAt: string | null
}

export type BoardType = 'Kanban' | 'Scrum'
export type WipLimitMode = 'Warn' | 'Block'

export interface BoardColumn {
  status: WorkItemStatus
  order: number
  wipLimit: number | null
  wipLimitMode: WipLimitMode
}

export interface Board {
  projectId: string
  name: string
  type: BoardType
  version: number
  columns: BoardColumn[]
}

export type SprintState = 'Future' | 'Active' | 'Closing' | 'Closed' | 'Reopened'

export interface Sprint {
  id: string
  projectId: string
  name: string
  goal: string | null
  state: SprintState
  startDate: string | null
  endDate: string | null
  version: number
  workItemIds: string[]
}

export interface BurndownPoint {
  date: string
  remainingPoints: number
}

export interface SprintScopeChange {
  workItemId: string | null
  factType: 'SprintAdded' | 'SprintRemoved' | 'EstimateChanged' | 'StatusChanged' | 'ColumnChanged' | 'SprintCompleted' | 'SprintReopened'
  estimateDelta: number | null
  occurredAt: string
}

export interface SprintReport {
  sprintId: string
  sprintName: string
  state: SprintState
  startDate: string | null
  endDate: string | null
  committedPoints: number
  completedPoints: number
  addedAfterStartPoints: number
  removedAfterStartPoints: number
  burndown: BurndownPoint[]
  scopeChanges: SprintScopeChange[]
}

export interface AuthSession {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
  sessionId: string
  userId: string
  displayName: string
  email: string
  workspaceId: string
  workspaceSlug: string
  workspaceName: string
  role: TenantRole
}

export interface AccountWorkspace {
  id: string
  slug: string
  name: string
  role: TenantRole
}

export interface SiteCapabilities {
  canCreateWorkspace: boolean
}

export interface CreatedWorkspace {
  id: string
  slug: string
  name: string
  membershipId: string
  role: TenantRole
}

export interface SessionSummary {
  sessionId: string
  workspaceId: string
  workspaceName: string
  userAgent: string | null
  ipAddress: string | null
  createdAt: string
  lastUsedAt: string
  expiresAt: string
  isCurrent: boolean
}

export interface ExternalIdentitySummary {
  id: string
  issuer: string
  subject: string
  createdAt: string
}
