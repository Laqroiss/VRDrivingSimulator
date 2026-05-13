import { NextResponse } from 'next/server'
import { connectDB } from '@/lib/mongodb'
import Attempt from '@/models/Attempt'

// GET — скачать данные повтора (вызывается Unity)
export async function GET(_, { params }) {
  await connectDB()
  const attempt = await Attempt.findById(params.id, 'replayData').lean()
  if (!attempt?.replayData)
    return NextResponse.json({ error: 'Повтор не найден' }, { status: 404 })

  const json = attempt.replayData.toString('utf8')
  return new NextResponse(json, { headers: { 'Content-Type': 'application/json' } })
}

// POST — сохранить данные повтора (вызывается Unity после экзамена)
export async function POST(request, { params }) {
  await connectDB()
  const text = await request.text()
  await Attempt.findByIdAndUpdate(params.id, {
    replayData: Buffer.from(text, 'utf8'),
  })
  return NextResponse.json({ ok: true })
}
