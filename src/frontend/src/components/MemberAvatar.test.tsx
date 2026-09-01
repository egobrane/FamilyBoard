import { fireEvent, render, screen } from '@testing-library/react'
import { expect, it } from 'vitest'
import { MemberAvatar } from './MemberAvatar'

const member = {
  displayName: 'Riley Morgan',
  avatarColor: 'sky',
  photo: {
    assetId: '40000000-0000-0000-0000-000000000001',
    smallUrl: '/api/photo/small',
    mediumUrl: '/api/photo/medium',
    largeUrl: '/api/photo/large',
    pixelWidth: 800,
    pixelHeight: 600,
    focalX: 0.25,
    focalY: 0.75,
  },
}

it('uses the authenticated photo variant and falls back to initials after an image failure', () => {
  const { container } = render(<MemberAvatar labelled member={member} size="medium" />)

  const image = container.querySelector('img')!
  expect(image).toHaveAttribute('src', 'http://localhost:8080/api/photo/medium')
  expect(image).toHaveStyle({ objectPosition: '25% 75%' })
  expect(screen.getByRole('img', { name: 'Riley Morgan' })).toBeInTheDocument()

  fireEvent.error(image)

  expect(container.querySelector('img')).not.toBeInTheDocument()
  expect(screen.getByText('RM')).toBeInTheDocument()
})
