import { useState, useRef, useCallback } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '@/lib/api'
import { useNotificationsHub } from '@/hooks/useSignalR'

// ── Types ─────────────────────────────────────────────────────────────────────
interface BettingForm {
  id: string
  type: string
  customerId?: string
  customer?: { firstName: string; lastName: string }
  status: 'Received' | 'Approved' | 'Sent'
  submittedAt: string
  receivedAt?: string
  approvedAt?: string
  sentAt?: string
}

interface Toast {
  id: number
  message: string
}

// ── Helpers ───────────────────────────────────────────────────────────────────
function formatTime(dateStr: string) {
  try {
    const d = new Date(dateStr)
    const hh = String(d.getHours()).padStart(2, '0')
    const mm = String(d.getMinutes()).padStart(2, '0')
    const dd = String(d.getDate()).padStart(2, '0')
    const mo = String(d.getMonth() + 1).padStart(2, '0')
    return `${hh}:${mm} ${dd}/${mo}`
  } catch {
    return dateStr
  }
}

// ── Toast Component ───────────────────────────────────────────────────────────
function ToastStack({ toasts, onDismiss }: { toasts: Toast[]; onDismiss: (id: number) => void }) {
  return (
    <div className="fixed top-4 left-4 z-50 flex flex-col gap-2">
      {toasts.map((toast) => (
        <div
          key={toast.id}
          className="bg-white border border-gray-200 rounded-xl shadow-lg px-4 py-3 flex items-center gap-3 min-w-64 transition-all duration-300"
        >
          <span className="text-xl">🔔</span>
          <span className="text-sm font-medium flex-1">{toast.message}</span>
          <button onClick={() => onDismiss(toast.id)} className="text-gray-400 hover:text-gray-600">✕</button>
        </div>
      ))}
    </div>
  )
}

// ── Form Card ─────────────────────────────────────────────────────────────────
function FormCard({
  form,
  onAction,
  isPending,
}: {
  form: BettingForm
  onAction?: () => void
  isPending: boolean
}) {
  const { t } = useTranslation()
  const customerName = form.customer
    ? `${form.customer.firstName} ${form.customer.lastName}`
    : 'אנונימי'

  return (
    <div className="bg-white rounded-xl shadow p-4 flex flex-col gap-2">
      <div className="flex justify-between items-start">
        <span className="font-semibold text-sm">{customerName}</span>
        <span className="text-xs text-gray-400">{formatTime(form.submittedAt)}</span>
      </div>
      <span className="text-xs bg-gray-100 text-gray-600 rounded px-2 py-0.5 w-fit capitalize">{form.type}</span>
      {onAction ? (
        <button
          disabled={isPending}
          onClick={onAction}
          className="mt-1 text-xs px-3 py-1.5 rounded-lg bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed w-fit"
        >
          {form.status === 'Received' ? t('forms.approve') : t('forms.markSent')}
        </button>
      ) : (
        <span className="mt-1 text-xs text-green-600 font-semibold">נשלח ✓</span>
      )}
    </div>
  )
}

// ── Panel ─────────────────────────────────────────────────────────────────────
function Panel({
  title,
  count,
  color,
  children,
}: {
  title: string
  count: number
  color: 'orange' | 'blue' | 'green'
  children: React.ReactNode
}) {
  const headerColors: Record<string, string> = {
    orange: 'bg-orange-500',
    blue: 'bg-blue-500',
    green: 'bg-green-500',
  }
  const badgeColors: Record<string, string> = {
    orange: 'bg-orange-200 text-orange-800',
    blue: 'bg-blue-200 text-blue-800',
    green: 'bg-green-200 text-green-800',
  }
  return (
    <div className="flex flex-col rounded-xl overflow-hidden shadow-md h-full min-h-0">
      <div className={`${headerColors[color]} text-white px-4 py-3 flex items-center justify-between`}>
        <span className="font-bold text-base">{title}</span>
        <span className={`${badgeColors[color]} rounded-full px-2.5 py-0.5 text-sm font-bold`}>{count}</span>
      </div>
      <div className="bg-gray-50 flex-1 overflow-y-auto p-3 flex flex-col gap-3">
        {children}
      </div>
    </div>
  )
}

// ── Sync Status ───────────────────────────────────────────────────────────────
function SyncStatus() {
  const { t } = useTranslation()
  const { data } = useQuery({
    queryKey: ['winner-sync'],
    queryFn: () => api.get<{ lastSync: string | null }>('/api/winner/sync-status'),
    refetchInterval: 60_000,
  })

  const lastSync = data?.lastSync
  let label = 'אין מידע'
  if (lastSync) {
    try {
      const d = new Date(lastSync)
      label = d.toLocaleTimeString('he-IL', { hour: '2-digit', minute: '2-digit' })
    } catch {
      label = lastSync
    }
  }

  return (
    <div className="fixed bottom-4 left-4 bg-white border border-gray-200 rounded-xl shadow px-4 py-2 text-xs text-gray-500 flex items-center gap-2">
      <span>🔄</span>
      <span>{t('common.lastSync')}: <strong>{label}</strong></span>
    </div>
  )
}

// ── Main Page ─────────────────────────────────────────────────────────────────
export default function KioskPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [toasts, setToasts] = useState<Toast[]>([])
  // Fix: useRef counter avoids stale closure on nextToastId
  const nextToastIdRef = useRef(1)

  const addToast = useCallback((message: string) => {
    const id = nextToastIdRef.current++
    setToasts((prev) => [...prev, { id, message }])
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id))
    }, 5000)
  }, [])

  const dismissToast = (id: number) => {
    setToasts((prev) => prev.filter((t) => t.id !== id))
  }

  // Fetch all forms
  const { data } = useQuery({
    queryKey: ['forms-kiosk'],
    queryFn: () => api.get<{ forms: BettingForm[] }>('/api/forms'),
    refetchInterval: 30_000,
  })

  const statusMutation = useMutation({
    mutationFn: ({ id, status }: { id: string; status: string }) =>
      api.patch(`/api/forms/${id}/status`, { status }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['forms-kiosk'] })
    },
  })

  // Real-time SignalR
  useNotificationsHub({
    onNewForm: (rawData) => {
      // Play sound
      new Audio('/notification.mp3').play().catch(() => {})

      // Show toast
      const formData = rawData as { customer?: { firstName: string; lastName: string }; type?: string }
      const customerName = formData?.customer
        ? `${formData.customer.firstName} ${formData.customer.lastName}`
        : 'אנונימי'
      const formType = formData?.type ?? 'טופס'
      addToast(`${t('forms.newForm')} — ${customerName} (${formType})`)

      queryClient.invalidateQueries({ queryKey: ['forms-kiosk'] })
    },
    onFormStatusChanged: () => {
      queryClient.invalidateQueries({ queryKey: ['forms-kiosk'] })
    },
  })

  const forms = data?.forms ?? []
  const receivedForms = forms.filter((f) => f.status === 'Received')
  const approvedForms = forms.filter((f) => f.status === 'Approved')
  const sentForms = forms.filter((f) => f.status === 'Sent')

  return (
    <div dir="rtl" className="h-screen flex flex-col bg-gray-100 overflow-hidden">
      {/* Page header */}
      <div className="bg-gray-800 text-white px-6 py-3 flex items-center justify-between flex-shrink-0">
        <h1 className="text-lg font-bold">{t('nav.kiosk')}</h1>
        <span className="text-gray-400 text-sm">{new Date().toLocaleDateString('he-IL')}</span>
      </div>

      {/* Three panel columns */}
      <div className="flex-1 grid grid-cols-3 gap-4 p-4 min-h-0">
        {/* Received panel */}
        <Panel title={t('forms.status.received')} count={receivedForms.length} color="orange">
          {receivedForms.length === 0 && (
            <p className="text-center text-gray-400 text-sm py-4">{t('common.noData')}</p>
          )}
          {receivedForms.map((form) => (
            <FormCard
              key={form.id}
              form={form}
              isPending={statusMutation.isPending}
              onAction={() => statusMutation.mutate({ id: form.id, status: 'approved' })}
            />
          ))}
        </Panel>

        {/* Approved panel */}
        <Panel title={t('forms.status.approved')} count={approvedForms.length} color="blue">
          {approvedForms.length === 0 && (
            <p className="text-center text-gray-400 text-sm py-4">{t('common.noData')}</p>
          )}
          {approvedForms.map((form) => (
            <FormCard
              key={form.id}
              form={form}
              isPending={statusMutation.isPending}
              onAction={() => statusMutation.mutate({ id: form.id, status: 'sent' })}
            />
          ))}
        </Panel>

        {/* Sent panel */}
        <Panel title={t('forms.status.sent')} count={sentForms.length} color="green">
          {sentForms.length === 0 && (
            <p className="text-center text-gray-400 text-sm py-4">{t('common.noData')}</p>
          )}
          {sentForms.map((form) => (
            <FormCard
              key={form.id}
              form={form}
              isPending={false}
              onAction={undefined}
            />
          ))}
        </Panel>
      </div>

      {/* Toast notifications */}
      <ToastStack toasts={toasts} onDismiss={dismissToast} />

      {/* Sync status */}
      <SyncStatus />
    </div>
  )
}
