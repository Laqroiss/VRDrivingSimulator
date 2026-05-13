'use client'
import { useState, useEffect } from 'react'
import Link from 'next/link'

function fmt(date) {
  return new Date(date).toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}
function dur(s) {
  if (!s) return '—'
  const m = Math.floor(s / 60), sec = Math.round(s % 60)
  return `${m}:${String(sec).padStart(2, '0')}`
}

export default function CabinetPage() {
  const [step, setStep]       = useState('login') // login | data
  const [phone, setPhone]     = useState('')
  const [password, setPass]   = useState('')
  const [error, setError]     = useState('')
  const [busy, setBusy]       = useState(false)
  const [user, setUser]       = useState(null)
  const [attempts, setAttempts] = useState([])

  useEffect(() => {
    const saved = localStorage.getItem('cabinet_user')
    if (saved) {
      const u = JSON.parse(saved)
      setUser(u)
      loadAttempts(u.id)
      setStep('data')
    }
  }, [])

  const login = async () => {
    setError('')
    if (!phone || !password) { setError('Заполните все поля'); return }
    setBusy(true)
    const res  = await fetch('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ phone, password }),
    })
    const data = await res.json()
    setBusy(false)
    if (!res.ok) { setError(data.error || 'Ошибка'); return }
    localStorage.setItem('cabinet_user', JSON.stringify({ id: data.id, fullName: data.fullName, phone: data.phone }))
    setUser(data)
    loadAttempts(data.id)
    setStep('data')
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

  const passed = attempts.filter(a => a.passed).length

  if (step === 'login') return (
    <div style={{ maxWidth: 400, margin: '60px auto' }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 24 }}>Личный кабинет</h1>
      <div className="card">
        <div style={{ marginBottom: 14 }}>
          <label style={{ display: 'block', color: 'var(--muted)', fontSize: 12, marginBottom: 6 }}>ТЕЛЕФОН</label>
          <input type="text" value={phone} onChange={e => setPhone(e.target.value)} placeholder="+7 (___) ___-__-__" style={{ width: '100%' }} />
        </div>
        <div style={{ marginBottom: 20 }}>
          <label style={{ display: 'block', color: 'var(--muted)', fontSize: 12, marginBottom: 6 }}>ПАРОЛЬ</label>
          <input type="password" value={password} onChange={e => setPass(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && login()} style={{ width: '100%' }} />
        </div>
        {error && <p style={{ color: 'var(--red)', marginBottom: 12, fontSize: 13 }}>{error}</p>}
        <button onClick={login} disabled={busy} style={{ width: '100%' }}>
          {busy ? 'Загрузка...' : 'Войти'}
        </button>
      </div>
    </div>
  )

  return (
    <div>
      {/* Шапка профиля */}
      <div className="card" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>
            <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 4 }}>{user?.fullName}</h1>
            <p style={{ color: 'var(--muted)' }}>📞 {user?.phone}</p>
          </div>
          <div style={{ display: 'flex', gap: 32, textAlign: 'center', marginRight: 32 }}>
            <div>
              <div style={{ fontSize: 28, fontWeight: 700 }}>{attempts.length}</div>
              <div style={{ color: 'var(--muted)', fontSize: 12 }}>ПОПЫТОК</div>
            </div>
            <div>
              <div style={{ fontSize: 28, fontWeight: 700, color: 'var(--green)' }}>{passed}</div>
              <div style={{ color: 'var(--muted)', fontSize: 12 }}>СДАЛ</div>
            </div>
            <div>
              <div style={{ fontSize: 28, fontWeight: 700, color: (attempts.length - passed) > 0 ? 'var(--red)' : 'var(--muted)' }}>
                {attempts.length - passed}
              </div>
              <div style={{ color: 'var(--muted)', fontSize: 12 }}>НЕ СДАЛ</div>
            </div>
          </div>
          <button className="ghost" onClick={logout}>Выйти</button>
        </div>
      </div>

      {/* Попытки */}
      <h2 style={{ fontSize: 16, fontWeight: 600, marginBottom: 12 }}>Мои экзамены</h2>
      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        <table>
          <thead>
            <tr>
              <th>Дата</th>
              <th>Результат</th>
              <th>Штрафных баллов</th>
              <th>Длительность</th>
              <th>Ошибок</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {attempts.length === 0 && (
              <tr><td colSpan={6} style={{ textAlign: 'center', color: 'var(--muted)', padding: 40 }}>
                Вы ещё не проходили экзамен
              </td></tr>
            )}
            {attempts.map(a => (
              <tr key={a._id}>
                <td style={{ color: 'var(--muted)' }}>{fmt(a.timestamp)}</td>
                <td><span className={`badge ${a.passed ? 'pass' : 'fail'}`}>{a.passed ? 'СДАЛ' : 'НЕ СДАЛ'}</span></td>
                <td style={{ color: a.totalPenaltyPoints >= 100 ? 'var(--red)' : 'var(--text)' }}>{a.totalPenaltyPoints ?? '—'}</td>
                <td style={{ color: 'var(--muted)' }}>{dur(a.examDuration)}</td>
                <td style={{ color: 'var(--muted)' }}>{a.penalties?.length ?? 0}</td>
                <td>
                  <Link href={`/attempts/${a._id}`}>
                    <button className="ghost" style={{ fontSize: 12 }}>Подробнее →</button>
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
