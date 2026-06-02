'use client'
import { useEffect, useRef, useState } from 'react'

// Count-up animation that triggers when scrolled into view.
export default function Counter({ value = 0, suffix = '', duration = 1100, className, style }) {
  const target = Number(value) || 0
  const [n, setN] = useState(0)
  const ref = useRef(null)
  const started = useRef(false)

  useEffect(() => {
    const el = ref.current
    if (!el) return
    const run = () => {
      if (started.current) return
      started.current = true
      const t0 = performance.now()
      const tick = (now) => {
        const p = Math.min(1, (now - t0) / duration)
        const eased = 1 - Math.pow(1 - p, 3)           // easeOutCubic
        setN(Math.round(target * eased))
        if (p < 1) requestAnimationFrame(tick)
      }
      requestAnimationFrame(tick)
    }
    const io = new IntersectionObserver(
      ([e]) => { if (e.isIntersecting) { run(); io.disconnect() } },
      { threshold: 0.3 }
    )
    io.observe(el)
    return () => io.disconnect()
  }, [target, duration])

  return <span ref={ref} className={className} style={style}>{n}{suffix}</span>
}
