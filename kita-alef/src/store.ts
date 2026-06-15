import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { GameProgress } from './content/types'

type Screen = 'welcome' | 'world' | 'game'

interface AppState {
  screen: Screen
  playerName: string
  activeRoomId: string | null
  progress: Record<string, GameProgress>

  setPlayerName: (name: string) => void
  goToWorld: () => void
  enterRoom: (roomId: string) => void
  exitRoom: () => void
  saveProgress: (roomId: string, stars: number) => void
  getStars: (roomId: string) => number
  isRoomUnlocked: (roomId: string, unlocksAfter?: string, requiredStars?: number) => boolean
}

export const useStore = create<AppState>()(
  persist(
    (set, get) => ({
      screen: 'welcome',
      playerName: '',
      activeRoomId: null,
      progress: {},

      setPlayerName: (name) => set({ playerName: name }),

      goToWorld: () => set({ screen: 'world', activeRoomId: null }),

      enterRoom: (roomId) => set({ screen: 'game', activeRoomId: roomId }),

      exitRoom: () => set({ screen: 'world', activeRoomId: null }),

      saveProgress: (roomId, stars) => {
        const existing = get().progress[roomId]
        if (!existing || stars > existing.stars) {
          set((state) => ({
            progress: {
              ...state.progress,
              [roomId]: {
                roomId,
                stars,
                completed: stars > 0,
                attempts: (existing?.attempts ?? 0) + 1,
              },
            },
          }))
        }
      },

      getStars: (roomId) => get().progress[roomId]?.stars ?? 0,

      isRoomUnlocked: (roomId, unlocksAfter, requiredStars = 1) => {
        if (!unlocksAfter) return true
        const prerequisiteStars = get().getStars(unlocksAfter)
        return prerequisiteStars >= requiredStars
      },
    }),
    { name: 'kita-alef-progress' }
  )
)
