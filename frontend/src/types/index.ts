// ─── Auth ───────────────────────────────────────────────────────────────────

export type UserRole = 'admin' | 'customer'

export interface AuthUser {
  id: string
  role: UserRole
  phone?: string   // customer
  name?: string    // admin
}

export interface AuthState {
  user: AuthUser | null
  token: string | null
}

// ─── Customer ───────────────────────────────────────────────────────────────

export interface Customer {
  id: string
  firstName: string
  lastName: string
  idNumber: string
  phone: string
  totalDebt: number
  debtCount: number
  createdAt: string
}

export interface DebtRecord {
  id: string
  customerId: string
  category: 'store' | 'winner' | 'toto' | 'lotto' | 'chance' | '777' | 'other'
  description?: string
  originalAmount: number
  paidAmount: number
  balance: number
  status: 'open' | 'partial' | 'settled'
  createdAt: string
}

// ─── Betting Forms ───────────────────────────────────────────────────────────

export type FormType = 'winner' | 'toto' | 'lotto' | 'chance' | '777'
export type FormStatus = 'received' | 'approved' | 'sent'

export interface BettingForm {
  id: string
  type: FormType
  customerId?: string
  customerName?: string
  payload: WinnerPayload | TotoPayload | LottoPayload | ChancePayload | Lucky777Payload
  submittedAt: string
  status: FormStatus
  receivedAt?: string
  approvedAt?: string
  sentAt?: string
}

// ─── Winner ──────────────────────────────────────────────────────────────────

export type WinnerPick = '1' | 'X' | '2'

export interface WinnerMatch {
  id: string
  externalId: string
  homeTeam: string
  awayTeam: string
  league: string
  scheduledAt: string
  odds: { '1': number; X: number; '2': number }
  status: 'upcoming' | 'live' | 'finished' | 'suspended'
  isLive: boolean
  betType?: string | null
  handicap?: string | null
  subMarket?: number | null
  formNumber?: string | null
}

export interface BetSlipItem {
  matchId: string
  homeTeam: string
  awayTeam: string
  pick: WinnerPick
  odds: number
}

export interface WinnerPayload {
  bets: BetSlipItem[]
  stake: number
  totalOdds: number
  potentialWin: number
}

// ─── Toto ────────────────────────────────────────────────────────────────────

export interface TotoMatch {
  id: string
  homeTeam: string
  awayTeam: string
  league: string
  scheduledAt: string
}

export interface TotoPayload {
  roundId: string
  columns: Array<{
    picks: Record<string, WinnerPick> // matchId → pick
  }>
}

// ─── Lotto ───────────────────────────────────────────────────────────────────

export interface LottoRow {
  numbers: number[]  // 6 unique from 1-37
  strong: number     // 1-7
}

export interface LottoPayload {
  rows: LottoRow[]
  costPerRow: number
}

// ─── Chance ──────────────────────────────────────────────────────────────────

export type ChanceSuit = 'spades' | 'hearts' | 'diamonds' | 'clubs'
export type ChanceCard = '7' | '8' | '9' | '10' | 'J' | 'Q' | 'K' | 'A'

export interface ChancePick {
  suit: ChanceSuit
  card: ChanceCard
}

export interface ChancePayload {
  picks: ChancePick[]     // one per suit (4 total)
  stake: number           // 5 | 10 | 25 | 50 | 100 | 250 | 500
  draws: number           // 2 | 3 | 4 | 5 | 7 | 10 | 12
}

// ─── 777 ─────────────────────────────────────────────────────────────────────

export interface Lucky777Row {
  numbers: number[]  // 7 unique from 1-70
}

export interface Lucky777Payload {
  rows: Lucky777Row[]
  costPerRow: number
}

// ─── Store ───────────────────────────────────────────────────────────────────

export interface Product {
  id: string
  name: string
  description?: string
  price: number
  imageUrl?: string
  inStock: boolean
  createdAt: string
  barcode?: string
}

// ─── API Response ────────────────────────────────────────────────────────────

export interface ApiResponse<T> {
  data: T
  message?: string
}

export interface PaginatedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface ApiError {
  message: string
  errors?: Record<string, string[]>
}

export interface SyncStatus {
  lastSync: string | null
  success: boolean
  isMock: boolean
  error: string | null
}

export interface FormSubmittedDto {
  formId: string
  status: string
}
