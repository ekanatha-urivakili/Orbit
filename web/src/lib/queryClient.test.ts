import { describe, it, expect } from 'vitest'
import { createQueryClient } from './queryClient'

describe('createQueryClient', () => {
  it('sets the §5.4 "everything else" defaults explicitly', () => {
    const client = createQueryClient()
    const defaults = client.getDefaultOptions()

    expect(defaults.queries?.staleTime).toBe(0)
    expect(defaults.queries?.gcTime).toBe(5 * 60 * 1000)
    expect(defaults.queries?.retry).toBe(1)
    expect(defaults.queries?.refetchOnWindowFocus).toBe(false)
    expect(defaults.mutations?.retry).toBe(false)
  })
})
