import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import he from './locales/he.json'
import ru from './locales/ru.json'
import en from './locales/en.json'

const RTL_LANGUAGES = ['he']

export function applyDirection(lang: string) {
  const dir = RTL_LANGUAGES.includes(lang) ? 'rtl' : 'ltr'
  document.documentElement.dir = dir
  document.documentElement.lang = lang
}

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: { he: { translation: he }, ru: { translation: ru }, en: { translation: en } },
    fallbackLng: 'he',
    supportedLngs: ['he', 'ru', 'en'],
    interpolation: { escapeValue: false },
    detection: {
      order: ['localStorage', 'navigator'],
      caches: ['localStorage'],
    },
  })

// Apply direction on load and on language change
applyDirection(i18n.language)
i18n.on('languageChanged', applyDirection)

export default i18n
