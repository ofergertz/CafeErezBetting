import { useState, useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useMutation } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { TotoMatch, TotoPayload, WinnerPick, FormSubmittedDto } from '@/types'
import { SubmitSuccessOverlay } from '@/components/forms/SubmitSuccessOverlay'
import { Trophy } from 'lucide-react'

interface TotoRound {
  roundId: string
  roundNumber: number
  matches: TotoMatch[]
}

const MAX_COLUMNS = 14
const PICKS: WinnerPick[] = ['1', 'X', '2']

export default function TotoPage() {
  const { t } = useTranslation()
  const [columns, setColumns] = useState<Record<string, WinnerPick>[]>([{}])
  const [validationError, setValidationError] = useState<string | null>(null)
  const [showSuccess, setShowSuccess] = useState(false)
  // Fix 1: store timer ref to clear on unmount
  const successTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    return () => {
      if (successTimer.current) clearTimeout(successTimer.current)
    }
  }, [])

  const { data: round, isLoading, isError } = useQuery<TotoRound>({
    queryKey: ['toto-current'],
    queryFn: () => api.get<TotoRound>('/api/toto/current'),
    retry: 1,
  })

  const mutation = useMutation({
    mutationFn: (payload: TotoPayload) =>
      api.post<FormSubmittedDto>('/api/forms/toto', payload),
    onSuccess: () => {
      setShowSuccess(true)
      successTimer.current = setTimeout(() => {
        setShowSuccess(false)
        setColumns([{}])
      }, 2000)
    },
  })

  const handlePick = (colIdx: number, matchId: string, pick: WinnerPick) => {
    setValidationError(null)
    setColumns((prev) =>
      prev.map((col, i) => i === colIdx ? { ...col, [matchId]: pick } : col),
    )
  }

  const handleSubmit = () => {
    if (!round) return
    const allFilled = columns.every((col) =>
      round.matches.every((m) => col[m.id] !== undefined),
    )
    if (!allFilled) {
      setValidationError(t('toto.fillAll'))
      return
    }
    mutation.mutate({ roundId: round.roundId, columns: columns.map((picks) => ({ picks })) })
  }

  if (isLoading) return (
    <div className="card p-8 text-center text-gray-400"><p>{t('common.loading')}</p></div>
  )

  if (isError || !round) return (
    <div className="card p-8 text-center text-[--color-danger]"><p>{t('common.error')}</p></div>
  )

  return (
    <div className="max-w-5xl mx-auto space-y-4 pb-8">
      <SubmitSuccessOverlay visible={showSuccess} />

      <div className="bg-gradient-to-l from-blue-700 to-blue-500 rounded-xl p-4 flex items-center gap-3">
        <Trophy size={28} className="text-white" />
        <div>
          <h2 className="text-white font-bold text-lg">טוטו</h2>
          <p className="text-blue-100 text-sm">נחש את תוצאות המשחקים</p>
        </div>
      </div>

      <div className="card p-4 flex items-center justify-between flex-wrap gap-2">
        <div>
          <h1 className="text-2xl font-bold text-[--color-accent]">{t('toto.title')}</h1>
          <p className="text-sm text-gray-500">{t('toto.round', { round: round.roundNumber })}</p>
        </div>
        <div className="flex gap-2 items-center">
          {columns.length >= MAX_COLUMNS && (
            <span className="text-xs text-[--color-danger] font-semibold">{t('toto.maxColumns')}</span>
          )}
          <button type="button" className="btn-secondary"
            onClick={() => { if (columns.length < MAX_COLUMNS) setColumns((prev) => [...prev, {}]) }}
            disabled={columns.length >= MAX_COLUMNS || showSuccess}>
            + {t('toto.addColumn')}
          </button>
        </div>
      </div>

      <div className="card overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-[--color-border]">
              <th className="ps-4 pe-2 py-3 text-start font-semibold text-gray-600 min-w-[200px]" />
              {columns.map((_, colIdx) => (
                <th key={colIdx} className="px-2 py-3 text-center font-semibold min-w-[140px]">
                  <div className="flex flex-col items-center gap-1">
                    <span className="text-[--color-accent]">{colIdx + 1}</span>
                    {columns.length > 1 && (
                      // Fix 4: was min-h-[30px] → min-h-[44px]
                      <button type="button" className="btn-danger text-xs px-2 min-h-[44px] min-w-[44px]"
                        onClick={() => setColumns((prev) => prev.filter((_, i) => i !== colIdx))}
                        disabled={showSuccess}>
                        ✕
                      </button>
                    )}
                  </div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {round.matches.map((match, matchIdx) => (
              <tr key={match.id} className={matchIdx % 2 === 0 ? 'bg-white' : 'bg-gray-50'}>
                <td className="ps-4 pe-2 py-3">
                  <div className="font-semibold text-gray-800 leading-tight">{match.homeTeam}</div>
                  <div className="text-gray-400 text-xs">vs</div>
                  <div className="font-semibold text-gray-800 leading-tight">{match.awayTeam}</div>
                  <div className="text-xs text-gray-400 mt-0.5">{match.league}</div>
                </td>
                {columns.map((col, colIdx) => (
                  <td key={colIdx} className="px-2 py-3 text-center">
                    <div className="flex justify-center gap-1">
                      {PICKS.map((pick) => (
                        <button key={pick} type="button"
                          onClick={() => handlePick(colIdx, match.id, pick)}
                          disabled={showSuccess}
                          // Fix 6: 1/X/2 buttons are primary kiosk input — must be min 44px
                          className={`min-h-[44px] min-w-[44px] text-sm font-bold rounded-lg border transition-all
                            ${col[match.id] === pick
                              ? 'bg-[--color-accent] text-white border-[--color-accent]'
                              : 'bg-white text-gray-700 border-[--color-border] hover:border-[--color-accent] hover:text-[--color-accent]'
                            }`}>
                          {pick}
                        </button>
                      ))}
                    </div>
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="card p-4 space-y-2">
        {validationError && (
          <p className="text-[--color-danger] text-sm font-semibold">⚠️ {validationError}</p>
        )}
        {mutation.isError && <p className="text-[--color-danger] text-sm">{t('common.error')}</p>}
        <button type="button" className="btn-primary w-full" onClick={handleSubmit}
          disabled={mutation.isPending || showSuccess}>
          {mutation.isPending ? t('common.loading') : t('common.submit')}
        </button>
      </div>
    </div>
  )
}
