import { describe, expect, it } from 'vitest'
import { nextRolloverName } from './sprintNaming'

describe('nextRolloverName', () => {
  it('increments a trailing number', () => {
    expect(nextRolloverName('Sprint 5', new Date('2026-01-01'))).toBe('Sprint 6')
  })

  it('preserves zero-padding width', () => {
    expect(nextRolloverName('Sprint 05', new Date('2026-01-01'))).toBe('Sprint 06')
  })

  it('rolls over multi-digit numbers', () => {
    expect(nextRolloverName('Sprint 9', new Date('2026-01-01'))).toBe('Sprint 10')
  })

  it('falls back to a date-based name when there is no trailing number', () => {
    expect(nextRolloverName('Alpha Release', new Date('2026-01-01'))).toBe('Sprint 2026-01-01')
  })
})
