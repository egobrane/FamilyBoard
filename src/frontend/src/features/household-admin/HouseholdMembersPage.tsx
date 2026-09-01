import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
  type KeyboardEvent,
} from 'react'
import { useParams } from 'react-router'
import {
  ApiError,
  createChildMember,
  listHouseholdMembers,
  removeHouseholdMemberPhoto,
  updateHouseholdMember,
  updateHouseholdMemberPhotoPosition,
  uploadHouseholdMemberPhoto,
  type HouseholdMemberResponse,
} from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { MemberAvatar } from '../../components/MemberAvatar'

const avatarColors = ['mint', 'sky', 'sun', 'coral'] as const

type LoadState =
  | { status: 'loading' }
  | { status: 'ready'; members: HouseholdMemberResponse[] }
  | { status: 'notFound' | 'forbidden' | 'failed' }

interface EditorState {
  mode: 'create' | 'edit'
  member?: HouseholdMemberResponse
}

interface ConfirmationState {
  member: HouseholdMemberResponse
  nextIsActive: boolean
  triggerId: string
}

function safeAvatarColor(value: string | null) {
  return avatarColors.includes(value as (typeof avatarColors)[number]) ? value! : 'mint'
}

function handleDialogKeyboard(
  event: KeyboardEvent<HTMLElement>,
  container: HTMLElement | null,
  close: () => void,
  blocked: boolean,
) {
  if (event.key === 'Escape' && !blocked) {
    event.preventDefault()
    close()
    return
  }
  if (event.key !== 'Tab' || container === null) return

  const focusable = Array.from(container.querySelectorAll<HTMLElement>(
    'button:not([disabled]), input:not([disabled]), select:not([disabled]), [href], [tabindex]:not([tabindex="-1"])',
  ))
  if (focusable.length === 0) return
  const first = focusable[0]
  const last = focusable[focusable.length - 1]
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}

export function HouseholdMembersPage() {
  const { householdId = '' } = useParams()
  const { state, refreshSilently } = useAuthentication()
  const [loadState, setLoadState] = useState<LoadState>({ status: 'loading' })
  const [editor, setEditor] = useState<EditorState | null>(null)
  const [confirmation, setConfirmation] = useState<ConfirmationState | null>(null)
  const [photoEditor, setPhotoEditor] = useState<HouseholdMemberResponse | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [isMutating, setIsMutating] = useState(false)
  const confirmationDialogRef = useRef<HTMLElement>(null)
  const editorTriggerIdRef = useRef<string | null>(null)
  const photoTriggerIdRef = useRef<string | null>(null)

  const currentMemberId = state.status === 'authenticated'
    ? state.currentUser.households.find((household) => household.id === householdId)?.memberId
    : undefined

  const load = useCallback(async () => {
    setLoadState({ status: 'loading' })
    try {
      setLoadState({ status: 'ready', members: await listHouseholdMembers(householdId) })
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        await refreshSilently()
      } else if (error instanceof ApiError && error.status === 404) {
        setLoadState({ status: 'notFound' })
      } else if (error instanceof ApiError && error.status === 403) {
        setLoadState({ status: 'forbidden' })
      } else {
        setLoadState({ status: 'failed' })
      }
    }
  }, [householdId, refreshSilently])

  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(timer)
  }, [load])

  const closeConfirmation = () => {
    const triggerId = confirmation?.triggerId
    setConfirmation(null)
    if (triggerId) window.requestAnimationFrame(() => document.getElementById(triggerId)?.focus())
  }

  const openEditor = (nextEditor: EditorState, triggerId: string) => {
    editorTriggerIdRef.current = triggerId
    setEditor(nextEditor)
  }

  const closeEditor = () => {
    const triggerId = editorTriggerIdRef.current
    setEditor(null)
    if (triggerId) window.requestAnimationFrame(() => document.getElementById(triggerId)?.focus())
  }

  const openPhotoEditor = (member: HouseholdMemberResponse, triggerId: string) => {
    photoTriggerIdRef.current = triggerId
    setPhotoEditor(member)
  }

  const closePhotoEditor = () => {
    const triggerId = photoTriggerIdRef.current
    setPhotoEditor(null)
    if (triggerId) window.requestAnimationFrame(() => document.getElementById(triggerId)?.focus())
  }

  const updateStatus = async () => {
    if (!confirmation) return
    setIsMutating(true)
    setActionError(null)
    setSuccess(null)
    try {
      const updated = await updateHouseholdMember(
        householdId,
        confirmation.member.id,
        { isActive: confirmation.nextIsActive },
      )
      setLoadState((current) => current.status === 'ready'
        ? {
            status: 'ready',
            members: current.members.map((member) => member.id === updated.id ? updated : member),
          }
        : current)
      setSuccess(`${updated.displayName} is now ${updated.isActive ? 'active' : 'inactive'}.`)
      closeConfirmation()
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        await refreshSilently()
      } else if (error instanceof ApiError && error.problem.code === 'last_active_adult') {
        setActionError('The final active adult cannot be deactivated.')
        closeConfirmation()
      } else if (error instanceof ApiError && error.problem.code === 'self_deactivation_requires_leave_flow') {
        setActionError('Leaving a household requires a dedicated leave-household workflow.')
        closeConfirmation()
      } else if (error instanceof ApiError && error.status === 409) {
        setActionError('The household changed at the same time. Refresh the member list and try again.')
        closeConfirmation()
      } else if (error instanceof ApiError && error.status === 404) {
        setActionError('That member is no longer available. Refresh the member list.')
        closeConfirmation()
      } else {
        setActionError('The member status could not be changed. Check your connection and try again.')
      }
    } finally {
      setIsMutating(false)
    }
  }

  if (loadState.status !== 'ready') {
    const content = {
      loading: ['Loading members…', 'Retrieving active and inactive household profiles.'],
      notFound: ['Household not found', 'This household is unavailable to the current account.'],
      forbidden: ['Adult access required', 'An adult household account must manage members.'],
      failed: ['Members could not be loaded', 'Check your connection and try again.'],
    }[loadState.status]
    return (
      <section className="admin-panel admin-status" role={loadState.status === 'loading' ? 'status' : 'alert'}>
        <h3>{content[0]}</h3>
        <p>{content[1]}</p>
        {loadState.status === 'failed' && <button className="secondary-action" onClick={() => void load()} type="button">Try again</button>}
      </section>
    )
  }

  const activeMembers = loadState.members.filter((member) => member.isActive)
  const inactiveMembers = loadState.members.filter((member) => !member.isActive)

  return (
    <section className="admin-panel" aria-labelledby="household-members-title">
      <div className="admin-panel__heading">
        <div>
          <p className="eyebrow">Profiles and access</p>
          <h3 id="household-members-title">Household members</h3>
        </div>
        <button className="primary-action" id="add-child-profile" onClick={() => openEditor({ mode: 'create' }, 'add-child-profile')} type="button">Add child</button>
      </div>
      <p className="admin-panel__lede">Children use profiles without Google accounts. New adults join through invitation links in a future increment.</p>
      {success && <p className="save-success" role="status">{success}</p>}
      {actionError && <p className="form-error-summary" role="alert">{actionError}</p>}

      <MemberGroup
        currentMemberId={currentMemberId}
        members={activeMembers}
        onEdit={(member, triggerId) => openEditor({ mode: 'edit', member }, triggerId)}
        onPhoto={openPhotoEditor}
        onToggle={(member, triggerId) => setConfirmation({ member, nextIsActive: false, triggerId })}
        title="Active members"
      />
      <MemberGroup
        currentMemberId={currentMemberId}
        emptyMessage="No inactive profiles."
        members={inactiveMembers}
        onEdit={(member, triggerId) => openEditor({ mode: 'edit', member }, triggerId)}
        onPhoto={openPhotoEditor}
        onToggle={(member, triggerId) => setConfirmation({ member, nextIsActive: true, triggerId })}
        title="Inactive profiles"
      />

      {editor && (
        <MemberEditor
          editor={editor}
          householdId={householdId}
          onClose={closeEditor}
          onSaved={(saved, created) => {
            setLoadState((current) => current.status === 'ready'
              ? {
                  status: 'ready',
                  members: created
                    ? [...current.members, saved]
                    : current.members.map((member) => member.id === saved.id ? saved : member),
                }
              : current)
            closeEditor()
            setSuccess(created ? `${saved.displayName} was added.` : `${saved.displayName} was updated.`)
          }}
          refreshAuthentication={refreshSilently}
        />
      )}

      {confirmation && (
        <div className="dialog-backdrop">
          <section
            aria-labelledby="member-status-dialog-title"
            aria-modal="true"
            className="confirmation-dialog"
            onKeyDown={(event) => handleDialogKeyboard(event, confirmationDialogRef.current, closeConfirmation, isMutating)}
            ref={confirmationDialogRef}
            role="dialog"
          >
            <p className="eyebrow">Confirm profile status</p>
            <h3 id="member-status-dialog-title">{confirmation.nextIsActive ? 'Reactivate' : 'Deactivate'} {confirmation.member.displayName}?</h3>
            <p>{confirmation.nextIsActive
              ? 'This profile will become available for household activity again.'
              : 'The profile and its history will be kept, but it will be shown as inactive.'}</p>
            <div className="dialog-actions">
              <button className="secondary-action" disabled={isMutating} onClick={closeConfirmation} type="button">Cancel</button>
              <button autoFocus className="danger-action" disabled={isMutating} onClick={() => void updateStatus()} type="button">
                {isMutating ? 'Saving…' : confirmation.nextIsActive ? 'Reactivate profile' : 'Deactivate profile'}
              </button>
            </div>
          </section>
        </div>
      )}
      {photoEditor && (
        <MemberPhotoEditor
          householdId={householdId}
          member={photoEditor}
          onClose={closePhotoEditor}
          onSaved={(saved, message) => {
            setPhotoEditor(saved)
            setLoadState((current) => current.status === 'ready' ? {
              status: 'ready', members: current.members.map((member) => member.id === saved.id ? saved : member),
            } : current)
            setSuccess(message)
          }}
          refreshAuthentication={refreshSilently}
        />
      )}
    </section>
  )
}

interface MemberGroupProps {
  title: string
  members: HouseholdMemberResponse[]
  currentMemberId?: string
  emptyMessage?: string
  onEdit: (member: HouseholdMemberResponse, triggerId: string) => void
  onPhoto: (member: HouseholdMemberResponse, triggerId: string) => void
  onToggle: (member: HouseholdMemberResponse, triggerId: string) => void
}

function MemberGroup({ title, members, currentMemberId, emptyMessage, onEdit, onPhoto, onToggle }: MemberGroupProps) {
  return (
    <section className="member-section" aria-labelledby={`member-group-${title.replace(/\s/g, '-').toLowerCase()}`}>
      <h4 id={`member-group-${title.replace(/\s/g, '-').toLowerCase()}`}>{title}</h4>
      {members.length === 0 && <p className="empty-state">{emptyMessage ?? 'No profiles found.'}</p>}
      <div className="admin-member-grid">
        {members.map((member) => {
          const isCurrentMember = member.id === currentMemberId
          const toggleId = `member-status-${member.id}`
          const editId = `member-edit-${member.id}`
          const photoId = `member-photo-${member.id}`
          return (
            <article className={`admin-member-card ${member.isActive ? '' : 'admin-member-card--inactive'}`} key={member.id}>
              <MemberAvatar className="admin-member-avatar" member={member} size="medium" />
              <div className="admin-member-card__identity">
                <h5>{member.displayName}</h5>
                <p>{member.role === 'adult' ? 'Adult' : 'Child profile'}{isCurrentMember ? ' · You' : ''}</p>
              </div>
              <div className="member-actions">
                <button className="secondary-action" id={photoId} onClick={() => onPhoto(member, photoId)} type="button">Photo</button>
                <button className="secondary-action" id={editId} onClick={() => onEdit(member, editId)} type="button">Edit</button>
                {!isCurrentMember && (
                  <button className={member.isActive ? 'danger-link' : 'secondary-action'} id={toggleId} onClick={() => onToggle(member, toggleId)} type="button">
                    {member.isActive ? 'Deactivate' : 'Reactivate'}
                  </button>
                )}
              </div>
            </article>
          )
        })}
      </div>
    </section>
  )
}

function MemberPhotoEditor({ householdId, member, onClose, onSaved, refreshAuthentication }: {
  householdId: string
  member: HouseholdMemberResponse
  onClose: () => void
  onSaved: (member: HouseholdMemberResponse, message: string) => void
  refreshAuthentication: () => Promise<void>
}) {
  const [file, setFile] = useState<File | null>(null)
  const [focalX, setFocalX] = useState(member.photo?.focalX ?? 0.5)
  const [focalY, setFocalY] = useState(member.photo?.focalY ?? 0.5)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [confirmReplace, setConfirmReplace] = useState(false)
  const [confirmRemove, setConfirmRemove] = useState(false)
  const dialogRef = useRef<HTMLElement>(null)

  const previewUrl = useMemo(() => file ? URL.createObjectURL(file) : null, [file])
  useEffect(() => () => { if (previewUrl) URL.revokeObjectURL(previewUrl) }, [previewUrl])

  async function run(action: () => Promise<HouseholdMemberResponse>, message: string) {
    setBusy(true); setError('')
    try {
      const saved = await action()
      setFocalX(saved.photo?.focalX ?? 0.5); setFocalY(saved.photo?.focalY ?? 0.5)
      setFile(null); setConfirmReplace(false); setConfirmRemove(false)
      onSaved(saved, message)
    } catch (reason) {
      if (reason instanceof ApiError && reason.status === 401) await refreshAuthentication()
      setError(reason instanceof ApiError
        ? reason.problem.errors?.photo?.[0] ?? reason.problem.title
        : 'The member photo could not be changed. Try again.')
    } finally { setBusy(false) }
  }

  const displayStyle = previewUrl ? { objectPosition: `${focalX * 100}% ${focalY * 100}%` } : undefined
  return <div className="dialog-backdrop">
    <section
      aria-labelledby="member-photo-title"
      aria-modal="true"
      className="member-editor member-photo-editor"
      onKeyDown={(event) => handleDialogKeyboard(event, dialogRef.current, onClose, busy)}
      ref={dialogRef}
      role="dialog"
    >
      <p className="eyebrow">Private household photo</p>
      <h3 id="member-photo-title">Photo for {member.displayName}</h3>
      <div className="member-photo-preview">
        {previewUrl
          ? <img alt={`Preview for ${member.displayName}`} src={previewUrl} style={displayStyle} />
          : <MemberAvatar labelled member={member} size="large" />}
      </div>
      {error && <p className="form-error-summary" role="alert">{error}</p>}
      <label className="form-field"><span>Choose a JPEG, PNG, or WebP photo</span>
        <input accept="image/jpeg,image/png,image/webp" autoFocus disabled={busy} onChange={(event) => {
          setFile(event.target.files?.[0] ?? null); setConfirmReplace(false)
        }} type="file" />
      </label>
      {(file || member.photo) && <div className="member-photo-focal-controls">
        <label>Horizontal focus<input disabled={busy} max="1" min="0" onChange={(event) => setFocalX(Number(event.target.value))} step="0.01" type="range" value={focalX} /></label>
        <label>Vertical focus<input disabled={busy} max="1" min="0" onChange={(event) => setFocalY(Number(event.target.value))} step="0.01" type="range" value={focalY} /></label>
      </div>}
      {confirmReplace && <p role="alert">Replace the current private photo with this selected image?</p>}
      {confirmRemove && <p role="alert">Remove this private photo and return to initials?</p>}
      <div className="dialog-actions">
        <button className="secondary-action" disabled={busy} onClick={onClose} type="button">Close</button>
        {member.photo && <button className="danger-action" disabled={busy} onClick={() => {
          if (!confirmRemove) { setConfirmRemove(true); return }
          void run(() => removeHouseholdMemberPhoto(householdId, member.id, member.photoVersion), `${member.displayName}'s photo was removed.`)
        }} type="button">{confirmRemove ? 'Confirm removal' : 'Remove photo'}</button>}
        {member.photo && !file && <button className="secondary-action" disabled={busy} onClick={() => void run(
          () => updateHouseholdMemberPhotoPosition(householdId, member.id, {
            expectedPhotoVersion: member.photoVersion, focalX, focalY,
          }), `${member.displayName}'s photo position was updated.`,
        )} type="button">Save position</button>}
        {file && <button className="primary-action" disabled={busy} onClick={() => {
          if (member.photo && !confirmReplace) { setConfirmReplace(true); return }
          void run(() => uploadHouseholdMemberPhoto(householdId, member.id, file, member.photoVersion),
            `${member.displayName}'s photo was ${member.photo ? 'replaced' : 'uploaded'}.`)
        }} type="button">{busy ? 'Uploading…' : member.photo && !confirmReplace ? 'Replace photo' : 'Upload photo'}</button>}
      </div>
    </section>
  </div>
}

interface MemberEditorProps {
  editor: EditorState
  householdId: string
  onClose: () => void
  onSaved: (member: HouseholdMemberResponse, created: boolean) => void
  refreshAuthentication: () => Promise<void>
}

function MemberEditor({ editor, householdId, onClose, onSaved, refreshAuthentication }: MemberEditorProps) {
  const errorRef = useRef<HTMLDivElement>(null)
  const dialogRef = useRef<HTMLElement>(null)
  const [displayName, setDisplayName] = useState(editor.member?.displayName ?? '')
  const [avatarColor, setAvatarColor] = useState(safeAvatarColor(editor.member?.avatarColor ?? null))
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [isSaving, setIsSaving] = useState(false)

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setErrors({})
    setIsSaving(true)
    try {
      const member = editor.mode === 'create'
        ? await createChildMember(householdId, { displayName, avatarColor })
        : await updateHouseholdMember(householdId, editor.member!.id, { displayName, avatarColor })
      onSaved(member, editor.mode === 'create')
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        await refreshAuthentication()
      } else if (error instanceof ApiError && error.problem.errors) {
        setErrors(error.problem.errors)
        window.requestAnimationFrame(() => errorRef.current?.focus())
      } else if (error instanceof ApiError && error.status === 404) {
        setErrors({ request: ['The household member is no longer available.'] })
        window.requestAnimationFrame(() => errorRef.current?.focus())
      } else {
        setErrors({ request: ['The profile could not be saved. Check your connection and try again.'] })
        window.requestAnimationFrame(() => errorRef.current?.focus())
      }
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <div className="dialog-backdrop">
      <section
        aria-labelledby="member-editor-title"
        aria-modal="true"
        className="member-editor"
        onKeyDown={(event) => handleDialogKeyboard(event, dialogRef.current, onClose, isSaving)}
        ref={dialogRef}
        role="dialog"
      >
        <p className="eyebrow">{editor.mode === 'create' ? 'New child profile' : 'Edit profile'}</p>
        <h3 id="member-editor-title">{editor.mode === 'create' ? 'Add a child' : `Edit ${editor.member!.displayName}`}</h3>
        <form className="admin-form" noValidate onSubmit={(event) => void submit(event)}>
          {Object.keys(errors).length > 0 && (
            <div className="form-error-summary" ref={errorRef} role="alert" tabIndex={-1}>
              <strong>We could not save this profile.</strong>
              <span>{errors.request?.[0] ?? 'Review the highlighted fields and try again.'}</span>
            </div>
          )}
          <label className="form-field">
            <span>Display name</span>
            <input aria-describedby={errors.displayName ? 'member-name-error' : undefined} aria-invalid={errors.displayName ? 'true' : undefined} autoFocus maxLength={80} onChange={(event) => setDisplayName(event.target.value)} required value={displayName} />
            {errors.displayName && <small className="field-error" id="member-name-error">{errors.displayName[0]}</small>}
          </label>
          <fieldset className="avatar-color-field">
            <legend>Avatar color</legend>
            <div className="avatar-color-options">
              {avatarColors.map((color) => (
                <label key={color}>
                  <input checked={avatarColor === color} name="avatar-color" onChange={() => setAvatarColor(color)} type="radio" value={color} />
                  <span className={`marker--${color}`} aria-hidden="true" />
                  {color.charAt(0).toUpperCase() + color.slice(1)}
                </label>
              ))}
            </div>
          </fieldset>
          <div className="dialog-actions">
            <button className="secondary-action" disabled={isSaving} onClick={onClose} type="button">Cancel</button>
            <button className="primary-action" disabled={isSaving} type="submit">{isSaving ? 'Saving…' : editor.mode === 'create' ? 'Add child' : 'Save profile'}</button>
          </div>
        </form>
      </section>
    </div>
  )
}
