// Mirrors Sprint.NextRolloverName (src/Orbit.Domain/Boards/Sprint.cs) so the close-sprint dialogs
// can preview the auto-created sprint's name before the PM confirms.
export function nextRolloverName(closedSprintName: string, fallbackDate: Date): string {
  const match = /(\d+)\s*$/.exec(closedSprintName)
  if (!match) {
    const iso = fallbackDate.toISOString().slice(0, 10)
    return `Sprint ${iso}`
  }

  const digits = match[1]
  const next = String(Number(digits) + 1).padStart(digits.length, '0')
  return closedSprintName.slice(0, match.index) + next
}
