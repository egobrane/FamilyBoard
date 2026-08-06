export interface ScheduleItem {
  id: string
  time: string
  title: string
  person: string
  color: 'coral' | 'sky' | 'sun' | 'mint'
}

export interface ChorePreview {
  id: string
  title: string
  person: string
  points: number
  completed: boolean
}

export const demoHousehold = {
  name: 'Bamford-Fahie-Waltz Family',
} as const

export const scheduleItems: ScheduleItem[] = [
  { id: 'school', time: '8:10', title: 'School drop-off', person: 'Everyone', color: 'sun' },
  { id: 'dentist', time: '3:30', title: 'Dentist appointment', person: 'Oliver', color: 'coral' },
  { id: 'soccer', time: '5:15', title: 'Soccer practice', person: 'Zoey', color: 'sky' },
  { id: 'dinner', time: '6:45', title: 'Taco night', person: 'Home', color: 'mint' },
]

export const chorePreviews: ChorePreview[] = [
  { id: 'dishwasher', title: 'Empty dishwasher', person: 'Oliver', points: 10, completed: true },
  { id: 'dog', title: 'Feed Milo', person: 'Zoey', points: 5, completed: false },
  { id: 'table', title: 'Set the table', person: 'Oliver', points: 5, completed: false },
]

export const familyMembers = [
  { name: 'Oliver', points: 85, color: 'coral' },
  { name: 'Zoey', points: 60, color: 'sky' },
] as const
