import { useState, useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { useBetSlipStore } from '@/store/betSlipStore'
import { useMatchesHub } from '@/hooks/useSignalR'
import type { WinnerMatch } from '@/types'
import BetSlip from './BetSlip'
import MatchCard from './MatchCard'
import { CalendarClock, Trophy } from 'lucide-react'
import { EmptyState } from '@/components/ui/EmptyState'
import { ErrorState } from '@/components/ui/ErrorState'

export default function WinnerPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [slipOpen, setSlipOpen] = useState(false)
  const betCount = useBetSlipStore((s) => s.items.length)

  const { data: matches = [], isLoading, isError } = useQuery<WinnerMatch[]>({
    queryKey: ['winner-matches'],
    queryFn: () => api.get<WinnerMatch[]>('/api/winner/matches'),
    refetchInterval: 60_000,
  })

  // Live updates via SignalR
  const handleMatchesUpdated = useCallback((updated: unknown) => {
    queryClient.setQueryData(['winner-matches'], updated)
  }, [queryClient])

  useMatchesHub(handleMatchesUpdated)

  if (isLoading) return (
    <div className="flex items-center justify-center h-64 text-gray-400">
      <div className="text-center">
        <div className="w-8 h-8 border-2 border-[--color-accent] border-t-transparent rounded-full animate-spin mx-auto mb-3" />
        <p className="text-sm">{t('common.loading')}</p>
      </div>
    </div>
  )

  if (isError) return (
    <div className="card">
      <ErrorState message={t('common.error')} />
    </div>
  )

  const live     = matches.filter(m => m.isLive)
  const upcoming = matches.filter(m => !m.isLive && m.status === 'upcoming')

  return (
    <div className="flex gap-6 relative">
      {/* ── Matches list ─────────────────────────────────── */}
      <div className="flex-1 min-w-0">
        <div className="bg-gradient-to-l from-[#2d6a4f] to-[#52b788] rounded-xl p-4 mb-6 flex items-center gap-3">
          <Trophy size={28} className="text-white" />
          <div>
            <h2 className="text-white font-bold text-lg">ווינר</h2>
            <p className="text-green-50 text-sm">הימור על תוצאות כדורגל</p>
          </div>
        </div>

        <div className="flex items-center justify-between mb-4">
          <h1 className="font-display font-bold text-xl text-[--color-accent]">
            {t('winner.title')}
          </h1>

          {/* Mobile bet slip toggle */}
          {betCount > 0 && (
            <button
              onClick={() => setSlipOpen(true)}
              className="btn-primary md:hidden relative"
            >
              {t('winner.betSlip')}
              <span className="absolute -top-2 -right-2 w-5 h-5 bg-red-500 text-white text-xs rounded-full flex items-center justify-center">
                {betCount}
              </span>
            </button>
          )}
        </div>

        {/* Live matches */}
        {live.length > 0 && (
          <section className="mb-6">
            <div className="flex items-center gap-2 mb-3">
              <span className="live-dot" />
              <h2 className="text-sm font-bold text-red-600 uppercase tracking-wide">{t('winner.matchLive')}</h2>
            </div>
            <div className="flex flex-col gap-3">
              {live.map(m => <MatchCard key={m.id} match={m} />)}
            </div>
          </section>
        )}

        {/* Upcoming matches */}
        {upcoming.length > 0 && (
          <section>
            <h2 className="text-sm font-bold text-gray-500 uppercase tracking-wide mb-3 flex items-center gap-1.5">
              <CalendarClock size={14} /> {t('winner.title')}
            </h2>
            <div className="flex flex-col gap-3">
              {upcoming.map(m => <MatchCard key={m.id} match={m} />)}
            </div>
          </section>
        )}

        {matches.length === 0 && (
          <div className="card">
            <EmptyState icon={Trophy} message={t('common.noData')} />
          </div>
        )}
      </div>

      {/* ── Desktop bet slip ─────────────────────────────── */}
      <div className="hidden md:block w-80 flex-shrink-0">
        <div className="sticky top-20">
          <BetSlip />
        </div>
      </div>

      {/* ── Mobile bet slip (bottom sheet) ──────────────── */}
      {slipOpen && (
        <div className="fixed inset-0 z-50 md:hidden">
          <div
            className="absolute inset-0 bg-black/40"
            onClick={() => setSlipOpen(false)}
          />
          <div className="absolute bottom-0 left-0 right-0 bg-white rounded-t-2xl p-4 max-h-[80vh] overflow-y-auto">
            <div className="flex items-center justify-between mb-4">
              <h2 className="font-bold text-lg">{t('winner.betSlip')}</h2>
              <button onClick={() => setSlipOpen(false)} className="text-gray-400 text-xl">✕</button>
            </div>
            <BetSlip onSubmit={() => setSlipOpen(false)} />
          </div>
        </div>
      )}
    </div>
  )
}
