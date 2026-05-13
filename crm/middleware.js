import { NextResponse } from 'next/server'

const PROTECTED = ['/', '/attempts', '/students']

export function middleware(request) {
  const { pathname } = request.nextUrl
  const isProtected = PROTECTED.some(p => pathname === p || pathname.startsWith(p + '/'))
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
  matcher: ['/', '/attempts', '/students/:path*'],
}
