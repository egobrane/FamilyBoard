import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { App } from '../../app/App'
import { AuthenticationProvider } from '../authentication/AuthenticationContext'
import { DashboardTasksCard } from './DashboardTasksCard'

const householdId = '20000000-0000-0000-0000-000000000001'
const connectionId = '40000000-0000-0000-0000-000000000001'
const response = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
const currentUser = { user: { id: 'user-1', displayName: 'Ryan', primaryEmail: 'ryan@example.test' },
  households: [{ id: householdId, name: 'Family', memberId: 'member-1', role: 'adult' }], selectedHouseholdId: householdId,
  session: { expiresAt: '2026-09-01T00:00:00Z', isSharedDisplay: false, deviceLabel: null,
    administrativeElevationHouseholdId: null, administrativeElevationExpiresAt: null } }

afterEach(() => vi.unstubAllGlobals())

describe('Google Tasks', () => {
  it('renders provider-owned tasks with date-only due information', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return response(currentUser)
      if (path.endsWith('/tasks')) return response({ tasks: [{ id: 'task-1', sourceId: 'source-1', taskListName: 'Family',
        title: 'Pack lunch', notes: null, status: 'needsAction', dueDate: '2026-08-27', completedAt: null,
        parentTaskId: null, position: '1', isSubtask: false, isAssigned: false }], nextCursor: null, isStale: false, warnings: [] })
      throw new Error(`Unexpected request: ${path}`)
    }))
    render(<MemoryRouter initialEntries={['/tasks']}><AuthenticationProvider><App /></AuthenticationProvider></MemoryRouter>)
    expect(await screen.findByRole('heading', { name: 'Tasks' })).toBeInTheDocument()
    expect(await screen.findByText('Pack lunch')).toBeInTheDocument()
    expect(screen.getByText(/Due Aug 27/)).toBeInTheDocument()
  })

  it('saves household-visible task lists with antiforgery', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return response(currentUser)
      if (path === '/api/auth/antiforgery') return response({ requestToken: 'csrf', headerName: 'X-CSRF-TOKEN' })
      if (path.endsWith('/tasks/connection')) return response({ isAvailable: true, connectionId, status: 'active', providerEmail: 'tasks@example.test', connectedAt: '2026-08-26T00:00:00Z', activeSourceCount: 0, activeHouseholdCount: 0 })
      if (path.endsWith('/tasks/provider-task-lists')) return response([{ id: 'list-1', name: 'Family tasks', isSelected: false }])
      if (path.endsWith('/tasks/sources') && init?.method === 'PUT') return response([{ id: 'source-1', connectionId, externalTaskListId: 'list-1', name: 'Family tasks', isActive: true, isOwnedByCurrentAdult: true }])
      if (path.endsWith('/tasks/sources')) return response([])
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    render(<MemoryRouter initialEntries={[`/households/${householdId}/tasks`]}><AuthenticationProvider><App /></AuthenticationProvider></MemoryRouter>)
    await userEvent.click(await screen.findByRole('checkbox', { name: /Family tasks/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Save visible task lists' }))
    expect(await screen.findByText('Visible Google task lists saved.')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/tasks/sources'), expect.objectContaining({
      method: 'PUT', credentials: 'include', headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }),
    }))
  })

  it('creates a task with date-only input and antiforgery', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return response(currentUser)
      if (path === '/api/auth/antiforgery') return response({ requestToken: 'csrf', headerName: 'X-CSRF-TOKEN' })
      if (path.endsWith('/tasks') && init?.method === 'POST') return response({ operation: 'create', taskId: 'created',
        sourceId: 'source-1', status: 'needsAction', dueDate: '2026-08-30', mutationVersion: 'version',
        attributedMemberId: 'member-1', recoveredExistingMutation: false })
      if (path.endsWith('/tasks')) return response({ tasks: [], nextCursor: null, isStale: false, warnings: [], canCreateTasks: true })
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    render(<MemoryRouter initialEntries={['/tasks/new']}><AuthenticationProvider><App /></AuthenticationProvider></MemoryRouter>)
    await userEvent.type(await screen.findByLabelText('Task title'), 'Pack lunch')
    await userEvent.type(screen.getByLabelText(/Due date/), '2026-08-30')
    await userEvent.click(screen.getByRole('button', { name: 'Add task' }))
    expect(await screen.findByText('Task added to Google Tasks.')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/tasks'), expect.objectContaining({
      method: 'POST', credentials: 'include', headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }),
    }))
  })

  it('creates a shared-display task without loading or submitting member attribution', async () => {
    const sharedUser = { ...currentUser, session: { ...currentUser.session, isSharedDisplay: true } }
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return response(sharedUser)
      if (path === '/api/auth/antiforgery') return response({ requestToken: 'csrf', headerName: 'X-CSRF-TOKEN' })
      if (path.endsWith('/tasks') && init?.method === 'POST') return response({ operation: 'create', taskId: 'created',
        sourceId: 'source-1', status: 'needsAction', dueDate: null, mutationVersion: 'version',
        attributedMemberId: null, recoveredExistingMutation: false })
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    render(<MemoryRouter initialEntries={['/tasks/new']}><AuthenticationProvider><App /></AuthenticationProvider></MemoryRouter>)

    await userEvent.type(await screen.findByLabelText('Task title'), 'Shared grocery reminder')
    expect(screen.queryByText('Who is adding this task?')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Add task' }))

    expect(await screen.findByText('Task added to Google Tasks.')).toBeInTheDocument()
    const createCall = fetchMock.mock.calls.find(([input, init]) =>
      new URL(String(input)).pathname.endsWith('/tasks') && init?.method === 'POST')
    expect(createCall).toBeDefined()
    expect(JSON.parse(String(createCall?.[1]?.body))).not.toHaveProperty('attributedMemberId')
    expect(fetchMock.mock.calls.some(([input]) => new URL(String(input)).pathname.includes('/members'))).toBe(false)
  })

  it('lets a shared display complete a task from its circle without member attribution', async () => {
    const sharedUser = { ...currentUser, session: { ...currentUser.session, isSharedDisplay: true } }
    const fetchMock = vi.fn(async (input: RequestInfo | URL, _init?: RequestInit) => {
      void _init
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return response(sharedUser)
      if (path === '/api/auth/antiforgery') return response({ requestToken: 'csrf', headerName: 'X-CSRF-TOKEN' })
      if (path.endsWith('/tasks/status')) return response({ operation: 'complete', taskId: 'task-1',
        sourceId: 'source-1', status: 'completed', dueDate: null, mutationVersion: 'version-2',
        attributedMemberId: null, recoveredExistingMutation: false })
      if (path.endsWith('/tasks')) return response({ tasks: [{ id: 'task-1', sourceId: 'source-1',
        taskListName: 'Family', title: 'Pack lunch', notes: null, status: 'needsAction', dueDate: null,
        completedAt: null, parentTaskId: null, position: '1', isSubtask: false, isAssigned: false,
        canChangeStatus: true, mutationVersion: 'version-1' }], nextCursor: null, isStale: false,
        warnings: [], canCreateTasks: true })
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    render(<MemoryRouter initialEntries={['/tasks']}><AuthenticationProvider><App /></AuthenticationProvider></MemoryRouter>)

    expect(screen.queryByText('Who is using the board?')).not.toBeInTheDocument()
    await userEvent.click(await screen.findByRole('button', { name: 'Complete Pack lunch' }))
    expect(await screen.findByText('Task completed in Google Tasks.')).toBeInTheDocument()
    const statusCall = fetchMock.mock.calls.find(([input]) => new URL(String(input)).pathname.endsWith('/tasks/status'))
    expect(statusCall).toBeDefined()
    expect(JSON.parse(String(statusCall?.[1]?.body))).toEqual(expect.objectContaining({
      sourceId: 'source-1', taskId: 'task-1', targetStatus: 'completed', mutationVersion: 'version-1',
    }))
    expect(JSON.parse(String(statusCall?.[1]?.body))).not.toHaveProperty('attributedMemberId')
  })

  it('completes a shared task directly from the Home dashboard', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return response(currentUser)
      if (path === '/api/auth/antiforgery') return response({ requestToken: 'csrf', headerName: 'X-CSRF-TOKEN' })
      if (path.endsWith('/tasks/status')) return response({ operation: 'complete', taskId: 'task-1',
        sourceId: 'source-1', status: 'completed', dueDate: null, mutationVersion: 'version-2',
        attributedMemberId: null, recoveredExistingMutation: false })
      if (path.endsWith('/tasks')) return response({ tasks: [{ id: 'task-1', sourceId: 'source-1',
        taskListName: 'Family', title: 'Take out recycling', notes: null, status: 'needsAction', dueDate: null,
        completedAt: null, parentTaskId: null, position: '1', isSubtask: false, isAssigned: false,
        canChangeStatus: true, mutationVersion: 'version-1' }], nextCursor: null, isStale: false,
        warnings: [], canCreateTasks: true })
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    render(<MemoryRouter><AuthenticationProvider><DashboardTasksCard /></AuthenticationProvider></MemoryRouter>)

    await userEvent.click(await screen.findByRole('button', { name: 'Complete Take out recycling' }))
    expect(await screen.findByText('Task completed.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([input]) => new URL(String(input)).pathname.endsWith('/tasks/status'))).toBe(true)
  })
})
