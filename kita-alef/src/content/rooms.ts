import type { Room } from './types'

export const rooms: Room[] = [
  {
    id: 'first-sound',
    name: 'בית הצלילים',
    icon: '🔤',
    description: 'מה הצליל הראשון?',
    gameType: 'first-sound',
    position: { x: 25, y: 55 },
    color: '#FF6B6B',
  },
  {
    id: 'rhyming',
    name: 'מבצר החריזה',
    icon: '🏰',
    description: 'מה מחרוזת?',
    gameType: 'rhyming',
    position: { x: 65, y: 55 },
    color: '#4ECDC4',
    unlocksAfter: 'first-sound',
    requiredStars: 1,
  },
  {
    id: 'ai-teacher',
    name: 'הבית של רינה',
    icon: '👩‍🏫',
    description: 'שיחה עם המורה',
    gameType: 'ai-teacher',
    position: { x: 45, y: 25 },
    color: '#FFD93D',
  },
]
