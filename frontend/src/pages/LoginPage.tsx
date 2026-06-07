import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useAuthStore } from '@/store/authStore'
import { api } from '@/lib/api'
import {
  otpSendSchema, otpVerifySchema, adminLoginSchema,
  type OtpSendData, type OtpVerifyData, type AdminLoginData
} from '@/lib/validations'
import type { AuthUser } from '@/types'

type LoginMode = 'customer' | 'admin'
type OtpStep   = 'phone' | 'code'

export default function LoginPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const setAuth = useAuthStore((s) => s.setAuth)

  const [mode, setMode] = useState<LoginMode>('customer')
  const [otpStep, setOtpStep] = useState<OtpStep>('phone')
  const [phone, setPhone] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  // Customer - send OTP
  const sendForm = useForm<OtpSendData>({ resolver: zodResolver(otpSendSchema) })
  // Customer - verify OTP
  const verifyForm = useForm<OtpVerifyData>({ resolver: zodResolver(otpVerifySchema) })
  // Admin login
  const adminForm = useForm<AdminLoginData>({ resolver: zodResolver(adminLoginSchema) })

  async function handleSendOtp(data: OtpSendData) {
    setLoading(true); setError('')
    try {
      await api.post('/api/auth/otp/send', data)
      setPhone(data.phone)
      setOtpStep('code')
    } catch (e: any) {
      setError(e.message)
    } finally { setLoading(false) }
  }

  async function handleVerifyOtp(data: OtpVerifyData) {
    setLoading(true); setError('')
    try {
      const res = await api.post<{ token: string; user: AuthUser }>(
        '/api/auth/otp/verify', { phone, code: data.code }
      )
      setAuth(res.user, res.token)
      navigate('/')
    } catch (e: any) {
      setError(t('auth.invalidOtp'))
    } finally { setLoading(false) }
  }

  async function handleAdminLogin(data: AdminLoginData) {
    setLoading(true); setError('')
    try {
      const res = await api.post<{ token: string; user: AuthUser }>(
        '/api/auth/admin/login', data
      )
      setAuth(res.user, res.token)
      navigate('/')
    } catch (e: any) {
      setError(t('auth.invalidCredentials'))
    } finally { setLoading(false) }
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-4" style={{ background: 'var(--color-bg)' }}>
      <div className="card p-8 w-full max-w-sm shadow-lg">
        <h1 className="font-display font-bold text-xl text-center text-[--color-accent] mb-6">
          ☕ קפה ארז הימורים
        </h1>

        {/* Mode toggle */}
        <div className="flex gap-2 mb-6">
          <button
            onClick={() => { setMode('customer'); setError('') }}
            className={`flex-1 py-2 rounded-lg text-sm font-semibold transition-colors ${mode === 'customer' ? 'bg-[--color-accent] text-white' : 'bg-gray-100 text-gray-600'}`}
          >
            👤 {t('auth.login')}
          </button>
          <button
            onClick={() => { setMode('admin'); setError('') }}
            className={`flex-1 py-2 rounded-lg text-sm font-semibold transition-colors ${mode === 'admin' ? 'bg-[--color-accent] text-white' : 'bg-gray-100 text-gray-600'}`}
          >
            🔑 {t('auth.adminLogin')}
          </button>
        </div>

        {/* Customer OTP flow */}
        {mode === 'customer' && (
          <>
            {otpStep === 'phone' ? (
              <form onSubmit={sendForm.handleSubmit(handleSendOtp)} className="space-y-4">
                <div>
                  <label className="block text-sm font-medium mb-1">{t('auth.phone')}</label>
                  <input
                    {...sendForm.register('phone')}
                    placeholder="050-1234567"
                    dir="ltr"
                    className="w-full border border-[--color-border] rounded-lg px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[--color-accent]"
                  />
                  {sendForm.formState.errors.phone && (
                    <p className="text-red-500 text-xs mt-1">{t('validation.invalidPhone')}</p>
                  )}
                </div>
                {error && <p className="text-red-500 text-sm">{error}</p>}
                <button type="submit" disabled={loading} className="btn-primary w-full">
                  {loading ? t('common.loading') : t('auth.sendOtp')}
                </button>
              </form>
            ) : (
              <form onSubmit={verifyForm.handleSubmit(handleVerifyOtp)} className="space-y-4">
                <p className="text-sm text-gray-600 text-center">{t('auth.otpSent', { phone })}</p>
                <p className="text-xs text-gray-400 text-center">{t('auth.otpExpiry')}</p>
                <div>
                  <label className="block text-sm font-medium mb-1">{t('auth.otp')}</label>
                  <input
                    {...verifyForm.register('code')}
                    placeholder="123456"
                    maxLength={6}
                    dir="ltr"
                    className="w-full border border-[--color-border] rounded-lg px-3 py-2.5 text-sm text-center tracking-widest focus:outline-none focus:ring-2 focus:ring-[--color-accent]"
                  />
                </div>
                {error && <p className="text-red-500 text-sm">{error}</p>}
                <button type="submit" disabled={loading} className="btn-primary w-full">
                  {loading ? t('common.loading') : t('auth.verifyOtp')}
                </button>
                <button type="button" onClick={() => setOtpStep('phone')} className="btn-secondary w-full text-xs">
                  ← {t('common.cancel')}
                </button>
              </form>
            )}
          </>
        )}

        {/* Admin login */}
        {mode === 'admin' && (
          <form onSubmit={adminForm.handleSubmit(handleAdminLogin)} className="space-y-4">
            <div>
              <label className="block text-sm font-medium mb-1">{t('auth.username')}</label>
              <input
                {...adminForm.register('username')}
                dir="ltr"
                className="w-full border border-[--color-border] rounded-lg px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[--color-accent]"
              />
            </div>
            <div>
              <label className="block text-sm font-medium mb-1">{t('auth.password')}</label>
              <input
                {...adminForm.register('password')}
                type="password"
                dir="ltr"
                className="w-full border border-[--color-border] rounded-lg px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[--color-accent]"
              />
            </div>
            {error && <p className="text-red-500 text-sm">{error}</p>}
            <button type="submit" disabled={loading} className="btn-primary w-full">
              {loading ? t('common.loading') : t('auth.login')}
            </button>
          </form>
        )}
      </div>
    </div>
  )
}
