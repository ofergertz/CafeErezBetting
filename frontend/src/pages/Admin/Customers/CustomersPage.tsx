import { useState } from 'react'
import React from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '@/lib/api'

// ── Types ────────────────────────────────────────────────────────────────────
interface CustomerListItem {
  id: string
  firstName: string
  lastName: string
  idNumber: string
  phone: string
  totalDebt: number
  debtCount: number
  createdAt: string
}

interface DebtRecord {
  id: string
  customerId: string
  category: string
  description?: string
  originalAmount: number
  paidAmount: number
  balance: number
  status: 'Open' | 'Partial' | 'Settled'
  createdAt: string
  updatedAt: string
}

const DEBT_CATEGORIES = ['store', 'winner', 'toto', 'lotto', 'chance', 'lucky777', 'other']

// ── Status Badge ─────────────────────────────────────────────────────────────
function StatusBadge({ status }: { status: DebtRecord['status'] }) {
  const { t } = useTranslation()
  const map: Record<DebtRecord['status'], { label: string; cls: string }> = {
    Open: { label: t('customers.status.open'), cls: 'bg-red-100 text-red-700' },
    Partial: { label: t('customers.status.partial'), cls: 'bg-yellow-100 text-yellow-700' },
    Settled: { label: t('customers.status.settled'), cls: 'bg-green-100 text-green-700' },
  }
  const { label, cls } = map[status]
  return <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${cls}`}>{label}</span>
}

// ── Modal Wrapper ─────────────────────────────────────────────────────────────
function Modal({ title, children, onClose }: { title: string; children: React.ReactNode; onClose: () => void }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={onClose}>
      <div
        className="bg-white rounded-xl shadow-2xl w-full max-w-md mx-4 p-6"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex justify-between items-center mb-4">
          <h2 className="text-lg font-bold">{title}</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl leading-none">✕</button>
        </div>
        {children}
      </div>
    </div>
  )
}

// ── Add/Edit Customer Modal ───────────────────────────────────────────────────
function CustomerModal({
  customer,
  onClose,
}: {
  customer?: CustomerListItem
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!customer

  const [form, setForm] = useState({
    firstName: customer?.firstName ?? '',
    lastName: customer?.lastName ?? '',
    idNumber: customer?.idNumber ?? '',
    phone: customer?.phone ?? '',
  })
  const [errors, setErrors] = useState<Record<string, string>>({})

  const validate = () => {
    const e: Record<string, string> = {}
    if (!form.firstName.trim()) e.firstName = t('validation.required')
    if (!form.lastName.trim()) e.lastName = t('validation.required')
    if (!isEdit && !form.idNumber.trim()) e.idNumber = t('validation.required')
    if (!form.phone.trim()) e.phone = t('validation.required')
    setErrors(e)
    return Object.keys(e).length === 0
  }

  const createMutation = useMutation({
    mutationFn: (data: typeof form) => api.post('/api/customers', data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['customers'] })
      onClose()
    },
  })

  const updateMutation = useMutation({
    mutationFn: (data: Partial<typeof form>) =>
      api.put(`/api/customers/${customer!.id}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['customers'] })
      onClose()
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!validate()) return
    if (isEdit) {
      updateMutation.mutate({ firstName: form.firstName, lastName: form.lastName, phone: form.phone })
    } else {
      createMutation.mutate(form)
    }
  }

  const isPending = createMutation.isPending || updateMutation.isPending

  return (
    <Modal title={isEdit ? t('common.edit') : t('customers.addCustomer')} onClose={onClose}>
      <form onSubmit={handleSubmit} className="space-y-3">
        {[
          { key: 'firstName', label: t('customers.firstName') },
          { key: 'lastName', label: t('customers.lastName') },
          ...(!isEdit ? [{ key: 'idNumber', label: t('customers.idNumber') }] : []),
          { key: 'phone', label: t('customers.phone') },
        ].map(({ key, label }) => (
          <div key={key}>
            <label className="block text-sm font-medium mb-1">{label}</label>
            <input
              className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
              value={form[key as keyof typeof form]}
              onChange={(e) => setForm({ ...form, [key]: e.target.value })}
            />
            {errors[key] && <p className="text-red-500 text-xs mt-1">{errors[key]}</p>}
          </div>
        ))}
        <div className="flex gap-2 justify-end pt-2">
          <button type="button" onClick={onClose} className="px-4 py-2 text-sm rounded-lg border hover:bg-gray-50">
            {t('common.cancel')}
          </button>
          <button type="submit" disabled={isPending} className="px-4 py-2 text-sm rounded-lg bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50">
            {isPending ? t('common.loading') : t('common.save')}
          </button>
        </div>
      </form>
    </Modal>
  )
}

// ── Add Debt Modal ────────────────────────────────────────────────────────────
function AddDebtModal({ customerId, onClose }: { customerId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [form, setForm] = useState({
    category: 'store',
    description: '',
    originalAmount: '',
    paidAmount: '0',
  })
  const [errors, setErrors] = useState<Record<string, string>>({})

  const validate = () => {
    const e: Record<string, string> = {}
    const amt = parseFloat(form.originalAmount)
    if (!form.originalAmount || isNaN(amt) || amt <= 0) e.originalAmount = t('validation.positiveAmount')
    setErrors(e)
    return Object.keys(e).length === 0
  }

  const mutation = useMutation({
    mutationFn: () =>
      api.post(`/api/customers/${customerId}/debts`, {
        category: form.category,
        description: form.description || undefined,
        originalAmount: parseFloat(form.originalAmount),
        paidAmount: parseFloat(form.paidAmount) || 0,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['debts', customerId] })
      queryClient.invalidateQueries({ queryKey: ['customers'] })
      onClose()
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!validate()) return
    mutation.mutate()
  }

  return (
    <Modal title={t('customers.addDebt')} onClose={onClose}>
      <form onSubmit={handleSubmit} className="space-y-3">
        <div>
          <label className="block text-sm font-medium mb-1">{t('customers.category')}</label>
          <select
            className="w-full border rounded-lg px-3 py-2 text-sm"
            value={form.category}
            onChange={(e) => setForm({ ...form, category: e.target.value })}
          >
            {DEBT_CATEGORIES.map((c) => (
              <option key={c} value={c}>
                {t(`customers.categories.${c === 'lucky777' ? '777' : c}`)}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">{t('customers.descriptionOptional')}</label>
          <input
            className="w-full border rounded-lg px-3 py-2 text-sm"
            value={form.description}
            onChange={(e) => setForm({ ...form, description: e.target.value })}
          />
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">{t('customers.amount')}</label>
          <input
            type="number"
            min="0"
            step="0.01"
            className="w-full border rounded-lg px-3 py-2 text-sm"
            value={form.originalAmount}
            onChange={(e) => setForm({ ...form, originalAmount: e.target.value })}
          />
          {errors.originalAmount && <p className="text-red-500 text-xs mt-1">{errors.originalAmount}</p>}
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">{t('customers.paid')}</label>
          <input
            type="number"
            min="0"
            step="0.01"
            className="w-full border rounded-lg px-3 py-2 text-sm"
            value={form.paidAmount}
            onChange={(e) => setForm({ ...form, paidAmount: e.target.value })}
          />
        </div>
        <div className="flex gap-2 justify-end pt-2">
          <button type="button" onClick={onClose} className="px-4 py-2 text-sm rounded-lg border hover:bg-gray-50">
            {t('common.cancel')}
          </button>
          <button type="submit" disabled={mutation.isPending} className="px-4 py-2 text-sm rounded-lg bg-green-600 text-white hover:bg-green-700 disabled:opacity-50">
            {mutation.isPending ? t('common.loading') : t('common.add')}
          </button>
        </div>
      </form>
    </Modal>
  )
}

// ── Update Payment Modal ──────────────────────────────────────────────────────
function UpdatePaymentModal({
  customerId,
  debt,
  onClose,
}: {
  customerId: string
  debt: DebtRecord
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [paidAmount, setPaidAmount] = useState(String(debt.paidAmount))
  const [error, setError] = useState('')

  const mutation = useMutation({
    mutationFn: () =>
      api.put(`/api/customers/${customerId}/debts/${debt.id}`, {
        paidAmount: parseFloat(paidAmount),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['debts', customerId] })
      queryClient.invalidateQueries({ queryKey: ['customers'] })
      onClose()
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    const val = parseFloat(paidAmount)
    if (isNaN(val) || val < 0) { setError(t('validation.positiveAmount')); return }
    if (val > debt.originalAmount) { setError(t('validation.exceedsBalance')); return }
    setError('')
    mutation.mutate()
  }

  return (
    <Modal title={`עדכן תשלום — ${debt.category}`} onClose={onClose}>
      <form onSubmit={handleSubmit} className="space-y-3">
        <div>
          <label className="block text-sm font-medium mb-1">{t('customers.paid')}</label>
          <input
            type="number"
            min="0"
            step="0.01"
            className="w-full border rounded-lg px-3 py-2 text-sm"
            value={paidAmount}
            onChange={(e) => setPaidAmount(e.target.value)}
          />
          {error && <p className="text-red-500 text-xs mt-1">{error}</p>}
        </div>
        <div className="flex gap-2 justify-end pt-2">
          <button type="button" onClick={onClose} className="px-4 py-2 text-sm rounded-lg border hover:bg-gray-50">
            {t('common.cancel')}
          </button>
          <button type="submit" disabled={mutation.isPending} className="px-4 py-2 text-sm rounded-lg bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50">
            {mutation.isPending ? t('common.loading') : t('common.save')}
          </button>
        </div>
      </form>
    </Modal>
  )
}

// ── Confirm Delete Dialog ─────────────────────────────────────────────────────
function ConfirmDialog({ message, onConfirm, onCancel }: { message: string; onConfirm: () => void; onCancel: () => void }) {
  const { t } = useTranslation()
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={onCancel}>
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-sm mx-4 p-6" onClick={(e) => e.stopPropagation()}>
        <p className="text-base font-medium mb-6">{message}</p>
        <div className="flex gap-2 justify-end">
          <button onClick={onCancel} className="px-4 py-2 text-sm rounded-lg border hover:bg-gray-50">{t('common.no')}</button>
          <button onClick={onConfirm} className="px-4 py-2 text-sm rounded-lg bg-red-600 text-white hover:bg-red-700">{t('common.yes')}</button>
        </div>
      </div>
    </div>
  )
}

// ── Debt Row ──────────────────────────────────────────────────────────────────
function DebtRows({ customerId }: { customerId: string }) {
  const { t } = useTranslation()
  const [payModal, setPayModal] = useState<DebtRecord | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['debts', customerId],
    queryFn: () => api.get<{ debts: DebtRecord[] }>(`/api/customers/${customerId}/debts`),
  })

  if (isLoading) return <tr><td colSpan={7} className="px-4 py-3 text-center text-sm text-gray-400">{t('common.loading')}</td></tr>

  const debts = data?.debts ?? []
  if (!debts.length) return <tr><td colSpan={7} className="px-4 py-3 text-center text-sm text-gray-400">{t('common.noData')}</td></tr>

  return (
    <>
      {debts.map((d) => (
        <tr key={d.id} className="bg-gray-50 border-t border-gray-100">
          <td className="px-4 py-2 text-xs text-gray-500">{t(`customers.categories.${d.category === 'lucky777' ? '777' : d.category}`)}</td>
          <td className="px-4 py-2 text-xs text-gray-500">{d.description || '—'}</td>
          <td className="px-4 py-2 text-xs">{t('common.currency')}{d.originalAmount.toLocaleString()}</td>
          <td className="px-4 py-2 text-xs">{t('common.currency')}{d.paidAmount.toLocaleString()}</td>
          <td className="px-4 py-2 text-xs font-semibold">{t('common.currency')}{d.balance.toLocaleString()}</td>
          <td className="px-4 py-2"><StatusBadge status={d.status} /></td>
          <td className="px-4 py-2">
            {d.status !== 'Settled' && (
              <button
                onClick={() => setPayModal(d)}
                className="text-xs px-2 py-1 rounded bg-blue-100 text-blue-700 hover:bg-blue-200"
              >
                עדכן תשלום
              </button>
            )}
          </td>
        </tr>
      ))}
      {payModal && (
        <UpdatePaymentModal
          customerId={customerId}
          debt={payModal}
          onClose={() => setPayModal(null)}
        />
      )}
    </>
  )
}

// ── Main Page ─────────────────────────────────────────────────────────────────
export default function CustomersPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const [expandedId, setExpandedId] = useState<string | null>(null)
  const [addModal, setAddModal] = useState(false)
  const [editCustomer, setEditCustomer] = useState<CustomerListItem | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<CustomerListItem | null>(null)
  const [debtTarget, setDebtTarget] = useState<CustomerListItem | null>(null)

  const { data, isLoading, isError } = useQuery({
    queryKey: ['customers'],
    queryFn: () => api.get<{ customers: CustomerListItem[] }>('/api/customers'),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.delete(`/api/customers/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['customers'] })
      setDeleteTarget(null)
    },
  })

  const customers = data?.customers ?? []

  return (
    <div dir="rtl" className="p-4 max-w-5xl mx-auto">
      {/* Header */}
      <div className="flex justify-between items-center mb-4">
        <h1 className="text-xl font-bold">{t('customers.title')}</h1>
        <button
          onClick={() => setAddModal(true)}
          className="px-4 py-2 text-sm rounded-lg bg-blue-600 text-white hover:bg-blue-700"
        >
          + {t('customers.addCustomer')}
        </button>
      </div>

      {isLoading && <p className="text-center text-gray-400 py-8">{t('common.loading')}</p>}
      {isError && <p className="text-center text-red-500 py-8">{t('common.error')}</p>}

      {!isLoading && !isError && (
        <div className="bg-white rounded-xl shadow overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-gray-500 text-xs uppercase">
              <tr>
                <th className="px-4 py-3 text-right">{t('customers.firstName')}</th>
                <th className="px-4 py-3 text-right">{t('customers.lastName')}</th>
                <th className="px-4 py-3 text-right">{t('customers.phone')}</th>
                <th className="px-4 py-3 text-right">{t('customers.totalDebt')}</th>
                <th className="px-4 py-3 text-right">{t('customers.debts')}</th>
                <th className="px-4 py-3 text-right"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {customers.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-gray-400">{t('common.noData')}</td>
                </tr>
              )}
              {customers.map((c) => {
                const isExpanded = expandedId === c.id
                return (
                  <React.Fragment key={c.id}>
                    {/* Main row */}
                    <tr
                      className="hover:bg-gray-50 cursor-pointer"
                      onClick={() => setExpandedId(isExpanded ? null : c.id)}
                    >
                      <td className="px-4 py-3 font-medium">{c.firstName}</td>
                      <td className="px-4 py-3">{c.lastName}</td>
                      <td className="px-4 py-3 text-gray-600">{c.phone}</td>
                      <td className="px-4 py-3 font-semibold text-red-600">
                        {t('common.currency')}{c.totalDebt.toLocaleString()}
                      </td>
                      <td className="px-4 py-3 text-gray-500">{c.debtCount}</td>
                      <td className="px-4 py-3">
                        <div className="flex gap-1 justify-end" onClick={(e) => e.stopPropagation()}>
                          <button
                            onClick={() => setDebtTarget(c)}
                            className="text-xs px-2 py-1 rounded bg-green-100 text-green-700 hover:bg-green-200"
                            title={t('customers.addDebt')}
                          >
                            + חוב
                          </button>
                          <button
                            onClick={() => setEditCustomer(c)}
                            className="text-xs px-2 py-1 rounded bg-gray-100 text-gray-700 hover:bg-gray-200"
                            title={t('common.edit')}
                          >
                            ✏️
                          </button>
                          <button
                            onClick={() => setDeleteTarget(c)}
                            className="text-xs px-2 py-1 rounded bg-red-100 text-red-700 hover:bg-red-200"
                            title={t('common.delete')}
                          >
                            🗑️
                          </button>
                          <span className="text-gray-400 px-1">{isExpanded ? '▲' : '▼'}</span>
                        </div>
                      </td>
                    </tr>

                    {/* Expanded debt rows */}
                    {isExpanded && (
                      <>
                        <tr className="bg-blue-50">
                          <td colSpan={6} className="px-6 py-2">
                            <div className="flex gap-2 items-center">
                              <span className="text-xs font-semibold text-blue-700">{t('customers.debts')}</span>
                            </div>
                          </td>
                        </tr>
                        <tr className="bg-gray-50">
                          <td colSpan={6}>
                            <div className="overflow-x-auto">
                              <table className="w-full text-xs">
                                <thead>
                                  <tr className="text-gray-500 text-xs bg-gray-100">
                                    <th className="px-4 py-2 text-right">{t('customers.category')}</th>
                                    <th className="px-4 py-2 text-right">{t('customers.description')}</th>
                                    <th className="px-4 py-2 text-right">{t('customers.amount')}</th>
                                    <th className="px-4 py-2 text-right">{t('customers.paid')}</th>
                                    <th className="px-4 py-2 text-right">{t('customers.balance')}</th>
                                    <th className="px-4 py-2 text-right">{t('common.status')}</th>
                                    <th className="px-4 py-2"></th>
                                  </tr>
                                </thead>
                                <tbody>
                                  <DebtRows customerId={c.id} />
                                </tbody>
                              </table>
                            </div>
                          </td>
                        </tr>
                      </>
                    )}
                  </React.Fragment>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      {/* Modals */}
      {addModal && <CustomerModal onClose={() => setAddModal(false)} />}
      {editCustomer && <CustomerModal customer={editCustomer} onClose={() => setEditCustomer(null)} />}
      {debtTarget && <AddDebtModal customerId={debtTarget.id} onClose={() => setDebtTarget(null)} />}
      {deleteTarget && (
        <ConfirmDialog
          message={`למחוק את ${deleteTarget.firstName} ${deleteTarget.lastName}?`}
          onConfirm={() => deleteMutation.mutate(deleteTarget.id)}
          onCancel={() => setDeleteTarget(null)}
        />
      )}
    </div>
  )
}
