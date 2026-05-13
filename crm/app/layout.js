import './globals.css'

export const metadata = { title: 'VR Driving CRM', description: 'Результаты экзаменов' }

export default function RootLayout({ children }) {
  return (
    <html lang="ru">
      <body>
        <header style={{ borderBottom: '1px solid var(--border)', padding: '14px 0', marginBottom: 32 }}>
          <div className="container" style={{ display: 'flex', alignItems: 'center', gap: 24 }}>
            <span style={{ fontWeight: 700, fontSize: 18, marginRight: 8 }}>🚗 VR Driving CRM</span>
            <a href="/" style={{ color: 'var(--muted)', fontSize: 13 }}>Курсанты</a>
            <a href="/attempts" style={{ color: 'var(--muted)', fontSize: 13 }}>Попытки</a>
            <div style={{ flex: 1 }} />
            <a href="/cabinet" style={{ color: 'var(--muted)', fontSize: 13 }}>Личный кабинет</a>
            <a href="/admin" style={{ color: 'var(--muted)', fontSize: 13 }}>Админ</a>
          </div>
        </header>
        <main className="container">{children}</main>
      </body>
    </html>
  )
}
