import Link from 'next/link'
import { connectDB } from '@/lib/mongodb'
import Attempt from '@/models/Attempt'
import User from '@/models/User'
import DeleteButton from '@/components/DeleteButton'
import Counter from '@/components/Counter'
import Tilt from '@/components/Tilt'

export const dynamic = 'force-dynamic'

function fmt(date) {
  return new Date(date).toLocaleString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}
function dur(s) {
  if (!s) return '—'
  const m = Math.floor(s / 60), sec = Math.round(s % 60)
  return `${m}:${String(sec).padStart(2, '0')}`
}

export default async function AttemptsPage({ searchParams }) {
  await connectDB()
  const q      = searchParams?.q || ''
  const filter = q ? { studentName: { $regex: q, $options: 'i' } } : {}
  const attempts = await Attempt.find(filter, '-track').sort({ timestamp: -1 }).limit(200).lean()

  const userIds = [...new Set(attempts.map(a => a.studentId).filter(Boolean))]
  const users   = await User.find({ _id: { $in: userIds } }, 'fullName').lean()
  const userMap = Object.fromEntries(users.map(u => [u._id.toString(), u.fullName]))

  const total  = attempts.length
  const passed = attempts.filter(a => a.completed !== false && a.passed).length

  return (
    <div>
      <div className="road-stripe" />
      <div className="page-header">
        <div>
          <div className="page-title">All attempts</div>
          <div className="page-sub">Live exam log from the simulator</div>
        </div>
        <form method="GET">
          <input type="search" name="q" defaultValue={q} placeholder="Search by student…" />
        </form>
      </div>

      <div className="stat-grid stat-grid-3 stagger" style={{ marginBottom: 24 }}>
        {[
          { label: 'Total attempts', value: total,          sub: 'in log',          color: 'var(--ink)'   },
          { label: 'Passed',         value: passed,         sub: 'successful',      color: 'var(--green)' },
          { label: 'Failed',         value: total - passed, sub: 'not passed',      color: 'var(--red)'   },
        ].map(s => (
          <Tilt key={s.label}>
            <div className="stat-card">
              <div className="stat-label">{s.label}</div>
              <div className="stat-value" style={{ color: s.color }}><Counter value={s.value} /></div>
              <div className="stat-sub">{s.sub}</div>
            </div>
          </Tilt>
        ))}
      </div>

      <div className="section-title">Attempt log</div>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Student</th>
              <th>Date</th>
              <th>Result</th>
              <th>Penalty points</th>
              <th>Duration</th>
              <th>Errors</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {attempts.length === 0 && (
              <tr><td colSpan={7}>
                <div className="empty-state">
                  <div className="icon"></div>
                  <p>No data yet. Take an exam in the simulator.</p>
                </div>
              </td></tr>
            )}
            {attempts.map(a => {
              const realName = a.studentId ? (userMap[a.studentId] ?? a.studentName) : a.studentName
              return (
                <tr key={a._id.toString()}>
                  <td style={{ fontWeight: 600 }}>
                    {a.studentId
                      ? <Link href={`/students/${a.studentId}`}>{realName}</Link>
                      : realName}
                  </td>
                  <td style={{ color: 'var(--muted2)' }}>{fmt(a.timestamp)}</td>
                  <td>
                    {a.completed === false
                      ? <span className="badge warn">INCOMPLETE</span>
                      : <span className={`badge ${a.passed ? 'pass' : 'fail'}`}>{a.passed ? 'PASSED' : 'FAILED'}</span>}
                  </td>
                  <td>
                    <span style={{ fontWeight: 700, color: a.totalPenaltyPoints >= 100 ? 'var(--red)' : 'var(--text)' }}>
                      {a.totalPenaltyPoints ?? '—'}
                    </span>
                  </td>
                  <td style={{ color: 'var(--muted2)' }}>{dur(a.examDuration)}</td>
                  <td style={{ color: 'var(--muted2)' }}>{a.penalties?.length ?? 0}</td>
                  <td>
                    <div style={{ display: 'flex', gap: 6 }}>
                      <Link href={`/attempts/${a._id}`}>
                        <button className="ghost">Details →</button>
                      </Link>
                      <DeleteButton id={a._id.toString()} />
                    </div>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </div>
  )
}
