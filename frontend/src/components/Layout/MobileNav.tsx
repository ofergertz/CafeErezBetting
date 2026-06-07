import { NavLink } from 'react-router-dom'
import { useTranslation } from 'react-i18next'

const NAV_ITEMS = [
  { path: '/winner', labelKey: 'nav.winner', icon: '⚽' },
  { path: '/toto',   labelKey: 'nav.toto',   icon: '🏆' },
  { path: '/lotto',  labelKey: 'nav.lotto',  icon: '🔮' },
  { path: '/chance', labelKey: 'nav.chance', icon: '🎯' },
  { path: '/777',    labelKey: 'nav.lucky777', icon: '7️⃣' },
  { path: '/store',  labelKey: 'nav.store',  icon: '🛍️' },
]

export default function MobileNav() {
  const { t } = useTranslation()

  return (
    <nav className="md:hidden fixed bottom-0 left-0 right-0 z-50 bg-white border-t border-gray-200 shadow-lg">
      <div className="flex items-stretch h-14">
        {NAV_ITEMS.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            aria-label={t(item.labelKey)}
            className={({ isActive }) =>
              `flex-1 flex flex-col items-center justify-center gap-0.5 text-xs transition-colors
              ${isActive
                ? 'text-[--color-accent] bg-[#2d6a4f]/10'
                : 'text-gray-500 hover:bg-gray-50'}`
            }
          >
            <span className="text-base leading-none">{item.icon}</span>
            <span className="leading-none">{t(item.labelKey)}</span>
          </NavLink>
        ))}
      </div>
    </nav>
  )
}
