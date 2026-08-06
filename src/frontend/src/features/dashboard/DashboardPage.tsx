import { DashboardCard } from '../../components/DashboardCard'
import { chorePreviews, familyMembers, scheduleItems } from './mockDashboardData'

export function DashboardPage() {
  return (
    <main className="dashboard" id="main-content">
      <DashboardCard
        className="schedule-card"
        eyebrow="Wednesday, August 5"
        id="calendar-preview"
        title="Today"
        action={<span className="status-pill">4 plans</span>}
      >
        <ol className="schedule-list">
          {scheduleItems.map((item) => (
            <li className="schedule-item" key={item.id}>
              <time className="schedule-item__time">{item.time}</time>
              <span className={`schedule-item__marker marker--${item.color}`} aria-hidden="true" />
              <span className="schedule-item__details">
                <strong>{item.title}</strong>
                <span>{item.person}</span>
              </span>
            </li>
          ))}
        </ol>
      </DashboardCard>

      <DashboardCard className="welcome-card" eyebrow="Good morning" title="Ready for a smooth day?">
        <p>Everything your family needs is gathering here—clear, calm, and easy to reach.</p>
        <div className="weather-preview" aria-label="Weather placeholder">
          <span className="weather-preview__icon" aria-hidden="true">☀</span>
          <span><strong>74°</strong> Sunny</span>
        </div>
      </DashboardCard>

      <DashboardCard
        className="chores-card"
        eyebrow="A little progress"
        id="chores-preview"
        title="Chores"
        action={<span className="status-pill status-pill--mint">1 of 3</span>}
      >
        <ul className="chore-list">
          {chorePreviews.map((chore) => (
            <li className={chore.completed ? 'chore chore--complete' : 'chore'} key={chore.id}>
              <span className="chore__check" aria-hidden="true">{chore.completed ? '✓' : ''}</span>
              <span className="chore__details">
                <strong>{chore.title}</strong>
                <span>{chore.person}</span>
              </span>
              <span className="points">+{chore.points}</span>
            </li>
          ))}
        </ul>
        <p className="preview-note">Chore actions arrive in a later milestone.</p>
      </DashboardCard>

      <DashboardCard className="family-card" eyebrow="Keep it going" id="rewards-preview" title="Family points">
        <div className="member-grid">
          {familyMembers.map((member) => (
            <div className="member" key={member.name}>
              <span className={`member__avatar marker--${member.color}`}>{member.name.charAt(0)}</span>
              <span><strong>{member.name}</strong><small>{member.points} points</small></span>
            </div>
          ))}
        </div>
        <div className="reward-preview">
          <span aria-hidden="true">★</span>
          <p><strong>Next family reward</strong><br />Movie night · 200 points</p>
        </div>
      </DashboardCard>
    </main>
  )
}
