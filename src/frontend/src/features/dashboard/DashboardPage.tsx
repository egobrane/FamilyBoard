import type { CSSProperties } from 'react'
import { DashboardCard } from '../../components/DashboardCard'
import { DashboardCalendarCard } from '../calendar/DashboardCalendarCard'
import { DashboardChoresCard } from '../chores/DashboardChoresCard'
import { DashboardRewardsCard } from '../rewards/DashboardRewardsCard'
import { DashboardTasksCard } from '../tasks/DashboardTasksCard'
import { demoHouseholdPhotoUrl } from './mockDashboardData'

export function DashboardPage({ householdPhotoUrl = demoHouseholdPhotoUrl }: { householdPhotoUrl?: string }) {
  return (
    <main className="dashboard" id="main-content">
      <DashboardCalendarCard />

      <DashboardCard
        className="welcome-card"
        eyebrow="Good morning"
        title="Ready for a good day?"
        style={{ '--household-photo': `url("${householdPhotoUrl}")` } as CSSProperties}
      >
        <p>FamilyBoard is in active development. Annie is beautiful.</p>
        <div className="weather-preview" aria-label="Weather placeholder">
          <span className="weather-preview__icon" aria-hidden="true">☀</span>
          <span><strong>74°</strong> Sunny</span>
        </div>
      </DashboardCard>

      <DashboardChoresCard />

      <DashboardTasksCard />

      <DashboardRewardsCard />
    </main>
  )
}
