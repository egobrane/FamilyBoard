import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { TouchKeyboard } from './TouchKeyboard'
import { isTouchKeyboardTarget, type TouchKeyboardTarget } from './keyboardTarget'

function ControlledHarness({ inputMode, min, multiline = false, step, type = 'text' }: {
  inputMode?: 'decimal' | 'numeric'
  min?: string
  multiline?: boolean
  step?: string
  type?: string
}) {
  const [value, setValue] = useState('')
  const [target, setTarget] = useState<TouchKeyboardTarget | null>(null)
  const field = multiline
    ? <label>Notes<textarea aria-label="Notes" onChange={(event) => setValue(event.target.value)} ref={setTarget} value={value} /></label>
    : <label>Event title<input aria-label="Event title" inputMode={inputMode} min={min} onChange={(event) => setValue(event.target.value)} ref={setTarget} step={step} type={type} value={value} /></label>
  return <>{field}{target && <TouchKeyboard onClose={() => undefined} target={target} />}</>
}

describe('TouchKeyboard', () => {
  it('updates a controlled text field with shift, spaces, and backspace', async () => {
    const user = userEvent.setup()
    render(<ControlledHarness />)
    await user.click(screen.getByLabelText('Event title'))

    await user.click(screen.getByRole('button', { name: 'h' }))
    await user.click(screen.getByRole('button', { name: 'i' }))
    await user.click(screen.getByRole('button', { name: 'Shift' }))
    await user.click(screen.getByRole('button', { name: 'A' }))
    expect(screen.getByLabelText('Event title')).toHaveValue('hiA')

    await user.click(screen.getByRole('button', { name: 'Backspace' }))
    await user.click(screen.getByRole('button', { name: 'Space' }))
    expect(screen.getByLabelText('Event title')).toHaveValue('hi ')
  })

  it('adds a line break to a controlled textarea', async () => {
    const user = userEvent.setup()
    render(<ControlledHarness multiline />)
    await user.click(screen.getByLabelText('Notes'))

    await user.click(screen.getByRole('button', { name: 'o' }))
    await user.click(screen.getByRole('button', { name: 'k' }))
    await user.click(screen.getByRole('button', { name: 'New line' }))
    await user.click(screen.getByRole('button', { name: 'y' }))
    expect(screen.getByLabelText('Notes')).toHaveValue('ok\ny')
  })

  it('offers context-appropriate sign and decimal keys for numeric entry', async () => {
    const user = userEvent.setup()
    render(<ControlledHarness inputMode="decimal" />)

    await user.click(screen.getByRole('button', { name: '4' }))
    await user.click(screen.getByRole('button', { name: 'decimal point' }))
    await user.click(screen.getByRole('button', { name: '2' }))
    await user.click(screen.getByRole('button', { name: 'change sign' }))

    expect(screen.getByLabelText('Event title')).toHaveValue('-4.2')
  })

  it('includes editable text and numeric fields but excludes protected and native controls', () => {
    const { container } = render(<div>
      <input data-testid="text" />
      <input data-testid="number" type="number" />
      <input data-testid="password" type="password" />
      <input data-testid="date" type="date" />
      <input data-testid="readonly" readOnly />
      <input data-testid="excluded" data-touch-keyboard="off" />
      <textarea data-testid="textarea" />
    </div>)
    const field = (id: string) => container.querySelector(`[data-testid="${id}"]`)
    expect(isTouchKeyboardTarget(field('text'))).toBe(true)
    expect(isTouchKeyboardTarget(field('number'))).toBe(true)
    expect(isTouchKeyboardTarget(field('textarea'))).toBe(true)
    expect(isTouchKeyboardTarget(field('password'))).toBe(false)
    expect(isTouchKeyboardTarget(field('date'))).toBe(false)
    expect(isTouchKeyboardTarget(field('readonly'))).toBe(false)
    expect(isTouchKeyboardTarget(field('excluded'))).toBe(false)
  })
})
