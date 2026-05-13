'use client'
import { useState } from 'react'
import { useRouter } from 'next/navigation'

export default function AdminLoginPage() {
  const [password, setPass] = useState('')
  const [error, setError]   = useState('')
  const [busy, setBusy]     = useState(false)
  const router = useRouter()

  const login = async () => {
    setError('')
    setBusy(true)
    const res  = await fetch('/api/admin/login', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ password }),
    })
    const data = await res.json()
    setBusy(false)
    if (!res.ok) { setError(data.error || 'Ошибка'); return }
    router.push('/')
  }

  return (
    <div style={{ maxWidth: 360, margin: '80px auto' }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 24 }}>Вход в систему</h1>
      <div className="card">
        <div style={{ marginBottom: 16 }}>
          <label style={{ display: 'block', color: 'var(--muted)', fontSize: 12, marginBottom: 6 }}>ПАРОЛЬ АДМИНИСТРАТОРА</label>
          <input type="password" value={password} onChange={e => setPass(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && login()} style={{ width: '100%' }} />
        </div>
        {error && <p style={{ color: 'var(--red)', marginBottom: 12, fontSize: 13 }}>{error}</p>}
        <button onClick={login} disabled={busy} style={{ width: '100%' }}>
          {busy ? 'Вход...' : 'Войти'}
        </button>
      </div>
    </div>
  )
}
