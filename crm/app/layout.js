import './globals.css'

export const metadata = { title: 'VR Driving CRM', description: 'Система управления автошколой' }

export default function RootLayout({ children }) {
  return (
    <html lang="ru">
      <body>
        <header className="site-header">
          <div className="container inner">
            <a href="/admin" className="site-logo">
              <div className="logo-icon">🚗</div>
              VR<span>Drive</span> CRM
            </a>
            <a href="/admin"   className="nav-link">👤 Курсанты</a>
            <a href="/attempts" className="nav-link">📋 Попытки</a>
            <div className="nav-spacer" />
            <a href="/cabinet" className="nav-cabinet">🔑 Личный кабинет</a>
          </div>
        </header>
        <main className="container" style={{ paddingTop: 32, paddingBottom: 48 }}>
          {children}
        </main>
      </body>
    </html>
  )
}
