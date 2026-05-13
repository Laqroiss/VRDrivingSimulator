import Link from 'next/link'
import { connectDB } from '@/lib/mongodb'
import Attempt from '@/models/Attempt'
import DeleteButton from '@/components/DeleteButton'

function fmt(date) {
  return new Date(date).toLocaleString('ru-RU', {
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}
function dur(s) {
  if (!s) return '—'
  const m = Math.floor(s / 60), sec = Math.round(s % 60)
  return `${m}:${String(sec).padStart(2, '0')}`
}

export const dynamic = 'force-dynamic'

export default async function Dashboard({ searchParams }) {
  await connectDB()
  const q = searchParams?.q || ''
  const filter = q ? { studentName: { $regex: q, $options: 'i' } } : {}
  const attempts = await Attempt.find(filter, '-track').sort({ timestamp: -1 }).limit(200).lean()

  const total  = attempts.length
  const passed = attempts.filter(a => a.passed).length

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 24 }}>
        <div>
          <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 4 }}>Попытки</h1>
          <p style={{ color: 'var(--muted)' }}>
            Всего: {total} &nbsp;·&nbsp;
            <span style={{ color: 'var(--green)' }}>Сдали: {passed}</span> &nbsp;·&nbsp;
            <span style={{ color: 'var(--red)' }}>Не сдали: {total - passed}</span>
          </p>
        </div>
        <form method="GET">
          <input type="search" name="q" defaultValue={q} placeholder="Поиск по курсанту…" />
        </form>
      </div>

      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        <table>
          <thead>
            <tr>
              <th>Курсант</th>
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
              <tr><td colSpan={7} style={{ textAlign: 'center', color: 'var(--muted)', padding: 40 }}>
                Нет данных. Пройдите экзамен в симуляторе.
              </td></tr>
            )}
            {attempts.map(a => (
              <tr key={a._id.toString()}>
                <td style={{ fontWeight: 500 }}>{a.studentName}</td>
                <td style={{ color: 'var(--muted)' }}>{fmt(a.timestamp)}</td>
                <td><span className={`badge ${a.passed ? 'pass' : 'fail'}`}>{a.passed ? 'СДАЛ' : 'НЕ СДАЛ'}</span></td>
                <td style={{ color: a.totalPenaltyPoints >= 100 ? 'var(--red)' : 'var(--text)' }}>
                  {a.totalPenaltyPoints ?? '—'}
                </td>
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
