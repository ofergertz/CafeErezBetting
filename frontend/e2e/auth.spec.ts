/**
 * auth.spec.ts
 * E2E tests for the LoginPage authentication flows:
 *  - Admin login (username/password → JWT)
 *  - Customer OTP send + verify flow
 *  - Logout
 *
 * All API calls are intercepted with page.route() — no real backend required.
 */

import { test, expect, Page } from '@playwright/test'

// ─── Helpers ──────────────────────────────────────────────────────────────────

/** Abort SignalR hub connections so the frontend doesn't stall waiting for WS */
async function blockSignalR(page: Page) {
  await page.route('**/hubs/**', (route) => route.abort())
}

/** Inject an admin auth state into Zustand's persisted localStorage key */
async function setAdminAuth(page: Page) {
  await page.evaluate(() => {
    localStorage.setItem(
      'cafe-erez-auth',
      JSON.stringify({
        state: {
          user: { id: '1', role: 'admin', name: 'מנהל' },
          token: 'fake-admin-jwt',
        },
        version: 0,
      }),
    )
  })
}

// ─── Admin Login ──────────────────────────────────────────────────────────────

test.describe('Admin login flow', () => {
  test.beforeEach(async ({ page }) => {
    await blockSignalR(page)
  })

  test('navigates to /login, fills credentials, submits and redirects home', async ({ page }) => {
    // Mock the admin login endpoint
    await page.route('**/api/auth/admin/login', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          token: 'fake-admin-jwt',
          user: { id: '1', role: 'admin', name: 'מנהל' },
        }),
      })
    })

    // Mock GET /api/winner/matches so WinnerPage doesn't hang
    await page.route('**/api/winner/matches', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      })
    })

    await page.goto('/login')

    // Switch to admin tab
    await page.getByRole('button', { name: /admin/i }).click()

    // Fill credentials
    await page.getByLabel(/שם משתמש|username/i).fill('admin')
    await page.getByLabel(/סיסמה|password/i).fill('Admin1234!')

    // Submit
    await page.getByRole('button', { name: /כניסה|login/i }).click()

    // After redirect the URL should not be /login any more
    await expect(page).not.toHaveURL('/login')
  })

  test('shows error message on invalid credentials', async ({ page }) => {
    await page.route('**/api/auth/admin/login', async (route) => {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'auth.invalidCredentials' }),
      })
    })

    await page.goto('/login')
    await page.getByRole('button', { name: /admin/i }).click()
    await page.getByLabel(/שם משתמש|username/i).fill('baduser')
    await page.getByLabel(/סיסמה|password/i).fill('wrongpass')
    await page.getByRole('button', { name: /כניסה|login/i }).click()

    // Error text should appear — the component shows t('auth.invalidCredentials')
    await expect(page.locator('p.text-red-500')).toBeVisible()
  })
})

// ─── Customer OTP flow ────────────────────────────────────────────────────────

test.describe('Customer OTP flow', () => {
  test.beforeEach(async ({ page }) => {
    await blockSignalR(page)
  })

  test('enters phone, sends OTP, shows code input step', async ({ page }) => {
    await page.route('**/api/auth/otp/send', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'sent' }),
      })
    })

    await page.goto('/login')

    // Customer tab is active by default — type phone
    await page.getByPlaceholder('050-1234567').fill('0501234567')
    await page.getByRole('button', { name: /שלח|send/i }).click()

    // OTP code input should appear
    await expect(page.getByPlaceholder('123456')).toBeVisible()
  })

  test('verifies OTP code and redirects to home', async ({ page }) => {
    // Step 1: send
    await page.route('**/api/auth/otp/send', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ message: 'sent' }) })
    })

    // Step 2: verify
    await page.route('**/api/auth/otp/verify', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          token: 'fake-customer-jwt',
          user: { id: '2', role: 'customer', phone: '0501234567' },
        }),
      })
    })

    // Mock winner matches so WinnerPage loads
    await page.route('**/api/winner/matches', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) })
    })

    await page.goto('/login')
    await page.getByPlaceholder('050-1234567').fill('0501234567')
    await page.getByRole('button', { name: /שלח|send/i }).click()

    // Fill code and submit
    await page.getByPlaceholder('123456').fill('123456')
    await page.getByRole('button', { name: /אמת|verify/i }).click()

    // Should redirect away from /login
    await expect(page).not.toHaveURL('/login')
  })

  test('shows error on invalid OTP code', async ({ page }) => {
    await page.route('**/api/auth/otp/send', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ message: 'sent' }) })
    })
    await page.route('**/api/auth/otp/verify', async (route) => {
      await route.fulfill({ status: 401, contentType: 'application/json', body: JSON.stringify({ message: 'auth.invalidOtp' }) })
    })

    await page.goto('/login')
    await page.getByPlaceholder('050-1234567').fill('0501234567')
    await page.getByRole('button', { name: /שלח|send/i }).click()
    await page.getByPlaceholder('123456').fill('000000')
    await page.getByRole('button', { name: /אמת|verify/i }).click()

    await expect(page.locator('p.text-red-500')).toBeVisible()
  })
})

// ─── Logout ───────────────────────────────────────────────────────────────────

test.describe('Logout', () => {
  test('clicking logout clears auth and redirects to login', async ({ page }) => {
    await blockSignalR(page)

    // Mock matches so page loads
    await page.route('**/api/winner/matches', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) })
    })
    await page.route('**/api/auth/logout', async (route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' })
    })

    await page.goto('/')
    await setAdminAuth(page)
    await page.reload()

    // Find and click logout button (text or icon varies by layout)
    const logoutBtn = page.getByRole('button', { name: /יציאה|logout|התנתק/i })
    await expect(logoutBtn).toBeVisible()
    await logoutBtn.click()

    // Should redirect to login
    await expect(page).toHaveURL(/\/login/)

    // localStorage token should be gone
    const stored = await page.evaluate(() => localStorage.getItem('cafe-erez-auth'))
    const parsed = stored ? JSON.parse(stored) : null
    expect(parsed?.state?.token ?? null).toBeNull()
  })
})
