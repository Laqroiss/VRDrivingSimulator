import { NextResponse } from 'next/server'

const ADMIN_PUBLIC = ['/admin/login']
const PROTECTED    = ['/attempts', '/students', '/admin']

export function middleware(request) {
  const { pathname } = request.nextUrl

  // / → /admin
  if (pathname === '/') {
    const url = request.nextUrl.clone()
    url.pathname = '/admin'
    return NextResponse.redirect(url)
  }

  const isPublic    = ADMIN_PUBLIC.some(p => pathname.startsWith(p))
  const isProtected = !isPublic && PROTECTED.some(p => pathname === p || pathname.startsWith(p + '/'))

  if (!isProtected) return NextResponse.next()

  const token = request.cookies.get('admin_token')?.value
  if (!token) {
    const url = request.nextUrl.clone()
    url.pathname = '/admin/login'
    return NextResponse.redirect(url)
  }
  return NextResponse.next()
}

export const config = {
  matcher: ['/', '/attempts', '/students/:path*', '/admin', '/admin/:path*'],
}
