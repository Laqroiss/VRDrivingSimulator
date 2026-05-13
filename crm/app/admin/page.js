'use client'
import { useState, useEffect } from 'react'
import { useRouter } from 'next/navigation'
import Link from 'next/link'

function fmt(date) {
  return new Date(date).toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric' })
}
function initials(name) {
  return name?.split(' ').slice(0, 2).map(w => w[0]).join('').toUpperCase() || '?'
}

export default function AdminPage() {
  const [students, setStudents] = useState([])
  const [loading, setLoading]   = useState(true)
  const [search, setSearch]     = useState('')
  const router = useRouter()

  useEffect(() => {
    fetch('/api/students')
      .then(r => { if (r.status === 401) { router.push('/admin/login'); return null } return r.json() })
      .then(data => { if (data) { setStudents(data); setLoading(false) } })
  }, [])

  const deleteStudent = async (id) => {
    if (!confirm('Удалить курсанта и все его попытки?')) return
    await fetch(`/api/students/${id}`, { method: 'DELETE' })
    setStudents(s => s.filter(u => u._id !== id))
  }

  const logout = async () => {
    await fetch('/api/admin/login', { method: 'DELETE' })
    router.push('/admin/login')
  }

  const filtered = students.filter(u =>
    u.fullName?.toLowerCase().includes(search.toLowerCase()) ||
    u.phone?.includes(search)
  )

  const totalAttempts = students.reduce((s, u) => s + (u.total || 0), 0)
  const totalPassed   = students.reduce((s, u) => s + (u.passed || 0), 0)
  const passRate      = totalAttempts ? Math.round(totalPassed / totalAttempts * 100) : 0

  if (loading) return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '60vh', color: 'var(--muted)' }}>
      Загрузка данных...
    </div>
  )

  return (
    <div>
      <div className="road-stripe" />

      <div className="page-header">
        <div>
          <div className="page-title">Панель администратора</div>
          <div className="page-sub">Управление курсантами и результатами экзаменов</div>
        </div>
        <button className="btn-ghost ghost" onClick={logout}>Выйти →</button>
      </div>

      {/* Статистика */}
      <div className="stat-grid stat-grid-4" style={{ marginBottom: 28 }}>
        {[
          { label: 'Курсантов',    value: students.length, sub: 'зарегистрировано',  color: 'var(--blue2)' },
          { label: 'Попыток',      value: totalAttempts,   sub: 'экзаменов сдано',   color: 'var(--text)' },
          { label: 'Сдали',        value: totalPassed,     sub: 'успешно',            color: 'var(--green)' },
          { label: 'Процент сдачи', value: passRate + '%', sub: 'средний результат',  color: passRate >= 70 ? 'var(--green)' : 'var(--accent)' },
        ].map(s => (
          <div key={s.label} className="stat-card">
            <div className="stat-label">{s.label}</div>
            <div className="stat-value" style={{ color: s.color }}>{s.value}</div>
            <div className="stat-sub">{s.sub}</div>
          </div>
        ))}
      </div>

      {/* Поиск */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <div style={{ fontWeight: 700, fontSize: 15 }}>Список курсантов</div>
        <input type="search" placeholder="Поиск по имени или телефону…"
          value={search} onChange={e => setSearch(e.target.value)} style={{ width: 280 }} />
      </div>

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Курсант</th>
              <th>Телефон</th>
              <th>Дата регистрации</th>
              <th>Попыток</th>
              <th>Сдал</th>
              <th>Не сдал</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 && (
              <tr><td colSpan={7}>
                <div className="empty-state">
                  <div className="icon">🎓</div>
                  <p>Курсанты не найдены</p>
                </div>
              </td></tr>
            )}
            {filtered.map(u => (
              <tr key={u._id}>
                <td>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <div style={{ width: 34, height: 34, borderRadius: 8, background: 'linear-gradient(135deg,#f59e0b,#b45309)', display: 'grid', placeItems: 'center', fontSize: 12, fontWeight: 800, color: '#000', flexShrink: 0 }}>
                      {initials(u.fullName)}
                    </div>
                    <span style={{ fontWeight: 600 }}>{u.fullName}</span>
                  </div>
                </td>
                <td style={{ color: 'var(--muted2)' }}>{u.phone}</td>
                <td style={{ color: 'var(--muted2)' }}>{fmt(u.createdAt)}</td>
                <td style={{ fontWeight: 600 }}>{u.total}</td>
                <td style={{ color: 'var(--green)', fontWeight: 600 }}>{u.passed}</td>
                <td style={{ color: u.failed > 0 ? 'var(--red)' : 'var(--muted)', fontWeight: 600 }}>{u.failed}</td>
                <td>
                  <div style={{ display: 'flex', gap: 6 }}>
                    <Link href={`/students/${u._id}`}>
                      <button className="ghost">Карточка →</button>
                    </Link>
                    <button className="btn-danger" onClick={() => deleteStudent(u._id)}>Удалить</button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
