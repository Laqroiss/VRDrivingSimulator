'use client'
import { useState, useEffect } from 'react'
import { useRouter } from 'next/navigation'
import Link from 'next/link'
import Counter from '@/components/Counter'
import Gauge from '@/components/Gauge'
import Tilt from '@/components/Tilt'

function fmt(date) {
  return new Date(date).toLocaleString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' })
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
    if (!confirm('Delete this student and all their attempts?')) return
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
    <div className="loader"><div className="ring" />Loading data…</div>
  )

  return (
    <div>
      <div className="road-stripe" />

      <div className="page-header">
        <div>
          <div className="page-title">Admin Dashboard</div>
          <div className="page-sub">Manage students and exam results</div>
        </div>
        <button className="btn-ghost ghost" onClick={logout}>Log out →</button>
      </div>

      {/* Overall pass-rate gauge */}
      <div style={{ marginBottom: 18 }}>
        <Gauge value={passRate} total={totalAttempts} passed={totalPassed} />
      </div>

      {/* Stats */}
      <div className="stat-grid stat-grid-4 stagger" style={{ marginBottom: 28 }}>
        {[
          { label: 'Students',  value: students.length, suffix: '',  sub: 'registered',     color: 'var(--blue)' },
          { label: 'Attempts',  value: totalAttempts,   suffix: '',  sub: 'exams total',     color: 'var(--ink)' },
          { label: 'Passed',    value: totalPassed,     suffix: '',  sub: 'successful',      color: 'var(--green)' },
          { label: 'Pass rate', value: passRate,        suffix: '%', sub: 'group average',   color: passRate >= 70 ? 'var(--green)' : passRate >= 40 ? 'var(--gold)' : 'var(--red)' },
        ].map(s => (
          <Tilt key={s.label}>
            <div className="stat-card">
              <div className="stat-label">{s.label}</div>
              <div className="stat-value" style={{ color: s.color }}>
                <Counter value={s.value} suffix={s.suffix} />
              </div>
              <div className="stat-sub">{s.sub}</div>
            </div>
          </Tilt>
        ))}
      </div>

      {/* Search */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <div className="section-title" style={{ marginBottom: 0 }}>Students</div>
        <input type="search" placeholder="Search by name or phone…"
          value={search} onChange={e => setSearch(e.target.value)} style={{ width: 280 }} />
      </div>

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Student</th>
              <th>Phone</th>
              <th>Registered</th>
              <th>Attempts</th>
              <th>Passed</th>
              <th>Failed</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 && (
              <tr><td colSpan={7}>
                <div className="empty-state">
                  <div className="icon"></div>
                  <p>No students found</p>
                </div>
              </td></tr>
            )}
            {filtered.map(u => (
              <tr key={u._id}>
                <td>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <div style={{ width: 34, height: 34, borderRadius: 8, background: 'linear-gradient(135deg,var(--red),var(--red-deep))', display: 'grid', placeItems: 'center', fontSize: 12, fontWeight: 800, color: '#fff', flexShrink: 0 }}>
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
                      <button className="ghost">Profile →</button>
                    </Link>
                    <button className="btn-danger" onClick={() => deleteStudent(u._id)}>Delete</button>
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
