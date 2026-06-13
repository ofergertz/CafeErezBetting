import { useState, useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { useMutation } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { LottoPayload, LottoRow, FormSubmittedDto } from '@/types'
import { NumberGrid } from '@/components/forms/NumberGrid'
import { SubmitSuccessOverlay } from '@/components/forms/SubmitSuccessOverlay'
import { Sparkles } from 'lucide-react'

const COST_REGULAR     = 3
const COST_DOUBLE      = 6
const MAX_ROWS_REGULAR = 14  // 7 pairs — regular Lotto
const MAX_ROWS_DOUBLE  = 10  // 5 pairs — Lotto Double
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
  const [isDouble, setIsDouble]               = useState(false)
  const [rows, setRows]                       = useState<LottoRowState[]>([createEmptyRow(), createEmptyRow()])
  const [validationError, setValidationError] = useState<string | null>(null)
  const [showSuccess, setShowSuccess]         = useState(false)

  const COST_PER_ROW = isDouble ? COST_DOUBLE : COST_REGULAR
  const maxRows      = isDouble ? MAX_ROWS_DOUBLE : MAX_ROWS_REGULAR
  const successTimer = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    return () => { if (successTimer.current) clearTimeout(successTimer.current) }
  }, [])

  const mutation = useMutation({
    mutationFn: (payload: LottoPayload) =>
      api.post<FormSubmittedDto>('/api/forms/lotto', payload),
    onSuccess: () => {
      setShowSuccess(true)
      successTimer.current = setTimeout(() => {
        setShowSuccess(false)
        setRows([createEmptyRow(), createEmptyRow()])
      }, 2000)
    },
  })

  const handleNumberToggle = (rowIdx: number, n: number) => {
    setValidationError(null)
    setRows(prev =>
      prev.map((row, i) => {
        if (i !== rowIdx) return row
        const already = row.numbers.includes(n)
        const numbers = already
          ? row.numbers.filter(x => x !== n)
          : row.numbers.length < PICK_COUNT
          ? [...row.numbers, n]
          : row.numbers
        return { ...row, numbers }
      }),
    )
  }

  const handleStrongToggle = (rowIdx: number, n: number) => {
    setValidationError(null)
    setRows(prev =>
      prev.map((row, i) =>
        i === rowIdx ? { ...row, strong: row.strong === n ? null : n } : row,
      ),
    )
  }

  const handleQuickPick = (rowIdx: number) => {
    setRows(prev =>
      prev.map((row, i) => {
        if (i !== rowIdx) return row
        const needed = PICK_COUNT - row.numbers.length
        const pool = Array.from({ length: NUMBERS_MAX }, (_, k) => k + 1).filter(n => !row.numbers.includes(n))
        const numbers = [...row.numbers, ...randomSample(pool, needed)]
        const strong  = row.strong ?? (Math.floor(Math.random() * STRONG_MAX) + 1)
        return { ...row, numbers, strong }
      }),
    )
  }

  // When switching to Double, trim rows to its lower max (10)
  const handleSetDouble = (double: boolean) => {
    setIsDouble(double)
    if (double && rows.length > MAX_ROWS_DOUBLE)
      setRows(prev => prev.slice(0, MAX_ROWS_DOUBLE))
  }

  // Always adds 2 rows (one pair) — Lotto forms require even multiples of tables
  const addPair = () => {
    if (rows.length < maxRows)
      setRows(prev => [...prev, createEmptyRow(), createEmptyRow()])
  }

  // Removes both rows of a pair
  const removePair = (pairIdx: number) => {
    const start = pairIdx * 2
    setRows(prev => prev.filter((_, i) => i !== start && i !== start + 1))
  }

  const handleSubmit = () => {
    const valid = rows.every(r => r.numbers.length === PICK_COUNT && r.strong !== null)
    if (!valid) {
      setValidationError(t('lotto.validation'))
      return
    }
    const payload: LottoPayload = {
      rows: rows.map(r => ({ numbers: r.numbers, strong: r.strong as number } satisfies LottoRow)),
      costPerRow: COST_PER_ROW,
    }
    mutation.mutate(payload)
  }

  const totalCost = rows.length * COST_PER_ROW
  const pairCount = rows.length / 2

  return (
    <div className="max-w-3xl mx-auto space-y-4 pb-8">
      <SubmitSuccessOverlay visible={showSuccess} />

      {/* Banner */}
      <div className="bg-gradient-to-l from-purple-700 to-purple-500 rounded-xl p-4 mb-2 flex items-center gap-3">
        <Sparkles size={28} className="text-white" />
        <div className="flex-1">
          <h2 className="text-white font-bold text-lg">לוטו</h2>
          <p className="text-purple-100 text-sm">
            בחר 6 מספרים + חזק · {rows.length} טבלאות ({pairCount} {pairCount === 1 ? 'זוג' : 'זוגות'})
          </p>
        </div>
      </div>

      {/* Regular / Double toggle */}
      <div className="card p-3">
        <div className="flex gap-1 bg-gray-100 rounded-lg p-1">
          <button
            type="button"
            onClick={() => handleSetDouble(false)}
            className={`flex-1 py-2 rounded-md text-sm font-semibold transition-all ${
              !isDouble ? 'bg-white text-purple-700 shadow-sm' : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            לוטו רגיל · 3 ₪
          </button>
          <button
            type="button"
            onClick={() => handleSetDouble(true)}
            className={`flex-1 py-2 rounded-md text-sm font-semibold transition-all ${
              isDouble ? 'bg-white text-purple-700 shadow-sm' : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            לוטו דאבל · 6 ₪
          </button>
        </div>
      </div>

      {/* Pairs of tables */}
      {Array.from({ length: pairCount }, (_, pairIdx) => (
        <div key={pairIdx} className="space-y-3">
          {/* Pair header */}
          <div className="flex items-center justify-between px-1 pt-1">
            <span className="text-xs font-bold text-gray-500 uppercase tracking-wide">
              טבלאות {pairIdx * 2 + 1}–{pairIdx * 2 + 2}
            </span>
            {pairCount > 1 && (
              <button
                type="button"
                disabled={showSuccess}
                onClick={() => removePair(pairIdx)}
                className="text-xs text-red-500 hover:text-red-700 font-semibold px-2 py-1 rounded min-h-[32px] disabled:opacity-40"
              >
                הסר זוג ✕
              </button>
            )}
          </div>

          {/* Two row cards */}
          {[0, 1].map(offset => {
            const rowIdx = pairIdx * 2 + offset
            const row = rows[rowIdx]
            return (
              <div key={rowIdx} className="card p-4 space-y-4">
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-sm text-gray-600">טבלה #{rowIdx + 1}</span>
                  <button
                    type="button"
                    className="btn-secondary text-xs px-3 min-h-[44px]"
                    onClick={() => handleQuickPick(rowIdx)}
                    disabled={showSuccess}
                  >
                    {t('lotto.quickPick')}
                  </button>
                </div>

                <div>
                  <p className="text-xs font-semibold text-gray-500 mb-2">
                    {t('lotto.numbers')} ({row.numbers.length}/{PICK_COUNT})
                  </p>
                  <NumberGrid
                    max={NUMBERS_MAX}
                    selected={row.numbers}
                    onToggle={n => handleNumberToggle(rowIdx, n)}
                    disabled={showSuccess}
                  />
                </div>

                <div>
                  <p className="text-xs font-semibold text-amber-600 mb-2">{t('lotto.strong')}</p>
                  <NumberGrid
                    max={STRONG_MAX}
                    selected={row.strong !== null ? [row.strong] : []}
                    onToggle={n => handleStrongToggle(rowIdx, n)}
                    isStrong
                    disabled={showSuccess}
                  />
                </div>
              </div>
            )
          })}
        </div>
      ))}

      {/* Add pair button */}
      <button
        type="button"
        className="btn-secondary w-full"
        onClick={addPair}
        disabled={rows.length >= maxRows || showSuccess}
      >
        + הוסף 2 טבלאות
        <span className="text-xs text-gray-400 me-1.5">({pairCount}/{maxRows / 2} זוגות)</span>
      </button>

      {/* Summary + submit */}
      <div className="card p-4 space-y-2">
        <div className="flex justify-between text-sm text-gray-500">
          <span>{t('lotto.costPerRow')}</span>
          <span className="font-semibold text-gray-800">{t('common.currency')}{COST_PER_ROW.toFixed(2)}</span>
        </div>
        <div className="flex justify-between text-sm text-gray-500">
          <span>מספר טבלאות</span>
          <span className="font-semibold text-gray-800">{rows.length}</span>
        </div>
        <div className="flex justify-between text-base font-bold border-t border-[--color-border] pt-2 mt-1">
          <span>{t('lotto.totalCost')}</span>
          <span className="text-[--color-accent]">{t('common.currency')}{totalCost.toFixed(2)}</span>
        </div>
        {validationError && (
          <p className="text-[--color-danger] text-sm font-semibold pt-1">⚠️ {validationError}</p>
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
