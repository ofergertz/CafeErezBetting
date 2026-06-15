import { AnimatePresence, motion } from 'framer-motion'
import { useStore } from './store'
import { WorldMap } from './components/WorldMap/WorldMap'
import { FirstSoundGame } from './games/FirstSoundGame/FirstSoundGame'
import { RhymingGame } from './games/RhymingGame/RhymingGame'
import { AITeacher } from './games/AITeacher/AITeacher'
import { WelcomeScreen } from './screens/WelcomeScreen'

export function App() {
  const { screen, activeRoomId } = useStore()

  const renderGame = () => {
    if (activeRoomId === 'first-sound') return <FirstSoundGame />
    if (activeRoomId === 'rhyming') return <RhymingGame />
    if (activeRoomId === 'ai-teacher') return <AITeacher />
    return null
  }

  return (
    <div className="w-full h-screen overflow-hidden font-hebrew" dir="rtl">
      <AnimatePresence mode="wait">
        {screen === 'welcome' && (
          <motion.div key="welcome" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}>
            <WelcomeScreen />
          </motion.div>
        )}
        {screen === 'world' && (
          <motion.div
            key="world"
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, scale: 1.05 }}
            transition={{ duration: 0.4 }}
          >
            <WorldMap />
          </motion.div>
        )}
        {screen === 'game' && (
          <motion.div
            key={activeRoomId}
            initial={{ opacity: 0, x: -30 }}
            animate={{ opacity: 1, x: 0 }}
            exit={{ opacity: 0, x: 30 }}
            transition={{ duration: 0.3 }}
          >
            {renderGame()}
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}
