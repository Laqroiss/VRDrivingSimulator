import { NextResponse } from 'next/server'

export async function POST(request) {
  const { password } = await request.json()
  const correct = process.env.ADMIN_PASSWORD || 'admin123'
  if (password !== correct)
    return NextResponse.json({ error: 'Неверный пароль' }, { status: 401 })

  const token = Buffer.from(`admin:${correct}`).toString('base64')
  const res   = NextResponse.json({ ok: true })
  res.cookies.set('admin_token', token, {
    httpOnly: true,
    sameSite: 'lax',
    maxAge:   60 * 60 * 8, // 8 часов
    path:     '/',
  })
  return res
}

export async function DELETE() {
  const res = NextResponse.json({ ok: true })
  res.cookies.set('admin_token', '', { maxAge: 0, path: '/' })
  return res
}
