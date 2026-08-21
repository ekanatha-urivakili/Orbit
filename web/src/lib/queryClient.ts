import { QueryClient } from '@tanstack/react-query'

// §5.4: explicit defaults rather than the library's implicit staleTime: 0 - "everything else"
// class in the cache policy table. Per-query overrides (reference data at Infinity, board/list
// views at a short staleTime) live at their useQuery call sites in App.tsx.
export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: 1, refetchOnWindowFocus: false, staleTime: 0, gcTime: 5 * 60 * 1000 },
      mutations: { retry: false },
    },
  })
}
