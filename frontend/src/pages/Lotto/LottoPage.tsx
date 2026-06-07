import { useState, useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { useMutation } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { LottoPayload, LottoRow, FormSubmittedDto } from '@/types'
import { NumberGrid } from '@/components/forms/NumberGrid'
import { SubmitSuccessOverlay } from '@/components/forms/SubmitSuccessOverlay'

const COST_PER_ROW = 8
const MAX_ROWS = 10
const NUMBERS_MAX = 37
const STRONG_MAX = 7
const PICK_COUNT = 6

interface LottoRowState {
  numbers: number[]
  strong: number | null
}

function createEmptyRow(): LottoRowState {
  return { numbers: [], strong: null }
}

function randomSample(pool: number[], count: number): number[] {
  const copy = [...pool]
  for (let i = copy.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[copy[i], copy[j]] = [copy[j], copy[i]]
  }
  return copy.slice(0, count)
}

export default function LottoPage() {
  const { t } = useTranslation()
  const [rows, setRows] = useState<LottoRowState[]>([createEmptyRow()])
  const [validationError, setValidationError] = useState<string | null>(null)
  const [showSuccess, setShowSuccess] = useState(false)
  // Fix 1: store timer ref to clear on unmount (memory leak prevention)
  const successTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    return () => {
      if (successTimer.current) clearTimeout(successTimer.current)
    }
  }, [])

  const mutation = useMutation({
    mutationFn: (payload: LottoPayload) =>
      api.post<FormSubmittedDto>('/api/forms/lotto', payload),
    onSuccess: () => {
      setShowSuccess(true)
      successTimer.current = setTimeout(() => {
        setShowSuccess(false)
        setRows([createEmptyRow()])
      }, 2000)
    },
  })

  const handleNumberToggle = (rowIdx: number, n: number) => {
    setValidationError(null)
    setRows((prev) =>
      prev.map((row, i) => {
        if (i !== rowIdx) return row
        const already = row.numbers.includes(n)
        const numbers = already
          ? row.numbers.filter((x) => x !== n)
          : row.numbers.length < PICK_COUNT
          ? [...row.numbers, n]
          : row.numbers
        return { ...row, numbers }
      }),
    )
  }

  const handleStrongToggle = (rowIdx: number, n: number) => {
    setValidationError(null)
    setRows((prev) =>
      prev.map((row, i) =>
        i === rowIdx ? { ...row, strong: row.strong === n ? null : n } : row,
      ),
    )
  }

  const handleQuickPick = (rowIdx: number) => {
    setRows((prev) =>
      prev.map((row, i) => {
        if (i !== rowIdx) return row
        const needed = PICK_COUNT - row.numbers.length
        const pool = Array.from({ length: NUMBERS_MAX }, (_, k) => k + 1).filter(
          (n) => !row.numbers.includes(n),
        )
        const extra = randomSample(pool, needed)
        const numbers = [...row.numbers, ...extra]
        const strong = row.strong ?? (Math.floor(Math.random() * STRONG_MAX) + 1)
        return { ...row, numbers, strong }
      }),
    )
  }

  const handleSubmit = () => {
    const valid = rows.every((r) => r.numbers.length === PICK_COUNT && r.strong !== null)
    if (!valid) {
      setValidationError(t('lotto.validation'))
      return
    }
    const payload: LottoPayload = {
      rows: rows.map((r) => ({ numbers: r.numbers, strong: r.strong as number } satisfies LottoRow)),
      costPerRow: COST_PER_ROW,
    }
    mutation.mutate(payload)
  }

  const totalCost = rows.length * COST_PER_ROW

  return (
    <div className="max-w-3xl mx-auto space-y-4 pb-8">
      <SubmitSuccessOverlay visible={showSuccess} />

      <div className="card p-4">
        <h1 className="text-2xl font-bold text-[--color-accent]">{t('lotto.title')}</h1>
      </div>

      {rows.map((row, rowIdx) => (
        <div key={rowIdx} className="card p-4 space-y-4">
          <div className="flex items-center justify-between">
            <span className="font-semibold text-sm text-gray-600">#{rowIdx + 1}</span>
            <div className="flex gap-2">
              {/* Fix 5: min-h-[44px] on kiosk buttons */}
              <button type="button" className="btn-secondary text-xs px-3 min-h-[44px]"
                onClick={() => handleQuickPick(rowIdx)}>
                {t('lotto.quickPick')}
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
              {t('lotto.numbers')} ({row.numbers.length}/{PICK_COUNT})
            </p>
            <NumberGrid max={NUMBERS_MAX} selected={row.numbers}
              onToggle={(n) => handleNumberToggle(rowIdx, n)} disabled={showSuccess} />
          </div>

          <div>
            <p className="text-xs font-semibold text-amber-600 mb-2">{t('lotto.strong')}</p>
            <NumberGrid max={STRONG_MAX} selected={row.strong !== null ? [row.strong] : []}
              onToggle={(n) => handleStrongToggle(rowIdx, n)} isStrong disabled={showSuccess} />
          </div>
        </div>
      ))}

      <button type="button" className="btn-secondary" onClick={() => {
        if (rows.length < MAX_ROWS) setRows((prev) => [...prev, createEmptyRow()])
      }} disabled={rows.length >= MAX_ROWS}>
        + {t('lotto.addRow')}
      </button>

      <div className="card p-4 space-y-2">
        <div className="flex justify-between text-sm">
          <span className="text-gray-500">{t('lotto.costPerRow')}</span>
          <span className="font-semibold">{t('common.currency')}{COST_PER_ROW.toFixed(2)}</span>
        </div>
        <div className="flex justify-between text-base font-bold">
          <span>{t('lotto.totalCost')}</span>
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
