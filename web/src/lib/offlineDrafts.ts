// Encrypted offline drafts (ORBIT-WORK-MANAGEMENT-ARCHITECTURE.md §13.5 PWA row): autosaves
// in-progress comment/work-item-creation text to IndexedDB so a reload, crash, or lost
// connection doesn't lose what the user typed. Encrypted at rest with a non-extractable
// AES-GCM key generated on first use and stored as a structured-cloned CryptoKey object
// (IndexedDB supports this natively) - this protects against casual inspection of the
// IndexedDB file on disk or in a backup, not against an attacker with full access to an
// unlocked device, since the key lives in the same origin-scoped store as the ciphertext.
// Degrades to a silent no-op wherever IndexedDB or SubtleCrypto isn't available (older
// browsers, private-browsing modes that disable storage, and the jsdom test environment).

const DB_NAME = 'orbit-offline-drafts'
const DB_VERSION = 1
const KEY_STORE = 'keys'
const DRAFT_STORE = 'drafts'
const DRAFT_KEY_ID = 'draft-key'

interface StoredDraft {
  id: string
  iv: Uint8Array
  ciphertext: ArrayBuffer
  updatedAt: number
}

function isSupported(): boolean {
  return typeof indexedDB !== 'undefined' && typeof crypto?.subtle !== 'undefined'
}

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION)
    request.onupgradeneeded = () => {
      const db = request.result
      if (!db.objectStoreNames.contains(KEY_STORE)) db.createObjectStore(KEY_STORE)
      if (!db.objectStoreNames.contains(DRAFT_STORE)) db.createObjectStore(DRAFT_STORE, { keyPath: 'id' })
    }
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error)
  })
}

function requestToPromise<T>(request: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error)
  })
}

async function getOrCreateKey(db: IDBDatabase): Promise<CryptoKey> {
  const readTx = db.transaction(KEY_STORE, 'readonly')
  const existing = await requestToPromise(readTx.objectStore(KEY_STORE).get(DRAFT_KEY_ID))
  if (existing) return existing as CryptoKey

  const key = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
  const writeTx = db.transaction(KEY_STORE, 'readwrite')
  writeTx.objectStore(KEY_STORE).put(key, DRAFT_KEY_ID)
  await new Promise<void>((resolve, reject) => {
    writeTx.oncomplete = () => resolve()
    writeTx.onerror = () => reject(writeTx.error)
  })
  return key
}

export async function saveDraft<T>(id: string, value: T): Promise<void> {
  if (!isSupported()) return
  try {
    const db = await openDb()
    const key = await getOrCreateKey(db)
    const iv = crypto.getRandomValues(new Uint8Array(12))
    const plaintext = new TextEncoder().encode(JSON.stringify(value))
    const ciphertext = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, key, plaintext)

    const tx = db.transaction(DRAFT_STORE, 'readwrite')
    tx.objectStore(DRAFT_STORE).put({ id, iv, ciphertext, updatedAt: Date.now() } satisfies StoredDraft)
    await new Promise<void>((resolve, reject) => {
      tx.oncomplete = () => resolve()
      tx.onerror = () => reject(tx.error)
    })
  } catch {
    // Best-effort: a draft that fails to save is no worse than no draft at all.
  }
}

export async function loadDraft<T>(id: string): Promise<T | null> {
  if (!isSupported()) return null
  try {
    const db = await openDb()
    const key = await getOrCreateKey(db)
    const tx = db.transaction(DRAFT_STORE, 'readonly')
    const stored = (await requestToPromise(tx.objectStore(DRAFT_STORE).get(id))) as StoredDraft | undefined
    if (!stored) return null

    const plaintext = await crypto.subtle.decrypt(
      { name: 'AES-GCM', iv: stored.iv as BufferSource },
      key,
      stored.ciphertext,
    )
    return JSON.parse(new TextDecoder().decode(plaintext)) as T
  } catch {
    return null
  }
}

export async function clearDraft(id: string): Promise<void> {
  if (!isSupported()) return
  try {
    const db = await openDb()
    const tx = db.transaction(DRAFT_STORE, 'readwrite')
    tx.objectStore(DRAFT_STORE).delete(id)
    await new Promise<void>((resolve, reject) => {
      tx.oncomplete = () => resolve()
      tx.onerror = () => reject(tx.error)
    })
  } catch {
    // Best-effort.
  }
}
