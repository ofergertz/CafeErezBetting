import { useState, useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { useBetSlipStore } from '@/store/betSlipStore'
import { useMatchesHub } from '@/hooks/useSignalR'
import { useAuthStore } from '@/store/authStore'
import type { WinnerMatch, SyncStatus } from '@/types'
import BetSlip from './BetSlip'
import MatchCard from './MatchCard'
import { CalendarClock, CheckCircle2, Trophy, Wifi, AlertTriangle, RefreshCw } from 'lucide-react'
import { EmptyState } from '@/components/ui/EmptyState'
import { ErrorState } from '@/components/ui/ErrorState'

type FilterTab = 'all' | 'live' | 'upcoming' | 'finished'

interface TabConfig {
  key: FilterTab
  label: string
  icon?: React.ReactNode
  filter: (m: WinnerMatch) => boolean
}

const TABS: TabConfig[] = [
  {
    key: 'all',
    label: 'הכל',
    filter: (m) => m.status !== 'finished',
  },
  {
    key: 'live',
    label: 'לייב',
    icon: <span className="live-dot" />,
    filter: (m) => m.isLive,
  },
  {
    key: 'upcoming',
    label: 'לא התחילו',
    icon: <CalendarClock size={13} />,
    filter: (m) => m.status === 'upcoming' && !m.isLive,
  },
  {
    key: 'finished',
    label: 'הסתיימו',
    icon: <CheckCircle2 size={13} />,
    filter: (m) => m.status === 'finished',
  },
]

export default function WinnerPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [slipOpen, setSlipOpen] = useState(false)
  const [activeTab, setActiveTab] = useState<FilterTab>('all')
  const betCount  = useBetSlipStore((s) => s.items.length)
  const isAdmin   = useAuthStore((s) => s.isAdmin())

  const { data: matches = [], isLoading, isError, refetch } = useQuery<WinnerMatch[]>({
    queryKey: ['winner-matches'],
    queryFn: () => api.get<WinnerMatch[]>('/api/winner/matches'),
    refetchInterval: 60_000,
    retry: 1,
  })

  const { data: syncStatus } = useQuery<SyncStatus>({
    queryKey: ['winner-sync-status'],
    queryFn: () => api.get<SyncStatus>('/api/winner/sync-status'),
    refetchInterval: 60_000,
    retry: false,
  })

  const syncMutation = useMutation({
    mutationFn: () => api.post<SyncStatus>('/api/winner/sync', {}),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['winner-matches'] })
      queryClient.invalidateQueries({ queryKey: ['winner-sync-status'] })
    },
  })

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
      <ErrorState message={t('common.error')} onRetry={() => refetch()} />
    </div>
  )

  const liveCount     = matches.filter(m => m.isLive).length
  const filteredMatches = TABS.find(t => t.key === activeTab)!.filter

  // For 'all' tab — show live first, then upcoming with section headers
  const visibleMatches = matches.filter(filteredMatches)
  const showSections   = activeTab === 'all'
  const live           = visibleMatches.filter(m => m.isLive)
  const rest           = visibleMatches.filter(m => !m.isLive)

  return (
    <div className="flex gap-6 relative">
      {/* ── Matches list ─────────────────────────────────── */}
      <div className="flex-1 min-w-0">

        {/* Banner */}
        <div className="bg-gradient-to-l from-[#2d6a4f] to-[#52b788] rounded-xl p-4 mb-4 flex items-center gap-3">
          <Trophy size={28} className="text-white" />
          <div className="flex-1">
            <h2 className="text-white font-bold text-lg">ווינר</h2>
            <p className="text-green-50 text-sm">הימור על תוצאות כדורגל</p>
          </div>
          {liveCount > 0 && (
            <div className="flex items-center gap-1.5 bg-white/20 rounded-full px-3 py-1">
              <Wifi size={13} className="text-white" />
              <span className="text-white text-xs font-bold">{liveCount} לייב</span>
            </div>
          )}
        </div>

        {/* Demo data warning + admin sync button */}
        {syncStatus?.isMock && (
          <div className="flex items-center gap-3 bg-amber-50 border border-amber-200 rounded-xl px-4 py-3 mb-4 text-sm" dir="rtl">
            <AlertTriangle size={16} className="text-amber-600 flex-shrink-0" />
            <span className="text-amber-800 flex-1">
              <strong>נתוני Demo</strong> — מוצגים נתוני הדגמה.
              {isAdmin
                ? ' הסקראפר לא הצליח להתחבר לאתר ווינר (Playwright/Chromium נדרש).'
                : ' הנתונים יתעדכנו בהמשך.'}
            </span>
            {isAdmin && (
              <button
                onClick={() => syncMutation.mutate()}
                disabled={syncMutation.isPending}
                className="flex items-center gap-1.5 bg-amber-600 text-white text-xs px-3 py-1.5 rounded-lg hover:bg-amber-700 disabled:opacity-50 flex-shrink-0"
              >
                <RefreshCw size={13} className={syncMutation.isPending ? 'animate-spin' : ''} />
                {syncMutation.isPending ? 'מסנכרן...' : 'נסה שוב'}
              </button>
            )}
          </div>
        )}

        {/* Filter Tabs */}
        <div className="flex gap-1 bg-gray-100 rounded-xl p-1 mb-4">
          {TABS.map(tab => {
            const count = matches.filter(tab.filter).length
            const isActive = activeTab === tab.key
            return (
              <button
                key={tab.key}
                onClick={() => setActiveTab(tab.key)}
                className={`
                  flex-1 flex items-center justify-center gap-1.5
                  min-h-[40px] rounded-lg text-sm font-semibold
                  transition-all duration-150
                  ${isActive
                    ? 'bg-white text-[--color-accent] shadow-sm'
                    : 'text-gray-500 hover:text-gray-700'
                  }
                `}
              >
                {tab.icon}
                <span>{tab.label}</span>
                {count > 0 && (
                  <span className={`
                    text-xs rounded-full px-1.5 py-0.5 font-bold
                    ${isActive
                      ? 'bg-[--color-accent] text-white'
                      : 'bg-gray-300 text-gray-600'
                    }
                  `}>
                    {count}
                  </span>
                )}
              </button>
            )
          })}
        </div>

        {/* Mobile bet slip toggle */}
        {betCount > 0 && (
          <div className="flex justify-end mb-3">
            <button
              onClick={() => setSlipOpen(true)}
              className="btn-primary md:hidden relative"
            >
              {t('winner.betSlip')}
              <span className="absolute -top-2 -right-2 w-5 h-5 bg-red-500 text-white text-xs rounded-full flex items-center justify-center">
                {betCount}
              </span>
            </button>
          </div>
        )}

        {/* Matches */}
        {visibleMatches.length === 0 ? (
          <div className="card">
            <EmptyState icon={Trophy} message={t('common.noData')} />
          </div>
        ) : showSections ? (
          <>
            {live.length > 0 && (
              <section className="mb-5">
                <div className="flex items-center gap-2 mb-3">
                  <span className="live-dot" />
                  <h2 className="text-sm font-bold text-red-600 uppercase tracking-wide">{t('winner.matchLive')}</h2>
                </div>
                <div className="flex flex-col gap-3">
                  {live.map(m => <MatchCard key={m.id} match={m} />)}
                </div>
              </section>
            )}
            {rest.length > 0 && (
              <section>
                <h2 className="text-sm font-bold text-gray-500 uppercase tracking-wide mb-3 flex items-center gap-1.5">
                  <CalendarClock size={14} /> משחקים קרובים
                </h2>
                <div className="flex flex-col gap-3">
                  {rest.map(m => <MatchCard key={m.id} match={m} />)}
                </div>
              </section>
            )}
          </>
        ) : (
          <div className="flex flex-col gap-3">
            {visibleMatches.map(m => <MatchCard key={m.id} match={m} />)}
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
          <div className="absolute inset-0 bg-black/40" onClick={() => setSlipOpen(false)} />
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
