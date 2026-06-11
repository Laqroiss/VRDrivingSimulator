'use client'
import { useState } from 'react'
import { useRouter } from 'next/navigation'

// Grants/revokes a student's admin rights. Admin accounts get the in-game admin tools
// (the exercise-skip debug panel). Only visible/usable from the admin-protected student card.
export default function AdminToggleButton({ id, isAdmin }) {
  const [busy, setBusy] = useState(false)
  const router = useRouter()

  const toggle = async () => {
    setBusy(true)
    const res = await fetch(`/api/students/${id}`, {
      method:  'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ isAdmin: !isAdmin }),
    })
    setBusy(false)
    if (res.ok) router.refresh()
  }

  return (
    <button
      className={isAdmin ? 'btn-primary' : 'ghost'}
      onClick={toggle}
      disabled={busy}
      title={isAdmin ? 'Click to revoke admin rights' : 'Grant in-game admin tools (exercise-skip panel)'}
    >
      {busy ? '…' : (isAdmin ? '★ Admin' : 'Make admin')}
    </button>
  )
}
