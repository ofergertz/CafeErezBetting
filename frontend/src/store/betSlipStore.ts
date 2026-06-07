import { create } from 'zustand'
import type { BetSlipItem, WinnerPick } from '@/types'

interface BetSlipStore {
  items: BetSlipItem[]
  stake: number
  addOrToggle: (item: BetSlipItem) => void
  remove: (matchId: string) => void
  changePick: (matchId: string, pick: WinnerPick) => void
  setStake: (amount: number) => void
  clear: () => void
  totalOdds: () => number
  potentialWin: () => number
}

export const useBetSlipStore = create<BetSlipStore>()((set, get) => ({
  items: [],
  stake: 0,

  addOrToggle: (item) =>
    set((state) => {
      const existing = state.items.find((i) => i.matchId === item.matchId)
      if (!existing) return { items: [...state.items, item] }
      // same pick → remove; different pick → replace
      if (existing.pick === item.pick) {
        return { items: state.items.filter((i) => i.matchId !== item.matchId) }
      }
      return {
        items: state.items.map((i) =>
          i.matchId === item.matchId ? { ...i, pick: item.pick, odds: item.odds } : i
        ),
      }
    }),

  remove: (matchId) =>
    set((state) => ({ items: state.items.filter((i) => i.matchId !== matchId) })),

  changePick: (matchId, pick) =>
    set((state) => ({
      items: state.items.map((i) => (i.matchId === matchId ? { ...i, pick } : i)),
    })),

  setStake: (amount) => set({ stake: amount }),

  clear: () => set({ items: [], stake: 0 }),

  totalOdds: () => {
    const { items } = get()
    if (items.length === 0) return 0
    return items.reduce((acc, item) => acc * item.odds, 1)
  },

  potentialWin: () => {
    const { stake } = get()
    const total = get().totalOdds()
    return parseFloat((total * stake).toFixed(2))
  },
}))
