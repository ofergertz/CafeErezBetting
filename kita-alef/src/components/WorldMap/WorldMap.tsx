import { motion } from 'framer-motion'
import { rooms } from '../../content/rooms'
import { useStore } from '../../store'
import { Stars } from '../ui/Stars'

export function WorldMap() {
  const { playerName, enterRoom, getStars, isRoomUnlocked } = useStore()

  return (
    <div
      className="w-full h-screen relative overflow-hidden select-none"
      style={{
        background: 'linear-gradient(180deg, #87CEEB 0%, #87CEEB 60%, #90EE90 60%, #90EE90 100%)',
      }}
    >
      {/* Clouds */}
      <motion.div
        className="absolute text-6xl opacity-70 top-8 left-10"
        animate={{ x: [0, 20, 0] }}
        transition={{ duration: 8, repeat: Infinity, ease: 'easeInOut' }}
      >
        ☁️
      </motion.div>
      <motion.div
        className="absolute text-4xl opacity-50 top-16 right-20"
        animate={{ x: [0, -15, 0] }}
        transition={{ duration: 10, repeat: Infinity, ease: 'easeInOut' }}
      >
        ☁️
      </motion.div>

      {/* Sun */}
      <motion.div
        className="absolute text-5xl top-6 right-8"
        animate={{ rotate: [0, 10, -10, 0] }}
        transition={{ duration: 5, repeat: Infinity }}
      >
        ☀️
      </motion.div>

      {/* Header */}
      <div className="absolute top-4 left-1/2 -translate-x-1/2 text-center">
        <p className="text-xl font-bold text-blue-900 bg-white/60 px-4 py-1 rounded-full">
          שלום {playerName}! 👋 בחר/י לאן ללכת
        </p>
      </div>

      {/* Path between rooms - simple decorative dots */}
      <svg className="absolute inset-0 w-full h-full pointer-events-none" style={{ zIndex: 0 }}>
        <line x1="25%" y1="55%" x2="45%" y2="25%" stroke="#F4A460" strokeWidth="4" strokeDasharray="10,8" opacity="0.6" />
        <line x1="65%" y1="55%" x2="45%" y2="25%" stroke="#F4A460" strokeWidth="4" strokeDasharray="10,8" opacity="0.6" />
      </svg>

      {/* Trees decoration */}
      {['15%', '80%', '10%', '85%'].map((pos, i) => (
        <div key={i} className="absolute text-4xl" style={{ left: pos, bottom: i < 2 ? '18%' : '8%' }}>
          🌳
        </div>
      ))}
      <div className="absolute text-3xl" style={{ left: '50%', bottom: '10%' }}>🌸</div>

      {/* Room doors */}
      {rooms.map((room) => {
        const stars = getStars(room.id)
        const unlocked = isRoomUnlocked(room.id, room.unlocksAfter, room.requiredStars)

        return (
          <motion.div
            key={room.id}
            className="absolute flex flex-col items-center cursor-pointer"
            style={{ left: `${room.position.x}%`, top: `${room.position.y}%`, transform: 'translate(-50%, -50%)', zIndex: 10 }}
            whileHover={unlocked ? { scale: 1.1 } : {}}
            whileTap={unlocked ? { scale: 0.95 } : {}}
            onClick={() => unlocked && enterRoom(room.id)}
          >
            {/* Door / building */}
            <div
              className="relative flex flex-col items-center justify-center rounded-2xl shadow-xl border-4 border-white"
              style={{
                width: 100,
                height: 110,
                background: unlocked ? room.color : '#aaa',
                opacity: unlocked ? 1 : 0.6,
              }}
            >
              <span className="text-5xl">{room.icon}</span>
              {!unlocked && (
                <div className="absolute inset-0 flex items-center justify-center rounded-2xl bg-black/30">
                  <span className="text-3xl">🔒</span>
                </div>
              )}
              {/* Pulse ring for unlocked rooms with no stars yet */}
              {unlocked && stars === 0 && (
                <motion.div
                  className="absolute inset-0 rounded-2xl border-4 border-yellow-300"
                  animate={{ scale: [1, 1.15, 1], opacity: [0.8, 0, 0.8] }}
                  transition={{ duration: 2, repeat: Infinity }}
                />
              )}
            </div>

            {/* Room name */}
            <div className="mt-1 bg-white/90 px-3 py-1 rounded-full text-sm font-bold text-gray-700 shadow text-center whitespace-nowrap">
              {room.name}
            </div>

            {/* Stars */}
            {stars > 0 && (
              <div className="mt-1">
                <Stars count={stars} />
              </div>
            )}

            {/* Unlock hint */}
            {!unlocked && (
              <div className="mt-1 bg-white/80 px-2 py-0.5 rounded-full text-xs text-gray-500 text-center">
                השלם קודם ⭐{room.requiredStars}
              </div>
            )}
          </motion.div>
        )
      })}
    </div>
  )
}
