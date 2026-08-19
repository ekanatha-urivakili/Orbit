export function applyTheme(preference: string) {
  const resolved =
    preference === 'system'
      ? (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')
      : preference
  document.documentElement.dataset.theme = resolved
}
