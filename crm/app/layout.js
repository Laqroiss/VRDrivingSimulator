import './globals.css'

export const metadata = { title: 'VR Driving CRM', description: '  ' }

export default function RootLayout({ children }) {
  return (
    <html lang="ru">
      <body>
        <header className="site-header">
          <div className="container inner">
            <a href="/admin" className="site-logo">
              <div className="logo-icon">ð—</div>
              VR<span>Drive</span> CRM
            </a>
            <a href="/admin"   className="nav-link">ð¤ </a>
            <a href="/attempts" className="nav-link">ð‹ </a>
            <div className="nav-spacer" />
            <a href="/cabinet" className="nav-cabinet">ð‘  </a>
          </div>
        </header>
        <main className="container" style={{ paddingTop: 32, paddingBottom: 48 }}>
          {children}
        </main>
      </body>
    </html>
  )
}
