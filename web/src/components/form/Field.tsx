import type { ReactNode } from 'react'

function renderLabel(label: string) {
  if (!label.endsWith(' *')) return label
  return <>{label.slice(0, -2)} <span className="required-asterisk">*</span></>
}

export function Field({
  label,
  children,
  variant = 'plain',
}: {
  label: string
  children: ReactNode
  variant?: 'plain' | 'panel'
}) {
  if (variant === 'panel') {
    return (
      <label className="block text-sm font-medium text-gray-700">
        <span className="mb-1.5 block">{renderLabel(label)}</span>
        <span className="settings-control block">{children}</span>
      </label>
    )
  }
  return <label>{renderLabel(label)}{children}</label>
}

export function Hint({ children, variant = 'plain' }: { children: ReactNode; variant?: 'plain' | 'panel' }) {
  return (
    <span className={variant === 'panel' ? 'mt-1 block text-xs font-normal text-gray-500' : 'text-xs font-normal text-gray-500'}>
      {children}
    </span>
  )
}

export interface MutationShape {
  isPending: boolean
  isError: boolean
  isSuccess: boolean
  error: Error | null
}

export function SubmitRow({ mutation }: { mutation: MutationShape }) {
  return (
    <div className="flex flex-wrap items-center justify-end gap-3 pt-2">
      {mutation.isError && <span className="mr-auto text-sm text-red-700">{mutation.error?.message}</span>}
      {mutation.isSuccess && <span className="mr-auto text-sm text-green-700">Saved</span>}
      <button
        type="submit"
        disabled={mutation.isPending}
        className="rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-60"
      >
        {mutation.isPending ? 'Saving…' : 'Save changes'}
      </button>
    </div>
  )
}
