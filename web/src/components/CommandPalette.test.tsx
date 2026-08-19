import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent, act } from '@testing-library/react'
import { CommandPalette } from './CommandPalette'
import type { Project, WorkItem } from '../api/types'

const project: Project = { id: 'project-1', key: 'ORB', name: 'Orbit Platform' } as Project

function buildWorkItem(): WorkItem {
  return {
    id: 'item-1',
    projectId: 'project-1',
    key: 'ORB-1',
    summary: 'Fix the login bug',
    description: null,
    parentId: null,
    epicName: null,
    acceptanceCriteria: null,
    stepsToConduct: null,
    assigneeUserId: null,
    developerUserId: null,
    productOwnerUserId: null,
    sprintName: null,
    identifiedOn: null,
    storyPoints: null,
    labels: [],
    countries: [],
    attachmentNames: [],
    type: 'Bug',
    status: 'Backlog',
    priority: 'Medium',
    rank: 1024,
    isFlagged: false,
    coverAttachmentId: null,
    isArchived: false,
    archivedAt: null,
    version: 1,
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  }
}

describe('CommandPalette', () => {
  it('opens on Cmd+K, filters results, and runs the selected command', () => {
    const onOpenWorkItem = vi.fn()
    render(
      <CommandPalette
        projects={[project]}
        workItems={[buildWorkItem()]}
        hasSelectedProject={false}
        onNavigateToProject={vi.fn()}
        onOpenWorkItem={onOpenWorkItem}
        onNavigateTab={vi.fn()}
        onOpenSettings={vi.fn()}
      />,
    )

    expect(screen.queryByPlaceholderText(/Search projects/)).not.toBeInTheDocument()

    fireEvent.keyDown(window, { key: 'k', metaKey: true })
    expect(screen.getByPlaceholderText(/Search projects/)).toBeInTheDocument()

    fireEvent.change(screen.getByPlaceholderText(/Search projects/), { target: { value: 'login bug' } })
    fireEvent.click(screen.getByText('Fix the login bug'))

    expect(onOpenWorkItem).toHaveBeenCalledWith(expect.objectContaining({ key: 'ORB-1' }))
    expect(screen.queryByPlaceholderText(/Search projects/)).not.toBeInTheDocument()
  })

  it('opens via the orbit:open-command-palette custom event', () => {
    render(
      <CommandPalette
        projects={[]}
        workItems={[]}
        hasSelectedProject={false}
        onNavigateToProject={vi.fn()}
        onOpenWorkItem={vi.fn()}
        onNavigateTab={vi.fn()}
        onOpenSettings={vi.fn()}
      />,
    )

    act(() => {
      window.dispatchEvent(new CustomEvent('orbit:open-command-palette'))
    })

    expect(screen.getByPlaceholderText(/Search projects/)).toBeInTheDocument()
  })
})
