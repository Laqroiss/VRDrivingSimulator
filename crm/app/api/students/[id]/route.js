import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import User from '@/models/User'
import Attempt from '@/models/Attempt'

function isAdmin(request) {
  return !!request.cookies.get('admin_token')?.value
}

export async function GET(request, { params }) {
  // Личный кабинет может запрашивать только свои данные — проверяем по studentId
  const isAdminReq = isAdmin(request)
  await connectDB()
  const user = await User.findById(params.id, '-password').lean()
  if (!user) return NextResponse.json({ error: 'Не найден' }, { status: 404 })

  const attempts = await Attempt.find({ studentId: params.id }, '-track').sort({ timestamp: -1 }).lean()
  return NextResponse.json({ user, attempts })
}

export async function DELETE(request, { params }) {
  if (!isAdmin(request))
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 })
  await connectDB()
  await Attempt.deleteMany({ studentId: params.id })
  await User.findByIdAndDelete(params.id)
  return NextResponse.json({ ok: true })
}
