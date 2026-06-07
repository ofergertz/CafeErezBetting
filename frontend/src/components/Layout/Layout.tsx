import { Outlet, NavLink, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuthStore } from '@/store/authStore'
import MobileNav from '@/components/Layout/MobileNav'
import Logo from '@/components/ui/Logo'

const LANGUAGES = [
  { code: 'he', label: 'עב', flag: '🇮🇱' },
  { code: 'ru', label: 'RU', flag: '🇷🇺' },
  { code: 'en', label: 'EN', flag: '🇬🇧' },
]

export default function Layout() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { user, clearAuth, isAdmin } = useAuthStore()

  const tabs = [
    { path: '/winner',    label: t('nav.winner') },
    { path: '/toto',      label: t('nav.toto') },
    { path: '/lotto',     label: t('nav.lotto') },
    { path: '/chance',    label: t('nav.chance') },
    { path: '/777',       label: t('nav.lucky777') },
    { path: '/store',     label: t('nav.store') },
    ...(isAdmin() ? [
      { path: '/customers',  label: t('nav.customers') },
      { path: '/forms',      label: t('nav.forms') },
      { path: '/kiosk',      label: t('nav.kiosk') },
      { path: '/audit-logs', label: t('nav.auditLogs') },
    ] : []),
  ]

  function handleLogout() {
    clearAuth()
    navigate('/login')
  }

  return (
    <div className="min-h-screen flex flex-col" style={{ background: 'var(--color-bg)' }}>
      {/* Header */}
      <header className="bg-white border-b border-[--color-border] shadow-sm sticky top-0 z-40">
        <div className="max-w-7xl mx-auto px-4 h-14 flex items-center justify-between gap-4">
          {/* Logo */}
          <div className="flex-shrink-0">
            <Logo size="header" />
          </div>

          {/* Nav tabs — desktop */}
          <nav className="hidden md:flex items-center gap-1 overflow-x-auto">
            {tabs.map((tab) => (
              <NavLink
                key={tab.path}
                to={tab.path}
                className={({ isActive }) =>
                  `px-3 py-1.5 rounded-lg text-sm font-medium transition-colors whitespace-nowrap
                  ${isActive
                    ? 'bg-[--color-accent] text-white'
                    : 'text-gray-600 hover:bg-gray-100'}`
                }
              >
                {tab.label}
              </NavLink>
            ))}
          </nav>

          {/* Right side: lang + auth */}
          <div className="flex items-center gap-2 flex-shrink-0">
            {/* Language switcher */}
            <div className="flex items-center gap-1">
              {LANGUAGES.map((lang) => (
                <button
                  key={lang.code}
                  onClick={() => i18n.changeLanguage(lang.code)}
                  className={`text-sm px-2 py-1 rounded transition-colors
                    ${i18n.language === lang.code
                      ? 'bg-[--color-accent] text-white'
                      : 'text-gray-500 hover:bg-gray-100'}`}
                  aria-label={`Switch to ${lang.label}`}
                >
                  {lang.flag}
                </button>
              ))}
            </div>

            {/* Auth */}
            {user ? (
              <button onClick={handleLogout} className="btn-secondary text-xs py-1 px-3 min-h-[32px]">
                {t('auth.logout')}
              </button>
            ) : (
              <button onClick={() => navigate('/login')} className="btn-primary text-xs py-1 px-3 min-h-[32px]">
                {t('auth.login')}
              </button>
            )}
          </div>
        </div>
      </header>

      {/* Main */}
      <main className="flex-1 max-w-7xl w-full mx-auto px-4 py-6 pb-20 md:pb-6">
        <Outlet />
      </main>
      <MobileNav />
    </div>
  )
}
