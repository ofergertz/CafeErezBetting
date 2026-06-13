import { useBetSlipStore } from '@/store/betSlipStore'
import type { WinnerMatch, WinnerPick } from '@/types'

interface Props { match: WinnerMatch }

const PICKS: { key: WinnerPick; label: string }[] = [
  { key: '1', label: '1' },
  { key: 'X', label: 'X' },
  { key: '2', label: '2' },
]

export default function MatchCard({ match }: Props) {
  const { items, addOrToggle } = useBetSlipStore()
  const selected = items.find(i => i.matchId === match.id)?.pick

  const isLocked = match.status === 'finished' || match.status === 'suspended'

  const scheduledAt = new Date(match.scheduledAt)
  const timeStr = scheduledAt.toLocaleTimeString('he-IL', { hour: '2-digit', minute: '2-digit' })

  return (
    <div
      dir="rtl"
      className={`flex items-center gap-2 py-2 px-3 border-b border-gray-100 last:border-b-0 bg-white transition-opacity ${isLocked ? 'opacity-50' : ''}`}
    >
      {/* Form number + sub-market (rightmost) */}
      <div className="text-[11px] text-gray-400 font-mono whitespace-nowrap flex-shrink-0 w-14 text-right leading-tight">
        {match.formNumber ? (
          <>
            <span>.{match.formNumber}</span>
            {match.subMarket != null && <span className="text-gray-300"> ({match.subMarket})</span>}
          </>
        ) : match.subMarket != null ? (
          <span className="text-gray-300">({match.subMarket})</span>
        ) : null}
      </div>

      {/* Status: blinking red dot + minute (live) OR gray dot + time (upcoming) */}
      <div className="flex items-center gap-1 flex-shrink-0 w-12">
        {match.isLive ? (
          <>
            <span className="live-dot" />
            <span className="text-red-600 font-bold text-[11px]">
              {match.minute ? `${match.minute}'` : 'חי'}
            </span>
          </>
        ) : (
          <>
            <span className="w-2 h-2 rounded-full bg-gray-300 flex-shrink-0" />
            <span className="text-gray-500 text-[11px]">{timeStr}</span>
          </>
        )}
      </div>

      {/* Teams + optional bet type (flex-1) */}
      <div className="flex-1 min-w-0">
        {match.betType && (
          <p className="text-[10px] text-emerald-700 leading-none mb-0.5 truncate">
            {match.betType}{match.handicap ? ` ${match.handicap}` : ''}
          </p>
        )}
        <p className="text-sm font-medium leading-tight truncate">{match.homeTeam}</p>
        <p className="text-xs text-gray-500 leading-tight truncate">{match.awayTeam}</p>
      </div>

      {/* Bet code (S,D etc.) */}
      {match.betCode && (
        <span className="text-[11px] text-gray-400 font-mono flex-shrink-0">{match.betCode}</span>
      )}

      {/* Odds buttons [1][X][2] */}
      <div className="flex gap-1 flex-shrink-0">
        {PICKS.map(({ key, label }) => {
          const odds = match.odds[key]
          const isSelected = selected === key
          return (
            <button
              key={key}
              disabled={isLocked || odds == null}
              aria-label={`בחר ${key}`}
              onClick={() => odds != null && addOrToggle({
                matchId: match.id,
                homeTeam: match.homeTeam,
                awayTeam: match.awayTeam,
                pick: key,
                odds,
              })}
              className={`
                flex flex-col items-center justify-center
                w-11 min-h-[38px] rounded border text-xs font-semibold
                transition-all duration-100 select-none
                disabled:cursor-not-allowed
                ${isSelected
                  ? 'bg-[--color-accent] text-white border-[--color-accent]'
                  : 'bg-white text-gray-700 border-gray-200 hover:border-[--color-accent] hover:text-[--color-accent]'
                }
              `}
            >
              <span className="text-gray-400 text-[10px] leading-tight">{label}</span>
              <span className="leading-tight">{odds != null ? odds.toFixed(2) : '—'}</span>
            </button>
          )
        })}
      </div>

      {/* Score / result (leftmost) */}
      <div className="text-[11px] font-mono text-gray-600 flex-shrink-0 w-8 text-center">
        {match.score ?? '-'}
      </div>
    </div>
  )
}
