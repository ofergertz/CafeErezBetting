/**
 * winner.spec.ts
 * E2E tests for the Winner (football betting) page:
 *  - Browse matches
 *  - Add a bet to the slip
 *  - Enter stake and see potential win
 *  - Submit bet slip (mocked success)
 *  - Verify submit is disabled when slip is empty
 *
 * All API calls are intercepted with page.route() — no real backend required.
 */

import { test, expect, Page } from '@playwright/test'

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const MOCK_MATCHES = [
  {
    id: 'm1',
    homeTeam: 'מכבי ת"א',
    awayTeam: 'הפועל ב"ש',
    league: 'ליגת העל',
    odds: { '1': 1.85, X: 3.40, '2': 4.10 },
    status: 'upcoming',
    isLive: false,
    scheduledAt: new Date(Date.now() + 3_600_000).toISOString(),
  },
  {
    id: 'm2',
    homeTeam: 'בני סכנין',
    awayTeam: 'עירוני נתניה',
    league: 'ליגת העל',
    odds: { '1': 2.10, X: 3.10, '2': 3.50 },
    status: 'upcoming',
    isLive: false,
    scheduledAt: new Date(Date.now() + 7_200_000).toISOString(),
  },
]

// ─── Helpers ──────────────────────────────────────────────────────────────────

async function setupPage(page: Page) {
  // Block SignalR — no backend
  await page.route('**/hubs/**', (route) => route.abort())

  // Inject admin auth state so layout renders fully
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

  // Mock matches endpoint
  await page.route('**/api/winner/matches', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_MATCHES),
    })
  })
}

// ─── Tests ────────────────────────────────────────────────────────────────────

test.describe('Winner page — browse matches', () => {
  test('renders match cards for all returned matches', async ({ page }) => {
    await setupPage(page)
    await page.goto('/winner')

    // Both home team names should appear
    await expect(page.getByText('מכבי ת"א')).toBeVisible()
    await expect(page.getByText('בני סכנין')).toBeVisible()
  })

  test('shows odds buttons for each match', async ({ page }) => {
    await setupPage(page)
    await page.goto('/winner')

    // Each card has 1 / X / 2 odds buttons — pick the first match's "1" button
    const oddsButton = page.getByRole('button', { name: /1\.85/ })
    await expect(oddsButton.first()).toBeVisible()
  })
})

test.describe('Winner page — bet slip', () => {
  test('adding a bet makes it appear in the bet slip', async ({ page }) => {
    await setupPage(page)
    await page.goto('/winner')

    // Click the home-win odds button for the first match (1.85)
    await page.getByRole('button', { name: /1\.85/ }).first().click()

    // The desktop slip (hidden on mobile but present in DOM) should list the bet
    await expect(page.getByText(/מכבי ת"א/)).toBeVisible()
  })

  test('entering a stake shows potential win', async ({ page }) => {
    await setupPage(page)
    await page.goto('/winner')

    // Add a bet
    await page.getByRole('button', { name: /1\.85/ }).first().click()

    // Fill the stake input (desktop slip is visible on wide viewport)
    const stakeInput = page.locator('input[type="number"]').first()
    await stakeInput.fill('50')

    // Potential win = 50 * 1.85 = 92.5 — check it appears in the slip
    await expect(page.getByText(/92\.50/)).toBeVisible()
  })

  test('submit button is disabled when no bets are added', async ({ page }) => {
    await setupPage(page)
    await page.goto('/winner')

    // With an empty slip the submit button should not exist or be disabled.
    // The BetSlip component renders an empty-state placeholder when items.length === 0.
    // The submit button only renders when there are items.
    const submitBtn = page.getByRole('button', { name: /שלח|submit/i })
    // If it renders at all it should be disabled
    if (await submitBtn.count() > 0) {
      await expect(submitBtn.first()).toBeDisabled()
    } else {
      // Empty state placeholder is visible instead
      await expect(page.getByText(/לחץ על יחס להוספה/)).toBeVisible()
    }
  })

  test('submitting the bet slip shows success overlay', async ({ page }) => {
    await setupPage(page)

    // Mock the form submission endpoint
    await page.route('**/api/forms/winner', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ id: 'form-1', status: 'received' }),
      })
    })

    await page.goto('/winner')

    // Add a bet then set stake
    await page.getByRole('button', { name: /1\.85/ }).first().click()
    await page.locator('input[type="number"]').first().fill('100')

    // Click submit
    await page.getByRole('button', { name: /שלח|submit/i }).click()

    // Success overlay: BetSlip renders ✅ and "received" text
    await expect(page.locator('text=✅').or(page.getByText(/received|התקבל/i))).toBeVisible()
  })

  test('toggling same odds button removes the bet from slip', async ({ page }) => {
    await setupPage(page)
    await page.goto('/winner')

    const oddsBtn = page.getByRole('button', { name: /1\.85/ }).first()
    await oddsBtn.click() // add
    await oddsBtn.click() // remove

    // After deselect the slip should show empty state
    const emptyState = page.getByText(/לחץ על יחס להוספה/)
    await expect(emptyState).toBeVisible()
  })
})
