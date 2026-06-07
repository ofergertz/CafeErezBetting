import { NavLink } from 'react-router-dom'

const NAV_ITEMS = [
  { path: '/winner', label: 'ווינר', icon: '⚽' },
  { path: '/toto',   label: 'טוטו',  icon: '🏆' },
  { path: '/lotto',  label: 'לוטו',  icon: '🔮' },
  { path: '/chance', label: "צ'אנס", icon: '🎯' },
  { path: '/777',    label: '777',   icon: '7️⃣' },
  { path: '/store',  label: 'חנות',  icon: '🛍️' },
]

export default function MobileNav() {

  return (
    <nav className="md:hidden fixed bottom-0 left-0 right-0 z-50 bg-white border-t border-gray-200 shadow-lg">
      <div className="flex items-stretch h-14">
        {NAV_ITEMS.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            className={({ isActive }) =>
              `flex-1 flex flex-col items-center justify-center gap-0.5 text-xs transition-colors
              ${isActive
                ? 'text-[--color-accent] bg-blue-50'
                : 'text-gray-500 hover:bg-gray-50'}`
            }
          >
            <span className="text-base leading-none">{item.icon}</span>
            <span className="leading-none">{item.label}</span>
          </NavLink>
        ))}
      </div>
    </nav>
  )
}
