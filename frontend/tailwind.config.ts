import type { Config } from 'tailwindcss'

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        brand: {
          50:  '#fefce8',
          100: '#fef9c3',
          200: '#fef08a',
          300: '#fde047',
          400: '#facc15',
          500: '#eab308',
          600: '#ca8a04',
          700: '#a16207',
          800: '#854d0e',
          900: '#713f12',
        },
        kiosk: {
          bg:      '#fffbf5',
          card:    '#ffffff',
          border:  '#e8e0d0',
          accent:  '#2d6a4f',
          amber:   '#d97706',
          danger:  '#dc2626',
          success: '#16a34a',
          pending: '#d97706',
          sent:    '#16a34a',
        },
      },
      fontFamily: {
        display: ['Rubik', 'system-ui', 'sans-serif'],
        body:    ['Inter', 'system-ui', 'sans-serif'],
      },
      borderRadius: {
        kiosk: '12px',
      },
      minHeight: {
        touch: '44px',
      },
      minWidth: {
        touch: '44px',
      },
    },
  },
  plugins: [],
} satisfies Config
