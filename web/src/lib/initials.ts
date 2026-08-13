export function getInitials(name?: string): string {
  if (!name) return 'OR'
  return name
    .split(/\s+/)
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase()
}
