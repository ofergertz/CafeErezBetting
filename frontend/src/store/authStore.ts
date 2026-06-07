import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { AuthUser } from '@/types'

interface AuthStore {
  user: AuthUser | null
  token: string | null
  setAuth: (user: AuthUser, token: string) => void
  clearAuth: () => void
  isAdmin: () => boolean
  isAuthenticated: () => boolean
}

export const useAuthStore = create<AuthStore>()(
  persist(
    (set, get) => ({
      user: null,
      token: null,
      setAuth: (user, token) => set({ user, token }),
      clearAuth: () => set({ user: null, token: null }),
      isAdmin: () => get().user?.role === 'admin',
      isAuthenticated: () => !!get().token,
    }),
    {
      name: 'cafe-erez-auth',
      partialize: (state) => ({ user: state.user, token: state.token }),
    }
  )
)
