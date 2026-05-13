import Link from 'next/link'
import { connectDB } from '@/lib/mongodb'
import User from '@/models/User'
import Attempt from '@/models/Attempt'

export const dynamic = 'force-dynamic'

function fmt(date) {
  return new Date(date).toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

export default async function StudentsPage({ searchParams }) {
  await connectDB()
  const q = searchParams?.q || ''

  const filter = q ? { fullName: { $regex: q, $options: 'i' } } : {}
  const users  = await User.find(filter).sort({ createdAt: -1 }).lean()

  // Для каждого курсанта подтягиваем статистику попыток
  const stats = await Promise.all(users.map(async u => {
    const attempts = await Attempt.find({ studentId: u._id.toString() }, 'passed totalPenaltyPoints timestamp').lean()
    const passed   = attempts.filter(a => a.passed).length
    const last     = attempts.sort((a, b) => new Date(b.timestamp) - new Date(a.timestamp))[0]
    return { user: u, total: attempts.length, passed, failed: attempts.length - passed, last }
  }))

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 24 }}>
        <div>
          <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 4 }}>Курсанты</h1>
          <p style={{ color: 'var(--muted)' }}>Всего зарегистрировано: {users.length}</p>
        </div>
        <form method="GET">
          <input type="search" name="q" defaultValue={q} placeholder="Поиск по имени…" />
        </form>
      </div>

      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        <table>
          <thead>
            <tr>
              <th>Курсант</th>
              <th>Телефон</th>
              <th>Зарегистрирован</th>
              <th>Попыток</th>
              <th>Сдал</th>
              <th>Не сдал</th>
              <th>Последняя попытка</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {stats.length === 0 && (
              <tr><td colSpan={8} style={{ textAlign: 'center', color: 'var(--muted)', padding: 40 }}>
                Нет зарегистрированных курсантов
              </td></tr>
            )}
            {stats.map(({ user, total, passed, failed, last }) => (
              <tr key={user._id.toString()}>
                <td style={{ fontWeight: 600 }}>{user.fullName}</td>
                <td style={{ color: 'var(--muted)' }}>{user.phone}</td>
                <td style={{ color: 'var(--muted)' }}>{fmt(user.createdAt)}</td>
                <td>{total}</td>
                <td style={{ color: 'var(--green)' }}>{passed}</td>
                <td style={{ color: failed > 0 ? 'var(--red)' : 'var(--muted)' }}>{failed}</td>
                <td style={{ color: 'var(--muted)' }}>{last ? fmt(last.timestamp) : '—'}</td>
                <td>
                  <Link href={`/students/${user._id}`}>
                    <button className="ghost" style={{ fontSize: 12 }}>Карточка →</button>
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
