'use client'
import { useState } from 'react'

export default function LaunchReplayButton({ attemptId, hasReplay }) {
  const [status, setStatus] = useState('')

  const launch = () => {
    if (!hasReplay) return
    setStatus('Запуск...')
    // Открываем локальный порт Unity — он ответит и закроет вкладку
    const win = window.open(`http://localhost:7779/?id=${attemptId}`, '_blank', 'width=400,height=200')
    setTimeout(() => {
      setStatus('Команда отправлена в игру')
      setTimeout(() => setStatus(''), 3000)
    }, 800)
  }

  if (!hasReplay) return (
    <div style={{ fontSize: 12, color: 'var(--muted)', textAlign: 'center' }}>
      <div>🎬</div>
      <div>Повтор</div>
      <div>не записан</div>
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
        ▶ Запустить в игре
      </button>
      {status && (
        <div style={{ fontSize: 12, color: 'var(--green)', marginTop: 6 }}>{status}</div>
      )}
    </div>
  )
}
