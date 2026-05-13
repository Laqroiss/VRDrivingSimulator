import { NextResponse } from 'next/server'

// Маршруты требующие авторизации админа
const PROTECTED = ['/', '/attempts', '/students', '/admin']
// Исключения внутри /admin (публичные)
const ADMIN_PUBLIC = ['/admin/login']

export function middleware(request) {
  const { pathname } = request.nextUrl

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
