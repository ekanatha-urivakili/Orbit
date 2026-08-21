import 'fake-indexeddb/auto'
import { describe, expect, it, vi } from 'vitest'
import { act, renderHook, waitFor } from '@testing-library/react'
import { useDraft } from './useDraft'
import { loadDraft, saveDraft } from '../lib/offlineDrafts'

const isBlank = (value: string) => value.trim().length === 0

describe('useDraft', () => {
  it('restores a previously saved draft on mount', async () => {
    const id = 'use-draft:restore'
    await saveDraft(id, 'restored text')
    const setValue = vi.fn()

    renderHook(() => useDraft(id, '', setValue, isBlank))

    await waitFor(() => expect(setValue).toHaveBeenCalledWith('restored text'))
  })

  it('does not call setValue when no draft exists', async () => {
    const setValue = vi.fn()

    const { result } = renderHook(() => useDraft('use-draft:none', '', setValue, isBlank))

    await waitFor(() => expect(result.current).toBeDefined())
    expect(setValue).not.toHaveBeenCalled()
  })

  it('autosaves the current value after the debounce window', async () => {
    const id = 'use-draft:autosave'
    const setValue = vi.fn()

    const { rerender } = renderHook(({ value }) => useDraft(id, value, setValue, isBlank), {
      initialProps: { value: '' },
    })
    rerender({ value: 'draft in progress' })

    await waitFor(async () => expect(await loadDraft(id)).toBe('draft in progress'), { timeout: 2000 })
  })

  it('discard clears the saved draft', async () => {
    const id = 'use-draft:discard'
    await saveDraft(id, 'to be discarded')
    const setValue = vi.fn()

    const { result } = renderHook(() => useDraft(id, 'to be discarded', setValue, isBlank))
    await waitFor(() => expect(setValue).toHaveBeenCalled())

    act(() => result.current.discard())

    await waitFor(async () => expect(await loadDraft(id)).toBeNull())
  })
})
