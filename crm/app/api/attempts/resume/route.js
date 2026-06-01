import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Attempt from '@/models/Attempt'

// GET /api/attempts/resume?id=...   —     
//     /api/attempts/resume?studentId=... | ?student= —   
//  ,        :
//    ( ),  ,  ,    .
//    — { found: false }.
export async function GET(request) {
  await connectDB()
  const { searchParams } = new URL(request.url)
  const id        = searchParams.get('id')
  const studentId = searchParams.get('studentId')
  const student   = searchParams.get('student')

  const select = 'totalPenaltyPoints examDuration exerciseStatuses activatedGates exerciseActivatedAt namedTimerKeys namedTimerStarts penalties track'
  let attempt

  if (id) {
    attempt = await Attempt.findById(id).select(select).lean()
  } else if (studentId || student) {
    const match = { completed: false }
    if (studentId) match.studentId = studentId
    else           match.studentName = { $regex: `^${student}$`, $options: 'i' }
    attempt = await Attempt.findOne(match).sort({ timestamp: -1 }).select(select).lean()
  } else {
    return NextResponse.json({ error: 'id, studentId or student required' }, { status: 400 })
  }

  if (!attempt) return NextResponse.json({ found: false })

  return NextResponse.json({
    found:              true,
    _id:                attempt._id.toString(),
    totalPenaltyPoints: attempt.totalPenaltyPoints ?? 0,
    examDuration:       attempt.examDuration ?? 0,
    exerciseStatuses:   attempt.exerciseStatuses ?? [],
    activatedGates:     attempt.activatedGates ?? [],
    exerciseActivatedAt: attempt.exerciseActivatedAt ?? [],
    namedTimerKeys:     attempt.namedTimerKeys ?? [],
    namedTimerStarts:   attempt.namedTimerStarts ?? [],
    penalties:          attempt.penalties ?? [],
    track:              attempt.track ?? [],
  })
}
