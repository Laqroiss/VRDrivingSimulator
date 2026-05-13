'use client'
import { useState } from 'react'
import { useRouter } from 'next/navigation'

export default function EditStudentButton({ student }) {
  const [open, setOpen]       = useState(false)
  const [fullName, setName]   = useState(student.fullName)
  const [phone, setPhone]     = useState(student.phone)
  const [password, setPass]   = useState('')
  const [error, setError]     = useState('')
  const [busy, setBusy]       = useState(false)
  const [saved, setSaved]     = useState(false)
  const router = useRouter()

  const save = async () => {
    setError('')
    if (!fullName.trim() || !phone.trim()) { setError('Имя и телефон обязательны'); return }
    setBusy(true)
    const res  = await fetch(`/api/students/${student._id}`, {
      method:  'PUT',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ fullName, phone, password: password || undefined }),
    })
    const data = await res.json()
    setBusy(false)
    if (!res.ok) { setError(data.error || 'Ошибка'); return }
    setSaved(true)
    setTimeout(() => { setSaved(false); setOpen(false); router.refresh() }, 800)
  }

  return (
    <>
      <button className="ghost" onClick={() => setOpen(true)}>✏️ Редактировать</button>

      {open && (
        <div style={{
          position: 'fixed', inset: 0, zIndex: 200,
          background: 'rgba(0,0,0,0.7)', backdropFilter: 'blur(6px)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
        }} onClick={e => { if (e.target === e.currentTarget) setOpen(false) }}>

          <div style={{
            background: 'var(--surface)', border: '1px solid var(--border)',
            borderRadius: 16, padding: 32, width: 440,
          }}>
            {/* Заголовок */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
              <div style={{ fontWeight: 800, fontSize: 17 }}>Редактировать курсанта</div>
              <button className="ghost" onClick={() => setOpen(false)} style={{ padding: '4px 10px' }}>✕</button>
            </div>

            <div className="road-stripe" style={{ marginBottom: 20 }} />

            <div className="form-field">
              <label>ФИО</label>
              <input type="text" value={fullName} onChange={e => setName(e.target.value)} />
            </div>

            <div className="form-field">
              <label>Телефон</label>
              <input type="tel" value={phone} onChange={e => setPhone(e.target.value)} />
            </div>

            <div className="form-field">
              <label>Новый пароль <span style={{ color: 'var(--muted)', fontWeight: 400, textTransform: 'none', letterSpacing: 0 }}>(оставьте пустым чтобы не менять)</span></label>
              <input type="password" value={password} onChange={e => setPass(e.target.value)}
                placeholder="••••••••" />
            </div>

            {error && <div className="error-msg">{error}</div>}

            {saved && (
              <div style={{ color: 'var(--green)', background: 'rgba(34,197,94,.1)', border: '1px solid rgba(34,197,94,.25)', borderRadius: 7, padding: '9px 12px', marginBottom: 12, fontSize: 13 }}>
                ✓ Сохранено успешно
              </div>
            )}

            <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end', marginTop: 8 }}>
              <button className="ghost" onClick={() => setOpen(false)} disabled={busy}>Отмена</button>
              <button className="btn-primary" onClick={save} disabled={busy}>
                {busy ? 'Сохранение...' : 'Сохранить'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
