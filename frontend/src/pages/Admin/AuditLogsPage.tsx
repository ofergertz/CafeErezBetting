import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '@/lib/api'

interface AuditLog {
  id: string
  userId: string
  role: string
  action: string
  ipAddress: string
  createdAt: string
}

interface AuditLogsResponse {
  items: AuditLog[]
  total: number
  page: number
  pageSize: number
}

const ACTIONS = ['login', 'logout', 'create', 'update', 'delete', 'view']

export default function AuditLogsPage() {
  const { t } = useTranslation()
  const [page, setPage] = useState(1)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [action, setAction] = useState('')

  const params = new URLSearchParams({
    page: String(page),
    pageSize: '50',
    ...(from ? { from } : {}),
    ...(to ? { to } : {}),
    ...(action ? { action } : {}),
  })

  const { data, isLoading, isError } = useQuery({
    queryKey: ['audit-logs', page, from, to, action],
    queryFn: () => api.get<AuditLogsResponse>(`/api/audit-logs?${params}`),
  })

  const totalPages = data ? Math.ceil(data.total / data.pageSize) : 1

  return (
    <div dir="rtl" className="p-4 max-w-6xl mx-auto">
      <h1 className="text-2xl font-bold mb-6">{t('auditLogs.title')}</h1>

      {/* Filters */}
      <div className="flex flex-wrap gap-3 mb-6 bg-white p-4 rounded-xl border border-gray-200">
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">{t('auditLogs.from')}</label>
          <input
            type="datetime-local"
            value={from}
            onChange={(e) => { setFrom(e.target.value); setPage(1) }}
            className="border rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">{t('auditLogs.to')}</label>
          <input
            type="datetime-local"
            value={to}
            onChange={(e) => { setTo(e.target.value); setPage(1) }}
            className="border rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">{t('auditLogs.action')}</label>
          <select
            value={action}
            onChange={(e) => { setAction(e.target.value); setPage(1) }}
            className="border rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
          >
            <option value="">{t('forms.filterAll')}</option>
            {ACTIONS.map(a => (
              <option key={a} value={a}>{a}</option>
            ))}
          </select>
        </div>
        <div className="flex items-end">
          <button
            onClick={() => { setFrom(''); setTo(''); setAction(''); setPage(1) }}
            className="btn btn-secondary text-sm"
          >
            {t('common.clearFilter')}
          </button>
        </div>
      </div>

      {isLoading && (
        <div className="text-center py-10 text-gray-400">{t('common.loading')}</div>
      )}
      {isError && (
        <div className="text-center py-10 text-red-500">{t('auditLogs.errorLoading')}</div>
      )}

      {data && (
        <>
          <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  <th className="px-4 py-3 text-right font-medium text-gray-500">{t('auditLogs.timestamp')}</th>
                  <th className="px-4 py-3 text-right font-medium text-gray-500">{t('auditLogs.user')}</th>
                  <th className="px-4 py-3 text-right font-medium text-gray-500">{t('auditLogs.action')}</th>
                  <th className="px-4 py-3 text-right font-medium text-gray-500">{t('auditLogs.ip')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {data.items.length === 0 ? (
                  <tr>
                    <td colSpan={4} className="text-center py-10 text-gray-400">{t('common.noData')}</td>
                  </tr>
                ) : (
                  data.items.map((log) => (
                    <tr key={log.id} className="hover:bg-gray-50 transition-colors">
                      <td className="px-4 py-3 text-gray-600 whitespace-nowrap" dir="ltr">
                        {new Date(log.createdAt).toLocaleString('he-IL')}
                      </td>
                      <td className="px-4 py-3 text-gray-800">
                        <span className="font-medium">{log.userId}</span>
                        <span className="ml-1 text-xs text-gray-400">({log.role})</span>
                      </td>
                      <td className="px-4 py-3">
                        <span className="bg-blue-50 text-blue-700 px-2 py-0.5 rounded text-xs font-medium">
                          {log.action}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-gray-500 font-mono text-xs" dir="ltr">
                        {log.ipAddress}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-center gap-2 mt-4">
              <button
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={page === 1}
                className="btn btn-secondary text-sm disabled:opacity-50"
              >
                {t('common.prev')}
              </button>
              <span className="text-sm text-gray-500">
                {page} / {totalPages}
              </span>
              <button
                onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                disabled={page === totalPages}
                className="btn btn-secondary text-sm disabled:opacity-50"
              >
                {t('common.next')}
              </button>
            </div>
          )}
          <p className="text-xs text-gray-400 text-center mt-2">{t('auditLogs.totalRecords', { count: data.total })}</p>
        </>
      )}
    </div>
  )
}
