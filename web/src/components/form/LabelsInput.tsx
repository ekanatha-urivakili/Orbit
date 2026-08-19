import { useState, type KeyboardEvent } from 'react'
import { X } from 'lucide-react'

export function LabelsInput({
  value,
  onChange,
  placeholder = 'frontend, customer-impact',
}: {
  value: string[]
  onChange: (labels: string[]) => void
  placeholder?: string
}) {
  const [draft, setDraft] = useState('')

  const addLabel = (raw: string) => {
    const label = raw.trim()
    if (!label || value.includes(label)) return
    onChange([...value, label])
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter' || event.key === ',') {
      event.preventDefault()
      addLabel(draft)
      setDraft('')
    } else if (event.key === 'Backspace' && !draft && value.length > 0) {
      onChange(value.slice(0, -1))
    }
  }

  const handleBlur = () => {
    if (draft.trim()) {
      addLabel(draft)
      setDraft('')
    }
  }

  return (
    <div className="labels-input">
      {value.map((label) => (
        <span key={label} className="label-chip">
          {label}
          <button
            type="button"
            className="label-chip-remove"
            aria-label={`Remove label ${label}`}
            onClick={() => onChange(value.filter((existing) => existing !== label))}
          >
            <X size={11} />
          </button>
        </span>
      ))}
      <input
        type="text"
        className="labels-input-field"
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        onKeyDown={handleKeyDown}
        onBlur={handleBlur}
        placeholder={value.length === 0 ? placeholder : ''}
      />
    </div>
  )
}
