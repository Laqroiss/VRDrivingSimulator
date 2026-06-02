'use client'
import { useRouter } from 'next/navigation'

export default function DeleteButton({ id }) {
  const router = useRouter()

  const handleDelete = async () => {
    if (!confirm('Delete this attempt?')) return
    await fetch(`/api/attempts/${id}`, { method: 'DELETE' })
    router.refresh()
  }

  return (
    <button onClick={handleDelete} className="ghost"
      style={{ fontSize: 12, color: 'var(--red)', borderColor: 'var(--red)' }}>
      Delete
    </button>
  )
}
