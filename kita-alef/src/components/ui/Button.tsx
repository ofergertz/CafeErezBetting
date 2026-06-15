import { motion } from 'framer-motion'

interface ButtonProps {
  children: React.ReactNode
  onClick: () => void
  color?: string
  disabled?: boolean
  size?: 'sm' | 'md' | 'lg'
}

export function Button({ children, onClick, color = '#4ECDC4', disabled = false, size = 'md' }: ButtonProps) {
  const padding = size === 'lg' ? 'px-10 py-5 text-2xl' : size === 'sm' ? 'px-4 py-2 text-sm' : 'px-6 py-3 text-lg'

  return (
    <motion.button
      onClick={onClick}
      disabled={disabled}
      whileHover={disabled ? {} : { scale: 1.05 }}
      whileTap={disabled ? {} : { scale: 0.95 }}
      className={`${padding} rounded-2xl font-bold text-white shadow-lg transition-opacity`}
      style={{ backgroundColor: disabled ? '#ccc' : color }}
    >
      {children}
    </motion.button>
  )
}
