import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Attempt from '@/models/Attempt'

export async function GET(_, { params }) {
  await connectDB()
  const attempt = await Attempt.findById(params.id).lean()
  if (!attempt) return NextResponse.json({ error: 'Not found' }, { status: 404 })
  return NextResponse.json(attempt)
}

export async function DELETE(_, { params }) {
  await connectDB()
  await Attempt.findByIdAndDelete(params.id)
  return NextResponse.json({ ok: true })
}
