import { useState } from 'react'
import { resolveApiUrl, type HouseholdMemberPhotoResponse } from '../lib/api'

export interface MemberAvatarValue {
  displayName: string
  avatarColor: string | null
  photo: HouseholdMemberPhotoResponse | null
}

function initials(displayName: string) {
  return displayName.trim().split(/\s+/).filter(Boolean).slice(0, 2)
    .map((part) => part.charAt(0).toUpperCase()).join('') || '?'
}

export function MemberAvatar({ member, size = 'small', className = '', labelled = false }: {
  member: MemberAvatarValue
  size?: 'small' | 'medium' | 'large'
  className?: string
  labelled?: boolean
}) {
  const [failedAssetId, setFailedAssetId] = useState<string | null>(null)
  const photo = member.photo?.assetId === failedAssetId ? null : member.photo
  const url = photo ? resolveApiUrl(size === 'large' ? photo.largeUrl : size === 'medium' ? photo.mediumUrl : photo.smallUrl) : null

  return (
    <span
      aria-label={labelled ? member.displayName : undefined}
      aria-hidden={labelled ? undefined : 'true'}
      className={`member-avatar marker--${member.avatarColor ?? 'mint'} ${className}`.trim()}
      role={labelled ? 'img' : undefined}
    >
      {url
        ? <img alt="" onError={() => setFailedAssetId(photo!.assetId)} src={url}
            style={{ objectPosition: `${photo!.focalX * 100}% ${photo!.focalY * 100}%` }} />
        : <span>{initials(member.displayName)}</span>}
    </span>
  )
}
