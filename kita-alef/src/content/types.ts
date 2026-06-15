export type GameType = 'first-sound' | 'rhyming' | 'ai-teacher'

export interface Room {
  id: string
  name: string
  icon: string
  description: string
  gameType: GameType
  position: { x: number; y: number }
  color: string
  unlocksAfter?: string
  requiredStars?: number
}

export interface FirstSoundRound {
  word: string
  emoji: string
  options: string[]
  correct: string
}

export interface RhymingRound {
  word: string
  emoji: string
  options: Array<{ word: string; emoji: string }>
  correct: string
}

export interface GameProgress {
  roomId: string
  stars: number
  completed: boolean
  attempts: number
}
