'use client'
import { useRef } from 'react'

// Wraps children and adds a subtle 3D tilt + glare that follows the cursor.
export default function Tilt({ children, className = '', style, max = 4 }) {
  const ref = useRef(null)

  const onMove = (e) => {
    const el = ref.current; if (!el) return
    const r = el.getBoundingClientRect()
    const px = (e.clientX - r.left) / r.width - 0.5
    const py = (e.clientY - r.top) / r.height - 0.5
    el.style.transform = `perspective(800px) rotateX(${(-py * max).toFixed(2)}deg) rotateY(${(px * max).toFixed(2)}deg) translateY(-4px)`
    el.style.setProperty('--gx', `${(px + 0.5) * 100}%`)
    el.style.setProperty('--gy', `${(py + 0.5) * 100}%`)
  }
  const reset = () => {
    const el = ref.current; if (!el) return
    el.style.transform = ''
  }

  return (
    <div
      ref={ref}
      className={`tilt ${className}`}
      style={{ transition: 'transform .25s cubic-bezier(.2,.7,.2,1)', willChange: 'transform', ...style }}
      onMouseMove={onMove}
      onMouseLeave={reset}
    >
      {children}
    </div>
  )
}
