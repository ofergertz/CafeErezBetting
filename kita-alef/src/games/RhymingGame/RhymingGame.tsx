import { useState, useEffect } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { rhymingRounds } from '../../content/games/rhyming'
import { useStore } from '../../store'
import { Stars } from '../../components/ui/Stars'
import { Button } from '../../components/ui/Button'

const ROUNDS_PER_GAME = 5

export function RhymingGame() {
  const { exitRoom, saveProgress } = useStore()
  const [rounds] = useState(() => [...rhymingRounds].sort(() => Math.random() - 0.5).slice(0, ROUNDS_PER_GAME))
  const [currentIndex, setCurrentIndex] = useState(0)
  const [selected, setSelected] = useState<string | null>(null)
  const [score, setScore] = useState(0)
  const [finished, setFinished] = useState(false)

  const round = rounds[currentIndex]

  const speak = (text: string) => {
    if ('speechSynthesis' in window) {
      const u = new SpeechSynthesisUtterance(text)
      u.lang = 'he-IL'
      u.rate = 0.85
      window.speechSynthesis.speak(u)
    }
  }

  useEffect(() => {
    if (round) {
      setTimeout(() => speak(`${round.word}. מה מחרוזת ל${round.word}?`), 300)
    }
  }, [currentIndex])

  const handleSelect = (word: string) => {
    if (selected) return
    setSelected(word)
    const correct = word === round.correct

    if (correct) {
      setScore((s) => s + 1)
      speak(`כן! ${word} מחרוזת ל${round.word}!`)
    } else {
      speak(`לא ממש... ${round.correct} מחרוזת ל${round.word}`)
    }

    setTimeout(() => {
      if (currentIndex + 1 >= ROUNDS_PER_GAME) {
        setFinished(true)
      } else {
        setCurrentIndex((i) => i + 1)
        setSelected(null)
      }
    }, 1600)
  }

  const stars = score >= 5 ? 3 : score >= 3 ? 2 : score >= 1 ? 1 : 0

  if (finished) {
    saveProgress('rhyming', stars)
    return (
      <motion.div
        className="flex flex-col items-center justify-center h-screen bg-gradient-to-b from-teal-100 to-cyan-50 gap-6 p-8"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
      >
        <motion.div
          className="text-8xl"
          animate={{ scale: [1, 1.3, 1] }}
          transition={{ duration: 0.8, repeat: 3 }}
        >
          🏰
        </motion.div>
        <h2 className="text-4xl font-black text-teal-600">כל הכבוד!</h2>
        <p className="text-2xl text-gray-700">
          ענית נכון על {score} מתוך {ROUNDS_PER_GAME}
        </p>
        <Stars count={stars} size="lg" />
        <Button onClick={exitRoom} color="#4ECDC4" size="lg">
          חזרה לעולם 🗺️
        </Button>
      </motion.div>
    )
  }

  return (
    <div className="flex flex-col items-center justify-between h-screen bg-gradient-to-b from-teal-100 to-cyan-50 p-6 pt-4">
      {/* Header */}
      <div className="w-full flex justify-between items-center">
        <Button onClick={exitRoom} color="#aaa" size="sm">← חזרה</Button>
        <div className="text-center">
          <p className="text-sm text-gray-500">שאלה {currentIndex + 1} מתוך {ROUNDS_PER_GAME}</p>
          <div className="flex gap-1 mt-1">
            {rounds.map((_, i) => (
              <div key={i} className={`w-3 h-3 rounded-full ${i < currentIndex ? 'bg-green-400' : i === currentIndex ? 'bg-teal-400' : 'bg-gray-200'}`} />
            ))}
          </div>
        </div>
        <p className="text-xl font-bold">✅ {score}</p>
      </div>

      {/* Question */}
      <AnimatePresence mode="wait">
        <motion.div
          key={currentIndex}
          className="flex flex-col items-center gap-3"
          initial={{ opacity: 0, scale: 0.8 }}
          animate={{ opacity: 1, scale: 1 }}
          exit={{ opacity: 0, scale: 0.8 }}
        >
          <p className="text-2xl font-bold text-gray-600">מה מחרוזת?</p>

          <motion.div
            className="text-9xl cursor-pointer"
            animate={{ y: [0, -8, 0] }}
            transition={{ duration: 2, repeat: Infinity }}
            onClick={() => speak(round.word)}
          >
            {round.emoji}
          </motion.div>

          <div className="flex items-center gap-3 bg-white/80 px-6 py-3 rounded-2xl shadow">
            <span className="text-4xl font-black text-gray-800">{round.word}</span>
            <button onClick={() => speak(round.word)} className="text-2xl opacity-60 hover:opacity-100">🔊</button>
          </div>
        </motion.div>
      </AnimatePresence>

      {/* Options */}
      <div className="flex flex-col gap-4 w-full max-w-xs mb-4">
        {round.options.map((option) => {
          const isSelected = selected === option.word
          const isCorrect = option.word === round.correct
          let bg = '#fff'
          if (isSelected && isCorrect) bg = '#4ade80'
          else if (isSelected && !isCorrect) bg = '#f87171'
          else if (selected && isCorrect) bg = '#4ade80'

          return (
            <motion.button
              key={option.word}
              onClick={() => handleSelect(option.word)}
              disabled={!!selected}
              whileHover={!selected ? { scale: 1.03, x: -4 } : {}}
              whileTap={!selected ? { scale: 0.97 } : {}}
              className="flex items-center gap-4 h-16 px-6 text-2xl font-bold rounded-2xl shadow-lg border-4 border-gray-200"
              style={{ backgroundColor: bg }}
            >
              <span className="text-3xl">{option.emoji}</span>
              <span>{option.word}</span>
            </motion.button>
          )
        })}
      </div>
    </div>
  )
}
