import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { PwaUpdateProvider } from './PwaUpdateProvider'

const serviceWorker = vi.hoisted(() => ({
  apply: vi.fn(async () => undefined),
  options: undefined as { onNeedRefresh?: () => void } | undefined,
}))

vi.mock('virtual:pwa-register', () => ({
  registerSW: vi.fn((options: { onNeedRefresh?: () => void }) => {
    serviceWorker.options = options
    return serviceWorker.apply
  }),
}))

describe('PWA update experience', () => {
  beforeEach(() => {
    serviceWorker.apply.mockClear()
    serviceWorker.options = undefined
  })

  afterEach(() => vi.restoreAllMocks())

  it('shows a reliable update prompt and applies the waiting worker', async () => {
    const user = userEvent.setup()
    render(<PwaUpdateProvider><main>Dashboard</main></PwaUpdateProvider>)

    act(() => serviceWorker.options?.onNeedRefresh?.())
    expect(await screen.findByRole('status')).toHaveTextContent('A fresh version is ready.')

    await user.click(screen.getByRole('button', { name: 'Update now' }))
    expect(serviceWorker.apply).toHaveBeenCalledWith(true)
  })

  it('does not activate an update while a form is mounted', async () => {
    render(
      <PwaUpdateProvider>
        <form><label>Event title<input /></label></form>
      </PwaUpdateProvider>,
    )

    act(() => serviceWorker.options?.onNeedRefresh?.())
    expect(await screen.findByText(/Finish or leave this form/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Update now' })).toBeDisabled()
    expect(serviceWorker.apply).not.toHaveBeenCalled()
  })

  it('retains the current release while the display is offline', async () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    render(<PwaUpdateProvider><main>Cached dashboard</main></PwaUpdateProvider>)

    act(() => serviceWorker.options?.onNeedRefresh?.())
    expect(await screen.findByText(/available when this display is online/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Update now' })).toBeDisabled()
    expect(screen.getByText('Cached dashboard')).toBeInTheDocument()
  })
})
