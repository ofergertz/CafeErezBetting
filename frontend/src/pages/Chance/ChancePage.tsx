import { useState, useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { useMutation } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { ChancePayload, ChanceSuit, ChanceCard } from '@/types'
import { SubmitSuccessOverlay } from '@/components/forms/SubmitSuccessOverlay'

// ── Game constants ─────────────────────────────────────────────────────────────
const CARDS: ChanceCard[]   = ['7', '8', '9', '10', 'J', 'Q', 'K', 'A']
const STAKES                = [5, 10, 25, 50, 100, 250, 500]
const DRAWS_OPTIONS         = [2, 3, 4, 5, 7, 10, 12]

const SUITS: { key: ChanceSuit; symbol: string; color: string }[] = [
  { key: 'spades',   symbol: '♠', color: 'text-gray-900' },
  { key: 'hearts',   symbol: '♥', color: 'text-red-600'  },
  { key: 'diamonds', symbol: '♦', color: 'text-red-600'  },
  { key: 'clubs',    symbol: '♣', color: 'text-gray-900' },
]

type Picks = Partial<Record<ChanceSuit, ChanceCard>>

function randomPick(): Picks {
  const picks: Picks = {}
  for (const suit of SUITS) {
    picks[suit.key] = CARDS[Math.floor(Math.random() * CARDS.length)]
  }
  return picks
}

export default function ChancePage() {
  const { t } = useTranslation()
  const [picks,      setPicks]      = useState<Picks>({})
  const [stake,      setStake]      = useState<number | null>(null)
  const [draws,      setDraws]      = useState<number | null>(null)
  const [validErr,   setValidErr]   = useState<string | null>(null)
  const [showSuccess,setShowSuccess]= useState(false)
  const successTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => () => { if (successTimer.current) clearTimeout(successTimer.current) }, [])

  const mutation = useMutation({
    mutationFn: (payload: ChancePayload) =>
      api.post<{ formId: string }>('/api/forms/chance', payload),
    onSuccess: () => {
      setShowSuccess(true)
      successTimer.current = setTimeout(() => {
        setShowSuccess(false)
        setPicks({})
      }, 2000)
    },
  })

  const toggleCard = (suit: ChanceSuit, card: ChanceCard) => {
    setValidErr(null)
    setPicks(prev => ({
      ...prev,
      [suit]: prev[suit] === card ? undefined : card,
    }))
  }

  const handleQuickPick = () => {
    setValidErr(null)
    setPicks(randomPick())
  }

  const handleSubmit = () => {
    if (stake === null) {
      setValidErr('יש לבחור סכום השתתפות')
      return
    }
    const missing = SUITS.filter(s => !picks[s.key])
    if (missing.length > 0) {
      setValidErr(`יש לבחור קלף בכל שורה (חסר: ${missing.map(s => s.symbol).join(' ')})`)
      return
    }
    const payload: ChancePayload = {
      picks: SUITS.map(s => ({ suit: s.key, card: picks[s.key]! })),
      stake,
      draws: draws ?? 1,
    }
    mutation.mutate(payload)
  }

  const picksCount  = SUITS.filter(s => picks[s.key]).length
  const effectiveDraws = draws ?? 1
  const totalCost   = stake !== null ? stake * effectiveDraws : null

  return (
    <div className="max-w-lg mx-auto space-y-4 pb-8" dir="rtl">
      <SubmitSuccessOverlay visible={showSuccess} />

      {/* Banner */}
      <div className="bg-gradient-to-l from-teal-700 to-teal-500 rounded-xl p-4 flex items-center gap-3">
        <span className="text-white text-3xl font-black">♠♥♦♣</span>
        <div>
          <h2 className="text-white font-bold text-lg">צ'אנס</h2>
          <p className="text-teal-100 text-sm">בחר קלף אחד בכל שורה</p>
        </div>
      </div>

      {/* Stake selector */}
      <div className="card p-4">
        <p className="text-sm font-bold text-gray-700 mb-3">יש לסמן את סכום ההשתתפות</p>
        <div className="flex flex-wrap gap-2">
          {STAKES.map(s => (
            <button
              key={s}
              type="button"
              onClick={() => { setValidErr(null); setStake(prev => prev === s ? null : s) }}
              className={`
                min-w-[48px] min-h-[44px] px-3 rounded-lg border text-sm font-semibold transition-all
                ${stake === s
                  ? 'bg-[--color-accent] text-white border-[--color-accent]'
                  : 'bg-white text-gray-700 border-[--color-border] hover:border-[--color-accent]'
                }
              `}
            >
              {s} ₪
            </button>
          ))}
        </div>
      </div>

      {/* Draws selector */}
      <div className="card p-4">
        <p className="text-sm font-bold text-gray-700 mb-3">
          יש לסמן מספר הגרלות{' '}
          <span className="text-gray-400 font-normal">(ללא סימן = הגרלה 1)</span>
        </p>
        <div className="flex flex-wrap gap-2">
          {DRAWS_OPTIONS.map(d => (
            <button
              key={d}
              type="button"
              onClick={() => setDraws(prev => prev === d ? null : d)}
              className={`
                min-w-[44px] min-h-[44px] px-3 rounded-lg border text-sm font-semibold transition-all
                ${draws === d
                  ? 'bg-[--color-accent] text-white border-[--color-accent]'
                  : 'bg-white text-gray-700 border-[--color-border] hover:border-[--color-accent]'
                }
              `}
            >
              {d}
            </button>
          ))}
        </div>
      </div>

      {/* Card grid — one suit per row */}
      <div className="card p-4 space-y-3">
        <p className="text-sm font-bold text-gray-700">יש לסמן קלף אחד בכל שורה</p>
        {SUITS.map(suit => (
          <div
            key={suit.key}
            className="flex items-center gap-2 p-2 rounded-lg border border-[--color-border]"
          >
            <span className={`text-2xl w-8 text-center flex-shrink-0 ${suit.color}`}>
              {suit.symbol}
            </span>
            <div className="flex flex-wrap gap-1.5 flex-1">
              {CARDS.map(card => {
                const isSelected = picks[suit.key] === card
                return (
                  <button
                    key={card}
                    type="button"
                    disabled={showSuccess}
                    onClick={() => toggleCard(suit.key, card)}
                    className={`
                      min-w-[36px] min-h-[36px] px-2 rounded-md border text-sm font-semibold
                      transition-all duration-100 active:scale-95
                      ${isSelected
                        ? 'bg-[--color-accent] text-white border-[--color-accent] scale-105'
                        : 'bg-white text-gray-700 border-[--color-border] hover:border-[--color-accent]'
                      }
                    `}
                  >
                    {card}
                  </button>
                )
              })}
            </div>
          </div>
        ))}
      </div>

      {/* Quick pick */}
      <button
        type="button"
        className="btn-secondary w-full"
        onClick={handleQuickPick}
        disabled={showSuccess}
      >
        🎲 לבחירת קלפים באופן אוטומטי
      </button>

      {/* Summary + submit */}
      <div className="card p-4 space-y-2">
        <div className="flex justify-between text-sm text-gray-500">
          <span>קלפים שנבחרו</span>
          <span className="font-semibold text-gray-800">{picksCount} / 4</span>
        </div>
        <div className="flex justify-between text-sm text-gray-500">
          <span>סכום × הגרלות</span>
          <span className="font-semibold text-gray-800">
            {stake !== null ? `${stake} ₪` : '—'} × {effectiveDraws}
          </span>
        </div>
        <div className="flex justify-between text-base font-bold border-t border-[--color-border] pt-2 mt-1">
          <span>סה"כ לתשלום</span>
          <span className="text-[--color-accent]">
            {totalCost !== null ? `${totalCost} ₪` : '—'}
          </span>
        </div>
        {validErr && (
          <p className="text-[--color-danger] text-sm font-semibold">⚠️ {validErr}</p>
        )}
        {mutation.isError && (
          <p className="text-[--color-danger] text-sm">{t('common.error')}</p>
        )}
        <button
          type="button"
          className="btn-primary w-full mt-2"
          onClick={handleSubmit}
          disabled={mutation.isPending || showSuccess}
        >
          {mutation.isPending ? t('common.loading') : t('common.submit')}
        </button>
      </div>
    </div>
  )
}
