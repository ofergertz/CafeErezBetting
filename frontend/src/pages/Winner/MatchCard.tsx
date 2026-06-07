import { useTranslation } from 'react-i18next'
import { useBetSlipStore } from '@/store/betSlipStore'
import type { WinnerMatch, WinnerPick } from '@/types'

interface Props { match: WinnerMatch }

const PICKS: { key: WinnerPick; label: string }[] = [
  { key: '1', label: '1' },
  { key: 'X', label: 'X' },
  { key: '2', label: '2' },
]

export default function MatchCard({ match }: Props) {
  const { t } = useTranslation()
  const { items, addOrToggle } = useBetSlipStore()
  const selected = items.find(i => i.matchId === match.id)?.pick

  const isLocked = match.status === 'finished' || match.status === 'suspended'

  const oddsMap: Record<WinnerPick, number> = {
    '1': match.odds['1'],
    'X': match.odds.X,
    '2': match.odds['2'],
  }

  const scheduledAt = new Date(match.scheduledAt)
  const timeStr = scheduledAt.toLocaleTimeString('he-IL', { hour: '2-digit', minute: '2-digit' })
  const dateStr = scheduledAt.toLocaleDateString('he-IL', { day: '2-digit', month: '2-digit' })

  return (
    <div className={`card p-4 transition-opacity ${isLocked ? 'opacity-50' : ''}`}>
      {/* Header */}
      <div className="flex items-center justify-between mb-3 text-xs text-gray-500">
        <span>{match.league}</span>
        <div className="flex items-center gap-2">
          {match.isLive && (
            <span className="flex items-center gap-1 text-red-600 font-bold text-xs">
              <span className="live-dot" /> {t('winner.live')}
            </span>
          )}
          {!match.isLive && <span>{dateStr} {timeStr}</span>}
          {match.status === 'suspended' && (
            <span className="text-amber-600 font-semibold">{t('winner.matchSuspended')}</span>
          )}
        </div>
      </div>

      {/* Teams + odds */}
      <div className="flex items-center gap-3">
        {/* Teams */}
        <div className="flex-1 min-w-0">
          <p className="font-semibold text-sm truncate">{match.homeTeam}</p>
          <p className="text-xs text-gray-400 my-0.5">vs</p>
          <p className="font-semibold text-sm truncate">{match.awayTeam}</p>
        </div>

        {/* Odds buttons */}
        <div className="flex gap-2 flex-shrink-0">
          {PICKS.map(({ key, label }) => {
            const odds = oddsMap[key]
            const isSelected = selected === key
            return (
              <button
                key={key}
                disabled={isLocked}
                onClick={() => addOrToggle({
                  matchId: match.id,
                  homeTeam: match.homeTeam,
                  awayTeam: match.awayTeam,
                  pick: key,
                  odds,
                })}
                className={`
                  flex flex-col items-center justify-center
                  min-w-[52px] min-h-[44px] rounded-lg border text-sm font-semibold
                  transition-all duration-100 select-none
                  disabled:cursor-not-allowed
                  ${isSelected
                    ? 'bg-[--color-accent] text-white border-[--color-accent] scale-105'
                    : 'bg-white text-gray-700 border-[--color-border] hover:border-[--color-accent] hover:text-[--color-accent]'
                  }
                `}
              >
                <span className="text-xs text-current opacity-60">{label}</span>
                <span>{odds.toFixed(2)}</span>
              </button>
            )
          })}
        </div>
      </div>
    </div>
  )
}
