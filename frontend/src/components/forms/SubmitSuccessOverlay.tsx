import { useTranslation } from 'react-i18next'

interface Props {
  visible: boolean
}

export function SubmitSuccessOverlay({ visible }: Props) {
  const { t } = useTranslation()

  if (!visible) return null

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="card p-12 flex flex-col items-center gap-4 shadow-2xl">
        <div className="text-8xl animate-bounce">✅</div>
        {/* Fix 7: emoji moved to locale strings, not hardcoded in JSX */}
        <p className="text-3xl font-bold text-[--color-success]">
          {t('forms.submitSuccess')}
        </p>
      </div>
    </div>
  )
}
