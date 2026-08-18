import { getStoredLogoUrl } from '../../lib/branding'

interface AuthLogoProps {
  logoUrl?: string | null
  className?: string
}

export function AuthLogo({ logoUrl, className = '' }: AuthLogoProps) {
  const effectiveLogoUrl = logoUrl !== undefined ? logoUrl : getStoredLogoUrl()

  return (
    <div className={`flex items-center justify-center mb-6 ${className}`} data-testid="auth-logo-container">
      {effectiveLogoUrl ? (
        <img
          src={effectiveLogoUrl}
          alt="Workspace logo"
          className="max-h-12 max-w-[200px] h-auto w-auto object-contain rounded"
          data-testid="auth-custom-logo"
        />
      ) : (
        <div className="flex items-center justify-center gap-2.5" data-testid="auth-default-logo">
          <svg
            className="h-9 w-9 rounded-xl shadow-sm flex-shrink-0"
            viewBox="0 0 512 512"
            fill="none"
            aria-label="Orbit logo"
            role="img"
          >
            <defs>
              <linearGradient id="orbit-auth-grad" x1="0" y1="0" x2="1" y2="1">
                <stop stopColor="#7690ff" />
                <stop offset=".55" stopColor="#3861fb" />
                <stop offset="1" stopColor="#9a56ed" />
              </linearGradient>
            </defs>
            <rect width="512" height="512" rx="128" fill="#11182a" />
            <circle cx="256" cy="256" r="139" fill="none" stroke="url(#orbit-auth-grad)" strokeWidth="56" />
            <circle cx="382" cy="146" r="39" fill="#fff" />
          </svg>
          <span className="text-2xl font-bold tracking-tight text-gray-900">Orbit</span>
        </div>
      )}
    </div>
  )
}
