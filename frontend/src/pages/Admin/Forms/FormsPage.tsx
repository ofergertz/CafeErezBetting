import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '@/lib/api'
import { useNotificationsHub } from '@/hooks/useSignalR'

// ── Types ─────────────────────────────────────────────────────────────────────
// Matches the FormSummaryDto returned by GET /api/forms (camelCase, lowercase status/type)
interface BettingForm {
  id: string
  type: string
  customerName?: string | null
  status: 'pending' | 'received' | 'approved' | 'sent'
  submittedAt: string
  receivedAt?: string
  approvedAt?: string
  sentAt?: string
}

const FORM_STATUSES = ['pending', 'received', 'approved', 'sent'] as const
const FORM_TYPES = ['winner', 'toto', 'lotto', 'chance', 'lucky777'] as const

// ── Status Badge ──────────────────────────────────────────────────────────────
function StatusBadge({ status }: { status: BettingForm['status'] }) {
  const { t } = useTranslation()
  const map: Record<BettingForm['status'], { label: string; cls: string }> = {
    pending:  { label: t('forms.status.pending'),  cls: 'bg-gray-100 text-gray-600' },
    received: { label: t('forms.status.received'), cls: 'bg-orange-100 text-orange-700' },
    approved: { label: t('forms.status.approved'), cls: 'bg-blue-100 text-blue-700' },
    sent:     { label: t('forms.status.sent'),     cls: 'bg-green-100 text-green-700' },
  }
  const entry = map[status] ?? { label: status, cls: 'bg-gray-100 text-gray-600' }
  return <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${entry.cls}`}>{entry.label}</span>
}

// ── Format date ───────────────────────────────────────────────────────────────
function formatDate(dateStr: string) {
  try {
    const d = new Date(dateStr)
    return d.toLocaleString('he-IL', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
  } catch {
    return dateStr
  }
}

// ── Main Page ─────────────────────────────────────────────────────────────────
export default function FormsPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const [filterStatus, setFilterStatus] = useState('')
  const [filterType, setFilterType] = useState('')
  const [filterDate, setFilterDate] = useState('')

  // Build query params
  const params = new URLSearchParams()
  if (filterStatus) params.set('status', filterStatus)
  if (filterType) params.set('type', filterType)
  if (filterDate) params.set('date', filterDate)
  const queryString = params.toString() ? `?${params.toString()}` : ''

  // API returns BettingForm[] directly (not {forms: BettingForm[]})
  const { data, isLoading, isError } = useQuery({
    queryKey: ['forms', filterStatus, filterType, filterDate],
    queryFn: () => api.get<BettingForm[]>(`/api/forms${queryString}`),
  })

  const statusMutation = useMutation({
    mutationFn: ({ id, status }: { id: string; status: string }) =>
      api.patch(`/api/forms/${id}/status`, { status }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['forms'] })
    },
  })

  // Real-time SignalR: refresh list on any form event
  useNotificationsHub({
    onNewForm: () => {
      queryClient.invalidateQueries({ queryKey: ['forms'] })
    },
    onFormStatusChanged: () => {
      queryClient.invalidateQueries({ queryKey: ['forms'] })
    },
  })

  const forms = data ?? []

  return (
    <div dir="rtl" className="p-4 max-w-5xl mx-auto">
      {/* Header */}
      <div className="mb-4">
        <h1 className="text-xl font-bold">{t('forms.title')}</h1>
      </div>

      {/* Filters */}
      <div className="bg-white rounded-xl shadow p-4 mb-4 flex flex-wrap gap-3 items-end">
        {/* Status filter */}
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">{t('common.filter')} {t('common.status')}</label>
          <select
            className="border rounded-lg px-3 py-2 text-sm"
            value={filterStatus}
            onChange={(e) => setFilterStatus(e.target.value)}
          >
            <option value="">{t('forms.filterAll')}</option>
            {FORM_STATUSES.map((s) => (
              <option key={s} value={s}>{t(`forms.status.${s}`)}</option>
            ))}
          </select>
        </div>

        {/* Type filter */}
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">{t('common.filter')} {t('forms.type')}</label>
          <select
            className="border rounded-lg px-3 py-2 text-sm"
            value={filterType}
            onChange={(e) => setFilterType(e.target.value)}
          >
            <option value="">{t('forms.filterAll')}</option>
            {FORM_TYPES.map((type) => (
              <option key={type} value={type}>{type}</option>
            ))}
          </select>
        </div>

        {/* Date filter */}
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">{t('forms.filterDate')}</label>
          <input
            type="date"
            className="border rounded-lg px-3 py-2 text-sm"
            value={filterDate}
            onChange={(e) => setFilterDate(e.target.value)}
          />
        </div>

        {/* Clear filters */}
        {(filterStatus || filterType || filterDate) && (
          <button
            onClick={() => { setFilterStatus(''); setFilterType(''); setFilterDate('') }}
            className="text-sm text-gray-500 underline"
          >
            {t('common.clearFilter')}
          </button>
        )}
      </div>

      {/* Table */}
      {isLoading && <p className="text-center text-gray-400 py-8">{t('common.loading')}</p>}
      {isError && <p className="text-center text-red-500 py-8">{t('common.error')}</p>}

      {!isLoading && !isError && (
        <div className="bg-white rounded-xl shadow overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-gray-500 text-xs uppercase">
              <tr>
                <th className="px-4 py-3 text-right">{t('forms.customer')}</th>
                <th className="px-4 py-3 text-right">{t('forms.type')}</th>
                <th className="px-4 py-3 text-right">{t('forms.submittedAt')}</th>
                <th className="px-4 py-3 text-right">{t('common.status')}</th>
                <th className="px-4 py-3 text-right">{t('forms.actions')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {forms.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-center text-gray-400">{t('common.noData')}</td>
                </tr>
              )}
              {forms.map((form) => {
                const customerName = form.customerName ?? t('forms.anonymous')

                // Forward-only workflow: pending → received → approved → sent
                const isPending  = form.status === 'pending'
                const isApproved = form.status === 'approved' || form.status === 'sent'
                const isSent     = form.status === 'sent'

                return (
                  <tr key={form.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3 font-medium">{customerName}</td>
                    <td className="px-4 py-3 text-gray-600 capitalize">{form.type}</td>
                    <td className="px-4 py-3 text-gray-500 text-xs">{formatDate(form.submittedAt)}</td>
                    <td className="px-4 py-3"><StatusBadge status={form.status} /></td>
                    <td className="px-4 py-3">
                      <div className="flex gap-1 flex-wrap">
                        {/* Acknowledge — enabled only for pending forms */}
                        <button
                          disabled={!isPending || statusMutation.isPending}
                          onClick={() => statusMutation.mutate({ id: form.id, status: 'received' })}
                          className="text-xs px-2 py-1 rounded bg-orange-100 text-orange-700 hover:bg-orange-200 disabled:opacity-40 disabled:cursor-not-allowed"
                        >
                          {t('forms.acknowledge')}
                        </button>

                        {/* Approve */}
                        <button
                          disabled={isApproved || statusMutation.isPending}
                          onClick={() => statusMutation.mutate({ id: form.id, status: 'approved' })}
                          className="text-xs px-2 py-1 rounded bg-blue-100 text-blue-700 hover:bg-blue-200 disabled:opacity-40 disabled:cursor-not-allowed"
                        >
                          {t('forms.approve')}
                        </button>

                        {/* Mark Sent */}
                        <button
                          disabled={isSent || statusMutation.isPending}
                          onClick={() => statusMutation.mutate({ id: form.id, status: 'sent' })}
                          className="text-xs px-2 py-1 rounded bg-green-100 text-green-700 hover:bg-green-200 disabled:opacity-40 disabled:cursor-not-allowed"
                        >
                          {t('forms.markSent')}
                        </button>
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
