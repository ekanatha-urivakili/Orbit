import type { ReactNode } from 'react'

export function OnboardingShell({
  eyebrow,
  title,
  description,
  children,
}: {
  eyebrow: string
  title: string
  description: string
  children: ReactNode
}) {
  return (
    <main className="onboarding">
      <div className="brand"><span className="brand-mark">O</span><span>Orbit</span></div>
      <section>
        <p className="eyebrow">{eyebrow}</p>
        <h1>{title}</h1>
        <p>{description}</p>
        {children}
      </section>
    </main>
  )
}
