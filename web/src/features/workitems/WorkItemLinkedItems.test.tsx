import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { WorkItemLinkedItems } from './WorkItemLinkedItems'
import type { WorkItem } from '../../api/types'

vi.mock('../../api/client', () => ({
  orbitApi: {
    listWorkItemLinks: vi.fn().mockResolvedValue([
      {
        id: 'link-1',
        workItemId: 'other-1',
        key: 'ORB-9',
        summary: 'Related bug',
        type: 'Bug',
        status: 'Backlog',
        kind: 'RelatesTo',
        direction: 'Outgoing',
      },
    ]),
    addWorkItemLink: vi.fn(),
    removeWorkItemLink: vi.fn(),
  },
}))

describe('WorkItemLinkedItems', () => {
  it('renders each linked item title as a new-tab link to /browse/<key>', async () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const workItems: WorkItem[] = []

    render(
      <QueryClientProvider client={queryClient}>
        <WorkItemLinkedItems workItemId="item-1" workItems={workItems} />
      </QueryClientProvider>,
    )

    const anchor = await screen.findByText('Related bug')
    expect(anchor.closest('a')).toHaveAttribute('href', '/browse/ORB-9')
    expect(anchor.closest('a')).toHaveAttribute('target', '_blank')
    expect(anchor.closest('a')).toHaveAttribute('rel', 'noopener noreferrer')
  })
})
