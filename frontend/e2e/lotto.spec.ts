/**
 * lotto.spec.ts
 * E2E tests for the Lotto page:
 *  - Pick 6 numbers
 *  - Pick 1 strong number
 *  - Quick Pick auto-fill
 *  - Validation error when incomplete
 *  - Successful submission
 *  - Add a second row
 *
 * All API calls are intercepted with page.route() — no real backend required.
 */

import { test, expect, Page } from '@playwright/test'

// ─── Helpers ──────────────────────────────────────────────────────────────────

async function setupLottoPage(page: Page) {
  // Block SignalR
  await page.route('**/hubs/**', (route) => route.abort())

  // Inject admin auth
  await page.addInitScript(() => {
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

// ─── Tests ────────────────────────────────────────────────────────────────────

test.describe('Lotto page — number selection', () => {
  test('can pick 6 regular numbers in the grid', async ({ page }) => {
    await setupLottoPage(page)
    await page.goto('/lotto')

    // NumberGrid renders buttons labelled 1..37.
    // Click numbers 1–6
    for (const n of [1, 2, 3, 4, 5, 6]) {
      await page.getByRole('button', { name: new RegExp(`^${n}$`) }).first().click()
    }

    // Counter should read 6/6
    await expect(page.getByText('6/6')).toBeVisible()
  })

  test('can pick 1 strong number', async ({ page }) => {
    await setupLottoPage(page)
    await page.goto('/lotto')

    // The strong number grid is rendered after "מספר חזק / strong" label.
    // Click the first button in the strong section (1..7 range)
    const strongSection = page.locator('text=strong').or(page.getByText(/חזק/i))
    await expect(strongSection.first()).toBeVisible()

    // The strong grid buttons share the same number labels 1-7.
    // We need the second occurrence of button "1" (strong grid).
    const strongBtn = page.getByRole('button', { name: /^1$/ }).nth(1)
    await strongBtn.click()

    // The button should appear highlighted (selected style)
    await expect(strongBtn).toHaveClass(/bg-amber|text-amber|ring|border-amber/i)
  })

  test('Quick Pick auto-fills 6 numbers and 1 strong', async ({ page }) => {
    await setupLottoPage(page)
    await page.goto('/lotto')

    await page.getByRole('button', { name: /quick pick|בחירה מהירה/i }).click()

    // Counter should reach 6/6
    await expect(page.getByText('6/6')).toBeVisible()
  })
})

test.describe('Lotto page — validation', () => {
  test('shows validation error when submitting without complete row', async ({ page }) => {
    await setupLottoPage(page)
    await page.goto('/lotto')

    // Pick only 3 numbers — not enough
    for (const n of [1, 2, 3]) {
      await page.getByRole('button', { name: new RegExp(`^${n}$`) }).first().click()
    }

    // Click submit without filling all picks
    await page.getByRole('button', { name: /שלח|submit/i }).click()

    // Validation error should appear (⚠️ prefix in LottoPage)
    await expect(page.locator('text=⚠️')).toBeVisible()
  })
})

test.describe('Lotto page — submission', () => {
  test('successfully submits fully filled row and shows success overlay', async ({ page }) => {
    await setupLottoPage(page)

    // Mock submission
    await page.route('**/api/forms/lotto', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ id: 'lotto-form-1', status: 'received' }),
      })
    })

    await page.goto('/lotto')

    // Quick Pick fills everything in one click
    await page.getByRole('button', { name: /quick pick|בחירה מהירה/i }).click()

    // Submit
    await page.getByRole('button', { name: /שלח|submit/i }).click()

    // SubmitSuccessOverlay should appear — it renders a large ✅ or success text
    await expect(page.locator('text=✅').or(page.getByText(/received|התקבל/i))).toBeVisible()
  })
})

test.describe('Lotto page — multiple rows', () => {
  test('clicking Add Row appends a second row', async ({ page }) => {
    await setupLottoPage(page)
    await page.goto('/lotto')

    // One row exists initially — count cards with "#1" label
    await expect(page.getByText('#1')).toBeVisible()

    await page.getByRole('button', { name: /\+ לוטו|add row|\+ הוסף שורה/i }).click()

    // Second row label appears
    await expect(page.getByText('#2')).toBeVisible()
  })

  test('cannot add more than 10 rows', async ({ page }) => {
    await setupLottoPage(page)
    await page.goto('/lotto')

    // Click Add Row 9 times (starting from 1 we reach 10)
    for (let i = 0; i < 9; i++) {
      await page.getByRole('button', { name: /\+ |add row/i }).first().click()
    }

    await expect(page.getByText('#10')).toBeVisible()

    // The Add Row button should now be disabled
    await expect(page.getByRole('button', { name: /\+ |add row/i }).first()).toBeDisabled()
  })
})
