'use client'
import { useState } from 'react'

export default function GameLoginPage({ searchParams }) {
  const port = searchParams?.port || '7777'

  const [mode, setMode]       = useState('login')   // 'login' | 'register'
  const [phone, setPhone]     = useState('')
  const [fullName, setName]   = useState('')
  const [password, setPass]   = useState('')
  const [status, setStatus]   = useState('')
  const [error, setError]     = useState('')
  const [busy, setBusy]       = useState(false)
  const [done, setDone]       = useState(false)

  const redirectToGame = (user) => {
    const name  = encodeURIComponent(user.fullName)
    const ph    = encodeURIComponent(user.phone)
    window.location.href = `http://localhost:${port}/?id=${user.id}&name=${name}&phone=${ph}`
  }

  const submit = async () => {
    setError('')
    if (!phone || !password) { setError('Заполните все поля'); return }
    if (mode === 'register' && !fullName) { setError('Введите ФИО'); return }
    setBusy(true)

    const url  = mode === 'login' ? '/api/auth/login' : '/api/auth/register'
    const body = mode === 'login'
      ? { phone, password }
      : { phone, fullName, password }

    try {
      const res  = await fetch(url, {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify(body),
      })
      const data = await res.json()
      if (!res.ok) { setError(data.error || 'Ошибка'); setBusy(false); return }
      setDone(true)
      setStatus(`Добро пожаловать, ${data.fullName?.split(' ')[0]}! Возвращаемся в игру...`)
      setTimeout(() => redirectToGame(data), 1000)
    } catch {
      setError('Нет соединения с сервером')
      setBusy(false)
    }
  }

  return (
    <div style={s.page}>
      <div style={s.card}>

        {/* Header */}
        <div style={s.header}>
          <div style={s.logo}>⟳</div>
          <div>
            <div style={s.kicker}>DRIVING SCHOOL SYSTEM</div>
            <div style={s.appTitle}>СИМУЛЯТОР ВОЖДЕНИЯ</div>
          </div>
        </div>

        <div style={s.divider} />

        {done ? (
          <div style={{ textAlign: 'center', padding: '32px 0' }}>
            <div style={{ fontSize: 40, marginBottom: 16 }}>✅</div>
            <div style={{ color: '#26d989', fontSize: 18 }}>{status}</div>
          </div>
        ) : (
          <>
            <h2 style={s.modeTitle}>
              {mode === 'login' ? 'Войти в систему' : 'Регистрация'}
            </h2>

            <div style={s.field}>
              <label style={s.label}>НОМЕР ТЕЛЕФОНА</label>
              <input style={s.input} placeholder="+7 (___) ___-__-__"
                value={phone} onChange={e => setPhone(e.target.value)} />
            </div>

            {mode === 'register' && (
              <div style={s.field}>
                <label style={s.label}>ФИО</label>
                <input style={s.input} placeholder="Фамилия Имя Отчество"
                  value={fullName} onChange={e => setName(e.target.value)} />
              </div>
            )}

            <div style={s.field}>
              <label style={s.label}>ПАРОЛЬ</label>
              <input style={s.input} type="password" placeholder="••••••••"
                value={password} onChange={e => setPass(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && submit()} />
            </div>

            {error && <div style={s.error}>{error}</div>}

            <button style={{ ...s.btn, opacity: busy ? 0.6 : 1 }}
              onClick={submit} disabled={busy}>
              {busy ? 'Загрузка...' : mode === 'login' ? 'Войти' : 'Зарегистрироваться'}
            </button>

            <button style={s.toggle}
              onClick={() => { setMode(mode === 'login' ? 'register' : 'login'); setError('') }}>
              {mode === 'login'
                ? 'Нет аккаунта? Зарегистрироваться'
                : 'Уже есть аккаунт? Войти'}
            </button>
          </>
        )}

        <div style={s.footer}>
          <span>● ГОС. ЛИЦЕНЗИЯ №2024-АВ</span>
          <span>v 3.4.1 / VR</span>
        </div>
      </div>
    </div>
  )
}

const s = {
  page:     { minHeight:'100vh', background:'#05070d', display:'flex', alignItems:'center', justifyContent:'center', fontFamily:'Montserrat,sans-serif' },
  card:     { width:420, background:'#1a1f2e', borderRadius:16, padding:'34px 32px 28px', border:'1px solid rgba(120,170,230,0.22)', boxShadow:'0 0 40px rgba(74,158,255,0.18)' },
  header:   { display:'flex', alignItems:'center', gap:14, marginBottom:6 },
  logo:     { width:46, height:46, background:'#0e1626', borderRadius:12, border:'1px solid rgba(74,158,255,0.35)', display:'flex', alignItems:'center', justifyContent:'center', fontSize:22, color:'#7fc7ff', flexShrink:0 },
  kicker:   { color:'#00D4FF', fontSize:10, fontWeight:700, letterSpacing:'0.22em', marginBottom:3 },
  appTitle: { color:'#fff', fontSize:14, fontWeight:700, letterSpacing:'0.18em' },
  divider:  { height:1, background:'rgba(120,170,230,0.22)', margin:'22px 0 4px' },
  modeTitle:{ color:'#fff', fontSize:22, fontWeight:600, margin:'14px 0 22px' },
  field:    { marginBottom:14 },
  label:    { display:'block', color:'#5b657a', fontSize:10, fontWeight:700, letterSpacing:'0.18em', marginBottom:8 },
  input:    { width:'100%', height:48, background:'#0f1320', border:'1px solid rgba(120,160,220,0.18)', borderRadius:10, color:'#fff', fontSize:14, padding:'0 14px', outline:'none', boxSizing:'border-box', fontFamily:'inherit' },
  error:    { color:'#ff5a6a', fontSize:12, marginTop:8, marginBottom:4 },
  btn:      { width:'100%', height:50, background:'linear-gradient(180deg,#4fa3ff,#2b7fdc)', border:'none', borderRadius:10, color:'#fff', fontWeight:700, fontSize:14, letterSpacing:'0.14em', cursor:'pointer', marginTop:8 },
  toggle:   { width:'100%', background:'none', border:'none', color:'#4A9EFF', fontSize:13, cursor:'pointer', marginTop:16, fontFamily:'inherit' },
  footer:   { display:'flex', justifyContent:'space-between', marginTop:22, paddingTop:16, borderTop:'1px solid rgba(120,160,220,0.1)', color:'#5b657a', fontSize:10 },
}
