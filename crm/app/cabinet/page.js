'use client'
import { useState, useEffect } from 'react'
import Link from 'next/link'
import { LogoMark } from '@/components/Logo'

function fmt(date) {
  return new Date(date).toLocaleString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}
function dur(s) {
  if (!s) return '—'
  const m = Math.floor(s / 60), sec = Math.round(s % 60)
  return `${m}:${String(sec).padStart(2, '0')}`
}
function initials(name) {
  return name?.split(' ').slice(0, 2).map(w => w[0]).join('').toUpperCase() || '?'
}

export default function CabinetPage() {
  const [step, setStep]         = useState('login')
  const [phone, setPhone]       = useState('')
  const [password, setPass]     = useState('')
  const [error, setError]       = useState('')
  const [busy, setBusy]         = useState(false)
  const [user, setUser]         = useState(null)
  const [attempts, setAttempts] = useState([])

  useEffect(() => {
    const saved = localStorage.getItem('cabinet_user')
    if (saved) {
      const u = JSON.parse(saved)
      setUser(u); loadAttempts(u.id); setStep('data')
    }
  }, [])

  const login = async () => {
    setError('')
    if (!phone || !password) { setError('Fill in all fields'); return }
    setBusy(true)
    const res  = await fetch('/api/auth/login', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ phone, password }),
    })
    const data = await res.json()
    setBusy(false)
    if (!res.ok) { setError(data.error || 'Error'); return }
    localStorage.setItem('cabinet_user', JSON.stringify({ id: data.id, fullName: data.fullName, phone: data.phone }))
    setUser(data); loadAttempts(data.id); setStep('data')
  }

  const loadAttempts = async (id) => {
    const res  = await fetch(`/api/students/${id}`)
    const data = await res.json()
    setAttempts(data.attempts || [])
  }

  const logout = () => {
    localStorage.removeItem('cabinet_user')
    setUser(null); setAttempts([]); setStep('login')
    setPhone(''); setPass('')
  }

  const passed = attempts.filter(a => a.completed !== false && a.passed).length

  if (step === 'login') return (
    <div className="login-wrap">
      <div className="login-card">
        <div className="login-logo">
          <LogoMark size={54} />
          <h1 style={{ marginTop: 12 }}>Student Portal</h1>
          <p>View your exam results</p>
        </div>
        <div className="road-stripe" style={{ marginBottom: 24 }} />
        <div className="form-field">
          <label>Phone number</label>
          <input type="tel" value={phone} placeholder="+1 (___) ___-____"
            onChange={e => setPhone(e.target.value)} />
        </div>
        <div className="form-field">
          <label>Password</label>
          <input type="password" value={password} placeholder="Enter password"
            onChange={e => setPass(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && login()} />
        </div>
        {error && <div className="error-msg">{error}</div>}
        <button className="btn-primary btn-block" onClick={login} disabled={busy}>
          {busy ? 'Loading…' : 'Sign in'}
        </button>
        <div style={{ textAlign: 'center', marginTop: 16 }}>
          <a href="/game-login" style={{ fontSize: 13, color: 'var(--muted2)' }}>No account? Sign up →</a>
        </div>
      </div>
    </div>
  )

  return (
    <div>
      <div className="road-stripe" />

      {/* Profile */}
      <div className="student-hero" style={{ marginBottom: 28 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 18 }}>
          <div className="student-avatar">{initials(user?.fullName)}</div>
          <div className="student-info">
            <div className="student-name">{user?.fullName}</div>
            <div className="student-meta">
              <span> {user?.phone}</span>
            </div>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 36 }}>
          <div className="student-stats">
            {[
              { val: attempts.length, lbl: 'Attempts', color: 'var(--text)'  },
              { val: passed,          lbl: 'Passed',   color: 'var(--green)' },
              { val: attempts.length - passed, lbl: 'Failed', color: (attempts.length - passed) > 0 ? 'var(--red)' : 'var(--muted)' },
            ].map(s => (
              <div key={s.lbl} className="student-stat">
                <div className="val" style={{ color: s.color }}>{s.val}</div>
                <div className="lbl">{s.lbl}</div>
              </div>
            ))}
          </div>
          <button className="ghost" onClick={logout}>Log out →</button>
        </div>
      </div>

      <div className="section-title">My exams</div>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Date</th><th>Result</th><th>Penalty points</th>
              <th>Duration</th><th>Errors</th><th></th>
            </tr>
          </thead>
          <tbody>
            {attempts.length === 0 && (
              <tr><td colSpan={6}>
                <div className="empty-state">
                  <div className="icon"></div>
                  <p>You haven't taken an exam in the simulator yet</p>
                </div>
              </td></tr>
            )}
            {attempts.map(a => (
              <tr key={a._id}>
                <td style={{ color: 'var(--muted2)' }}>{fmt(a.timestamp)}</td>
                <td>
                  {a.completed === false
                    ? <span className="badge warn">INCOMPLETE</span>
                    : <span className={`badge ${a.passed ? 'pass' : 'fail'}`}>{a.passed ? 'PASSED' : 'FAILED'}</span>}
                </td>
                <td><span style={{ fontWeight: 700, color: a.totalPenaltyPoints >= 100 ? 'var(--red)' : 'var(--text)' }}>{a.totalPenaltyPoints ?? '—'}</span></td>
                <td style={{ color: 'var(--muted2)' }}>{dur(a.examDuration)}</td>
                <td style={{ color: 'var(--muted2)' }}>{a.penalties?.length ?? 0}</td>
                <td>
                  <Link href={`/attempts/${a._id}`}>
                    <button className="ghost">Details →</button>
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
