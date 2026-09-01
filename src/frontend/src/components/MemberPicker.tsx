import { MemberAvatar, type MemberAvatarValue } from './MemberAvatar'

export interface MemberPickerOption extends MemberAvatarValue {
  id: string
  detail?: string
}

export function MemberPicker({ legend, members, value, onChange, autoFocus = false }: {
  legend: string
  members: MemberPickerOption[]
  value: string
  onChange: (memberId: string) => void
  autoFocus?: boolean
}) {
  return (
    <fieldset className="member-picker">
      <legend>{legend}</legend>
      <div className="member-picker__options">
        {members.map((member, index) => (
          <label className={value === member.id ? 'member-picker__option member-picker__option--selected' : 'member-picker__option'} key={member.id}>
            <input autoFocus={autoFocus && index === 0} checked={value === member.id} name="household-member" onChange={() => onChange(member.id)} type="radio" value={member.id} />
            <MemberAvatar member={member} />
            <span><strong>{member.displayName}</strong>{member.detail && <small>{member.detail}</small>}</span>
          </label>
        ))}
      </div>
    </fieldset>
  )
}
