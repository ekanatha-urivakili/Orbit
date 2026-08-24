import { X } from 'lucide-react'

export interface BacklogViewSettings {
  showEmptySprints: boolean
  density: 'Default' | 'Compact'
  showEpic: boolean
  showDueDate: boolean
  showStatus: boolean
  showEstimate: boolean
}

export const DEFAULT_BACKLOG_VIEW_SETTINGS: BacklogViewSettings = {
  showEmptySprints: true,
  density: 'Default',
  showEpic: true,
  showDueDate: true,
  showStatus: true,
  showEstimate: true,
}

function Toggle({ checked, disabled, onChange }: { checked: boolean; disabled?: boolean; onChange?: () => void }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      disabled={disabled}
      onClick={onChange}
      className={`relative inline-flex h-4 w-7 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${
        disabled ? 'opacity-30 cursor-not-allowed bg-gray-200' : checked ? 'bg-green-600' : 'bg-gray-300 dark:bg-gray-600'
      }`}
    >
      <span
        aria-hidden="true"
        className={`pointer-events-none inline-block h-3 w-3 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${
          checked ? 'translate-x-3' : 'translate-x-0'
        }`}
      />
    </button>
  )
}

const FIELD_ROWS: { key: keyof BacklogViewSettings; label: string }[] = [
  { key: 'showEpic', label: 'Epic' },
  { key: 'showDueDate', label: 'Due date' },
  { key: 'showStatus', label: 'Status' },
  { key: 'showEstimate', label: 'Estimate' },
]

export function BacklogViewSettingsPanel({
  settings,
  onChange,
  onClose,
}: {
  settings: BacklogViewSettings
  onChange: (settings: BacklogViewSettings) => void
  onClose: () => void
}) {
  return (
    <aside className="w-[360px] shrink-0 border-l border-gray-200 dark:border-[#394047] bg-white dark:bg-[#1d2125] h-full overflow-y-auto z-20">
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100 dark:border-[#394047]">
        <h2 className="text-base font-bold text-[#172b4d] dark:text-gray-100">View settings</h2>
        <button className="icon-button text-gray-500 hover:text-gray-700" type="button" aria-label="Close" onClick={onClose}>
          <X size={18} />
        </button>
      </div>

      <div className="p-5 space-y-6">
        <div className="flex items-center justify-between">
          <span className="text-xs font-semibold text-gray-700 dark:text-gray-300">Epic panel</span>
          <Toggle checked={false} disabled />
        </div>

        <div className="flex items-center justify-between">
          <span className="text-xs font-semibold text-gray-700 dark:text-gray-300">Empty sprints</span>
          <Toggle
            checked={settings.showEmptySprints}
            onChange={() => onChange({ ...settings, showEmptySprints: !settings.showEmptySprints })}
          />
        </div>

        <div className="space-y-2">
          <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300">Density</label>
          <div className="flex flex-col gap-2">
            {(['Default', 'Compact'] as const).map((option) => (
              <label key={option} className="flex items-center gap-2 text-xs text-gray-700 dark:text-gray-300 cursor-pointer">
                <input
                  type="radio"
                  name="backlog-density"
                  checked={settings.density === option}
                  onChange={() => onChange({ ...settings, density: option })}
                  className="accent-blue-600"
                />
                {option}
              </label>
            ))}
          </div>
        </div>

        <div className="space-y-3 pt-2">
          <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300">Fields</label>
          <div className="divide-y divide-gray-100 dark:divide-[#394047]">
            <div className="flex items-center justify-between py-2 text-xs">
              <span className="text-gray-400 dark:text-gray-500">Work type</span>
              <Toggle checked disabled />
            </div>
            <div className="flex items-center justify-between py-2 text-xs">
              <span className="text-gray-400 dark:text-gray-500">Work item key</span>
              <Toggle checked disabled />
            </div>
            {FIELD_ROWS.map((field) => (
              <div key={field.key} className="flex items-center justify-between py-2 text-xs">
                <span className="text-gray-700 dark:text-gray-200 font-medium">{field.label}</span>
                <Toggle checked={settings[field.key] as boolean} onChange={() => onChange({ ...settings, [field.key]: !settings[field.key] })} />
              </div>
            ))}
          </div>
        </div>
      </div>
    </aside>
  )
}
