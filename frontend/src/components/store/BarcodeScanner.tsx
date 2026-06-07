import { useEffect, useRef, useState, useCallback } from 'react'
import { ScanBarcode } from 'lucide-react'
import { api } from '@/lib/api'

interface Product {
  id: string
  name: string
  barcode?: string
}

interface BarcodeScannerProps {
  onFound: (productId: string) => void
}

export default function BarcodeScanner({ onFound }: BarcodeScannerProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const bufferRef = useRef<string>('')
  const lastKeyTime = useRef<number>(0)
  const [toast, setToast] = useState<string | null>(null)

  const handleScan = useCallback(async (barcode: string) => {
    if (!barcode.trim()) return
    try {
      const product = await api.get<Product>(`/api/products/barcode/${encodeURIComponent(barcode)}`)
      // Update query cache (product exists)
      onFound(product.id)
    } catch {
      setToast('מוצר לא נמצא')
      setTimeout(() => setToast(null), 2500)
    }
  }, [onFound])

  const handleKeyDown = useCallback((e: KeyboardEvent) => {
    const now = Date.now()
    const elapsed = now - lastKeyTime.current
    lastKeyTime.current = now

    if (e.key === 'Enter') {
      const barcode = bufferRef.current
      bufferRef.current = ''
      if (barcode.length >= 3) {
        handleScan(barcode)
      }
      return
    }

    // Barcode scanners type very fast (< 100ms between keys)
    if (elapsed > 100) {
      bufferRef.current = ''
    }

    if (e.key.length === 1) {
      bufferRef.current += e.key
    }
  }, [handleScan])

  // Focus the hidden input & keep it focused
  const focusInput = useCallback(() => {
    inputRef.current?.focus()
  }, [])

  useEffect(() => {
    focusInput()
    document.addEventListener('click', focusInput)
    return () => document.removeEventListener('click', focusInput)
  }, [focusInput])

  useEffect(() => {
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [handleKeyDown])

  return (
    <>
      <input
        ref={inputRef}
        type="text"
        className="sr-only"
        aria-hidden="true"
        readOnly
        tabIndex={-1}
      />
      {toast && (
        <div className="fixed bottom-20 left-1/2 -translate-x-1/2 z-50 bg-gray-800 text-white text-sm px-4 py-2 rounded-lg shadow-lg">
          {toast}
        </div>
      )}
      <div className="mb-4 p-3 bg-blue-50 border border-blue-200 rounded-lg text-sm text-blue-700 text-center flex items-center justify-center gap-2">
        <ScanBarcode size={16} /> מצב סריקה פעיל — כוון את הסורק למוצר
      </div>
    </>
  )
}
