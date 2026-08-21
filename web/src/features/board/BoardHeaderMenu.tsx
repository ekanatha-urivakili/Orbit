import { useState } from 'react'
import { MoreHorizontal } from 'lucide-react'

export function BoardHeaderMenu({ items }: { items: { label: string; onClick: () => void }[] }) {
  const [open, setOpen] = useState(false)

  return (
    <div className="relative inline-flex items-center">
      <button
        type="button"
        onClick={() => setOpen((current) => !current)}
        className="flex items-center justify-center p-2 rounded-md border border-gray-200 dark:border-[#394047] text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-[#2c333a] transition-colors"
        title="Board settings"
        aria-label="Board settings"
      >
        <MoreHorizontal size={16} />
      </button>

      {open && (
        <div className="absolute right-0 top-full mt-1.5 w-56 bg-white dark:bg-[#1d2125] border border-[#dfe1e6] dark:border-[#394047] shadow-2xl rounded-xl py-1.5 z-50 text-sm">
          {items.map((item) => (
            <button
              key={item.label}
              type="button"
              className="w-full text-left px-3.5 py-2 hover:bg-[#f4f5f7] dark:hover:bg-[#2c333a] text-[#172b4d] dark:text-gray-200"
              onClick={() => {
                setOpen(false)
                item.onClick()
              }}
            >
              {item.label}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
