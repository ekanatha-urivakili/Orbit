export function LoadingScreen() {
  return (
    <main className="center-screen">
      <span className="brand-mark">O</span>
      <p>Loading your workspace…</p>
    </main>
  )
}

export function ErrorScreen({ message, correlationId }: { message: string; correlationId?: string }) {
  return (
    <main className="center-screen">
      <span className="brand-mark">O</span>
      <h1>Orbit is unavailable</h1>
      <p>{message}</p>
      {correlationId && (
        <p style={{ fontSize: '0.75rem', opacity: 0.7, fontFamily: 'monospace', userSelect: 'all' }}>
          Correlation ID: {correlationId}
        </p>
      )}
      <button className="primary-button" onClick={() => location.reload()}>Try again</button>
    </main>
  )
}

