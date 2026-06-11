import './globals.css'
import NavLinks from '@/components/NavLinks'
import Logo from '@/components/Logo'

export const metadata = { title: 'VRDrive CRM', description: 'Driving school management system' }

export default function RootLayout({ children }) {
  return (
    <html lang="en">
      <body>
        <div className="bg-fx" aria-hidden="true">
          <div className="bg-aurora" />
        </div>

        <header className="site-header">
          <div className="container inner">
            <Logo />
            <NavLinks />
            <div className="nav-spacer" />
            <a href="/cabinet" className="nav-cabinet"> Student Portal</a>
          </div>
        </header>
        <main className="container" style={{ paddingTop: 32, paddingBottom: 56 }}>
          {children}
        </main>
      </body>
    </html>
  )
}
