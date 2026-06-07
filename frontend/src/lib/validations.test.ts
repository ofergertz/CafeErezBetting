import { describe, it, expect } from 'vitest'
import { validateIsraeliId, validateIsraeliPhone } from './validations'

describe('validateIsraeliId', () => {
  it('accepts a valid ID', () => {
    expect(validateIsraeliId('123456782')).toBe(true)
  })

  it('accepts all-zeros (sum=0, 0%10=0)', () => {
    expect(validateIsraeliId('000000000')).toBe(true)
  })

  it('rejects invalid checksum', () => {
    expect(validateIsraeliId('123456789')).toBe(false)
  })

  it('rejects 8-digit input', () => {
    expect(validateIsraeliId('12345678')).toBe(false)
  })

  it('rejects 10-digit input', () => {
    expect(validateIsraeliId('1234567890')).toBe(false)
  })

  it('rejects non-numeric input', () => {
    expect(validateIsraeliId('abcdefghi')).toBe(false)
  })
})

describe('validateIsraeliPhone', () => {
  it('accepts 050 prefix', () => {
    expect(validateIsraeliPhone('0501234567')).toBe(true)
  })

  it('accepts 054 prefix', () => {
    expect(validateIsraeliPhone('0541234567')).toBe(true)
  })

  it('accepts phone with dashes', () => {
    expect(validateIsraeliPhone('050-1234567')).toBe(true)
  })

  it('rejects landline prefix', () => {
    expect(validateIsraeliPhone('0721234567')).toBe(false)
  })

  it('rejects too-short number', () => {
    expect(validateIsraeliPhone('050123')).toBe(false)
  })

  it('rejects empty string', () => {
    expect(validateIsraeliPhone('')).toBe(false)
  })
})
