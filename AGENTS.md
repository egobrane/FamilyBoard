# Family Dashboard Engineering Guidelines

## Vision

This project is a touch-first family organization platform intended to become a central operating system for a household.

It should provide one convenient place for calendars, reminders, chores, rewards, household tasks, and future family-management features.

The application should reduce household friction, encourage positive habits, and be enjoyable for both adults and children to use.

Long-term maintainability, portability, usability, and extensibility are more important than implementing features quickly.

---

## Product Principles

The application must:

- Be touch-first.
- Feel closer to a native application than a traditional website.
- Be intuitive enough for children to use with minimal instruction.
- Minimize the number of taps required for common actions.
- Use large touch targets, clear visual hierarchy, and readable typography.
- Work well when users are standing and interacting with the display briefly.
- Remain responsive across wall-mounted displays, tablets, phones, and desktop browsers.
- Function as a Progressive Web App.
- Remain suitable for future Android and iOS applications without requiring a major backend redesign.
- Make routine household participation simple and rewarding.

The primary display target is a 24–32-inch landscape capacitive touchscreen connected to a Linux computer and running the application in a fullscreen browser.

---

## Architecture

### Frontend

- React
- TypeScript
- Vite
- Progressive Web App support

### Backend

- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL

### Development and Deployment

- Docker Compose for local development
- Production-ready Docker containers
- Kubernetes manifests suitable for K3s
- GitHub Actions for continuous integration
- GitHub Container Registry for backend container images
- Netlify as the preferred frontend deployment platform
- Netlify deploy previews for pull requests and feature branches where practical

The frontend and backend must remain independently deployable.

---

## Hosting and Portability

Netlify is the preferred hosting platform for the frontend, but it is not part of the core application architecture.

The frontend should remain deployable to other static hosting providers with minimal changes.

The backend should remain deployable to any environment capable of running Docker containers, including Docker Compose, Kubernetes, K3s, or a conventional container hosting platform.

Avoid vendor lock-in.

Do not rely on Netlify-specific APIs, Netlify Functions, Netlify Identity, or other provider-specific services unless explicitly requested.

Hosting-provider-specific behavior should remain isolated to deployment configuration whenever possible.

Changing hosting providers should not require meaningful changes to application business logic.

---

## Data Ownership

Whenever practical, data should remain owned by the external service that created it.

Examples:

- Google Calendar remains the source of truth for calendar events.
- Google Tasks remains the source of truth for personal tasks and reminders.
- Future Home Assistant integrations should treat Home Assistant as the source of truth for smart-home state.

The application should reference, retrieve, or temporarily cache external data rather than duplicate ownership of it.

Locally cached external data must be treated as disposable and replaceable.

Data unique to this application may be stored locally, including:

- Households
- Household members
- User preferences
- Household configuration
- Chore definitions
- Chore assignments
- Chore completions
- Point transactions
- Rewards
- Reward redemptions
- Application-specific roles and permissions

Store stable external identifiers when relationships to external services are required.

---

## Authentication and Integrations

Google OAuth 2.0 should use a secure server-side authorization flow.

The frontend must never receive or store:

- OAuth client secrets
- Refresh tokens
- Database credentials
- Signing keys
- Other privileged credentials

OAuth access and refresh tokens must be handled by the backend.

Future integrations should be implemented behind clear service interfaces so that external providers can be changed or expanded without rewriting unrelated application features.

---

## Security

Never commit:

- Secrets
- Credentials
- Access tokens
- Refresh tokens
- Private keys
- Production connection strings

Configuration must be supplied through environment variables or an appropriate secret-management system.

Do not include secrets in:

- Frontend environment variables
- Browser-delivered JavaScript
- Source control
- Docker images
- `netlify.toml`
- Example configuration files

Example configuration files may contain placeholder values only.

Authentication and authorization must be enforced by the backend. Frontend route protection is not a substitute for backend authorization.

Explain the security implications of authentication, authorization, token-storage, networking, and external-integration changes.

---

## User Experience

The user interface is a primary product feature.

Design every major screen under the assumption that:

- Users may be standing.
- Users may be several feet away from the display.
- Users may interact while walking past.
- Children will use the interface.
- Common actions should take only a few seconds.
- The wall display may remain open for long periods.

Optimize for:

- Clarity
- Speed
- Accessibility
- Readability
- Predictability
- Minimal interaction
- Consistent navigation
- Large touch targets
- Clear success and error feedback

Do not rely on hover interactions for essential functionality.

Support keyboard and screen-reader accessibility where practical, even though touch is the primary interaction method.

Avoid unnecessary visual density, small controls, and deeply nested navigation.

---

## Engineering Principles

Prefer:

- Simple architecture
- Readable code
- Strong typing
- Explicit behavior
- Dependency injection
- Composition over inheritance
- Small, reusable components
- Feature-based organization
- Clear boundaries between frontend, backend, persistence, and external integrations
- Established framework conventions
- Incremental implementation

Avoid:

- Premature optimization
- Unnecessary abstractions
- Global mutable state
- Tight coupling
- Hidden side effects
- Large unrelated changes
- Provider-specific business logic
- Microservices without a demonstrated need
- Event sourcing, CQRS, message brokers, or distributed infrastructure without explicit approval

Apply SOLID principles where they improve clarity and maintainability, not as a reason to introduce unnecessary complexity.

---

## Decision Making

When multiple valid approaches exist:

- Prefer the simpler solution.
- Prefer the more maintainable solution.
- Prefer portable, well-supported technologies.
- Explain meaningful tradeoffs.
- Avoid premature optimization.
- Avoid introducing infrastructure that is not required by current needs.
- Ask for approval before introducing a significant dependency, architectural pattern, or provider-specific service.

Do not silently change the documented architecture.

Record important architectural decisions in documentation when they are likely to affect future development.

---

## Development Workflow

Before implementing a significant feature or architectural change:

1. Inspect the existing repository.
2. Review this file and relevant documentation.
3. Present a concise implementation plan.
4. Identify architectural implications and risks.
5. Identify dependencies or patterns that would be introduced.
6. Describe the files expected to change.
7. Wait for approval when the change introduces meaningful architectural complexity.

Changes should be:

- Focused
- Incremental
- Reviewable
- Tested
- Documented when appropriate

Do not combine unrelated refactoring with feature work unless necessary.

Do not rewrite working areas of the application solely for stylistic consistency.

---

## Testing and Validation

Add or update automated tests when behavior changes.

Prioritize:

- Backend unit tests
- Backend integration tests
- Frontend component tests
- End-to-end tests for important household workflows
- Responsive-layout testing
- Touch-oriented interaction testing
- Authentication and authorization tests

Before declaring work complete:

- Run the relevant frontend build.
- Run the relevant backend build.
- Run applicable automated tests.
- Report any tests or checks that could not be run.
- Summarize the changed files.
- Identify remaining risks or follow-up work.

Do not claim that work is complete when builds or required tests are failing.

---

## Configuration

Use environment variables for runtime configuration.

Keep frontend and backend configuration separate.

Frontend variables are public once included in a production build and must never contain sensitive values.

Public frontend configuration may include values such as:

- API base URL
- Public application name
- Public feature flags
- Google OAuth client ID when appropriate

Sensitive backend configuration may include:

- Database connection strings
- Google OAuth client secrets
- Token-encryption keys
- Signing keys
- Administrative credentials

Provide a `.env.example` or equivalent example configuration containing placeholders and documentation, but no real credentials.

---

## Repository Documentation

Maintain documentation for:

- Local development
- Docker Compose startup
- Database migrations
- Frontend deployment to Netlify
- Backend container builds
- K3s deployment
- Environment variables
- Testing
- Authentication setup
- Major architectural decisions

Maintain a roadmap that separates current work from future ideas.

Suggested roadmap sections:

- Now
- Next
- Later
- Someday

---

## Future Direction

The architecture should allow future modules to be added without major restructuring.

Potential future modules include:

- Grocery lists
- Meal planning
- Family messaging
- Photo displays
- Package tracking
- Weather
- School information
- Smart-home dashboards
- Household maintenance
- Family budgeting
- Screen-time rewards
- Notifications
- Native mobile applications

These are future possibilities, not requirements for the initial milestone. Do not implement speculative modules before they are requested.