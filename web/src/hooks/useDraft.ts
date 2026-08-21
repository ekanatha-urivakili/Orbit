import { useEffect, useRef, useState } from 'react'
import { clearDraft, loadDraft, saveDraft } from '../lib/offlineDrafts'

const SAVE_DEBOUNCE_MS = 800

/**
 * Restores an encrypted offline draft for `draftId` on mount (once), then autosaves `value`
 * back to it on every change after a short debounce. Call the returned `discard` once the
 * real submission succeeds, so a stale draft doesn't resurface on the next visit.
 */
export function useDraft<T>(
  draftId: string,
  value: T,
  setValue: (value: T) => void,
  isBlank: (value: T) => boolean,
) {
  const [ready, setReady] = useState(false)
  const setValueRef = useRef(setValue)
  setValueRef.current = setValue

  useEffect(() => {
    let cancelled = false
    setReady(false)
    loadDraft<T>(draftId).then((restored) => {
      if (cancelled) return
      if (restored !== null && !isBlank(restored)) {
        setValueRef.current(restored)
      }
      setReady(true)
    })
    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draftId])

  useEffect(() => {
    if (!ready) return
    const timeout = setTimeout(() => {
      if (isBlank(value)) {
        void clearDraft(draftId)
      } else {
        void saveDraft(draftId, value)
      }
    }, SAVE_DEBOUNCE_MS)
    return () => clearTimeout(timeout)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draftId, value, ready])

  const discard = () => void clearDraft(draftId)

  return { discard }
}
