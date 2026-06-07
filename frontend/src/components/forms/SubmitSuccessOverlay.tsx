import { useTranslation } from 'react-i18next'

interface Props {
  visible: boolean
}

export function SubmitSuccessOverlay({ visible }: Props) {
  const { t } = useTranslation()

  if (!visible) return null

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="card p-12 flex flex-col items-center gap-4 shadow-2xl animate-[scale-in_0.2s_ease-out]">
        <svg
          viewBox="0 0 52 52"
          width="80"
          height="80"
          className="text-[--color-success]"
          aria-hidden="true"
        >
          <circle cx="26" cy="26" r="25" fill="none" stroke="currentColor" strokeWidth="2" opacity="0.2" />
          <path
            fill="none"
            stroke="currentColor"
            strokeWidth="3"
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M14 27 l9 9 l15-16"
            style={{
              strokeDasharray: 38,
              strokeDashoffset: 0,
              animation: 'dash-in 0.4s ease-out 0.1s both',
            }}
          />
        </svg>
        <style>{`
          @keyframes dash-in {
            from { stroke-dashoffset: 38; }
            to { stroke-dashoffset: 0; }
          }
          @keyframes scale-in {
            from { transform: scale(0.85); opacity: 0; }
            to { transform: scale(1); opacity: 1; }
          }
        `}</style>
        <p className="text-3xl font-bold text-[--color-success]">
          {t('forms.submitSuccess')}
        </p>
      </div>
    </div>
  )
}
