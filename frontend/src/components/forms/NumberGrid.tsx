import clsx from 'clsx'

interface Props {
  max: number
  selected: number[]
  onToggle: (n: number) => void
  isStrong?: boolean
  disabled?: boolean
}

export function NumberGrid({
  max,
  selected,
  onToggle,
  isStrong = false,
  disabled = false,
}: Props) {
  return (
    <div className="flex flex-wrap gap-1.5">
      {Array.from({ length: max }, (_, i) => i + 1).map((n) => {
        const isSelected = selected.includes(n)
        return (
          <button
            key={n}
            type="button"
            disabled={disabled}
            aria-pressed={isSelected}
            aria-label={n.toString()}
            onClick={() => onToggle(n)}
            className={clsx(
              'num-cell active:scale-[1.15] transform-gpu',
              isSelected && !isStrong && 'selected',
              isSelected && isStrong && 'strong',
              disabled && 'opacity-50 cursor-not-allowed pointer-events-none',
            )}
          >
            {n}
          </button>
        )
      })}
    </div>
  )
}
