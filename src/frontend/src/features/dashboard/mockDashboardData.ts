export interface ScheduleItem {
  id: string
  time: string
  title: string
  person: string
  color: 'coral' | 'sky' | 'sun' | 'mint'
}

export const demoHouseholdPhotoUrl = '/images/demo-family-photo.jpg'

export const scheduleItems: ScheduleItem[] = [
  { id: 'school', time: '8:10', title: 'School drop-off', person: 'Everyone', color: 'sun' },
  { id: 'dentist', time: '3:30', title: 'Dentist appointment', person: 'Oliver', color: 'coral' },
  { id: 'soccer', time: '5:15', title: 'Soccer practice', person: 'Zoey', color: 'sky' },
  { id: 'dinner', time: '6:45', title: 'Taco night', person: 'Home', color: 'mint' },
]
