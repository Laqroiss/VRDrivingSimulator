import Link from 'next/link'
import { connectDB } from '@/lib/mongodb'
import User from '@/models/User'
import Attempt from '@/models/Attempt'
import DeleteButton from '@/components/DeleteButton'
import EditStudentButton from '@/components/EditStudentButton'
import Counter from '@/components/Counter'

export const dynamic = 'force-dynamic'

function fmt(date) {
  return new Date(date).toLocaleString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}
function dur(s) {
  if (!s) return '‚Äî'
  const m = Math.floor(s / 60), sec = Math.round(s % 60)
  return `${m}:${String(sec).padStart(2, '0')}`
}
function initials(name) {
  return name?.split(' ').slice(0, 2).map(w => w[0]).join('').toUpperCase() || '?'
}

export default async function StudentPage({ params }) {
  await connectDB()
  const user = await User.findById(params.id, '-password').lean()
  if (!user) return <div className="empty-state"><div className="icon">‚</div><p>Student not found</p></div>

  const attempts = await Attempt.find({ studentId: params.id }, '-track').sort({ timestamp: -1 }).lean()
  const passed   = attempts.filter(a => a.passed).length
  const failed   = attempts.length - passed
  const avgScore = attempts.length
    ? Math.round(attempts.reduce((s, a) => s + (a.totalPenaltyPoints ?? 0), 0) / attempts.length)
    : 0

  return (
    <div>
      <div className="road-stripe" />
      <Link href="/admin" style={{ color: 'var(--muted2)', fontSize: 13, display: 'inline-flex', alignItems: 'center', gap: 4, marginBottom: 20 }}>
        ‚Üê Back to list
      </Link>

      {/* Student card */}
      <div className="student-hero">
        <div style={{ display: 'flex', alignItems: 'center', gap: 18 }}>
          <div className="student-avatar">{initials(user.fullName)}</div>
          <div className="student-info">
            <div className="student-name">{user.fullName}</div>
            <div className="student-meta">
              <span>û {user.phone}</span>
              <span>Ö Registered {new Date(user.createdAt).toLocaleDateString('en-GB')}</span>
            </div>
            <div style={{ marginTop: 10 }}>
              <EditStudentButton student={{ _id: user._id.toString(), fullName: user.fullName, phone: user.phone }} />
            </div>
          </div>
        </div>
        <div className="student-stats">
          {[
            { val: attempts.length, lbl: 'Attempts',  color: 'var(--text)'  },
            { val: passed,          lbl: 'Passed',     color: 'var(--green)' },
            { val: failed,          lbl: 'Failed',     color: failed > 0 ? 'var(--red)' : 'var(--muted)' },
            { val: avgScore,        lbl: 'Avg points', color: avgScore >= 100 ? 'var(--red)' : 'var(--gold)' },
          ].map(s => (
            <div key={s.lbl} className="student-stat">
              <div className="val" style={{ color: s.color }}><Counter value={s.val} /></div>
              <div className="lbl">{s.lbl}</div>
            </div>
          ))}
        </div>
      </div>

      {/* History */}
      <div className="section-title">Exam history</div>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
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
              <tr><td colSpan={6}>
                <div className="empty-state">
                  <div className="icon">¶</div>
                  <p>No exams yet</p>
                </div>
              </td></tr>
            )}
            {attempts.map(a => (
              <tr key={a._id.toString()}>
                <td style={{ color: 'var(--muted2)' }}>{fmt(a.timestamp)}</td>
                <td><span className={`badge ${a.passed ? 'pass' : 'fail'}`}>{a.passed ? 'PASSED' : 'FAILED'}</span></td>
                <td>
                  <span style={{ fontWeight: 700, color: a.totalPenaltyPoints >= 100 ? 'var(--red)' : a.totalPenaltyPoints <= 20 ? 'var(--green)' : 'var(--text)' }}>
                    {a.totalPenaltyPoints ?? '‚Äî'}
                  </span>
                </td>
                <td style={{ color: 'var(--muted2)' }}>{dur(a.examDuration)}</td>
                <td style={{ color: 'var(--muted2)' }}>{a.penalties?.length ?? 0}</td>
                <td>
                  <div style={{ display: 'flex', gap: 6 }}>
                    <Link href={`/attempts/${a._id}`}>
                      <button className="ghost">Details ‚Üí</button>
                    </Link>
                    <DeleteButton id={a._id.toString()} />
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
