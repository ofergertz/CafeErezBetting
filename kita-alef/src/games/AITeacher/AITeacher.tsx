import { useState, useRef, useEffect } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { useStore } from '../../store'
import { Button } from '../../components/ui/Button'

interface Message {
  role: 'user' | 'assistant'
  content: string
}

const SYSTEM_PROMPT = `אתה רינה, מורה חמה, סבלנית ועליזה לילדים בגיל 5-6 שעומדים להתחיל כיתה א׳.

חוקים חשובים:
- דבר/י עברית פשוטה וברורה, בגובה עיניים של ילד.
- תמיד חיובי/ת, מעודד/ת, ולא שיפוטי/ת. אף פעם לא תגיד "טעות" — רק "כמעט" / "בוא ננסה ביחד".
- כל תשובה קצרה — מקסימום 2-3 משפטים קצרים.
- תתמקד/י אך ורק בנושאים הקשורים למוכנות לכיתה א׳: אותיות, צלילים, מספרים, רגשות, חברים, בית ספר.
- אם ילד שואל על נושא אחר, החזר/י אותו בחביבות: "זה מעניין! בואו נדבר על..." ושוב למשחקים.
- אסור לדון בנושאים כמו: מדיה, חדשות, אלימות, דברים לא מתאימים לגיל.
- פתח/י כל שיחה בשאלה חמה ופתוחה.
- השתמש/י לפעמים באמוג׳י כדי להיות עליז/ה 🌟😊`

const WELCOME_MESSAGE: Message = {
  role: 'assistant',
  content: 'שלום! אני רינה המורה שלך 👩‍🏫 כיף שבאת לבקר! איך אתה מרגיש היום? 😊',
}

export function AITeacher() {
  const { exitRoom, playerName } = useStore()
  const [messages, setMessages] = useState<Message[]>([WELCOME_MESSAGE])
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const [isTalking, setIsTalking] = useState(false)
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  const speak = (text: string) => {
    if ('speechSynthesis' in window) {
      window.speechSynthesis.cancel()
      const u = new SpeechSynthesisUtterance(text)
      u.lang = 'he-IL'
      u.rate = 0.85
      u.pitch = 1.1
      setIsTalking(true)
      u.onend = () => setIsTalking(false)
      window.speechSynthesis.speak(u)
    }
  }

  const sendMessage = async () => {
    const text = input.trim()
    if (!text || loading) return
    setInput('')

    const newMessages: Message[] = [...messages, { role: 'user', content: text }]
    setMessages(newMessages)
    setLoading(true)

    try {
      const response = await fetch('/api/teacher', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          model: 'claude-haiku-4-5-20251001',
          max_tokens: 200,
          system: SYSTEM_PROMPT + `\nשם הילד/ה: ${playerName}`,
          messages: newMessages.map((m) => ({ role: m.role, content: m.content })),
        }),
      })

      const data = await response.json()
      const reply = data.content?.[0]?.text ?? 'אני כאן! 😊'
      const assistantMessage: Message = { role: 'assistant', content: reply }
      setMessages([...newMessages, assistantMessage])
      speak(reply)
    } catch {
      const errorMsg: Message = {
        role: 'assistant',
        content: 'אופס, קרה משהו. בוא ננסה שוב! 🌟',
      }
      setMessages([...newMessages, errorMsg])
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex flex-col h-screen bg-gradient-to-b from-yellow-50 to-amber-50">
      {/* Header */}
      <div className="flex items-center gap-3 p-4 bg-yellow-400/80 shadow">
        <Button onClick={exitRoom} color="#aaa" size="sm">← חזרה</Button>
        <div className="flex items-center gap-2 flex-1 justify-center">
          <span className="text-3xl">👩‍🏫</span>
          <h1 className="text-2xl font-black text-yellow-900">הבית של רינה</h1>
        </div>
      </div>

      {/* Teacher character */}
      <div className="flex justify-center pt-4 pb-2">
        <motion.div
          className="relative"
          animate={isTalking ? { y: [0, -4, 0] } : { y: 0 }}
          transition={{ duration: 0.3, repeat: isTalking ? Infinity : 0 }}
        >
          <div className="text-7xl">👩‍🏫</div>
          {isTalking && (
            <motion.div
              className="absolute -top-2 -right-2 text-xl"
              animate={{ scale: [1, 1.2, 1], opacity: [1, 0.5, 1] }}
              transition={{ duration: 0.5, repeat: Infinity }}
            >
              🔊
            </motion.div>
          )}
        </motion.div>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto px-4 py-2 flex flex-col gap-3">
        <AnimatePresence>
          {messages.map((msg, i) => (
            <motion.div
              key={i}
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              className={`flex ${msg.role === 'user' ? 'justify-start' : 'justify-end'}`}
            >
              <div
                className={`max-w-[80%] px-4 py-3 rounded-2xl text-lg font-medium shadow ${
                  msg.role === 'user'
                    ? 'bg-blue-100 text-blue-900 rounded-br-sm'
                    : 'bg-yellow-300 text-yellow-900 rounded-bl-sm'
                }`}
              >
                {msg.content}
                {msg.role === 'assistant' && (
                  <button
                    onClick={() => speak(msg.content)}
                    className="mr-2 text-sm opacity-60 hover:opacity-100"
                  >
                    🔊
                  </button>
                )}
              </div>
            </motion.div>
          ))}
        </AnimatePresence>

        {loading && (
          <motion.div
            className="flex justify-end"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
          >
            <div className="bg-yellow-300 px-5 py-3 rounded-2xl rounded-bl-sm shadow">
              <motion.span
                animate={{ opacity: [1, 0.3, 1] }}
                transition={{ duration: 1, repeat: Infinity }}
                className="text-yellow-900 text-lg"
              >
                רינה חושבת...
              </motion.span>
            </div>
          </motion.div>
        )}
        <div ref={bottomRef} />
      </div>

      {/* Input */}
      <div className="p-4 bg-white border-t border-yellow-200 flex gap-3 items-center">
        <input
          type="text"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && sendMessage()}
          placeholder="כתוב/י לרינה..."
          className="flex-1 border-2 border-yellow-300 rounded-2xl px-4 py-3 text-lg focus:outline-none focus:border-yellow-500 bg-yellow-50"
          dir="rtl"
          disabled={loading}
        />
        <motion.button
          onClick={sendMessage}
          disabled={!input.trim() || loading}
          whileHover={{ scale: 1.05 }}
          whileTap={{ scale: 0.95 }}
          className="w-14 h-14 rounded-full bg-yellow-400 text-2xl shadow-lg disabled:opacity-40 flex items-center justify-center"
        >
          ➤
        </motion.button>
      </div>
    </div>
  )
}
