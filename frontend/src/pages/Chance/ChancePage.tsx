import { useState, useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { useMutation } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { ChancePayload, FormSubmittedDto } from '@/types'
import { NumberGrid } from '@/components/forms/NumberGrid'
import { SubmitSuccessOverlay } from '@/components/forms/SubmitSuccessOverlay'
import { Target } from 'lucide-react'

const COST_PER_ROW = 3.7
const MAX_ROWS = 10
const NUMBERS_MAX = 36
const PICK_COUNT = 5

function randomSample(pool: number[], count: number): number[] {
  const copy = [...pool]
  for (let i = copy.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[copy[i], copy[j]] = [copy[j], copy[i]]
  }
  return copy.slice(0, count)
}

export default function ChancePage() {
  const { t } = useTranslation()
  const [rows, setRows] = useState<number[][]>([[]])
  const [validationError, setValidationError] = useState<string | null>(null)
  const [showSuccess, setShowSuccess] = useState(false)
  // Fix 1: store timer ref to clear on unmount
  const successTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    return () => {
      if (successTimer.current) clearTimeout(successTimer.current)
    }
  }, [])

  const mutation = useMutation({
    mutationFn: (payload: ChancePayload) =>
      api.post<FormSubmittedDto>('/api/forms/chance', payload),
    onSuccess: () => {
      setShowSuccess(true)
      successTimer.current = setTimeout(() => {
        setShowSuccess(false)
        setRows([[]])
      }, 2000)
    },
  })

  const handleToggle = (rowIdx: number, n: number) => {
    setValidationError(null)
    setRows((prev) =>
      prev.map((row, i) => {
        if (i !== rowIdx) return row
        return row.includes(n)
          ? row.filter((x) => x !== n)
          : row.length < PICK_COUNT ? [...row, n] : row
      }),
    )
  }

  const handleQuickPick = (rowIdx: number) => {
    setRows((prev) =>
      prev.map((row, i) => {
        if (i !== rowIdx) return row
        const pool = Array.from({ length: NUMBERS_MAX }, (_, k) => k + 1).filter(n => !row.includes(n))
        return [...row, ...randomSample(pool, PICK_COUNT - row.length)]
      }),
    )
  }

  const handleSubmit = () => {
    if (!rows.every((r) => r.length === PICK_COUNT)) {
      setValidationError(t('chance.validation'))
      return
    }
    mutation.mutate({ rows: rows.map((numbers) => ({ numbers })), costPerRow: COST_PER_ROW })
  }

  const totalCost = rows.length * COST_PER_ROW

  return (
    <div className="max-w-3xl mx-auto space-y-4 pb-8">
      <SubmitSuccessOverlay visible={showSuccess} />

      <div className="bg-gradient-to-l from-teal-600 to-teal-400 rounded-xl p-4 mb-2 flex items-center gap-3">
        <Target size={28} className="text-white" />
        <div>
          <h2 className="text-white font-bold text-lg">צ'אנס</h2>
          <p className="text-teal-50 text-sm">בחר את המספרים שלך</p>
        </div>
      </div>

      <div className="card p-4">
        <h1 className="text-2xl font-bold text-[--color-accent]">{t('chance.title')}</h1>
      </div>

      {rows.map((row, rowIdx) => (
        <div key={rowIdx} className="card p-4 space-y-4">
          <div className="flex items-center justify-between">
            <span className="font-semibold text-sm text-gray-600">#{rowIdx + 1}</span>
            <div className="flex gap-2">
              {/* Fix 5: min-h-[44px] on kiosk buttons */}
              <button type="button" className="btn-secondary text-xs px-3 min-h-[44px]"
                onClick={() => handleQuickPick(rowIdx)}>
                {t('chance.quickPick')}
              </button>
              {rows.length > 1 && (
                <button type="button" className="btn-danger text-xs px-3 min-h-[44px]"
                  onClick={() => setRows((prev) => prev.filter((_, i) => i !== rowIdx))}>
                  ✕
                </button>
              )}
            </div>
          </div>
          <div>
            <p className="text-xs font-semibold text-gray-500 mb-2">
              {t('chance.numbers')} ({row.length}/{PICK_COUNT})
            </p>
            <NumberGrid max={NUMBERS_MAX} selected={row}
              onToggle={(n) => handleToggle(rowIdx, n)} disabled={showSuccess} />
          </div>
        </div>
      ))}

      <button type="button" className="btn-secondary"
        onClick={() => { if (rows.length < MAX_ROWS) setRows((prev) => [...prev, []]) }}
        disabled={rows.length >= MAX_ROWS}>
        + {t('chance.addRow')}
      </button>

      <div className="card p-4 space-y-2">
        <div className="flex justify-between text-sm">
          <span className="text-gray-500">{t('chance.costPerRow')}</span>
          <span className="font-semibold">{t('common.currency')}{COST_PER_ROW.toFixed(2)}</span>
        </div>
        <div className="flex justify-between text-base font-bold">
          {/* Fix 2: was t('lotto.totalCost') → correct key */}
          <span>{t('chance.totalCost')}</span>
          <span className="text-[--color-accent]">{t('common.currency')}{totalCost.toFixed(2)}</span>
        </div>
        {validationError && <p className="text-[--color-danger] text-sm font-semibold pt-1">⚠️ {validationError}</p>}
        {mutation.isError && <p className="text-[--color-danger] text-sm">{t('common.error')}</p>}
        <button type="button" className="btn-primary w-full mt-2" onClick={handleSubmit}
          disabled={mutation.isPending || showSuccess}>
          {mutation.isPending ? t('common.loading') : t('common.submit')}
        </button>
      </div>
    </div>
  )
}
