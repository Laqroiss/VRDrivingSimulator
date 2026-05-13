'use client'
import { useRef, useEffect, useState, useCallback } from 'react'

const W = 640, H = 640
const CAR_LEN = 10, CAR_WID = 6

// ── Конфиг трассы ──────────────────────────────────────────────────────────
// Замени на реальные Unity world-координаты краёв карты.
// Найди их в Unity: поставь пустой объект в левый-нижний и правый-верхний углы трассы.
const TRACK_BOUNDS = {
  minX: -125, maxX: 125,
  minZ: -125, maxZ: 125,
}
const TRACK_IMAGE = '/track.png'   // файл в crm/public/
// ───────────────────────────────────────────────────────────────────────────

function worldToCanvas(x, z, bounds) {
  const px = (x - bounds.minX) / (bounds.maxX - bounds.minX) * W
  const py = (1 - (z - bounds.minZ) / (bounds.maxZ - bounds.minZ)) * H
  return { px, py }
}

const PHASE_COLOR = {
  Green: '#22c55e', BlinkGreen: '#86efac', Yellow: '#facc15',
  Red: '#ef4444', RedYellow: '#f97316', Off: '#64748b',
}
const PHASE_RU = {
  Green: 'Зелёный', BlinkGreen: 'Мигающий зелёный', Yellow: 'Жёлтый',
  Red: 'Красный', RedYellow: 'Красный+жёлтый', Off: 'Выключен',
}

function getLightState(lightId, currentT, lightEvents) {
  // Находим последнее событие для этого светофора до текущего времени
  let last = null
  for (const e of lightEvents) {
    if (e.id === lightId && e.t <= currentT) last = e
  }
  if (!last) return null
  const elapsed = currentT - last.t
  const remaining = Math.max(0, last.duration - elapsed)
  return { phaseA: last.phaseA, phaseB: last.phaseB, remaining }
}

export default function ReplayViewer({ track, penalties, lightEvents = [], lightPositions = [] }) {
  const canvasRef  = useRef(null)
  const imgRef     = useRef(null)
  const [playing, setPlaying]       = useState(false)
  const [idx, setIdx]               = useState(0)
  const [activeLight, setActiveLight] = useState(null) // id светофора
  const rafRef   = useRef(null)
  const lastTs   = useRef(null)
  const idxRef   = useRef(0)

  const duration = track.length > 0 ? track[track.length - 1].t : 1

  // Предзагрузка картинки трассы
  useEffect(() => {
    const img = new Image()
    img.src = TRACK_IMAGE
    img.onload = () => { imgRef.current = img; draw(idxRef.current) }
    img.onerror = () => { imgRef.current = null }
  }, [])

  const draw = useCallback((frameIdx) => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    ctx.clearRect(0, 0, W, H)

    // Фон
    ctx.fillStyle = '#1a1d27'
    ctx.fillRect(0, 0, W, H)

    // Картинка трассы — растягиваем на весь канвас (Plane квадратный, канвас тоже)
    if (imgRef.current) {
      ctx.globalAlpha = 0.75
      ctx.drawImage(imgRef.current, 0, 0, W, H)
      ctx.globalAlpha = 1
    }

    // Весь путь (dim)
    ctx.beginPath()
    track.forEach((pt, i) => {
      const { px, py } = worldToCanvas(pt.x, pt.z, TRACK_BOUNDS)
      i === 0 ? ctx.moveTo(px, py) : ctx.lineTo(px, py)
    })
    ctx.strokeStyle = 'rgba(255,255,255,0.15)'
    ctx.lineWidth = 2
    ctx.stroke()

    // Пройденный путь
    ctx.beginPath()
    for (let i = 0; i <= frameIdx; i++) {
      const { px, py } = worldToCanvas(track[i].x, track[i].z, TRACK_BOUNDS)
      i === 0 ? ctx.moveTo(px, py) : ctx.lineTo(px, py)
    }
    ctx.strokeStyle = '#4f8ef7'
    ctx.lineWidth = 2.5
    ctx.stroke()

    // Маркеры ошибок
    penalties.forEach((p, i) => {
      if (p.x == null || p.z == null) return
      const { px, py } = worldToCanvas(p.x, p.z, TRACK_BOUNDS)
      // Пульсирующий круг для текущей ошибки (ближайшей к кадру)
      ctx.beginPath()
      ctx.arc(px, py, 9, 0, Math.PI * 2)
      ctx.fillStyle = '#ef4444'
      ctx.fill()
      ctx.fillStyle = '#fff'
      ctx.font = 'bold 9px sans-serif'
      ctx.textAlign = 'center'
      ctx.textBaseline = 'middle'
      ctx.fillText(i + 1, px, py)
    })

    // Машинка
    const cur = track[frameIdx]
    const { px: cx, py: cy } = worldToCanvas(cur.x, cur.z, TRACK_BOUNDS)
    const ang = (cur.rot ?? 0) * Math.PI / 180

    ctx.save()
    ctx.translate(cx, cy)
    ctx.rotate(-ang)
    ctx.fillStyle = '#4f8ef7'
    ctx.beginPath()
    ctx.roundRect(-CAR_LEN / 2, -CAR_WID / 2, CAR_LEN, CAR_WID, 2)
    ctx.fill()
    // нос
    ctx.fillStyle = '#fff'
    ctx.beginPath()
    ctx.moveTo(CAR_LEN / 2, 0)
    ctx.lineTo(CAR_LEN / 2 - 4, -2.5)
    ctx.lineTo(CAR_LEN / 2 - 4, 2.5)
    ctx.closePath()
    ctx.fill()
    ctx.restore()

    // HUD
    ctx.fillStyle = 'rgba(0,0,0,0.5)'
    ctx.fillRect(6, 6, 200, 22)
    ctx.fillStyle = '#e2e8f0'
    ctx.font = '12px monospace'
    ctx.textAlign = 'left'
    ctx.textBaseline = 'top'
    ctx.fillText(`${cur.speed?.toFixed(0) ?? 0} км/ч · ${cur.rpm?.toFixed(0) ?? 0} RPM · ${cur.t?.toFixed(1) ?? 0}s`, 10, 10)
  }, [track, penalties])

  useEffect(() => { draw(0) }, [draw])

  // Анимация
  useEffect(() => {
    if (!playing) return
    const step = (ts) => {
      if (lastTs.current == null) lastTs.current = ts
      const dt = ts - lastTs.current
      lastTs.current = ts
      const interval = track.length > 1 ? (track[1].t - track[0].t) : 0.2
      idxRef.current = Math.min(idxRef.current + dt / 1000 / interval, track.length - 1)
      const i = Math.floor(idxRef.current)
      setIdx(i)
      draw(i)
      if (i < track.length - 1) rafRef.current = requestAnimationFrame(step)
      else setPlaying(false)
    }
    rafRef.current = requestAnimationFrame(step)
    return () => { cancelAnimationFrame(rafRef.current); lastTs.current = null }
  }, [playing, draw, track])

  const toggle = () => {
    if (idx >= track.length - 1) { idxRef.current = 0; setIdx(0); draw(0) }
    setPlaying(p => !p)
  }

  const seekToError = (p) => {
    setPlaying(false)
    let best = -1
    let bestDiff = Infinity

    const startX = track[0]?.x ?? 0
    const startZ = track[0]?.z ?? 0

    if (p.t > 0) {
      // Ищем по времени
      track.forEach((pt, i) => {
        const d = Math.abs(pt.t - p.t)
        if (d < bestDiff) { bestDiff = d; best = i }
      })
    } else if (
      p.x != null && p.z != null &&
      Math.hypot(p.x - startX, p.z - startZ) > 1  // не совпадает со стартом
    ) {
      // Фолбек: ищем по позиции
      track.forEach((pt, i) => {
        const d = Math.hypot(pt.x - p.x, pt.z - p.z)
        if (d < bestDiff) { bestDiff = d; best = i }
      })
    }

    if (best < 0) return  // нет валидных данных — не перематываем

    idxRef.current = best
    setIdx(best)
    draw(best)
  }

  const onSlider = (e) => {
    const t = Number(e.target.value)
    let best = 0, bestDiff = Infinity
    track.forEach((pt, i) => {
      const d = Math.abs(pt.t - t)
      if (d < bestDiff) { bestDiff = d; best = i }
    })
    idxRef.current = best
    setIdx(best)
    draw(best)
    setPlaying(false)
  }


  return (
    <div>
      <div style={{ position: 'relative', display: 'inline-block', maxWidth: '100%' }}>
        <canvas ref={canvasRef} width={W} height={H}
          style={{ display: 'block', borderRadius: 8, maxWidth: '100%', border: '1px solid var(--border)' }} />

        {/* Дебаг: кол-во светофоров в данных */}
        {lightPositions.length === 0 && (
          <div style={{
            position: 'absolute', top: 8, right: 8,
            background: 'rgba(0,0,0,0.7)', color: '#f97316',
            fontSize: 11, padding: '3px 8px', borderRadius: 4
          }}>
            Нет данных светофоров — пройди экзамен заново
          </div>
        )}

        {/* Дебаг позиций */}
        {lightPositions.length > 0 && (
          <div style={{
            position: 'absolute', bottom: 8, left: 8,
            background: 'rgba(0,0,0,0.8)', color: '#e2e8f0',
            fontSize: 10, padding: '4px 8px', borderRadius: 4, lineHeight: 1.6
          }}>
            {lightPositions.map(lp => {
              const { px, py } = worldToCanvas(lp.x, lp.z, TRACK_BOUNDS)
              return <div key={lp.id}>🚦{lp.id}: world({lp.x.toFixed(0)},{lp.z.toFixed(0)}) → canvas({px.toFixed(0)},{py.toFixed(0)})</div>
            })}
          </div>
        )}

        {/* Иконки светофоров на карте */}
        {lightPositions.map(lp => {
          const { px, py } = worldToCanvas(lp.x, lp.z, TRACK_BOUNDS)
          const state = getLightState(lp.id, track[idx]?.t ?? 0, lightEvents)
          const color = state ? PHASE_COLOR[state.phaseA] ?? '#fff' : '#fff'
          return (
            <button key={lp.id}
              onClick={() => setActiveLight(activeLight === lp.id ? null : lp.id)}
              style={{
                position: 'absolute',
                left: `${(px / W) * 100}%`,
                top:  `${(py / H) * 100}%`,
                transform: 'translate(-50%, -50%)',
                background: '#1a1d27',
                border: `2px solid ${color}`,
                borderRadius: '50%',
                width: 24, height: 24,
                cursor: 'pointer',
                fontSize: 13,
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                boxShadow: `0 0 6px ${color}`,
                zIndex: 10,
              }}>
              🚦
            </button>
          )
        })}

        {/* Попап выбранного светофора */}
        {activeLight !== null && (() => {
          const lp = lightPositions.find(p => p.id === activeLight)
          if (!lp) return null
          const { px, py } = worldToCanvas(lp.x, lp.z, TRACK_BOUNDS)
          const state = getLightState(activeLight, track[idx]?.t ?? 0, lightEvents)
          return (
            <div style={{
              position: 'absolute',
              left: `${(px / W) * 100}%`,
              top:  `${(py / H) * 100}%`,
              transform: 'translate(-50%, -110%)',
              background: '#1a1d27',
              border: '1px solid var(--border)',
              borderRadius: 8,
              padding: '8px 12px',
              minWidth: 160,
              zIndex: 20,
              fontSize: 12,
              pointerEvents: 'none',
            }}>
              <div style={{ fontWeight: 600, marginBottom: 6, color: '#e2e8f0' }}>
                Светофор {activeLight + 1}
              </div>
              {state ? (<>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
                  <div style={{
                    width: 14, height: 14, borderRadius: '50%',
                    background: PHASE_COLOR[state.phaseA] ?? '#fff',
                    boxShadow: `0 0 6px ${PHASE_COLOR[state.phaseA] ?? '#fff'}`,
                    flexShrink: 0,
                  }} />
                  <span style={{ color: PHASE_COLOR[state.phaseA] ?? '#fff', fontWeight: 700 }}>
                    {PHASE_RU[state.phaseA] ?? state.phaseA}
                  </span>
                </div>
                <div style={{ color: '#facc15', fontWeight: 700, textAlign: 'center' }}>
                  осталось {state.remaining.toFixed(1)}с
                </div>
              </>) : (
                <div style={{ color: '#64748b' }}>Нет данных</div>
              )}
            </div>
          )
        })()}
      </div>

      {/* Таймлайн с метками ошибок */}
      <div style={{ marginTop: 12, position: 'relative' }}>
<div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <button onClick={toggle} style={{ width: 90, flexShrink: 0 }}>
            {playing ? '⏸ Пауза' : '▶ Играть'}
          </button>
          <input type="range" min={0} max={duration} step={0.1}
            value={track[idx]?.t ?? 0} onChange={onSlider}
            style={{ flex: 1, accentColor: 'var(--accent)' }} />
          <span style={{ color: 'var(--muted)', minWidth: 55, textAlign: 'right', fontSize: 12 }}>
            {track[idx]?.t?.toFixed(1) ?? 0}s / {duration.toFixed(1)}s
          </span>
        </div>
      </div>

      {/* Список ошибок — кликабельный */}
      {penalties.length > 0 && (
        <div style={{ marginTop: 16 }}>
          <p style={{ fontSize: 12, color: 'var(--muted)', marginBottom: 8 }}>
            {penalties.some(p => p.t > 0)
              ? 'Кликни по ошибке чтобы перемотать реплей:'
              : '⚠ Данные о моменте ошибок отсутствуют — пройди экзамен заново чтобы активировать перемотку'}
          </p>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
            {penalties.map((p, i) => (
              <div key={i}
                onClick={() => seekToError(p)}
                style={{
                  display: 'flex', alignItems: 'center', gap: 10,
                  padding: '6px 10px',
                  background: 'rgba(239,68,68,0.08)',
                  borderRadius: 6,
                  borderLeft: '3px solid #ef4444',
                  cursor: 'pointer',
                  transition: 'background .15s',
                }}
                onMouseEnter={e => e.currentTarget.style.background = 'rgba(239,68,68,0.18)'}
                onMouseLeave={e => e.currentTarget.style.background = 'rgba(239,68,68,0.08)'}
              >
                <span style={{
                  background: '#ef4444', color: '#fff', borderRadius: '50%',
                  width: 20, height: 20, display: 'flex', alignItems: 'center',
                  justifyContent: 'center', fontSize: 10, fontWeight: 700, flexShrink: 0,
                }}>{i + 1}</span>
                <span style={{ flex: 1, fontSize: 13 }}>
                  {p.exerciseNum > 0 ? `Упр.${p.exerciseNum} · ` : ''}{p.description}
                </span>
                {p.t != null && (
                  <span style={{ color: 'var(--muted)', fontSize: 11, whiteSpace: 'nowrap' }}>
                    {p.t.toFixed(1)}s
                  </span>
                )}
                <span style={{ color: '#ef4444', fontWeight: 700, whiteSpace: 'nowrap' }}>
                  −{p.points}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
