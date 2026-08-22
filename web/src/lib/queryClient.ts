import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query'

// §5.4: explicit defaults rather than the library's implicit staleTime: 0 - "everything else"
// class in the cache policy table. Per-query overrides (reference data at Infinity, board/list
// views at a short staleTime) live at their useQuery call sites in App.tsx.
//
// onError (§4.5) is a plain callback rather than a hardcoded Sentry import, so this stays
// vendor-agnostic and testable without mocking an error-tracking SDK; main.tsx wires it to Sentry.
export function createQueryClient(onError: (error: unknown) => void = () => {}) {
  return new QueryClient({
    queryCache: new QueryCache({ onError }),
    mutationCache: new MutationCache({ onError }),
    defaultOptions: {
      queries: { retry: 1, refetchOnWindowFocus: false, staleTime: 0, gcTime: 5 * 60 * 1000 },
      mutations: { retry: false },
    },
  })
}
