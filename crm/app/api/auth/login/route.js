import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import User from '@/models/User'

export async function POST(request) {
  try {
    await connectDB()
    const { phone, password } = await request.json()

    if (!phone || !password)
      return NextResponse.json({ error: 'Введите телефон и пароль' }, { status: 400 })

    const user = await User.findOne({ phone: phone.trim() })
    if (!user)
      return NextResponse.json({ error: 'Неверный телефон или пароль' }, { status: 401 })

    const ok = await user.comparePassword(password)
    if (!ok)
      return NextResponse.json({ error: 'Неверный телефон или пароль' }, { status: 401 })

    return NextResponse.json({ id: user._id.toString(), phone: user.phone, fullName: user.fullName })
  } catch (err) {
    console.error('[login]', err)
    return NextResponse.json({ error: 'Ошибка сервера' }, { status: 500 })
  }
}
