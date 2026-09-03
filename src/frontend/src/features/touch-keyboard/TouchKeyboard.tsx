import { useMemo, useState, type PointerEvent } from 'react'
import { letterRows, numericRows, symbolRows, type TouchKeyboardKey } from './keyboardLayouts'
import { isTouchKeyboardTarget, type TouchKeyboardTarget } from './keyboardTarget'

function targetLabel(target: TouchKeyboardTarget) {
  return target.getAttribute('aria-label')
    ?? target.labels?.[0]?.textContent
    ?? target.closest('label')?.querySelector('span')?.textContent
    ?? target.closest('label')?.childNodes[0]?.textContent
    ?? target.name
    ?? 'text field'
}

function setNativeValue(target: TouchKeyboardTarget, value: string) {
  const prototype = target instanceof HTMLTextAreaElement
    ? HTMLTextAreaElement.prototype
    : HTMLInputElement.prototype
  Object.getOwnPropertyDescriptor(prototype, 'value')?.set?.call(target, value)
  target.dispatchEvent(new Event('input', { bubbles: true }))
}

function selection(target: TouchKeyboardTarget) {
  try {
    return {
      start: target.selectionStart ?? target.value.length,
      end: target.selectionEnd ?? target.value.length,
    }
  } catch {
    return { start: target.value.length, end: target.value.length }
  }
}

function restoreCaret(target: TouchKeyboardTarget, position: number) {
  try { target.setSelectionRange(position, position) } catch { /* Number inputs do not expose a text selection. */ }
}

function replaceSelection(target: TouchKeyboardTarget, value: string) {
  const current = selection(target)
  const available = target.maxLength > -1
    ? Math.max(0, target.maxLength - (target.value.length - (current.end - current.start)))
    : value.length
  const inserted = value.slice(0, available)
  const next = `${target.value.slice(0, current.start)}${inserted}${target.value.slice(current.end)}`
  setNativeValue(target, next)
  restoreCaret(target, current.start + inserted.length)
}

function backspace(target: TouchKeyboardTarget) {
  const current = selection(target)
  const start = current.start === current.end ? Math.max(0, current.start - 1) : current.start
  const next = `${target.value.slice(0, start)}${target.value.slice(current.end)}`
  setNativeValue(target, next)
  restoreCaret(target, start)
}

function focusRelative(target: TouchKeyboardTarget, direction: -1 | 1) {
  const form = target.closest('form')
  const candidates = Array.from((form ?? document).querySelectorAll<TouchKeyboardTarget>('input, textarea'))
    .filter(isTouchKeyboardTarget)
  const index = candidates.indexOf(target)
  const next = candidates[index + direction]
  if (next) next.focus({ preventScroll: true })
}

function KeyboardKey({ item, onPress, shifted }: {
  item: TouchKeyboardKey
  onPress: (value: string) => void
  shifted?: boolean
}) {
  const display = shifted ? item.label.toUpperCase() : item.label
  const value = shifted ? item.value.toUpperCase() : item.value
  return <button aria-label={item.spokenLabel ?? display} className="touch-keyboard__key" onClick={() => onPress(value)}
    onPointerDown={(event: PointerEvent<HTMLButtonElement>) => event.preventDefault()} tabIndex={-1} type="button">{display}</button>
}

export function TouchKeyboard({ target, onClose }: { target: TouchKeyboardTarget; onClose: () => void }) {
  const [symbols, setSymbols] = useState(false)
  const [shifted, setShifted] = useState(false)
  const [capsLock, setCapsLock] = useState(false)
  const input = target instanceof HTMLInputElement ? target : null
  const numeric = target.type === 'number' || target.inputMode === 'numeric' || target.inputMode === 'decimal'
  const email = target.type === 'email' || target.inputMode === 'email'
  const supportsNegative = input?.type !== 'number' || Number(input.min) < 0
  const supportsDecimal = target.inputMode === 'decimal' || input?.step === 'any' || input?.step.includes('.') === true
  const rows = numeric
    ? numericRows.map((row) => row.filter((item) => (item.value !== '-' || supportsNegative) && (item.value !== '.' || supportsDecimal)))
    : symbols ? symbolRows : letterRows
  const uppercase = shifted !== capsLock
  const label = useMemo(() => targetLabel(target)?.trim() || 'text field', [target])

  const typeValue = (value: string) => {
    if (numeric && value === '-') {
      const next = target.value.startsWith('-') ? target.value.slice(1) : `-${target.value}`
      setNativeValue(target, next)
      restoreCaret(target, next.length)
      return
    }
    if (numeric && value === '.' && target.value.includes('.')) return
    replaceSelection(target, value)
    if (shifted && !capsLock) setShifted(false)
  }

  return (
    <section aria-label={`On-screen keyboard for ${label}`} className={`touch-keyboard${numeric ? ' touch-keyboard--numeric' : ''}`}
      data-workspace-swipe-ignore role="region">
      <div className="touch-keyboard__toolbar">
        <strong>{label}</strong>
        <div>
          <button onClick={() => focusRelative(target, -1)} onPointerDown={(event) => event.preventDefault()} tabIndex={-1} type="button">Previous</button>
          <button onClick={() => focusRelative(target, 1)} onPointerDown={(event) => event.preventDefault()} tabIndex={-1} type="button">Next</button>
          <button className="touch-keyboard__done" onClick={() => { target.blur(); onClose() }}
            onPointerDown={(event) => event.preventDefault()} tabIndex={-1} type="button">Done</button>
        </div>
      </div>
      <div className="touch-keyboard__rows">
        {rows.map((row, index) => <div className="touch-keyboard__row" key={index}>
          {row.map((item) => <KeyboardKey item={item} key={item.value} onPress={typeValue} shifted={!numeric && !symbols && uppercase} />)}
        </div>)}
      </div>
      {!numeric && <div className="touch-keyboard__row touch-keyboard__controls">
        <button aria-pressed={symbols} onClick={() => { setSymbols((value) => !value); setShifted(false) }}
          onPointerDown={(event) => event.preventDefault()} tabIndex={-1} type="button">{symbols ? 'ABC' : '123'}</button>
        {!symbols && <button aria-pressed={shifted} onClick={() => setShifted((value) => !value)}
          onPointerDown={(event) => event.preventDefault()} tabIndex={-1} type="button">Shift</button>}
        {!symbols && <button aria-pressed={capsLock} onClick={() => setCapsLock((value) => !value)}
          onPointerDown={(event) => event.preventDefault()} tabIndex={-1} type="button">Caps</button>}
        {email && <button onClick={() => typeValue('@')} onPointerDown={(event) => event.preventDefault()} tabIndex={-1} type="button">@</button>}
        <button className="touch-keyboard__space" onClick={() => typeValue(' ')}
          onPointerDown={(event) => event.preventDefault()} tabIndex={-1} type="button">Space</button>
        {email && <button onClick={() => typeValue('.com')} onPointerDown={(event) => event.preventDefault()} tabIndex={-1} type="button">.com</button>}
        {target instanceof HTMLTextAreaElement && <button onClick={() => typeValue('\n')}
          onPointerDown={(event) => event.preventDefault()} tabIndex={-1} type="button">New line</button>}
        <button aria-label="Backspace" onClick={() => backspace(target)}
          onPointerDown={(event) => event.preventDefault()} tabIndex={-1} type="button">⌫</button>
      </div>}
      {numeric && <div className="touch-keyboard__row touch-keyboard__controls">
        <button className="touch-keyboard__space" onClick={() => setNativeValue(target, '')}
          onPointerDown={(event) => event.preventDefault()} tabIndex={-1} type="button">Clear</button>
        <button aria-label="Backspace" onClick={() => backspace(target)}
          onPointerDown={(event) => event.preventDefault()} tabIndex={-1} type="button">⌫</button>
      </div>}
    </section>
  )
}
