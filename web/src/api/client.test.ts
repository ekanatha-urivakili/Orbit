import { describe, it, expect, vi, afterEach } from 'vitest'
import { ApiError, orbitApi } from './client'

describe('ApiError (§4.5 correlation id join key)', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('carries status and the X-Correlation-Id response header when a request fails', async () => {
    const response = new Response(JSON.stringify({ title: 'Bad request', detail: 'Invalid input' }), {
      status: 400,
      headers: { 'X-Correlation-Id': 'abc-123' },
    })
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response))

    const error = await orbitApi.getChoices().catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(400)
    expect((error as ApiError).correlationId).toBe('abc-123')
    expect((error as ApiError).message).toBe('Invalid input')
  })

  it('leaves correlationId undefined when the response has no header', async () => {
    const response = new Response(JSON.stringify({ title: 'Server error' }), { status: 500 })
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response))

    const error = await orbitApi.getChoices().catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).correlationId).toBeUndefined()
  })
})
