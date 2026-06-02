'use client'
import { useState } from 'react'

export default function LaunchReplayButton({ attemptId, hasReplay }) {
  const [status, setStatus] = useState('')

  const launch = () => {
    if (!hasReplay) return
    setStatus('Launchingâ€¦')
    // Open the local Unity port â€” it responds and closes the tab
    const win = window.open(`http://localhost:7779/?id=${attemptId}`, '_blank', 'width=400,height=200')
    setTimeout(() => {
      setStatus('Sent to the game')
      setTimeout(() => setStatus(''), 3000)
    }, 800)
  }

  if (!hasReplay) return (
    <div style={{ fontSize: 12, color: 'var(--muted)', textAlign: 'center' }}>
      <div>ğ¬</div>
      <div>No replay</div>
      <div>recorded</div>
    </div>
  )

  return (
    <div style={{ textAlign: 'center' }}>
      <button
        onClick={launch}
        style={{
          background: 'linear-gradient(135deg, #1d4ed8, #2563eb)',
          border: '1px solid rgba(99,179,255,0.3)',
          borderRadius: 10,
          color: '#fff',
          padding: '10px 20px',
          fontWeight: 700,
          fontSize: 14,
          cursor: 'pointer',
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          boxShadow: '0 0 20px rgba(59,130,246,0.3)',
        }}
      >
        â Play in game
      </button>
      {status && (
        <div style={{ fontSize: 12, color: 'var(--green)', marginTop: 6 }}>{status}</div>
      )}
    </div>
  )
}
