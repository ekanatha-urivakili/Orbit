import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { AssigneePicker } from './AssigneePicker'
import type { TenantMembership } from '../api/types'

const member: TenantMembership = {
  id: 'membership-1',
  userId: 'user-1',
  issuer: 'local',
  subject: 'user-1',
  principalType: 'User',
  role: 'Member',
  tier: 'Standard',
  isActive: true,
  createdAt: '2026-08-20T00:00:00Z',
  displayName: 'Ada Lovelace',
  avatarUrl: null,
}

describe('AssigneePicker', () => {
  it('selects a member and prevents duplicate changes while pending', () => {
    const onChange = vi.fn()
    const { rerender } = render(<AssigneePicker members={[member]} onChange={onChange} />)

    fireEvent.click(screen.getByRole('button', { name: 'Change assignee' }))
    fireEvent.click(screen.getByRole('button', { name: 'Ada Lovelace' }))

    expect(onChange).toHaveBeenCalledWith('user-1')

    rerender(<AssigneePicker members={[member]} onChange={onChange} disabled />)
    expect(screen.getByRole('button', { name: 'Change assignee' })).toBeDisabled()
  })
})
