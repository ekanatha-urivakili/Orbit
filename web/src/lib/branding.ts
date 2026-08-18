export const STORAGE_KEY_CUSTOM_LOGO = 'orbit_workspace_logo_url'

export function getStoredLogoUrl(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY_CUSTOM_LOGO) || null
  } catch {
    return null
  }
}

export function setStoredLogoUrl(url: string | null | undefined): void {
  try {
    if (url) {
      localStorage.setItem(STORAGE_KEY_CUSTOM_LOGO, url)
    } else {
      localStorage.removeItem(STORAGE_KEY_CUSTOM_LOGO)
    }
  } catch {
    // Ignore storage quota/permission errors
  }
}
