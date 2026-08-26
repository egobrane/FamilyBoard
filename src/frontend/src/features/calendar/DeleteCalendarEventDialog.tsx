import { useEffect, useRef } from 'react'

export function DeleteCalendarEventDialog({ title, busy, onCancel, onConfirm }: {
  title: string
  busy: boolean
  onCancel: () => void
  onConfirm: () => void
}) {
  const cancelRef = useRef<HTMLButtonElement>(null)
  useEffect(() => {
    cancelRef.current?.focus()
    const close = (event: KeyboardEvent) => { if (event.key === 'Escape' && !busy) onCancel() }
    window.addEventListener('keydown', close)
    return () => window.removeEventListener('keydown', close)
  }, [busy, onCancel])
  return (
    <div aria-labelledby="delete-calendar-event-title" aria-modal="true" className="modal-backdrop" role="dialog">
      <div className="confirmation-dialog">
        <h3 id="delete-calendar-event-title">Delete from Google Calendar?</h3>
        <p><strong>{title}</strong> will be permanently removed from Google Calendar on every device.</p>
        <div className="calendar-event-form__actions">
          <button className="secondary-action" disabled={busy} onClick={onCancel} ref={cancelRef} type="button">Keep event</button>
          <button className="danger-action" disabled={busy} onClick={onConfirm} type="button">
            {busy ? 'Deleting…' : 'Delete from Google Calendar'}
          </button>
        </div>
      </div>
    </div>
  )
}
