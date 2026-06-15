interface StarsProps {
  count: number
  max?: number
  size?: 'sm' | 'lg'
}

export function Stars({ count, max = 3, size = 'sm' }: StarsProps) {
  const sz = size === 'lg' ? 'text-3xl' : 'text-lg'
  return (
    <div className="flex gap-1 justify-center" dir="ltr">
      {Array.from({ length: max }).map((_, i) => (
        <span key={i} className={sz}>
          {i < count ? '⭐' : '☆'}
        </span>
      ))}
    </div>
  )
}
