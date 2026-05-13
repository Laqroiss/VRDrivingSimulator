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
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ password }),
    })
    const data = await res.json()
    setBusy(false)
    if (!res.ok) { setError(data.error || 'Ошибка'); return }
    router.push('/admin')
  }

  return (
    <div className="login-wrap">
      <div className="login-card">
        <div className="login-logo">
          <div className="icon">🚗</div>
          <h1>VRDrive CRM</h1>
          <p>Система управления автошколой</p>
        </div>
        <div className="road-stripe" style={{ marginBottom: 24 }} />
        <div className="form-field">
          <label>Пароль администратора</label>
          <input type="password" value={password} placeholder="Введите пароль"
            onChange={e => setPass(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && login()} />
        </div>
        {error && <div className="error-msg">{error}</div>}
        <button className="btn-primary btn-block" onClick={login} disabled={busy}>
          {busy ? 'Вход...' : 'Войти в систему'}
        </button>
        <div style={{ textAlign: 'center', marginTop: 16 }}>
          <a href="/cabinet" style={{ fontSize: 13, color: 'var(--muted2)' }}>Личный кабинет курсанта →</a>
        </div>
      </div>
    </div>
  )
}
