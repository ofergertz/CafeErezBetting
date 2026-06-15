import type { RhymingRound } from '../types'

export const rhymingRounds: RhymingRound[] = [
  {
    word: 'כלב', emoji: '🐕',
    options: [
      { word: 'חלב', emoji: '🥛' },
      { word: 'ספר', emoji: '📚' },
      { word: 'שמש', emoji: '☀️' },
    ],
    correct: 'חלב',
  },
  {
    word: 'בית', emoji: '🏠',
    options: [
      { word: 'כדור', emoji: '⚽' },
      { word: 'זית', emoji: '🫒' },
      { word: 'ילד', emoji: '👦' },
    ],
    correct: 'זית',
  },
  {
    word: 'שמש', emoji: '☀️',
    options: [
      { word: 'נמש', emoji: '🦶' },
      { word: 'ירח', emoji: '🌙' },
      { word: 'ענן', emoji: '☁️' },
    ],
    correct: 'נמש',
  },
  {
    word: 'דג', emoji: '🐟',
    options: [
      { word: 'בית', emoji: '🏠' },
      { word: 'רג', emoji: '🦵' },
      { word: 'עץ', emoji: '🌳' },
    ],
    correct: 'רג',
  },
  {
    word: 'ילד', emoji: '👦',
    options: [
      { word: 'כלד', emoji: '🐕' },
      { word: 'שמש', emoji: '☀️' },
      { word: 'ים', emoji: '🌊' },
    ],
    correct: 'כלד',
  },
  {
    word: 'ים', emoji: '🌊',
    options: [
      { word: 'כדור', emoji: '⚽' },
      { word: 'צים', emoji: '🚢' },
      { word: 'עץ', emoji: '🌳' },
    ],
    correct: 'צים',
  },
]
