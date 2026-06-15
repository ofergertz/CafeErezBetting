import express from 'express'
import { createServer as createViteServer } from 'vite'
import { fileURLToPath } from 'url'
import { dirname, join } from 'path'

const __dirname = dirname(fileURLToPath(import.meta.url))
const isDev = process.env.NODE_ENV !== 'production'
const PORT = process.env.PORT || 3001

async function main() {
  const app = express()
  app.use(express.json())

  // AI Teacher proxy — API key stays on server only
  app.post('/api/teacher', async (req, res) => {
    const apiKey = process.env.ANTHROPIC_API_KEY
    if (!apiKey) {
      return res.status(500).json({ error: 'ANTHROPIC_API_KEY not set on server' })
    }

    try {
      const response = await fetch('https://api.anthropic.com/v1/messages', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'x-api-key': apiKey,
          'anthropic-version': '2023-06-01',
        },
        body: JSON.stringify(req.body),
      })

      const data = await response.json()
      res.json(data)
    } catch (err) {
      res.status(500).json({ error: 'Failed to reach AI' })
    }
  })

  if (isDev) {
    // In dev: Vite handles the frontend, Express handles /api
    const vite = await createViteServer({ server: { middlewareMode: true } })
    app.use(vite.middlewares)
  } else {
    // In production: serve the Vite build
    app.use(express.static(join(__dirname, 'dist')))
    app.get('*', (_, res) => res.sendFile(join(__dirname, 'dist/index.html')))
  }

  app.listen(PORT, () => {
    console.log(`Server running on http://localhost:${PORT}`)
    if (isDev) console.log('Frontend: Vite dev mode active')
  })
}

main()
