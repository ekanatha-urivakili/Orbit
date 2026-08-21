import 'fake-indexeddb/auto'
import { describe, expect, it } from 'vitest'
import { clearDraft, loadDraft, saveDraft } from './offlineDrafts'

describe('offlineDrafts', () => {
  it('round-trips a saved draft through encryption', async () => {
    const id = 'test:round-trip'
    await saveDraft(id, { summary: 'Fix the flaky test', description: '<p>Details</p>' })

    const restored = await loadDraft<{ summary: string; description: string }>(id)

    expect(restored).toEqual({ summary: 'Fix the flaky test', description: '<p>Details</p>' })
  })

  it('returns null for a draft that was never saved', async () => {
    const restored = await loadDraft('test:never-saved')

    expect(restored).toBeNull()
  })

  it('returns null after a draft is cleared', async () => {
    const id = 'test:cleared'
    await saveDraft(id, 'some text')

    await clearDraft(id)

    expect(await loadDraft(id)).toBeNull()
  })

  it('overwrites a previous draft under the same id', async () => {
    const id = 'test:overwrite'
    await saveDraft(id, 'first draft')
    await saveDraft(id, 'second draft')

    expect(await loadDraft(id)).toBe('second draft')
  })

  it('stores ciphertext, not the plaintext value, at rest', async () => {
    const id = 'test:encrypted-at-rest'
    const secret = 'this text must not appear in plain form in storage'
    await saveDraft(id, secret)

    const db = await new Promise<IDBDatabase>((resolve, reject) => {
      const request = indexedDB.open('orbit-offline-drafts')
      request.onsuccess = () => resolve(request.result)
      request.onerror = () => reject(request.error)
    })
    const stored = await new Promise<{ ciphertext: ArrayBuffer }>((resolve, reject) => {
      const request = db.transaction('drafts', 'readonly').objectStore('drafts').get(id)
      request.onsuccess = () => resolve(request.result)
      request.onerror = () => reject(request.error)
    })

    const rawBytes = new TextDecoder().decode(stored.ciphertext)
    expect(rawBytes).not.toContain(secret)
  })
})
