import { connectDB } from '@/lib/mongodb'
import Attempt from '@/models/Attempt'
import Link from 'next/link'
import ReplayViewer from '@/components/ReplayViewer'
import SpeedChart from '@/components/SpeedChart'
import LaunchReplayButton from '@/components/LaunchReplayButton'

export const dynamic = 'force-dynamic'

const EXERCISE_NAMES = [
  '', 'Старт', 'Нерег. перекрёстки', 'Рег. перекрёсток',
  'Пешеходный переход', 'Разворот и парковка', 'Параллельная парковка',
  'ЖД переезд', 'Аварийная остановка', 'Подъём и спуск', 'Финиш',
]

function statusColor(s) {
  if (s === 'Completed') return 'var(--green)'
  if (s === 'Failed')    return 'var(--red)'
  return 'var(--muted)'
}
function statusLabel(s) {
  if (s === 'Completed') return '✓'
  if (s === 'Failed')    return '✗'
  return '–'
}

export default async function AttemptPage({ params }) {
  await connectDB()
  const attempt = await Attempt.findById(params.id).lean()
  if (!attempt) return <p>Попытка не найдена</p>

  const a = JSON.parse(JSON.stringify(attempt)) // serialize for client

  return (
    <div style={{ paddingBottom: 60 }}>
      <div style={{ marginBottom: 24 }}>
        <Link href="/" style={{ color: 'var(--muted)', fontSize: 13 }}>← Назад</Link>
      </div>

      {/* Заголовок */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 16, marginBottom: 32 }}>
        <div>
          <h1 style={{ fontSize: 22, fontWeight: 700 }}>{a.studentName}</h1>
          <p style={{ color: 'var(--muted)', marginTop: 4 }}>
            {new Date(a.timestamp).toLocaleString('ru-RU')} &nbsp;·&nbsp;
            Длительность: {Math.floor(a.examDuration/60)}:{String(Math.round(a.examDuration%60)).padStart(2,'0')}
          </p>
        </div>
        <span className={`badge ${a.passed ? 'pass' : 'fail'}`} style={{ fontSize: 15, padding: '4px 16px' }}>
          {a.passed ? 'СДАЛ' : 'НЕ СДАЛ'}
        </span>
        <span style={{ marginLeft: 'auto', fontSize: 28, fontWeight: 800,
          color: a.totalPenaltyPoints >= 100 ? 'var(--red)' : 'var(--text)' }}>
          {a.totalPenaltyPoints} б.
        </span>
        <LaunchReplayButton attemptId={params.id} hasReplay={!!attempt.replayData} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 20, marginBottom: 20 }}>
        {/* Статусы упражнений */}
        <div className="card">
          <h2 style={{ fontSize: 14, fontWeight: 600, marginBottom: 14, color: 'var(--muted)' }}>УПРАЖНЕНИЯ</h2>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {(a.exerciseStatuses || []).map((s, i) => (
              <div key={i} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span>{EXERCISE_NAMES[i + 1] || `Упр. ${i + 1}`}</span>
                <span style={{ fontWeight: 700, color: statusColor(s) }}>{statusLabel(s)}</span>
              </div>
            ))}
          </div>
        </div>

        {/* Ошибки */}
        <div className="card">
          <h2 style={{ fontSize: 14, fontWeight: 600, marginBottom: 14, color: 'var(--muted)' }}>
            ОШИБКИ ({a.penalties?.length ?? 0})
          </h2>
          {(!a.penalties || a.penalties.length === 0)
            ? <p style={{ color: 'var(--muted)' }}>Ошибок нет</p>
            : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6, maxHeight: 280, overflowY: 'auto' }}>
                {a.penalties.map((p, i) => (
                  <div key={i} style={{ display: 'flex', justifyContent: 'space-between',
                    padding: '6px 10px', background: 'rgba(239,68,68,0.08)',
                    borderRadius: 6, borderLeft: '3px solid var(--red)' }}>
                    <span style={{ fontSize: 13 }}>
                      {p.exerciseNum > 0 ? `Упр.${p.exerciseNum} · ` : ''}{p.description}
                    </span>
                    <span style={{ color: 'var(--red)', fontWeight: 700, whiteSpace: 'nowrap', marginLeft: 12 }}>
                      −{p.points}
                    </span>
                  </div>
                ))}
              </div>
            )
          }
        </div>
      </div>

      {/* 2D Реплей */}
      {a.track && a.track.length > 0 && (
        <div className="card" style={{ marginBottom: 20 }}>
          <h2 style={{ fontSize: 14, fontWeight: 600, marginBottom: 16, color: 'var(--muted)' }}>2D РЕПЛЕЙ</h2>
          <ReplayViewer
            track={a.track}
            penalties={a.penalties ?? []}
            lightEvents={a.lightEvents ?? []}
            lightPositions={a.lightPositions ?? []}
          />
        </div>
      )}

      {/* График скорости */}
      {a.track && a.track.length > 0 && (
        <div className="card">
          <h2 style={{ fontSize: 14, fontWeight: 600, marginBottom: 16, color: 'var(--muted)' }}>СКОРОСТЬ И RPM</h2>
          <SpeedChart track={a.track} />
        </div>
      )}
    </div>
  )
}
