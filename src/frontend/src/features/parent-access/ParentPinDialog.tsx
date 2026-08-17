import { useEffect, useRef, useState } from 'react'

interface ParentPinDialogProps {
  heading: string
  description: string
  pinLength: number
  submitLabel: string
  isBusy: boolean
  error: string | null
  onSubmit: (pin: string) => Promise<void>
}

export function ParentPinDialog({
  heading,
  description,
  pinLength,
  submitLabel,
  isBusy,
  error,
  onSubmit,
}: ParentPinDialogProps) {
  const [pin, setPin] = useState('')
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    inputRef.current?.focus()
    return () => setPin('')
  }, [])

  const addDigit = (digit: string) => setPin((current) =>
    current.length < pinLength ? `${current}${digit}` : current)

  return (
    <section aria-labelledby="parent-pin-heading" className="parent-pin-panel">
      <p className="eyebrow">Parent controls</p>
      <h2 id="parent-pin-heading">{heading}</h2>
      <p>{description}</p>
      <form
        onSubmit={(event) => {
          event.preventDefault()
          if (pin.length === pinLength) {
            void onSubmit(pin).finally(() => setPin(''))
          }
        }}
      >
        <label htmlFor="parent-pin">{pinLength}-digit parent PIN</label>
        <input
          autoComplete="off"
          id="parent-pin"
          inputMode="numeric"
          maxLength={pinLength}
          onChange={(event) => setPin(event.target.value.replace(/\D/g, '').slice(0, pinLength))}
          pattern={`[0-9]{${pinLength}}`}
          ref={inputRef}
          type="password"
          value={pin}
        />
        <div aria-label="PIN keypad" className="pin-keypad">
          {'123456789'.split('').map((digit) => (
            <button disabled={isBusy} key={digit} onClick={() => addDigit(digit)} type="button">
              {digit}
            </button>
          ))}
          <button disabled={isBusy || pin.length === 0} onClick={() => setPin((value) => value.slice(0, -1))} type="button">
            Delete
          </button>
          <button disabled={isBusy} onClick={() => addDigit('0')} type="button">0</button>
          <button disabled={isBusy || pin.length === 0} onClick={() => setPin('')} type="button">Clear</button>
        </div>
        {error && <p className="form-error" role="alert">{error}</p>}
        <button className="primary-action" disabled={isBusy || pin.length !== pinLength} type="submit">
          {isBusy ? 'Checking…' : submitLabel}
        </button>
      </form>
    </section>
  )
}
