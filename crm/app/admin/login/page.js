'use client'
import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { LogoMark } from '@/components/Logo'

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
    if (!res.ok) { setError(data.error || 'Error'); return }
    router.push('/admin')
  }

  return (
    <div className="login-wrap">
      <div className="login-card">
        <div className="login-logo">
          <LogoMark size={54} />
          <h1 style={{ marginTop: 12 }}>VRDrive CRM</h1>
          <p>Driving school management system</p>
        </div>
        <div className="road-stripe" style={{ marginBottom: 24 }} />
        <div className="form-field">
          <label>Admin password</label>
          <input type="password" value={password} placeholder="Enter password"
            onChange={e => setPass(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && login()} />
        </div>
        {error && <div className="error-msg">{error}</div>}
        <button className="btn-primary btn-block" onClick={login} disabled={busy}>
          {busy ? 'Signing in…' : 'Sign in'}
        </button>
        <div style={{ textAlign: 'center', marginTop: 16 }}>
          <a href="/cabinet" style={{ fontSize: 13, color: 'var(--muted2)' }}>Student portal →</a>
        </div>
      </div>
    </div>
  )
}
