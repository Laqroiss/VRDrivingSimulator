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
    if (!res.ok) { setError(data.error || ''); return }
    router.push('/admin')
  }

  return (
    <div className="login-wrap">
      <div className="login-card">
        <div className="login-logo">
          <div className="icon">ð—</div>
          <h1>VRDrive CRM</h1>
          <p>  </p>
        </div>
        <div className="road-stripe" style={{ marginBottom: 24 }} />
        <div className="form-field">
          <label> </label>
          <input type="password" value={password} placeholder=" "
            onChange={e => setPass(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && login()} />
        </div>
        {error && <div className="error-msg">{error}</div>}
        <button className="btn-primary btn-block" onClick={login} disabled={busy}>
          {busy ? '...' : '  '}
        </button>
        <div style={{ textAlign: 'center', marginTop: 16 }}>
          <a href="/cabinet" style={{ fontSize: 13, color: 'var(--muted2)' }}>   â†’</a>
        </div>
      </div>
    </div>
  )
}
