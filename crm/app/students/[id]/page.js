import Link from 'next/link'
import { connectDB } from '@/lib/mongodb'
import User from '@/models/User'
import Attempt from '@/models/Attempt'
import DeleteButton from '@/components/DeleteButton'

export const dynamic = 'force-dynamic'

function fmt(date) {
  return new Date(date).toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}
function dur(s) {
  if (!s) return '—'
  const m = Math.floor(s / 60), sec = Math.round(s % 60)
  return `${m}:${String(sec).padStart(2, '0')}`
}

export default async function StudentPage({ params }) {
  await connectDB()
  const user = await User.findById(params.id, '-password').lean()
  if (!user) return <div style={{ padding: 40, color: 'var(--muted)' }}>Курсант не найден</div>

  const attempts = await Attempt.find({ studentId: params.id }, '-track').sort({ timestamp: -1 }).lean()
  const passed   = attempts.filter(a => a.passed).length
  const failed   = attempts.length - passed
  const avgScore = attempts.length
    ? Math.round(attempts.reduce((s, a) => s + (a.totalPenaltyPoints ?? 0), 0) / attempts.length)
    : 0

  return (
    <div>
      <Link href="/" style={{ color: 'var(--muted)', fontSize: 13 }}>← Назад</Link>

      {/* Карточка курсанта */}
      <div className="card" style={{ marginTop: 16, marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <div>
            <h1 style={{ fontSize: 24, fontWeight: 700, marginBottom: 6 }}>{user.fullName}</h1>
            <p style={{ color: 'var(--muted)', marginBottom: 4 }}>📞 {user.phone}</p>
            <p style={{ color: 'var(--muted)', fontSize: 13 }}>Зарегистрирован: {fmt(user.createdAt)}</p>
          </div>
          <div style={{ display: 'flex', gap: 32, textAlign: 'center' }}>
            <div>
              <div style={{ fontSize: 28, fontWeight: 700 }}>{attempts.length}</div>
              <div style={{ color: 'var(--muted)', fontSize: 12 }}>ПОПЫТОК</div>
            </div>
            <div>
              <div style={{ fontSize: 28, fontWeight: 700, color: 'var(--green)' }}>{passed}</div>
              <div style={{ color: 'var(--muted)', fontSize: 12 }}>СДАЛ</div>
            </div>
            <div>
              <div style={{ fontSize: 28, fontWeight: 700, color: failed > 0 ? 'var(--red)' : 'var(--muted)' }}>{failed}</div>
              <div style={{ color: 'var(--muted)', fontSize: 12 }}>НЕ СДАЛ</div>
            </div>
            <div>
              <div style={{ fontSize: 28, fontWeight: 700 }}>{avgScore}</div>
              <div style={{ color: 'var(--muted)', fontSize: 12 }}>СР. БАЛЛОВ</div>
            </div>
          </div>
        </div>
      </div>

      {/* Попытки */}
      <h2 style={{ fontSize: 16, fontWeight: 600, marginBottom: 12 }}>История экзаменов</h2>
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
                Нет попыток
              </td></tr>
            )}
            {attempts.map(a => (
              <tr key={a._id.toString()}>
                <td style={{ color: 'var(--muted)' }}>{fmt(a.timestamp)}</td>
                <td><span className={`badge ${a.passed ? 'pass' : 'fail'}`}>{a.passed ? 'СДАЛ' : 'НЕ СДАЛ'}</span></td>
                <td style={{ color: a.totalPenaltyPoints >= 100 ? 'var(--red)' : 'var(--text)' }}>{a.totalPenaltyPoints ?? '—'}</td>
                <td style={{ color: 'var(--muted)' }}>{dur(a.examDuration)}</td>
                <td style={{ color: 'var(--muted)' }}>{a.penalties?.length ?? 0}</td>
                <td style={{ display: 'flex', gap: 6 }}>
                  <Link href={`/attempts/${a._id}`}>
                    <button className="ghost" style={{ fontSize: 12 }}>Подробнее →</button>
                  </Link>
                  <DeleteButton id={a._id.toString()} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
