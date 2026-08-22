import { describe, it, expect } from 'vitest'
import { BOARD_LIST_STALE_TIME_MS, BOARD_LIST_GC_TIME_MS } from './App'

describe('board/list-view cache policy (§5.4)', () => {
  it('keeps staleTime within the documented 10-15s range', () => {
    expect(BOARD_LIST_STALE_TIME_MS).toBeGreaterThanOrEqual(10_000)
    expect(BOARD_LIST_STALE_TIME_MS).toBeLessThanOrEqual(15_000)
  })

  it('keeps gcTime within the documented 5-10min range', () => {
    expect(BOARD_LIST_GC_TIME_MS).toBeGreaterThanOrEqual(5 * 60 * 1000)
    expect(BOARD_LIST_GC_TIME_MS).toBeLessThanOrEqual(10 * 60 * 1000)
  })
})
