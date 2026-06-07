import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useMutation } from '@tanstack/react-query'
import { useBetSlipStore } from '@/store/betSlipStore'
import { useAuthStore } from '@/store/authStore'
import { api } from '@/lib/api'
import type { FormSubmittedDto } from '@/types'

interface Props { onSubmit?: () => void }

export default function BetSlip({ onSubmit }: Props) {
  const { t } = useTranslation()
  const { items, stake, setStake, remove, clear, totalOdds, potentialWin } = useBetSlipStore()
  const user = useAuthStore(s => s.user)
  const [submitted, setSubmitted] = useState(false)

  const mutation = useMutation({
    mutationFn: () => api.post<FormSubmittedDto>('/api/forms/winner', {
      bets: items.map(i => ({
        matchId:  i.matchId,
        homeTeam: i.homeTeam,
        awayTeam: i.awayTeam,
        pick:     i.pick,
        odds:     i.odds,
      })),
      stake,
      totalOdds: totalOdds(),
      potentialWin: potentialWin(),
      customerId: user?.role === 'customer' ? user.id : null,
    }),
    onSuccess: () => {
      setSubmitted(true)
      setTimeout(() => {
        clear()
        setSubmitted(false)
        onSubmit?.()
      }, 2000)
    },
  })

  const canSubmit = items.length > 0 && stake > 0 && !mutation.isPending

  if (submitted) return (
    <div className="card p-6 text-center">
      <div className="text-4xl mb-3">✅</div>
      <p className="font-bold text-[--color-success]">{t('forms.status.received')}</p>
      <p className="text-xs text-gray-400 mt-1">{t('forms.newForm')}</p>
    </div>
  )

  if (items.length === 0) return (
    <div className="card p-6 text-center text-gray-400">
      <p className="text-3xl mb-2">🎯</p>
      <p className="text-sm">{t('winner.betSlip')}</p>
      <p className="text-xs mt-1 opacity-60">לחץ על יחס להוספה</p>
    </div>
  )

  const oddsTotal = totalOdds()
  const win = potentialWin()

  return (
    <div className="card overflow-hidden">
      {/* Header */}
      <div className="bg-[--color-accent] text-white px-4 py-3 flex items-center justify-between">
        <h3 className="font-bold text-sm">{t('winner.betSlip')}</h3>
        <button onClick={clear} className="text-white/70 text-xs hover:text-white">
          {t('winner.clearAll')}
        </button>
      </div>

      {/* Bets list */}
      <div className="divide-y divide-[--color-border]">
        {items.map(item => (
          <div key={item.matchId} className="px-4 py-3 flex items-start justify-between gap-2">
            <div className="flex-1 min-w-0">
              <p className="text-xs font-semibold truncate">{item.homeTeam} - {item.awayTeam}</p>
              <p className="text-xs text-gray-500 mt-0.5">
                {item.pick === '1' ? item.homeTeam : item.pick === '2' ? item.awayTeam : t('winner.pickX')}
                {' · '}
                <span className="font-bold text-[--color-accent]">{item.odds.toFixed(2)}</span>
              </p>
            </div>
            <button
              onClick={() => remove(item.matchId)}
              className="text-gray-300 hover:text-red-500 text-lg leading-none flex-shrink-0"
            >
              ✕
            </button>
          </div>
        ))}
      </div>

      {/* Summary + stake */}
      <div className="p-4 space-y-3 bg-gray-50">
        {/* Total odds */}
        <div className="flex justify-between text-sm">
          <span className="text-gray-500">{t('winner.totalOdds')}</span>
          <span className="font-bold text-[--color-accent]">{oddsTotal.toFixed(2)}</span>
        </div>

        {/* Stake input */}
        <div>
          <label className="text-xs text-gray-500 block mb-1">{t('winner.stake')}</label>
          <div className="relative">
            <input
              type="number"
              min="1"
              step="1"
              value={stake || ''}
              onChange={e => setStake(Math.max(0, parseFloat(e.target.value) || 0))}
              placeholder="0"
              dir="ltr"
              className="w-full border border-[--color-border] rounded-lg px-3 py-2 text-sm pe-7
                         focus:outline-none focus:ring-2 focus:ring-[--color-accent]"
            />
            <span className="absolute end-2.5 top-1/2 -translate-y-1/2 text-gray-400 text-sm">₪</span>
          </div>
        </div>

        {/* Potential win */}
        {stake > 0 && (
          <div className="flex justify-between text-sm">
            <span className="text-gray-500">{t('winner.potentialWin')}</span>
            <span className="font-bold text-[--color-success]">{win.toFixed(2)} ₪</span>
          </div>
        )}

        {/* Error */}
        {mutation.isError && (
          <p className="text-red-500 text-xs">{t('common.error')}</p>
        )}

        {/* Submit */}
        <button
          onClick={() => mutation.mutate()}
          disabled={!canSubmit}
          className="btn-primary w-full"
        >
          {mutation.isPending ? t('common.loading') : t('winner.submit')}
        </button>

        {items.length === 0 && (
          <p className="text-xs text-center text-gray-400">{t('winner.minOneBet')}</p>
        )}
        {stake === 0 && items.length > 0 && (
          <p className="text-xs text-center text-amber-600">{t('winner.minStake')}</p>
        )}
      </div>
    </div>
  )
}
