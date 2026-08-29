import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { describe, expect, it } from 'vitest'
import { WorkspaceLayout } from './WorkspaceLayout'

function TestPage({ name, interactive = false }: { name: string; interactive?: boolean }) {
  return (
    <main id="main-content" tabIndex={-1}>
      <h1>{name}</h1>
      {interactive && <button type="button">Keep this action safe</button>}
    </main>
  )
}

function renderWorkspace(path = '/') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route element={<WorkspaceLayout />}>
          <Route element={<TestPage name="Home view" />} path="/" />
          <Route element={<TestPage name="Calendar view" />} path="/calendar" />
          <Route element={<TestPage interactive name="Tasks view" />} path="/tasks" />
          <Route element={<TestPage name="Chores view" />} path="/chores" />
          <Route element={<TestPage name="Rewards view" />} path="/rewards" />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('WorkspaceLayout', () => {
  it('keeps real links and focuses the destination after tab navigation', async () => {
    const user = userEvent.setup()
    renderWorkspace()

    await user.click(screen.getByRole('link', { name: 'Tasks' }))

    const heading = await screen.findByRole('heading', { name: 'Tasks view' })
    expect(screen.getByRole('link', { name: 'Tasks' })).toHaveAttribute('aria-current', 'page')
    await waitFor(() => expect(heading.closest('main')).toHaveFocus())
  })

  it('moves to the adjacent route after a committed horizontal drag', async () => {
    renderWorkspace()
    const viewport = document.querySelector<HTMLElement>('.workspace-viewport')!

    fireEvent.pointerDown(viewport, { button: 0, clientX: 500, clientY: 100, isPrimary: true, pointerId: 1 })
    fireEvent.pointerMove(viewport, { clientX: 300, clientY: 104, isPrimary: true, pointerId: 1 })
    fireEvent.pointerUp(viewport, { clientX: 300, clientY: 104, isPrimary: true, pointerId: 1 })

    expect(await screen.findByRole('heading', { name: 'Calendar view' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Calendar' })).toHaveAttribute('aria-current', 'page')
  })

  it('snaps back when a horizontal drag does not cross the navigation threshold', () => {
    renderWorkspace('/calendar')
    const viewport = document.querySelector<HTMLElement>('.workspace-viewport')!

    fireEvent.pointerDown(viewport, { button: 0, clientX: 300, clientY: 100, isPrimary: true, pointerId: 1 })
    fireEvent.pointerMove(viewport, { clientX: 275, clientY: 102, isPrimary: true, pointerId: 1 })
    fireEvent.pointerUp(viewport, { clientX: 275, clientY: 102, isPrimary: true, pointerId: 1 })

    expect(screen.getByRole('heading', { name: 'Calendar view' })).toBeInTheDocument()
    expect(viewport).not.toHaveAttribute('data-dragging')
  })

  it('does not capture vertical gestures or gestures that begin on controls', () => {
    renderWorkspace('/tasks')
    const viewport = document.querySelector<HTMLElement>('.workspace-viewport')!

    fireEvent.pointerDown(viewport, { button: 0, clientX: 300, clientY: 100, isPrimary: true, pointerId: 1 })
    fireEvent.pointerMove(viewport, { clientX: 290, clientY: 250, isPrimary: true, pointerId: 1 })
    fireEvent.pointerUp(viewport, { clientX: 290, clientY: 250, isPrimary: true, pointerId: 1 })

    const button = screen.getByRole('button', { name: 'Keep this action safe' })
    fireEvent.pointerDown(button, { button: 0, clientX: 300, clientY: 100, isPrimary: true, pointerId: 2 })
    fireEvent.pointerMove(viewport, { clientX: 100, clientY: 100, isPrimary: true, pointerId: 2 })
    fireEvent.pointerUp(viewport, { clientX: 100, clientY: 100, isPrimary: true, pointerId: 2 })

    expect(screen.getByRole('heading', { name: 'Tasks view' })).toBeInTheDocument()
  })
})
