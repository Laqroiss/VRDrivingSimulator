import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Attempt from '@/models/Attempt'

export async function GET(request) {
  await connectDB()
  const { searchParams } = new URL(request.url)
  const studentId = searchParams.get('studentId')
  const student   = searchParams.get('student')

  //     studentId (  ) —  ,   :
  //       studentId     .
  const match = studentId
    ? { studentId }
    : student
      ? { studentName: { $regex: student, $options: 'i' } }
      : {}

  //  :    (track, replayData, penalties...),
  //    hasReplay —    .  , 
  //      ,    .
  const attempts = await Attempt.aggregate([
    { $match: match },
    { $sort: { timestamp: -1 } },
    { $limit: 100 },
    {
      $project: {
        studentId: 1, studentName: 1, studentPhone: 1, timestamp: 1,
        passed: 1, totalPenaltyPoints: 1, examDuration: 1,
        exerciseStatuses: 1,
        //   =  completed:true  .10  ( 9) .
        //     ,     completed.
        completed: {
          $cond: [
            { $or: [
              { $eq: ['$completed', true] },
              { $eq: [{ $arrayElemAt: ['$exerciseStatuses', 9] }, 'Completed'] },
            ] },
            true, false,
          ],
        },
        hasReplay: { $cond: [{ $ne: ['$replayData', null] }, true, false] },
        //     — ,    (  ).
        hasTrack: { $cond: [{ $gt: [{ $size: { $ifNull: ['$track', []] } }, 0] }, true, false] },
      },
    },
  ])

  return NextResponse.json(attempts)
}

export async function POST(request) {
  await connectDB()
  const body = await request.json()
  const attempt = await Attempt.create(body)
  return NextResponse.json({ id: attempt._id }, { status: 201 })
}
