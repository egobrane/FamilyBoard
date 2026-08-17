import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ParentPinDialog } from './ParentPinDialog'

describe('ParentPinDialog', () => {
  it('supports keypad and keyboard entry without exposing the PIN as text', async () => {
    const submit = vi.fn().mockResolvedValue(undefined)
    render(
      <ParentPinDialog
        description="Unlock administration."
        error={null}
        heading="Unlock parent controls"
        isBusy={false}
        onSubmit={submit}
        pinLength={6}
        submitLabel="Unlock"
      />,
    )

    const input = screen.getByLabelText('6-digit parent PIN')
    expect(input).toHaveAttribute('type', 'password')
    fireEvent.change(input, { target: { value: '12letters34' } })
    expect(input).toHaveValue('1234')
    fireEvent.click(screen.getByRole('button', { name: '5' }))
    fireEvent.click(screen.getByRole('button', { name: '6' }))
    fireEvent.click(screen.getByRole('button', { name: 'Unlock' }))

    await waitFor(() => expect(submit).toHaveBeenCalledWith('123456'))
    await waitFor(() => expect(input).toHaveValue(''))
  })

  it('announces a verification error', () => {
    render(
      <ParentPinDialog
        description="Unlock administration."
        error="That PIN did not work."
        heading="Unlock parent controls"
        isBusy={false}
        onSubmit={vi.fn()}
        pinLength={6}
        submitLabel="Unlock"
      />,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('That PIN did not work.')
  })
})
