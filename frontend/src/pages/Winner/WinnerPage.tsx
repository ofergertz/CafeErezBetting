import { useState, useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { useBetSlipStore } from '@/store/betSlipStore'
import { useMatchesHub } from '@/hooks/useSignalR'
import type { WinnerMatch } from '@/types'
import BetSlip from './BetSlip'
import MatchCard from './MatchCard'
import { CalendarClock, CheckCircle2, Trophy, Wifi, ChevronDown } from 'lucide-react'
import { EmptyState } from '@/components/ui/EmptyState'
import { ErrorState } from '@/components/ui/ErrorState'

type FilterTab = 'all' | 'live' | 'upcoming' | 'finished'

interface Source { index: number; name: string; url: string }

interface TabConfig {
  key: FilterTab
  label: string
  icon?: React.ReactNode
  filter: (m: WinnerMatch) => boolean
}

const TABS: TabConfig[] = [
  { key: 'all',      label: 'הכל',         filter: () => true },
  { key: 'live',     label: 'לייב',        icon: <span className="live-dot" />, filter: (m) => m.isLive },
  { key: 'upcoming', label: 'לא התחילו',   icon: <CalendarClock size={13} />,   filter: (m) => m.status === 'upcoming' && !m.isLive },
  { key: 'finished', label: 'הסתיימו',     icon: <CheckCircle2 size={13} />,    filter: (m) => m.status === 'finished' },
]

// Map league name keywords → flag emoji
function leagueFlag(league: string): string {
  const map: [string, string][] = [
    ['ישראל', '🇮🇱'], ['ליגת העל', '🇮🇱'], ['ליגה א', '🇮🇱'], ['ווינר', '🇮🇱'],
    ['ספרד', '🇪🇸'], ['לה ליגה', '🇪🇸'], ['La Liga', '🇪🇸'], ['ספרדי', '🇪🇸'],
    ['אנגלי', '🏴󠁧󠁢󠁥󠁮󠁧󠁿'], ['Premier', '🏴󠁧󠁢󠁥󠁮󠁧󠁿'], ['אנגלית', '🏴󠁧󠁢󠁥󠁮󠁧󠁿'],
    ['גרמניה', '🇩🇪'], ['Bundesliga', '🇩🇪'], ['גרמנ', '🇩🇪'],
    ['איטליה', '🇮🇹'], ['Serie A', '🇮🇹'], ['איטלק', '🇮🇹'],
    ['צרפת', '🇫🇷'], ['Ligue', '🇫🇷'], ['צרפת', '🇫🇷'],
    ['פורטוגל', '🇵🇹'],
    ['הולנד', '🇳🇱'], ['ארצות השפלה', '🇳🇱'],
    ['בלגיה', '🇧🇪'],
    ['טורקיה', '🇹🇷'], ['טורקי', '🇹🇷'],
    ['יוון', '🇬🇷'], ['יוונ', '🇬🇷'],
    ['רוסיה', '🇷🇺'], ['רוסי', '🇷🇺'],
    ['אוקראינה', '🇺🇦'], ['אוקראינ', '🇺🇦'],
    ['גאורגיה', '🇬🇪'], ['גאורג', '🇬🇪'],
    ['בוליביה', '🇧🇴'],
    ['ארגנטינה', '🇦🇷'], ['ארגנטינ', '🇦🇷'],
    ['ברזיל', '🇧🇷'],
    ['מקסיקו', '🇲🇽'],
    ['Champions', '🏆'], ['צ\'מפיונס', '🏆'], ['ליגת האלופות', '🏆'],
    ['Europa', '🇪🇺'], ['יורופה', '🇪🇺'],
    ['שוודיה', '🇸🇪'],
    ['נורווגיה', '🇳🇴'],
    ['דנמרק', '🇩🇰'],
    ['פינלנד', '🇫🇮'],
    ['שוויץ', '🇨🇭'],
    ['אוסטריה', '🇦🇹'],
    ['פולין', '🇵🇱'],
    ['צ\'כיה', '🇨🇿'], ['צ׳כיה', '🇨🇿'],
    ['רומניה', '🇷🇴'],
    ['הונגריה', '🇭🇺'],
    ['סרביה', '🇷🇸'],
    ['קרואטיה', '🇭🇷'],
    ['בולגריה', '🇧🇬'],
    ['סקוטלנד', '🏴󠁧󠁢󠁳󠁣󠁴󠁿'],
    ['אירלנד', '🇮🇪'],
    ['יפן', '🇯🇵'],
    ['קוריאה', '🇰🇷'],
    ['אוסטרליה', '🇦🇺'],
    ['מרוקו', '🇲🇦'],
    ['מצרים', '🇪🇬'],
    ['ניגריה', '🇳🇬'],
    ['ארה"ב', '🇺🇸'], ['MLS', '🇺🇸'],
    ['קולומביה', '🇨🇴'],
    ['צ\'ילה', '🇨🇱'],
    ['פרו', '🇵🇪'],
    ['אקוודור', '🇪🇨'],
    ['ונצואלה', '🇻🇪'],
    ['פרגוואי', '🇵🇾'],
    ['אורוגוואי', '🇺🇾'],
    ['דרום אפריקה', '🇿🇦'], ['אפריקאית', '🇿🇦'],
    ['לטביה', '🇱🇻'], ['לטבי', '🇱🇻'],
    ['אסטוניה', '🇪🇪'],
    ['ליטא', '🇱🇹'], ['ליטאי', '🇱🇹'],
    ['סלובניה', '🇸🇮'],
    ['סלובקיה', '🇸🇰'],
    ['אלבניה', '🇦🇱'],
    ['קפריסין', '🇨🇾'],
    ['איסלנד', '🇮🇸'],
    ['מקדוניה', '🇲🇰'],
    ['בוסניה', '🇧🇦'],
    ['מונטנגרו', '🇲🇪'],
    ['מולדובה', '🇲🇩'],
    ['בלארוס', '🇧🇾'],
    ['אזרבייג', '🇦🇿'],
    ['קזחסטן', '🇰🇿'],
    ['ארמניה', '🇦🇲'],
    ['סין', '🇨🇳'], ['סינית', '🇨🇳'],
    ['הודו', '🇮🇳'],
    ['תאילנד', '🇹🇭'],
    ['אינדונזיה', '🇮🇩'],
    ['סעודיה', '🇸🇦'], ['סעודית', '🇸🇦'],
    ['קטאר', '🇶🇦'],
    ['אמירויות', '🇦🇪'],
    ['אירן', '🇮🇷'],
    ['ניו זילנד', '🇳🇿'],
    ['קנדה', '🇨🇦'],
    ['גאנה', '🇬🇭'],
    ['חוף השנהב', '🇨🇮'],
    ['סנגל', '🇸🇳'],
    ['טוניסיה', '🇹🇳'],
    ['אלג', '🇩🇿'],
    ['קוסטה ריקה', '🇨🇷'],
    ['פנמה', '🇵🇦'],
    ['גואטמלה', '🇬🇹'],
  ]
  for (const [kw, flag] of map) {
    if (league.includes(kw)) return flag
  }
  return '⚽'
}

export default function WinnerPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [slipOpen, setSlipOpen] = useState(false)
  const [activeTab, setActiveTab] = useState<FilterTab>('all')
  const [selectedSource, setSelectedSource] = useState<number | null>(null)
  const betCount = useBetSlipStore((s) => s.items.length)

  const { data: sources = [] } = useQuery<Source[]>({
    queryKey: ['winner-sources'],
    queryFn: () => api.get<Source[]>('/api/winner/sources'),
    staleTime: Infinity,
  })

  const { data: matches = [], isPending, isError, isFetching, failureCount, refetch } = useQuery<WinnerMatch[]>({
    queryKey: ['winner-matches', selectedSource],
    queryFn: () => api.get<WinnerMatch[]>(
      selectedSource !== null
        ? `/api/winner/matches?source=${selectedSource}`
        : '/api/winner/matches'
    ),
    refetchInterval: selectedSource === null ? 60_000 : false,
    retry: 8,
    retryDelay: (attempt) => Math.min(3_000 * 2 ** attempt, 30_000),
  })

  const handleMatchesUpdated = useCallback((updated: unknown) => {
    if (selectedSource === null)
      queryClient.setQueryData(['winner-matches', null], updated)
  }, [queryClient, selectedSource])

  useMatchesHub(handleMatchesUpdated)

  if (isPending) return (
    <div className="flex items-center justify-center h-64 text-gray-400">
      <div className="text-center">
        <div className="w-8 h-8 border-2 border-[--color-accent] border-t-transparent rounded-full animate-spin mx-auto mb-3" />
        <p className="text-sm">
          {selectedSource !== null
            ? `טוען ממקור ${selectedSource + 1}...`
            : failureCount > 0 ? 'מתחבר לשרת...' : t('common.loading')}
        </p>
        {failureCount > 2 && selectedSource === null && (
          <p className="text-xs text-gray-400 mt-1">השרת עדיין מתחיל, אנא המתן...</p>
        )}
      </div>
    </div>
  )

  if (isError) return (
    <div className="card">
      <ErrorState message="לא ניתן להתחבר לשרת" onRetry={() => refetch()} />
    </div>
  )

  const liveCount = matches.filter(m => m.isLive).length
  const tabFilter = TABS.find(t => t.key === activeTab)!.filter
  const visibleMatches = matches.filter(tabFilter)

  // Group by league, preserving scraper order
  const leagueOrder: string[] = []
  const leagueMap: Record<string, WinnerMatch[]> = {}
  for (const m of visibleMatches) {
    const key = m.league || 'ווינר'
    if (!leagueMap[key]) { leagueMap[key] = []; leagueOrder.push(key) }
    leagueMap[key].push(m)
  }

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

        {/* Source selector */}
        {sources.length > 0 && (
          <div className="flex items-center gap-2 mb-3">
            <span className="text-xs text-gray-500 flex-shrink-0">מקור נתונים:</span>
            <div className="relative">
              <select
                value={selectedSource ?? ''}
                onChange={e => setSelectedSource(e.target.value === '' ? null : Number(e.target.value))}
                className="appearance-none text-sm border border-[--color-border] rounded-lg ps-3 pe-8 py-1.5 bg-white focus:outline-none focus:ring-2 focus:ring-[--color-accent] cursor-pointer"
              >
                <option value="">נתונים מאוחסנים</option>
                {sources.map(s => (
                  <option key={s.index} value={s.index}>{s.name}</option>
                ))}
              </select>
              <ChevronDown size={14} className="absolute end-2.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none" />
            </div>
            {isFetching && (
              <div className="w-4 h-4 border-2 border-[--color-accent] border-t-transparent rounded-full animate-spin" />
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

        {/* Matches — grouped by league */}
        {visibleMatches.length === 0 ? (
          <div className="card">
            <EmptyState icon={Trophy} message={t('common.noData')} />
          </div>
        ) : (
          <div className="space-y-3">
            {leagueOrder.map(league => (
              <section key={league}>
                {/* League header — dark navy bar, like the betting site */}
                <div className="flex items-center gap-2 bg-slate-800 text-white px-3 py-2.5 rounded-t-lg border-b-2 border-slate-900" dir="rtl">
                  <span className="text-base leading-none flex-shrink-0">{leagueFlag(league)}</span>
                  <h3 className="text-sm font-bold tracking-wide truncate flex-1">{league}</h3>
                  <span className="text-xs text-slate-300 bg-slate-700 rounded px-1.5 py-0.5 flex-shrink-0">{leagueMap[league].length}</span>
                </div>
                {/* Match rows */}
                <div className="border border-t-0 border-gray-200 rounded-b-lg overflow-hidden">
                  {leagueMap[league].map(m => <MatchCard key={m.id} match={m} />)}
                </div>
              </section>
            ))}
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
