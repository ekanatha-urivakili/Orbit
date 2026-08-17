import { useState, useRef, useEffect, useId, useMemo, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { ChevronDown, Search, Check, X } from 'lucide-react'

export interface SelectOption<T extends string = string> {
  value: T
  label: string
  description?: string
  badge?: string
  icon?: ReactNode
  disabled?: boolean
}

export interface SearchableSelectProps<T extends string = string> {
  value?: T | null
  onChange?: (value: T) => void
  options: (SelectOption<T> | string)[]
  placeholder?: string
  searchPlaceholder?: string
  disabled?: boolean
  required?: boolean
  searchable?: boolean
  clearable?: boolean
  onClear?: () => void
  className?: string
  triggerClassName?: string
  menuClassName?: string
  id?: string
  name?: string
  'aria-label'?: string
  size?: 'sm' | 'md' | 'lg' | 'xl'
  variant?: 'default' | 'panel' | 'header' | 'compact'
  emptyMessage?: string
}

export function SearchableSelect<T extends string = string>({
  value,
  onChange,
  options: rawOptions,
  placeholder = 'Select an option…',
  searchPlaceholder = 'Search…',
  disabled = false,
  required = false,
  searchable = true,
  clearable = false,
  onClear,
  className = '',
  triggerClassName = '',
  menuClassName = '',
  id,
  name,
  'aria-label': ariaLabel,
  size = 'md',
  variant = 'default',
  emptyMessage = 'No options found',
}: SearchableSelectProps<T>) {
  const [isOpen, setIsOpen] = useState(false)
  const [searchQuery, setSearchQuery] = useState('')
  const [activeIndex, setActiveIndex] = useState(-1)
  const [menuPosition, setMenuPosition] = useState<{
    top: number
    left: number
    width: number
    openUpward: boolean
  }>({ top: 0, left: 0, width: 0, openUpward: false })

  const generatedId = useId()
  const selectId = id || generatedId
  const triggerRef = useRef<HTMLButtonElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)
  const searchInputRef = useRef<HTMLInputElement>(null)
  const listRef = useRef<HTMLUListElement>(null)

  // Normalize options to SelectOption[]
  const options = useMemo<SelectOption<T>[]>(() => {
    return rawOptions.map((opt) => {
      if (typeof opt === 'string') {
        return { value: opt as T, label: opt }
      }
      return opt
    })
  }, [rawOptions])

  // Selected option
  const selectedOption = useMemo(() => {
    return options.find((opt) => opt.value === value)
  }, [options, value])

  // Filter options based on search query
  const filteredOptions = useMemo(() => {
    if (!searchQuery.trim()) return options
    const q = searchQuery.toLowerCase().trim()
    return options.filter((opt) => {
      const matchLabel = opt.label.toLowerCase().includes(q)
      const matchDesc = opt.description ? opt.description.toLowerCase().includes(q) : false
      const matchValue = opt.value.toLowerCase().includes(q)
      return matchLabel || matchDesc || matchValue
    })
  }, [options, searchQuery])

  // Calculate menu position
  const updatePosition = () => {
    if (!triggerRef.current) return
    const rect = triggerRef.current.getBoundingClientRect()
    const spaceBelow = window.innerHeight - rect.bottom
    const spaceAbove = rect.top
    const menuHeight = 280
    const openUpward = spaceBelow < menuHeight && spaceAbove > spaceBelow

    setMenuPosition({
      top: openUpward ? rect.top : rect.bottom,
      left: rect.left,
      width: Math.max(rect.width, size === 'xl' ? 320 : 220),
      openUpward,
    })
  }

  // Toggle open
  const toggleOpen = () => {
    if (disabled) return
    if (!isOpen) {
      updatePosition()
      setIsOpen(true)
      setSearchQuery('')
      setActiveIndex(-1)
    } else {
      setIsOpen(false)
    }
  }

  // Close menu
  const closeMenu = () => {
    setIsOpen(false)
    setSearchQuery('')
    setActiveIndex(-1)
  }

  // Select item
  const handleSelect = (option: SelectOption<T>) => {
    if (option.disabled) return
    onChange?.(option.value)
    closeMenu()
    triggerRef.current?.focus()
  }

  // Handle clear
  const handleClear = (e: React.MouseEvent) => {
    e.stopPropagation()
    onClear?.()
    if (onChange) onChange('' as T)
  }

  // Focus search input on open
  useEffect(() => {
    if (isOpen) {
      updatePosition()
      const timer = setTimeout(() => {
        searchInputRef.current?.focus()
      }, 50)
      return () => clearTimeout(timer)
    }
  }, [isOpen])

  // Handle click outside and window resize/scroll
  useEffect(() => {
    if (!isOpen) return

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault()
        closeMenu()
        triggerRef.current?.focus()
        return
      }

      if (e.key === 'ArrowDown') {
        e.preventDefault()
        setActiveIndex((prev) => {
          const next = prev < filteredOptions.length - 1 ? prev + 1 : 0
          return next
        })
        return
      }

      if (e.key === 'ArrowUp') {
        e.preventDefault()
        setActiveIndex((prev) => {
          const next = prev > 0 ? prev - 1 : filteredOptions.length - 1
          return next
        })
        return
      }

      if (e.key === 'Enter' && activeIndex >= 0 && activeIndex < filteredOptions.length) {
        e.preventDefault()
        handleSelect(filteredOptions[activeIndex])
        return
      }

      if (e.key === 'Tab') {
        closeMenu()
      }
    }

    const handleClickOutside = (e: MouseEvent) => {
      const target = e.target as Node
      if (
        triggerRef.current &&
        !triggerRef.current.contains(target) &&
        menuRef.current &&
        !menuRef.current.contains(target)
      ) {
        closeMenu()
      }
    }

    const handleScrollOrResize = (e: Event) => {
      // Don't close if scrolling inside the dropdown menu itself
      if (menuRef.current && menuRef.current.contains(e.target as Node)) return
      updatePosition()
    }

    window.addEventListener('keydown', handleKeyDown)
    document.addEventListener('mousedown', handleClickOutside)
    window.addEventListener('scroll', handleScrollOrResize, true)
    window.addEventListener('resize', handleScrollOrResize)

    return () => {
      window.removeEventListener('keydown', handleKeyDown)
      document.removeEventListener('mousedown', handleClickOutside)
      window.removeEventListener('scroll', handleScrollOrResize, true)
      window.removeEventListener('resize', handleScrollOrResize)
    }
  }, [isOpen, filteredOptions, activeIndex])

  // Scroll active option into view
  useEffect(() => {
    if (activeIndex >= 0 && listRef.current) {
      const items = listRef.current.querySelectorAll('li')
      if (items[activeIndex]) {
        items[activeIndex].scrollIntoView({ block: 'nearest' })
      }
    }
  }, [activeIndex])

  // Variants and sizing
  const sizeClasses = {
    sm: 'h-7 px-2 py-0.5 text-[10px] rounded-md',
    md: 'min-h-[30px] px-2.5 py-1.5 text-xs rounded-lg',
    lg: 'min-h-[34px] px-3 py-2 text-sm rounded-lg',
    xl: 'min-h-[40px] px-3 py-2 text-sm rounded-lg',
  }[size]

  const menuTextClasses = {
    sm: 'text-[10px]',
    md: 'text-[10px]',
    lg: 'text-xs',
    xl: 'text-xs',
  }[size]

  const menuDescriptionClasses = {
    sm: 'text-[9px]',
    md: 'text-[9px]',
    lg: 'text-[10px]',
    xl: 'text-[10px]',
  }[size]

  const variantClasses = {
    default:
      'bg-white dark:bg-[#22272b] border border-gray-300 dark:border-[#4b5563] text-gray-900 dark:text-gray-100 shadow-sm hover:border-gray-400 dark:hover:border-gray-500 focus:border-blue-500 dark:focus:border-blue-400 focus:ring-2 focus:ring-blue-500/20',
    panel:
      'bg-white dark:bg-[#22272b] border border-gray-300 dark:border-[#4b5563] text-gray-900 dark:text-gray-100 shadow-sm hover:border-gray-400 dark:hover:border-gray-500 focus:border-blue-500 dark:focus:border-blue-400 focus:ring-2 focus:ring-blue-500/20',
    header:
      'bg-white/15 hover:bg-white/25 border border-white/30 text-white shadow-none focus:ring-2 focus:ring-white/40',
    compact:
      'bg-white dark:bg-[#22272b] border border-gray-200 dark:border-[#394047] text-gray-700 dark:text-gray-200 hover:border-gray-300 dark:hover:border-gray-600 focus:border-blue-500',
  }[variant]

  const showSearch = searchable && options.length > 3

  return (
    <div className={`relative inline-block w-full text-left ${className}`}>
      {/* Hidden input for form integration */}
      {name && <input type="hidden" name={name} value={value ?? ''} required={required} />}

      {/* Trigger Button */}
      <button
        ref={triggerRef}
        id={selectId}
        type="button"
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-label={ariaLabel}
        onClick={toggleOpen}
        className={`group flex w-full items-center justify-between gap-2 text-left font-normal transition-all duration-150 outline-none select-none disabled:cursor-not-allowed disabled:opacity-50 ${sizeClasses} ${variantClasses} ${triggerClassName}`}
      >
        <div className="flex min-w-0 flex-1 items-center gap-2">
          {selectedOption?.icon && (
            <span className="shrink-0 flex items-center">{selectedOption.icon}</span>
          )}
          {selectedOption ? (
            <span className="truncate block font-medium">
              {selectedOption.label}
              {selectedOption.badge && (
                <span className="ml-2 inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold bg-blue-100 text-blue-700 dark:bg-blue-900/50 dark:text-blue-300">
                  {selectedOption.badge}
                </span>
              )}
            </span>
          ) : (
            <span className={`truncate block ${variant === 'header' ? 'text-white/70' : 'text-gray-400 dark:text-gray-500'}`}>
              {placeholder}
            </span>
          )}
        </div>

        <div className="flex shrink-0 items-center gap-1.5 ml-1">
          {clearable && selectedOption && !disabled && (
            <span
              role="button"
              tabIndex={0}
              onClick={handleClear}
              className="p-0.5 rounded-full hover:bg-gray-200 dark:hover:bg-gray-700 text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 transition-colors"
              title="Clear selection"
            >
              <X size={13} />
            </span>
          )}
          <ChevronDown
            size={size === 'sm' ? 14 : 16}
            className={`transition-transform duration-200 ease-in-out shrink-0 ${
              variant === 'header'
                ? 'text-white/80'
                : 'text-gray-400 dark:text-gray-400 group-hover:text-gray-600 dark:group-hover:text-gray-200'
            } ${isOpen ? 'rotate-180 text-blue-600 dark:text-blue-400' : ''}`}
          />
        </div>
      </button>

      {/* Dropdown Menu Portal */}
      {isOpen &&
        createPortal(
          <div
            ref={menuRef}
            style={{
              position: 'fixed',
              top: menuPosition.openUpward ? undefined : `${menuPosition.top + 5}px`,
              bottom: menuPosition.openUpward
                ? `${window.innerHeight - menuPosition.top + 5}px`
                : undefined,
              left: `${menuPosition.left}px`,
              width: `${menuPosition.width}px`,
              maxHeight: '340px',
              zIndex: 99999,
            }}
            className={`flex flex-col rounded-xl border border-gray-200 dark:border-[#394047] bg-white dark:bg-[#1e2327] shadow-2xl overflow-hidden backdrop-blur-md transition-all duration-150 animate-in fade-in zoom-in-95 ${menuClassName}`}
          >
            {/* Search Input Bar */}
            {showSearch && (
              <div className="p-2 border-b border-gray-100 dark:border-[#2d343c] bg-gray-50/70 dark:bg-[#161a1d]/70">
                <div className="relative flex items-center">
                  <Search
                    size={14}
                    className="absolute left-2.5 text-gray-400 dark:text-gray-500 pointer-events-none"
                  />
                  <input
                    ref={searchInputRef}
                    type="text"
                    value={searchQuery}
                    onChange={(e) => {
                      setSearchQuery(e.target.value)
                      setActiveIndex(0)
                    }}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') {
                        e.preventDefault()
                        e.stopPropagation()
                      }
                    }}
                    placeholder={searchPlaceholder}
                    className={`w-full pl-8 pr-7 py-1.5 ${menuTextClasses} bg-white dark:bg-[#22272b] border border-gray-200 dark:border-[#394047] rounded-lg text-gray-900 dark:text-gray-100 placeholder-gray-400 dark:placeholder-gray-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500`}
                  />
                  {searchQuery && (
                    <button
                      type="button"
                      onClick={() => {
                        setSearchQuery('')
                        searchInputRef.current?.focus()
                      }}
                      className="absolute right-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-200"
                    >
                      <X size={12} />
                    </button>
                  )}
                </div>
              </div>
            )}

            {/* Options List */}
            <ul
              ref={listRef}
              role="listbox"
              aria-label={ariaLabel || 'Options'}
              className="flex-1 overflow-y-auto p-1.5 max-h-60 space-y-0.5 focus:outline-none scrollbar-thin"
            >
              {filteredOptions.length === 0 ? (
                <li className={`px-3 py-6 text-center ${menuTextClasses} text-gray-400 dark:text-gray-500`}>
                  {emptyMessage}
                </li>
              ) : (
                filteredOptions.map((option, idx) => {
                  const isSelected = option.value === value
                  const isActive = idx === activeIndex

                  return (
                    <li
                      key={option.value}
                      role="option"
                      aria-selected={isSelected}
                      aria-disabled={option.disabled}
                      onClick={() => handleSelect(option)}
                      onMouseEnter={() => setActiveIndex(idx)}
                      className={`flex items-center justify-between gap-2 px-2.5 py-2 rounded-lg ${menuTextClasses} cursor-pointer select-none transition-colors ${
                        option.disabled
                          ? 'opacity-40 cursor-not-allowed text-gray-400'
                          : isSelected
                          ? 'bg-blue-50 dark:bg-blue-950/50 text-blue-700 dark:text-blue-300 font-medium'
                          : isActive
                          ? 'bg-gray-100 dark:bg-[#2b323a] text-gray-900 dark:text-gray-100'
                          : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-[#252b32]'
                      }`}
                    >
                      <div className="flex items-center gap-2 min-w-0 flex-1">
                        {option.icon && <span className="shrink-0">{option.icon}</span>}
                        <div className="min-w-0 flex-1">
                          <div className="flex items-center gap-1.5">
                            <span className="truncate">{option.label}</span>
                            {option.badge && (
                              <span className="shrink-0 px-1.5 py-0.2 rounded text-[10px] font-semibold bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-300">
                                {option.badge}
                              </span>
                            )}
                          </div>
                          {option.description && (
                            <p className={`truncate ${menuDescriptionClasses} text-gray-400 dark:text-gray-500 mt-0.5`}>
                              {option.description}
                            </p>
                          )}
                        </div>
                      </div>

                      {isSelected && (
                        <Check
                          size={14}
                          className="shrink-0 text-blue-600 dark:text-blue-400 stroke-[2.5]"
                        />
                      )}
                    </li>
                  )
                })
              )}
            </ul>
          </div>,
          document.body,
        )}
    </div>
  )
}
