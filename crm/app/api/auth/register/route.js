import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import User from '@/models/User'

export async function POST(request) {
  try {
    await connectDB()
    const { phone, fullName, password } = await request.json()

    if (!phone || !fullName || !password)
      return NextResponse.json({ error: 'Заполните все поля' }, { status: 400 })

    const exists = await User.findOne({ phone: phone.trim() })
    if (exists)
      return NextResponse.json({ error: 'Номер телефона уже зарегистрирован' }, { status: 409 })

    const user = await User.create({ phone: phone.trim(), fullName: fullName.trim(), password })
    return NextResponse.json({ id: user._id.toString(), phone: user.phone, fullName: user.fullName }, { status: 201 })
  } catch (err) {
    console.error('[register]', err)
    return NextResponse.json({ error: 'Ошибка сервера' }, { status: 500 })
  }
}
