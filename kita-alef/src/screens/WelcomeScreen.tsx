import { useState } from 'react'
import { motion } from 'framer-motion'
import { useStore } from '../store'
import { Button } from '../components/ui/Button'

export function WelcomeScreen() {
  const { playerName, setPlayerName, goToWorld } = useStore()
  const [name, setName] = useState(playerName)

  const handleStart = () => {
    if (!name.trim()) return
    setPlayerName(name.trim())
    goToWorld()
  }

  return (
    <div className="flex flex-col items-center justify-center h-screen bg-gradient-to-b from-sky-300 to-green-300 p-8 gap-8">
      {/* Title */}
      <motion.div
        className="text-center"
        initial={{ y: -50, opacity: 0 }}
        animate={{ y: 0, opacity: 1 }}
        transition={{ duration: 0.7 }}
      >
        <motion.div
          className="text-8xl mb-4"
          animate={{ rotate: [0, 5, -5, 0] }}
          transition={{ duration: 2, repeat: Infinity }}
        >
          🎒
        </motion.div>
        <h1 className="text-5xl font-black text-white drop-shadow-lg">מוכן לכיתה א׳!</h1>
        <p className="text-2xl text-white/90 mt-2 font-medium">המסע מתחיל כאן</p>
      </motion.div>

      {/* Name input */}
      <motion.div
        className="bg-white/90 rounded-3xl p-8 shadow-2xl w-full max-w-sm flex flex-col gap-5 items-center"
        initial={{ scale: 0.8, opacity: 0 }}
        animate={{ scale: 1, opacity: 1 }}
        transition={{ delay: 0.3, duration: 0.5 }}
      >
        <p className="text-2xl font-bold text-gray-700">מה השם שלך?</p>
        <input
          type="text"
          value={name}
          onChange={(e) => setName(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleStart()}
          placeholder="כתוב/י את שמך..."
          className="w-full border-3 border-sky-300 rounded-2xl px-5 py-4 text-2xl text-center focus:outline-none focus:border-sky-500 bg-sky-50"
          dir="rtl"
          maxLength={20}
          autoFocus
        />
        <Button onClick={handleStart} color="#4ECDC4" size="lg" disabled={!name.trim()}>
          בואו נתחיל! 🚀
        </Button>
      </motion.div>

      {/* Decorations */}
      <motion.div
        className="flex gap-6 text-4xl"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ delay: 0.6 }}
      >
        {['✏️', '📚', '🔤', '🌟', '🏫'].map((emoji, i) => (
          <motion.span
            key={i}
            animate={{ y: [0, -8, 0] }}
            transition={{ duration: 2, delay: i * 0.2, repeat: Infinity }}
          >
            {emoji}
          </motion.span>
        ))}
      </motion.div>
    </div>
  )
}
