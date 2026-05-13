import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Attempt from '@/models/Attempt'

export async function GET(request) {
  await connectDB()
  const { searchParams } = new URL(request.url)
  const student = searchParams.get('student')

  const filter = student ? { studentName: { $regex: student, $options: 'i' } } : {}
  const attempts = await Attempt.find(filter, '-track')
    .sort({ timestamp: -1 })
    .limit(100)
    .lean()

  return NextResponse.json(attempts)
}

export async function POST(request) {
  await connectDB()
  const body = await request.json()
  const attempt = await Attempt.create(body)
  return NextResponse.json({ id: attempt._id }, { status: 201 })
}
