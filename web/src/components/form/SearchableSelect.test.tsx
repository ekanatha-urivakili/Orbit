import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { SearchableSelect } from './SearchableSelect'

describe('SearchableSelect', () => {
  const options = [
    { value: 'opt-1', label: 'Option One' },
    { value: 'opt-2', label: 'Option Two' },
    { value: 'opt-3', label: 'Option Three' },
    { value: 'opt-4', label: 'Option Four' },
  ]

  it('renders placeholder and toggles dropdown on click', () => {
    render(<SearchableSelect options={options} placeholder="Pick one" />)

    const trigger = screen.getByRole('button', { name: /Pick one/i })
    expect(trigger).toBeInTheDocument()

    fireEvent.click(trigger)
    expect(screen.getByRole('listbox')).toBeInTheDocument()
    expect(screen.getByRole('option', { name: /Option One/i })).toBeInTheDocument()
  })

  it('selects option and triggers onChange', () => {
    const onChange = vi.fn()
    render(<SearchableSelect options={options} value={null} onChange={onChange} />)

    fireEvent.click(screen.getByRole('button', { name: /Select an option/i }))
    fireEvent.click(screen.getByRole('option', { name: /Option Two/i }))

    expect(onChange).toHaveBeenCalledWith('opt-2')
  })

  it('clears selection when clear button is clicked without toggling menu', () => {
    const onChange = vi.fn()
    const onClear = vi.fn()
    render(
      <SearchableSelect
        options={options}
        value="opt-1"
        clearable
        onChange={onChange}
        onClear={onClear}
      />
    )

    const clearBtn = screen.getByRole('button', { name: /Clear selection/i })
    expect(clearBtn).toBeInTheDocument()

    fireEvent.click(clearBtn)
    expect(onClear).toHaveBeenCalled()
    expect(onChange).toHaveBeenCalledWith('')
  })
})
