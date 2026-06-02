// Brand mark — a minimalist speedometer (ties to the driving/performance theme).
export function LogoMark({ size = 36, className = '' }) {
  return (
    <span className={`logo-mark ${className}`} style={{ width: size, height: size }}>
      <svg viewBox="0 0 32 32" width={size * 0.64} height={size * 0.64} fill="none" aria-hidden="true">
        {/* dim track */}
        <path d="M7.2 22.2 A10 10 0 1 1 24.8 22.2" stroke="#fff" strokeWidth="2.3" strokeLinecap="round" opacity="0.45" />
        {/* lit value arc */}
        <path d="M7.2 22.2 A10 10 0 0 1 20.4 7.3" stroke="#fff" strokeWidth="2.7" strokeLinecap="round" />
        {/* needle */}
        <path d="M16 16 L20.8 10.6" stroke="#fff" strokeWidth="2.6" strokeLinecap="round" />
        {/* hub */}
        <circle cx="16" cy="16" r="2.3" fill="#fff" />
      </svg>
    </span>
  )
}

export default function Logo() {
  return (
    <a href="/admin" className="site-logo">
      <LogoMark />
      <span className="logo-word">VR<b>Drive</b></span>
      <span className="logo-tag">CRM</span>
    </a>
  )
}
