import { useMutation, useQueryClient } from '@tanstack/react-query'
import { orbitApi } from '../api/client'
import type { PagedResult, UpdateWorkItemInput, WorkItem } from '../api/types'

export function useUpdateWorkItem(projectId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ workItem, input }: { workItem: WorkItem; input: UpdateWorkItemInput }) =>
      orbitApi.updateWorkItem(workItem, input),
    onSuccess: (updated) => {
      queryClient.setQueryData<PagedResult<WorkItem>>(['work-items', projectId], (current) =>
        current && { ...current, items: current.items.map((item) => (item.id === updated.id ? updated : item)) },
      )
    },
  })
}
