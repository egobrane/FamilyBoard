import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import {
  ApiError,
  createHousehold as createHouseholdRequest,
  getCurrentUser,
  logout as logoutRequest,
  selectHousehold as persistHouseholdSelection,
  type CreateHouseholdRequest,
  type CurrentUser,
} from '../../lib/api'

export type AuthenticationState =
  | { status: 'loading' }
  | { status: 'signedOut' }
  | { status: 'accountUnavailable' }
  | { status: 'unavailable'; message: string }
  | { status: 'authenticated'; currentUser: CurrentUser }

interface AuthenticationContextValue {
  state: AuthenticationState
  isMutating: boolean
  refresh: () => Promise<void>
  refreshSilently: () => Promise<void>
  createHousehold: (request: CreateHouseholdRequest) => Promise<void>
  selectHousehold: (householdId: string) => Promise<void>
  logout: () => Promise<void>
}

const AuthenticationContext = createContext<AuthenticationContextValue | null>(null)

function normalizedCurrentUser(currentUser: CurrentUser): CurrentUser {
  const selectedIsActive = currentUser.households.some(
    (household) => household.id === currentUser.selectedHouseholdId,
  )
  return selectedIsActive
    ? currentUser
    : { ...currentUser, selectedHouseholdId: null }
}

function stateForError(error: unknown): AuthenticationState {
  if (error instanceof ApiError) {
    if (error.problem.code === 'authentication_required') {
      return { status: 'signedOut' }
    }
    if (error.problem.code === 'account_unavailable') {
      return { status: 'accountUnavailable' }
    }
  }

  return {
    status: 'unavailable',
    message: 'Family Dashboard could not connect. Check your connection and try again.',
  }
}

export function AuthenticationProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthenticationState>({ status: 'loading' })
  const [isMutating, setIsMutating] = useState(false)

  const load = useCallback(async (showLoading: boolean) => {
    if (showLoading) {
      setState({ status: 'loading' })
    }

    try {
      let currentUser = normalizedCurrentUser(await getCurrentUser())
      if (currentUser.households.length === 1 && currentUser.selectedHouseholdId === null) {
        const householdId = currentUser.households[0].id
        await persistHouseholdSelection(householdId)
        currentUser = { ...currentUser, selectedHouseholdId: householdId }
      }
      setState({ status: 'authenticated', currentUser })
    } catch (error) {
      setState(stateForError(error))
    }
  }, [])

  useEffect(() => {
    const timer = window.setTimeout(() => void load(false), 0)
    return () => window.clearTimeout(timer)
  }, [load])

  const mutate = useCallback(async (operation: () => Promise<void>) => {
    setIsMutating(true)
    try {
      await operation()
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setState(stateForError(error))
      }
      throw error
    } finally {
      setIsMutating(false)
    }
  }, [])

  const createHousehold = useCallback(async (request: CreateHouseholdRequest) => {
    await mutate(async () => {
      await createHouseholdRequest(request)
      await load(false)
    })
  }, [load, mutate])

  const selectHousehold = useCallback(async (householdId: string) => {
    await mutate(async () => {
      await persistHouseholdSelection(householdId)
      setState((current) => current.status === 'authenticated'
        ? {
            status: 'authenticated',
            currentUser: { ...current.currentUser, selectedHouseholdId: householdId },
          }
        : current)
    })
  }, [mutate])

  const logout = useCallback(async () => {
    await mutate(async () => {
      await logoutRequest()
      setState({ status: 'signedOut' })
    })
  }, [mutate])

  const value = useMemo<AuthenticationContextValue>(() => ({
    state,
    isMutating,
    refresh: () => load(true),
    refreshSilently: () => load(false),
    createHousehold,
    selectHousehold,
    logout,
  }), [createHousehold, isMutating, load, logout, selectHousehold, state])

  return (
    <AuthenticationContext.Provider value={value}>
      {children}
    </AuthenticationContext.Provider>
  )
}

// This hook intentionally shares the provider module's private context.
// eslint-disable-next-line react-refresh/only-export-components
export function useAuthentication() {
  const context = useContext(AuthenticationContext)
  if (context === null) {
    throw new Error('useAuthentication must be used within AuthenticationProvider.')
  }
  return context
}
