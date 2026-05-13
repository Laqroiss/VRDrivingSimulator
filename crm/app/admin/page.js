'use client'
import { useState, useEffect } from 'react'
import { useRouter } from 'next/navigation'
import Link from 'next/link'

function fmt(date) {
  return new Date(date).toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

export default function AdminPage() {
  const [students, setStudents] = useState([])
  const [loading, setLoading]   = useState(true)
  const [search, setSearch]     = useState('')
  const router = useRouter()

  useEffect(() => {
    const token = localStorage.getItem('admin_token')
    if (!token) { router.push('/admin/login'); return }
    fetch('/api/students')
      .then(r => r.json())
      .then(data => { setStudents(data); setLoading(false) })
  }, [])

  const deleteStudent = async (id) => {
    if (!confirm('Удалить курсанта и все его попытки?')) return
    await fetch(`/api/students/${id}`, { method: 'DELETE' })
    setStudents(s => s.filter(u => u._id !== id))
  }

  const logout = () => { localStorage.removeItem('admin_token'); router.push('/admin/login') }

  const filtered = students.filter(u =>
    u.fullName?.toLowerCase().includes(search.toLowerCase()) ||
    u.phone?.includes(search)
  )

  const totalAttempts = students.reduce((s, u) => s + (u.total || 0), 0)
  const totalPassed   = students.reduce((s, u) => s + (u.passed || 0), 0)

  if (loading) return <div style={{ padding: 40, color: 'var(--muted)' }}>Загрузка...</div>

  return (
    <div>
      {/* Шапка */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <div>
          <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 4 }}>Панель администратора</h1>
          <p style={{ color: 'var(--muted)' }}>
            Курсантов: {students.length} &nbsp;·&nbsp;
            Попыток: {totalAttempts} &nbsp;·&nbsp;
            <span style={{ color: 'var(--green)' }}>Сдали: {totalPassed}</span> &nbsp;·&nbsp;
            <span style={{ color: 'var(--red)' }}>Не сдали: {totalAttempts - totalPassed}</span>
          </p>
        </div>
        <button className="ghost" onClick={logout}>Выйти</button>
      </div>

      {/* Статистика */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 16, marginBottom: 24 }}>
        {[
          { label: 'Всего курсантов', value: students.length, color: 'var(--accent)' },
          { label: 'Всего попыток',   value: totalAttempts,   color: 'var(--text)' },
          { label: 'Процент сдачи',   value: totalAttempts ? Math.round(totalPassed / totalAttempts * 100) + '%' : '—', color: 'var(--green)' },
        ].map(s => (
          <div key={s.label} className="card" style={{ textAlign: 'center' }}>
            <div style={{ fontSize: 32, fontWeight: 700, color: s.color, marginBottom: 4 }}>{s.value}</div>
            <div style={{ color: 'var(--muted)', fontSize: 12 }}>{s.label}</div>
          </div>
        ))}
      </div>

      {/* Поиск */}
      <div style={{ marginBottom: 16 }}>
        <input type="search" placeholder="Поиск по имени или телефону…"
          value={search} onChange={e => setSearch(e.target.value)} />
      </div>

      {/* Таблица */}
      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        <table>
          <thead>
            <tr>
              <th>Курсант</th>
              <th>Телефон</th>
              <th>Регистрация</th>
              <th>Попыток</th>
              <th>Сдал</th>
              <th>Не сдал</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 && (
              <tr><td colSpan={7} style={{ textAlign: 'center', color: 'var(--muted)', padding: 40 }}>Нет курсантов</td></tr>
            )}
            {filtered.map(u => (
              <tr key={u._id}>
                <td style={{ fontWeight: 600 }}>{u.fullName}</td>
                <td style={{ color: 'var(--muted)' }}>{u.phone}</td>
                <td style={{ color: 'var(--muted)' }}>{fmt(u.createdAt)}</td>
                <td>{u.total}</td>
                <td style={{ color: 'var(--green)' }}>{u.passed}</td>
                <td style={{ color: u.failed > 0 ? 'var(--red)' : 'var(--muted)' }}>{u.failed}</td>
                <td style={{ display: 'flex', gap: 6 }}>
                  <Link href={`/students/${u._id}`}>
                    <button className="ghost" style={{ fontSize: 12 }}>Карточка →</button>
                  </Link>
                  <button onClick={() => deleteStudent(u._id)}
                    style={{ fontSize: 12, background: '#450a0a', color: 'var(--red)' }}>
                    Удалить
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
