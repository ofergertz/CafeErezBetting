import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { api } from '@/lib/api'
import { useAuthStore } from '@/store/authStore'
import BarcodeScanner from '@/components/store/BarcodeScanner'
import { Pencil, Trash2, ShoppingBag } from 'lucide-react'

// ── Types ────────────────────────────────────────────────────────────────────
interface Product {
  id: string
  name: string
  description?: string
  price: number
  imageUrl?: string
  inStock: boolean
  createdAt: string
  barcode?: string
}

// ── Schema ───────────────────────────────────────────────────────────────────
const schema = z.object({
  name: z.string().min(1, 'Name is required'),
  description: z.string().optional(),
  price: z
    .number({ invalid_type_error: 'Price required' })
    .positive('Price must be positive'),
  imageUrl: z
    .string()
    .url('Must be a valid URL')
    .optional()
    .or(z.literal('')),
  inStock: z.boolean(),
  barcode: z.string().max(50).optional().or(z.literal('')),
})

type FormValues = z.infer<typeof schema>

// ── Product Form Modal ────────────────────────────────────────────────────────
function ProductModal({
  product,
  onClose,
}: {
  product?: Product
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!product

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: product?.name ?? '',
      description: product?.description ?? '',
      price: product?.price ?? undefined,
      imageUrl: product?.imageUrl ?? '',
      inStock: product?.inStock ?? true,
      barcode: product?.barcode ?? '',
    },
  })

  const createMutation = useMutation({
    mutationFn: (data: FormValues) => api.post<Product>('/api/products', data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] })
      onClose()
    },
  })

  const updateMutation = useMutation({
    mutationFn: (data: FormValues) =>
      api.put<Product>(`/api/products/${product!.id}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] })
      onClose()
    },
  })

  const onSubmit = (data: FormValues) => {
    const payload = {
      ...data,
      imageUrl: data.imageUrl || undefined,
      description: data.description || undefined,
      barcode: data.barcode || undefined,
    }
    if (isEdit) {
      updateMutation.mutate(payload)
    } else {
      createMutation.mutate(payload)
    }
  }

  const isPending = isSubmitting || createMutation.isPending || updateMutation.isPending
  const serverError =
    (createMutation.error as Error)?.message ||
    (updateMutation.error as Error)?.message

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
      onClick={onClose}
    >
      <div
        className="bg-white rounded-xl shadow-2xl w-full max-w-md mx-4 p-6"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex justify-between items-center mb-5">
          <h2 className="text-lg font-bold">
            {isEdit ? t('store.editProduct') : t('store.addProduct')}
          </h2>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none"
          >
            ✕
          </button>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" dir="rtl">
          {/* Name */}
          <div>
            <label className="block text-sm font-medium mb-1">שם מוצר *</label>
            <input
              {...register('name')}
              className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
            />
            {errors.name && (
              <p className="text-red-500 text-xs mt-1">{errors.name.message}</p>
            )}
          </div>

          {/* Description */}
          <div>
            <label className="block text-sm font-medium mb-1">תיאור</label>
            <textarea
              {...register('description')}
              rows={2}
              className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 resize-none"
            />
          </div>

          {/* Price */}
          <div>
            <label className="block text-sm font-medium mb-1">
              {t('store.price')} (₪) *
            </label>
            <input
              type="number"
              step="0.01"
              min="0.01"
              {...register('price', { valueAsNumber: true })}
              className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
            />
            {errors.price && (
              <p className="text-red-500 text-xs mt-1">{errors.price.message}</p>
            )}
          </div>

          {/* Image URL */}
          <div>
            <label className="block text-sm font-medium mb-1">קישור לתמונה</label>
            <input
              {...register('imageUrl')}
              placeholder="https://..."
              className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
              dir="ltr"
            />
            {errors.imageUrl && (
              <p className="text-red-500 text-xs mt-1">{errors.imageUrl.message}</p>
            )}
          </div>

          {/* Barcode */}
          <div>
            <label className="block text-sm font-medium mb-1">ברקוד</label>
            <input
              {...register('barcode')}
              placeholder="לדוגמה: 1234567890123"
              className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
              dir="ltr"
            />
            {errors.barcode && (
              <p className="text-red-500 text-xs mt-1">{errors.barcode.message}</p>
            )}
          </div>

          {/* In Stock */}
          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              {...register('inStock')}
              id="inStock"
              className="w-4 h-4 accent-green-600"
            />
            <label htmlFor="inStock" className="text-sm font-medium">
              {t('store.inStock')}
            </label>
          </div>

          {serverError && (
            <p className="text-red-500 text-sm">{serverError}</p>
          )}

          {/* Buttons */}
          <div className="flex gap-2 justify-start pt-2">
            <button
              type="submit"
              disabled={isPending}
              className="btn btn-primary disabled:opacity-50"
            >
              {isPending ? t('common.loading') : t('common.save')}
            </button>
            <button
              type="button"
              onClick={onClose}
              className="btn btn-secondary"
            >
              ביטול
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ── Skeleton Card ─────────────────────────────────────────────────────────────
function SkeletonCard() {
  return (
    <div className="card animate-pulse">
      <div className="bg-gray-200 rounded-lg h-40 mb-3" />
      <div className="h-4 bg-gray-200 rounded w-3/4 mb-2" />
      <div className="h-3 bg-gray-200 rounded w-1/2 mb-3" />
      <div className="h-5 bg-gray-200 rounded w-1/3" />
    </div>
  )
}

// ── Product Card ──────────────────────────────────────────────────────────────
function ProductCard({
  product,
  isAdmin,
  onEdit,
  onDelete,
  highlighted,
}: {
  product: Product
  isAdmin: boolean
  onEdit: (p: Product) => void
  onDelete: (p: Product) => void
  highlighted?: boolean
}) {
  const { t } = useTranslation()

  return (
    <div
      ref={highlighted ? (el) => { if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' }) } : undefined}
      className={`card relative group ${!product.inStock ? 'opacity-75' : ''} ${highlighted ? 'ring-2 ring-blue-400 animate-pulse' : ''}`}
    >
      {/* Image */}
      {product.imageUrl ? (
        <img
          src={product.imageUrl}
          alt={product.name}
          className="w-full h-40 object-cover rounded-lg mb-3"
        />
      ) : (
        <div className="w-full h-40 bg-gray-100 rounded-lg mb-3 flex items-center justify-center text-gray-300">
          <ShoppingBag size={48} strokeWidth={1} />
        </div>
      )}

      {/* Admin overlay */}
      {isAdmin && (
        <div className="absolute top-2 left-2 flex gap-1 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity">
          <button
            onClick={() => onEdit(product)}
            className="bg-white/90 hover:bg-white rounded-full p-1.5 shadow leading-none text-gray-600 hover:text-[#2d6a4f] focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#2d6a4f]"
            title={t('store.editProduct')}
            aria-label={t('store.editProduct')}
          >
            <Pencil size={14} />
          </button>
          <button
            onClick={() => onDelete(product)}
            className="bg-white/90 hover:bg-white rounded-full p-1.5 shadow leading-none text-gray-600 hover:text-red-500 focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-400"
            title={t('store.deleteProduct')}
            aria-label={t('store.deleteProduct')}
          >
            <Trash2 size={14} />
          </button>
        </div>
      )}

      {/* Details */}
      <h3 className="font-semibold text-sm mb-1 truncate">{product.name}</h3>
      {product.description && (
        <p className="text-xs text-gray-500 mb-2 line-clamp-2">
          {product.description}
        </p>
      )}

      <div className="flex items-center justify-between">
        <span className="font-bold text-base text-gray-900">
          ₪{product.price.toFixed(2)}
        </span>
        <span
          className={`text-xs px-2 py-0.5 rounded-full font-medium ${
            product.inStock
              ? 'bg-green-100 text-green-700'
              : 'bg-gray-100 text-gray-500'
          }`}
        >
          {product.inStock ? t('store.inStock') : t('store.outOfStock')}
        </span>
      </div>
      {product.barcode && (
        <p className="text-xs text-gray-400 mt-1">{product.barcode}</p>
      )}
    </div>
  )
}

// ── Main Page ─────────────────────────────────────────────────────────────────
export default function StorePage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isAdmin = useAuthStore((s) => s.isAdmin())

  const [addModal, setAddModal] = useState(false)
  const [editProduct, setEditProduct] = useState<Product | null>(null)
  const [scannerMode, setScannerMode] = useState(false)
  const [highlightedId, setHighlightedId] = useState<string | null>(null)

  const { data: products, isLoading, isError } = useQuery({
    queryKey: ['products'],
    queryFn: () => api.get<Product[]>('/api/products'),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.delete(`/api/products/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['products'] }),
  })

  const handleDelete = (product: Product) => {
    if (window.confirm(t('store.confirmDelete'))) {
      deleteMutation.mutate(product.id)
    }
  }

  return (
    <div dir="rtl" className="p-4 max-w-6xl mx-auto">
      {/* Header */}
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-2xl font-bold">{t('store.title')}</h1>
        <div className="flex gap-2">
          {isAdmin && (
            <button
              onClick={() => setScannerMode(v => !v)}
              className={`btn ${scannerMode ? 'btn-primary' : 'btn-secondary'} flex items-center gap-1.5`}
              title="מצב סריקה"
            >
              מצב סריקה
            </button>
          )}
          {isAdmin && (
            <button
              onClick={() => setAddModal(true)}
              className="btn btn-primary"
            >
              + {t('store.addProduct')}
            </button>
          )}
        </div>
      </div>

      {scannerMode && (
        <BarcodeScanner
          onFound={(id) => setHighlightedId(id)}
        />
      )}

      {/* Loading */}
      {isLoading && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {Array.from({ length: 8 }).map((_, i) => (
            <SkeletonCard key={i} />
          ))}
        </div>
      )}

      {/* Error */}
      {isError && (
        <div className="text-center py-16 text-red-500">שגיאה בטעינת המוצרים</div>
      )}

      {/* Empty */}
      {!isLoading && !isError && products?.length === 0 && (
        <div className="flex flex-col items-center justify-center py-16 text-gray-300">
          <ShoppingBag size={48} strokeWidth={1} />
          <p className="text-gray-400 mt-3">אין מוצרים להצגה</p>
        </div>
      )}

      {/* Products Grid */}
      {!isLoading && !isError && products && products.length > 0 && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {products.map((p) => (
            <ProductCard
              key={p.id}
              product={p}
              isAdmin={isAdmin}
              onEdit={setEditProduct}
              onDelete={handleDelete}
              highlighted={highlightedId === p.id}
            />
          ))}
        </div>
      )}

      {/* Modals */}
      {addModal && <ProductModal onClose={() => setAddModal(false)} />}
      {editProduct && (
        <ProductModal
          product={editProduct}
          onClose={() => setEditProduct(null)}
        />
      )}
    </div>
  )
}
