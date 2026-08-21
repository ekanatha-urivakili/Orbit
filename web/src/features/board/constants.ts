import type { WorkItemStatusDefinition } from '../../api/types'

/** Presentational metadata for a workflow status, sourced from the project's own status catalog (§13.5 "Edit workflow") rather than a fixed enum. */
export function statusMeta(status: WorkItemStatusDefinition): { label: string; tone: string } {
  return { label: status.name, tone: status.colorToken }
}
