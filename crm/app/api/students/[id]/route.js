import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import User from '@/models/User'
import Attempt from '@/models/Attempt'

function isAdmin(request) {
  return !!request.cookies.get('admin_token')?.value
}

export async function GET(request, { params }) {
  //        —   studentId
  const isAdminReq = isAdmin(request)
  await connectDB()
  const user = await User.findById(params.id, '-password').lean()
  if (!user) return NextResponse.json({ error: 'Not found' }, { status: 404 })

  const attempts = await Attempt.find({ studentId: params.id }, '-track').sort({ timestamp: -1 }).lean()
  return NextResponse.json({ user, attempts })
}

export async function PUT(request, { params }) {
  if (!isAdmin(request))
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 })
  await connectDB()
  const { fullName, phone, password } = await request.json()
  if (!fullName || !phone)
    return NextResponse.json({ error: 'Name and phone are required' }, { status: 400 })

  const update = { fullName: fullName.trim(), phone: phone.trim() }

  if (password) {
    const bcrypt = await import('bcryptjs')
    update.password = await bcrypt.hash(password, 10)
  }

  const user = await User.findByIdAndUpdate(params.id, update, { new: true, select: '-password' })
  if (!user) return NextResponse.json({ error: 'Not found' }, { status: 404 })
  return NextResponse.json(user)
}

// Toggle a student's admin rights (grants the in-game admin tools, e.g. the exercise-skip panel).
export async function PATCH(request, { params }) {
  if (!isAdmin(request))
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 })
  await connectDB()
  const { isAdmin: makeAdmin } = await request.json()
  const user = await User.findByIdAndUpdate(
    params.id, { isAdmin: !!makeAdmin }, { new: true, select: '-password' })
  if (!user) return NextResponse.json({ error: 'Not found' }, { status: 404 })
  return NextResponse.json(user)
}

export async function DELETE(request, { params }) {
  if (!isAdmin(request))
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 })
  await connectDB()
  await Attempt.deleteMany({ studentId: params.id })
  await User.findByIdAndDelete(params.id)
  return NextResponse.json({ ok: true })
}
