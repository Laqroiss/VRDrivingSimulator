import { NextResponse } from 'next/server'

export async function POST(request) {
  const { password } = await request.json()
  const correct = process.env.ADMIN_PASSWORD || 'admin123'
  if (password !== correct)
    return NextResponse.json({ error: 'Неверный пароль' }, { status: 401 })
  return NextResponse.json({ ok: true, token: Buffer.from(`admin:${correct}`).toString('base64') })
}
