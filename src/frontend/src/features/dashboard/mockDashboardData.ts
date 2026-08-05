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

export const scheduleItems: ScheduleItem[] = [
  { id: 'school', time: '8:10', title: 'School drop-off', person: 'Everyone', color: 'sun' },
  { id: 'dentist', time: '3:30', title: 'Dentist appointment', person: 'Maya', color: 'coral' },
  { id: 'soccer', time: '5:15', title: 'Soccer practice', person: 'Leo', color: 'sky' },
  { id: 'dinner', time: '6:45', title: 'Taco night', person: 'Home', color: 'mint' },
]

export const chorePreviews: ChorePreview[] = [
  { id: 'dishwasher', title: 'Empty dishwasher', person: 'Maya', points: 10, completed: true },
  { id: 'dog', title: 'Feed Pepper', person: 'Leo', points: 5, completed: false },
  { id: 'table', title: 'Set the table', person: 'Maya', points: 5, completed: false },
]

export const familyMembers = [
  { name: 'Maya', points: 85, color: 'coral' },
  { name: 'Leo', points: 60, color: 'sky' },
] as const
