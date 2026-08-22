import { nextRolloverName } from '../../lib/sprintNaming'

interface RolloverChoiceProps {
  sprintName: string
  createRollover: boolean
  onChange: (createRollover: boolean) => void
  idPrefix: string
}

export function RolloverChoice({ sprintName, createRollover, onChange, idPrefix }: RolloverChoiceProps) {
  const nextName = nextRolloverName(sprintName, new Date())

  return (
    <div className="space-y-2">
      <label className="flex items-start gap-2.5 text-sm text-gray-700 dark:text-gray-300">
        <input
          type="radio"
          name={`${idPrefix}-rollover`}
          checked={createRollover}
          onChange={() => onChange(true)}
          className="mt-0.5"
        />
        <span>
          Move incomplete items to a new sprint — creates <strong className="font-semibold text-gray-900 dark:text-gray-100">{nextName}</strong>
        </span>
      </label>
      <label className="flex items-start gap-2.5 text-sm text-gray-700 dark:text-gray-300">
        <input
          type="radio"
          name={`${idPrefix}-rollover`}
          checked={!createRollover}
          onChange={() => onChange(false)}
          className="mt-0.5"
        />
        <span>Move incomplete items to the top of the backlog</span>
      </label>
    </div>
  )
}
