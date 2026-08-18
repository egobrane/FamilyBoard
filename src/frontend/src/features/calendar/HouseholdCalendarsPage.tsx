import { useParams } from 'react-router'
import { CalendarSettingsPage } from './CalendarSettingsPage'

export function HouseholdCalendarsPage() {
  const { householdId } = useParams()
  return householdId ? <CalendarSettingsPage householdId={householdId} /> : null
}
