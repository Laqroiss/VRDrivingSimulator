import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Attempt from '@/models/Attempt'

export async function GET(_, { params }) {
  await connectDB()
  const attempt = await Attempt.findById(params.id)
    .select('-replayData -track -lightEvents -lightPositions')
    .lean()
  if (!attempt) return NextResponse.json({ error: 'Not found' }, { status: 404 })
  return NextResponse.json(attempt)
}

// PUT —    ( /    Unity)
export async function PUT(request, { params }) {
  await connectDB()
  const body = await request.json()
  const attempt = await Attempt.findByIdAndUpdate(params.id, body, { new: true })
  if (!attempt) return NextResponse.json({ error: 'Not found' }, { status: 404 })
  return NextResponse.json({ id: attempt._id })
}

export async function DELETE(_, { params }) {
  await connectDB()
  await Attempt.findByIdAndDelete(params.id)
  return NextResponse.json({ ok: true })
}
