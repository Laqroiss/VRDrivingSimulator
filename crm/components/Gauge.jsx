'use client'
import { useEffect, useRef, useState } from 'react'

// Animated speedometer-style pass-rate ring (conic-gradient fill + count-up).
export default function Gauge({ value = 0, total = 0, passed = 0 }) {
  const v = Math.max(0, Math.min(100, Number(value) || 0))
  const ring = v >= 70 ? 'var(--green)' : v >= 40 ? 'var(--gold)' : 'var(--red)'
  const [shown, setShown] = useState(0)
  const ref = useRef(null)
  const started = useRef(false)

  useEffect(() => {
    const el = ref.current
    if (!el) return
    const run = () => {
      if (started.current) return
      started.current = true
      const t0 = performance.now(), dur = 1400
      const tick = (now) => {
        const p = Math.min(1, (now - t0) / dur)
        const eased = 1 - Math.pow(1 - p, 3)
        setShown(Math.round(v * eased))
        if (p < 1) requestAnimationFrame(tick)
      }
      requestAnimationFrame(tick)
    }
    const io = new IntersectionObserver(([e]) => { if (e.isIntersecting) { run(); io.disconnect() } }, { threshold: 0.3 })
    io.observe(el)
    return () => io.disconnect()
  }, [v])

  return (
    <div className="gauge-card anim-pop" ref={ref}>
      <div className="gauge" style={{ '--val': shown, '--ring': ring }}>
        <div style={{ textAlign: 'center' }}>
          <div className="gauge-num" style={{ color: ring }}>{shown}%</div>
          <div className="gauge-cap">passed</div>
        </div>
      </div>
      <div className="gauge-info">
        <div className="gauge-title">Overall pass rate</div>
        <div className="gauge-desc">
          {passed} of {total} {total === 1 ? 'attempt' : 'attempts'} passed.
          {v >= 70 ? ' Excellent group result.' : v >= 40 ? ' Room for improvement.' : ' Needs instructor attention.'}
        </div>
        <div className="gauge-legend">
          <span><i style={{ background: 'var(--green)' }} />Passed  {passed}</span>
          <span><i style={{ background: 'var(--red)' }} />Failed  {Math.max(0, total - passed)}</span>
        </div>
      </div>
    </div>
  )
}
