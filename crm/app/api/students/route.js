import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import User from '@/models/User'
import Attempt from '@/models/Attempt'

function isAdmin(request) {
  return !!request.cookies.get('admin_token')?.value
}

export async function GET(request) {
  if (!isAdmin(request))
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 })
  await connectDB()
  const users = await User.find({}, '-password').sort({ createdAt: -1 }).lean()

  const result = await Promise.all(users.map(async u => {
    const attempts = await Attempt.find({ studentId: u._id.toString() }, 'passed totalPenaltyPoints timestamp').lean()
    const passed   = attempts.filter(a => a.passed).length
    const last     = [...attempts].sort((a, b) => new Date(b.timestamp) - new Date(a.timestamp))[0]
    return { ...u, total: attempts.length, passed, failed: attempts.length - passed, lastAttempt: last?.timestamp ?? null }
  }))

  return NextResponse.json(result)
}
