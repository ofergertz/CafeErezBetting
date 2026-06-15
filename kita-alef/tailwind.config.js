/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      fontFamily: {
        hebrew: ['Segoe UI', 'Arial', 'sans-serif'],
      },
      colors: {
        world: {
          sky: '#87CEEB',
          grass: '#90EE90',
          path: '#F4A460',
        },
      },
    },
  },
  plugins: [],
}
