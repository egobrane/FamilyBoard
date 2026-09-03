export type TouchKeyboardTarget = HTMLInputElement | HTMLTextAreaElement

export function isTouchKeyboardTarget(value: unknown): value is TouchKeyboardTarget {
  if (value instanceof HTMLTextAreaElement) {
    return !value.disabled && !value.readOnly && value.dataset.touchKeyboard !== 'off'
  }
  if (!(value instanceof HTMLInputElement) || value.disabled || value.readOnly
    || value.dataset.touchKeyboard === 'off') return false
  return ['text', 'search', 'email', 'url', 'tel', 'number'].includes(value.type)
}

