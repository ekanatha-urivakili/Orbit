import { useMutation, useQueryClient } from '@tanstack/react-query'
import { orbitApi } from '../api/client'

export function useCreateWorkItem(projectId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: orbitApi.createWorkItem,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['work-items', projectId] }),
  })
}
