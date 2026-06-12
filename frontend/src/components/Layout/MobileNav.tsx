import { NavLink } from 'react-router-dom'
import { Trophy, Sparkles, Target, ShoppingBag, Users, FileText } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { useAuthStore } from '@/store/authStore'

interface NavItem {
  path: string
  label: string
  Icon: LucideIcon | null
  iconText?: string
}

const BASE_ITEMS: NavItem[] = [
  { path: '/winner', label: 'ווינר', Icon: Trophy },
  { path: '/toto',   label: 'טוטו',  Icon: Trophy },
  { path: '/lotto',  label: 'לוטו',  Icon: Sparkles },
  { path: '/chance', label: "צ'אנס", Icon: Target },
  { path: '/777',    label: '777',   Icon: null, iconText: '7' },
  { path: '/store',  label: 'חנות',  Icon: ShoppingBag },
]

const ADMIN_ITEMS: NavItem[] = [
  { path: '/forms',     label: 'טפסים',  Icon: FileText },
  { path: '/customers', label: 'לקוחות', Icon: Users },
]

export default function MobileNav() {
  const isAdmin = useAuthStore((s) => s.isAdmin())
  const navItems = isAdmin ? [...BASE_ITEMS, ...ADMIN_ITEMS] : BASE_ITEMS

  return (
    <nav className="md:hidden fixed bottom-0 left-0 right-0 z-50 bg-white border-t border-gray-200 shadow-lg">
      <div className="flex items-stretch h-14 overflow-x-auto">
        {navItems.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            aria-label={item.label}
            className={({ isActive }) =>
              `flex-shrink-0 flex flex-col items-center justify-center gap-0.5 text-xs transition-colors px-3
              ${isActive
                ? 'text-[--color-accent] bg-[#2d6a4f]/10'
                : 'text-gray-500 hover:bg-gray-50'}`
            }
          >
            <span className="leading-none">
              {item.Icon
                ? <item.Icon size={18} />
                : <span className="font-black text-sm">{item.iconText}</span>
              }
            </span>
            <span className="leading-none whitespace-nowrap">{item.label}</span>
          </NavLink>
        ))}
      </div>
    </nav>
  )
}
