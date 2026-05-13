import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import User from '@/models/User'
import Attempt from '@/models/Attempt'

export async function GET(_, { params }) {
  await connectDB()
  const user = await User.findById(params.id, '-password').lean()
  if (!user) return NextResponse.json({ error: 'Не найден' }, { status: 404 })

  const attempts = await Attempt.find({ studentId: params.id }, '-track').sort({ timestamp: -1 }).lean()
  return NextResponse.json({ user, attempts })
}

export async function DELETE(_, { params }) {
  await connectDB()
  await Attempt.deleteMany({ studentId: params.id })
  await User.findByIdAndDelete(params.id)
  return NextResponse.json({ ok: true })
}
