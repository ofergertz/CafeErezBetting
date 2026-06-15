import { useState, useEffect } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { firstSoundRounds } from '../../content/games/firstSound'
import { useStore } from '../../store'
import { Stars } from '../../components/ui/Stars'
import { Button } from '../../components/ui/Button'

const ROUNDS_PER_GAME = 5

export function FirstSoundGame() {
  const { exitRoom, saveProgress } = useStore()
  const [rounds] = useState(() => [...firstSoundRounds].sort(() => Math.random() - 0.5).slice(0, ROUNDS_PER_GAME))
  const [currentIndex, setCurrentIndex] = useState(0)
  const [selected, setSelected] = useState<string | null>(null)
  const [score, setScore] = useState(0)
  const [finished, setFinished] = useState(false)
  const [shake, setShake] = useState(false)

  const round = rounds[currentIndex]

  const speak = (text: string) => {
    if ('speechSynthesis' in window) {
      const utterance = new SpeechSynthesisUtterance(text)
      utterance.lang = 'he-IL'
      utterance.rate = 0.85
      window.speechSynthesis.speak(utterance)
    }
  }

  useEffect(() => {
    if (round) setTimeout(() => speak(round.word), 300)
  }, [currentIndex])

  const handleSelect = (letter: string) => {
    if (selected) return
    setSelected(letter)
    const correct = letter === round.correct

    if (correct) {
      setScore((s) => s + 1)
      speak('כל הכבוד!')
    } else {
      setShake(true)
      speak('כמעט! נסה שוב')
      setTimeout(() => setShake(false), 600)
    }

    setTimeout(() => {
      if (currentIndex + 1 >= ROUNDS_PER_GAME) {
        setFinished(true)
      } else {
        setCurrentIndex((i) => i + 1)
        setSelected(null)
      }
    }, 1200)
  }

  const stars = score >= 5 ? 3 : score >= 3 ? 2 : score >= 1 ? 1 : 0

  if (finished) {
    saveProgress('first-sound', stars)
    return (
      <motion.div
        className="flex flex-col items-center justify-center h-screen bg-gradient-to-b from-orange-100 to-yellow-50 gap-6 p-8"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
      >
        <motion.div
          className="text-8xl"
          animate={{ scale: [1, 1.3, 1], rotate: [0, 10, -10, 0] }}
          transition={{ duration: 1, repeat: 2 }}
        >
          🎉
        </motion.div>
        <h2 className="text-4xl font-black text-orange-600">כל הכבוד!</h2>
        <p className="text-2xl text-gray-700">
          ענית נכון על {score} מתוך {ROUNDS_PER_GAME}
        </p>
        <Stars count={stars} size="lg" />
        <Button onClick={exitRoom} color="#FF6B6B" size="lg">
          חזרה לעולם 🗺️
        </Button>
      </motion.div>
    )
  }

  return (
    <div className="flex flex-col items-center justify-between h-screen bg-gradient-to-b from-red-100 to-orange-50 p-6 pt-4">
      {/* Header */}
      <div className="w-full flex justify-between items-center">
        <Button onClick={exitRoom} color="#aaa" size="sm">← חזרה</Button>
        <div className="text-center">
          <p className="text-sm text-gray-500">שאלה {currentIndex + 1} מתוך {ROUNDS_PER_GAME}</p>
          <div className="flex gap-1 mt-1">
            {rounds.map((_, i) => (
              <div key={i} className={`w-3 h-3 rounded-full ${i < currentIndex ? 'bg-green-400' : i === currentIndex ? 'bg-orange-400' : 'bg-gray-200'}`} />
            ))}
          </div>
        </div>
        <p className="text-xl font-bold">✅ {score}</p>
      </div>

      {/* Question */}
      <AnimatePresence mode="wait">
        <motion.div
          key={currentIndex}
          className="flex flex-col items-center gap-4"
          initial={{ opacity: 0, y: 30 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: -30 }}
        >
          <p className="text-2xl font-bold text-gray-600">מה הצליל הראשון?</p>

          <motion.div
            className="text-9xl cursor-pointer"
            animate={shake ? { x: [-10, 10, -10, 10, 0] } : {}}
            onClick={() => speak(round.word)}
          >
            {round.emoji}
          </motion.div>

          <div className="flex items-center gap-3">
            <span className="text-4xl font-black text-gray-800">{round.word}</span>
            <button onClick={() => speak(round.word)} className="text-2xl opacity-60 hover:opacity-100">🔊</button>
          </div>
        </motion.div>
      </AnimatePresence>

      {/* Options */}
      <div className="grid grid-cols-2 gap-4 w-full max-w-xs mb-4">
        {round.options.map((letter) => {
          const isSelected = selected === letter
          const isCorrect = letter === round.correct
          let bg = '#fff'
          if (isSelected && isCorrect) bg = '#4ade80'
          else if (isSelected && !isCorrect) bg = '#f87171'
          else if (selected && isCorrect) bg = '#4ade80'

          return (
            <motion.button
              key={letter}
              onClick={() => handleSelect(letter)}
              disabled={!!selected}
              whileHover={!selected ? { scale: 1.05 } : {}}
              whileTap={!selected ? { scale: 0.95 } : {}}
              className="h-20 text-4xl font-black rounded-2xl shadow-lg border-4 border-gray-200"
              style={{ backgroundColor: bg }}
            >
              {letter}
            </motion.button>
          )
        })}
      </div>
    </div>
  )
}
