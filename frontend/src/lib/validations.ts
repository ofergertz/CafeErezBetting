import { z } from 'zod'

// ─── Israeli ID (Luhn-like algorithm) ────────────────────────────────────────
export function validateIsraeliId(id: string): boolean {
  if (!/^\d{9}$/.test(id)) return false
  const sum = id
    .split('')
    .reduce((acc, digit, index) => {
      let val = parseInt(digit) * (index % 2 === 0 ? 1 : 2)
      if (val > 9) val -= 9
      return acc + val
    }, 0)
  return sum % 10 === 0
}

// ─── Israeli Phone ────────────────────────────────────────────────────────────
export function validateIsraeliPhone(phone: string): boolean {
  return /^0(50|52|53|54|55|58|2|3|4|8|9)\d{7}$/.test(phone.replace(/[-\s]/g, ''))
}

// ─── Zod schemas ─────────────────────────────────────────────────────────────

export const customerSchema = z.object({
  firstName: z.string().min(2, 'validation.required'),
  lastName:  z.string().min(2, 'validation.required'),
  idNumber:  z.string().refine(validateIsraeliId, 'validation.invalidId'),
  phone:     z.string().refine(validateIsraeliPhone, 'validation.invalidPhone'),
})

export const debtSchema = z.object({
  category:       z.enum(['store', 'winner', 'toto', 'lotto', 'chance', '777', 'other']),
  description:    z.string().optional(),
  originalAmount: z.number().positive('validation.positiveAmount').multipleOf(0.01),
  paidAmount:     z.number().min(0).multipleOf(0.01).optional(),
})

export const otpSendSchema = z.object({
  phone: z.string().refine(validateIsraeliPhone, 'validation.invalidPhone'),
})

export const otpVerifySchema = z.object({
  phone: z.string(),
  code:  z.string().length(6).regex(/^\d+$/, 'validation.required'),
})

export const adminLoginSchema = z.object({
  username: z.string().min(1, 'validation.required'),
  password: z.string().min(1, 'validation.required'),
})

export const winnerStakeSchema = z.object({
  stake: z.number().positive('validation.positiveAmount').multipleOf(0.01),
})

export const lottoRowSchema = z.object({
  numbers: z
    .array(z.number().int().min(1).max(37))
    .length(6)
    .refine((nums) => new Set(nums).size === 6, 'validation.validation'),
  strong: z.number().int().min(1).max(7),
})

export const chanceRowSchema = z.object({
  numbers: z
    .array(z.number().int().min(1).max(36))
    .length(5)
    .refine((nums) => new Set(nums).size === 5, 'validation.validation'),
})

export const lucky777RowSchema = z.object({
  numbers: z
    .array(z.number().int().min(1).max(70))
    .length(7)
    .refine((nums) => new Set(nums).size === 7, 'validation.validation'),
})

export type CustomerFormData  = z.infer<typeof customerSchema>
export type DebtFormData      = z.infer<typeof debtSchema>
export type OtpSendData       = z.infer<typeof otpSendSchema>
export type OtpVerifyData     = z.infer<typeof otpVerifySchema>
export type AdminLoginData    = z.infer<typeof adminLoginSchema>
