export function LoadingScreen() {
  return (
    <main className="center-screen">
      <span className="brand-mark">O</span>
      <p>Loading your workspace…</p>
    </main>
  )
}

export function ErrorScreen({ message }: { message: string }) {
  return (
    <main className="center-screen">
      <span className="brand-mark">O</span>
      <h1>Orbit is unavailable</h1>
      <p>{message}</p>
      <button className="primary-button" onClick={() => location.reload()}>Try again</button>
    </main>
  )
}
