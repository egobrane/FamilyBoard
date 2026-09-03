export interface TouchKeyboardKey {
  label: string
  value: string
  spokenLabel?: string
  grow?: number
}

const key = (value: string, label = value, spokenLabel?: string): TouchKeyboardKey => ({
  label,
  value,
  spokenLabel,
})

export const letterRows = [
  [...'qwertyuiop'].map((value) => key(value)),
  [...'asdfghjkl'].map((value) => key(value)),
  [...'zxcvbnm'].map((value) => key(value)),
]

export const symbolRows = [
  [...'1234567890'].map((value) => key(value)),
  ['-', '/', ':', ';', '(', ')', '$', '&', '@', '"'].map((value) => key(value)),
  ['.', ',', '?', '!', "'", '#', '%', '+', '='].map((value) => key(value)),
]

export const numericRows = [
  ['1', '2', '3'].map((value) => key(value)),
  ['4', '5', '6'].map((value) => key(value)),
  ['7', '8', '9'].map((value) => key(value)),
  [key('-', '±', 'change sign'), key('0'), key('.', '.', 'decimal point')],
]
