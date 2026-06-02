'use client'
import { usePathname } from 'next/navigation'

const LINKS = [
  { href: '/admin',    label: 'Students', icon: 'ð¤' },
  { href: '/attempts', label: 'Attempts', icon: 'ð‹' },
]

export default function NavLinks() {
  const path = usePathname() || ''
  return (
    <>
      {LINKS.map(l => {
        const active = path === l.href || path.startsWith(l.href + '/') ||
          (l.href === '/admin' && path.startsWith('/students'))
        return (
          <a key={l.href} href={l.href} className={`nav-link${active ? ' active' : ''}`}>
            <span>{l.icon}</span>{l.label}
          </a>
        )
      })}
    </>
  )
}
