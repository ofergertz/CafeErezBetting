/**
 * store.spec.ts
 * E2E tests for the Store page (StorePage.tsx):
 *  - Product grid renders for authenticated users
 *  - Admin sees add/edit/delete controls; customers do not
 *  - Admin adds a product via the modal
 *  - Barcode scan mode highlights a product
 *
 * All API calls are intercepted with page.route() — no real backend required.
 */

import { test, expect, Page } from '@playwright/test'

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const MOCK_PRODUCTS = [
  {
    id: 'p1',
    name: 'קפה שחור',
    description: 'אספרסו כפול',
    price: 12.0,
    inStock: true,
    createdAt: '2025-01-01T00:00:00Z',
    barcode: '1234567890123',
  },
  {
    id: 'p2',
    name: 'כריך גבינה',
    description: 'גבינה צהובה',
    price: 25.0,
    inStock: false,
    createdAt: '2025-01-02T00:00:00Z',
    barcode: '9876543210987',
  },
]

// ─── Helpers ──────────────────────────────────────────────────────────────────

async function setupStorePage(page: Page, role: 'admin' | 'customer' = 'admin') {
  // Block SignalR
  await page.route('**/hubs/**', (route) => route.abort())

  const authState =
    role === 'admin'
      ? { user: { id: '1', role: 'admin', name: 'מנהל' }, token: 'fake-admin-jwt' }
      : { user: { id: '2', role: 'customer', phone: '0501234567' }, token: 'fake-customer-jwt' }

  await page.addInitScript((state) => {
    localStorage.setItem(
      'cafe-erez-auth',
      JSON.stringify({ state, version: 0 }),
    )
  }, authState)

  // Mock GET /api/products
  await page.route('**/api/products', async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_PRODUCTS),
      })
    } else {
      await route.continue()
    }
  })
}

// ─── Tests ────────────────────────────────────────────────────────────────────

test.describe('Store page — product grid', () => {
  test('renders product cards for all products', async ({ page }) => {
    await setupStorePage(page)
    await page.goto('/store')

    await expect(page.getByText('קפה שחור')).toBeVisible()
    await expect(page.getByText('כריך גבינה')).toBeVisible()
  })

  test('shows price for each product', async ({ page }) => {
    await setupStorePage(page)
    await page.goto('/store')

    await expect(page.getByText('₪12.00')).toBeVisible()
    await expect(page.getByText('₪25.00')).toBeVisible()
  })

  test('shows out-of-stock badge for unavailable products', async ({ page }) => {
    await setupStorePage(page)
    await page.goto('/store')

    // t('store.outOfStock') — checking for Hebrew or key match
    await expect(page.getByText(/אזל|out of stock/i).first()).toBeVisible()
  })
})

test.describe('Store page — admin vs customer UI', () => {
  test('admin sees Add Product button', async ({ page }) => {
    await setupStorePage(page, 'admin')
    await page.goto('/store')

    await expect(page.getByRole('button', { name: /הוסף מוצר|add product/i })).toBeVisible()
  })

  test('customer does NOT see Add Product button', async ({ page }) => {
    await setupStorePage(page, 'customer')
    await page.goto('/store')

    await expect(page.getByRole('button', { name: /הוסף מוצר|add product/i })).not.toBeVisible()
  })

  test('admin sees Scan Mode button', async ({ page }) => {
    await setupStorePage(page, 'admin')
    await page.goto('/store')

    await expect(page.getByRole('button', { name: /סריקה|scan/i })).toBeVisible()
  })

  test('customer does NOT see edit or delete buttons on cards', async ({ page }) => {
    await setupStorePage(page, 'customer')
    await page.goto('/store')

    // Edit (✏️) and delete (🗑️) buttons are admin-only — rendered on hover via group-hover
    // They should not be in the DOM at all for non-admin users.
    await expect(page.getByTitle(/ערוך|edit product/i)).not.toBeVisible()
    await expect(page.getByTitle(/מחק|delete product/i)).not.toBeVisible()
  })
})

test.describe('Store page — add product modal', () => {
  test('clicking Add Product opens the modal', async ({ page }) => {
    await setupStorePage(page, 'admin')
    await page.goto('/store')

    await page.getByRole('button', { name: /הוסף מוצר|add product/i }).click()

    // Modal heading
    await expect(page.getByRole('heading', { name: /הוסף מוצר|add product/i })).toBeVisible()
  })

  test('filling the modal form and submitting calls POST /api/products', async ({ page }) => {
    await setupStorePage(page, 'admin')

    let postBody: unknown = null
    await page.route('**/api/products', async (route) => {
      if (route.request().method() === 'POST') {
        postBody = JSON.parse(route.request().postData() ?? '{}')
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'p-new', name: 'מוצר חדש', price: 9.99, inStock: true, createdAt: new Date().toISOString() }),
        })
      } else {
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MOCK_PRODUCTS) })
      }
    })

    await page.goto('/store')
    await page.getByRole('button', { name: /הוסף מוצר|add product/i }).click()

    // Fill name
    await page.getByLabel(/שם מוצר|name/i).fill('מוצר חדש')

    // Fill price
    const priceInput = page.locator('input[type="number"]').first()
    await priceInput.fill('9.99')

    // Submit — button text "שמור"
    await page.getByRole('button', { name: /שמור|save/i }).click()

    // POST was called with correct name
    expect(postBody).toMatchObject({ name: 'מוצר חדש' })

    // Modal should close after successful save
    await expect(page.getByRole('heading', { name: /הוסף מוצר|add product/i })).not.toBeVisible()
  })

  test('closing the modal via cancel button works', async ({ page }) => {
    await setupStorePage(page, 'admin')
    await page.goto('/store')

    await page.getByRole('button', { name: /הוסף מוצר|add product/i }).click()
    await expect(page.getByRole('heading', { name: /הוסף מוצר|add product/i })).toBeVisible()

    await page.getByRole('button', { name: /ביטול|cancel/i }).click()
    await expect(page.getByRole('heading', { name: /הוסף מוצר|add product/i })).not.toBeVisible()
  })
})

test.describe('Store page — barcode scan', () => {
  test('entering scan mode and finding product highlights it', async ({ page }) => {
    await setupStorePage(page, 'admin')

    // Mock the barcode lookup
    await page.route('**/api/products/barcode/**', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_PRODUCTS[0]),
      })
    })

    await page.goto('/store')

    // Enable scan mode
    await page.getByRole('button', { name: /📷|מצב סריקה|scan/i }).click()

    // BarcodeScanner component should be visible
    // It renders a barcode input field or camera UI — check for an input
    const barcodeInput = page.locator('input[placeholder*="ברקוד"], input[placeholder*="barcode"]')
    if (await barcodeInput.isVisible()) {
      await barcodeInput.fill('1234567890123')
      await barcodeInput.press('Enter')

      // The product card should be highlighted (ring-2 ring-blue-400 class) and scrolled into view
      await expect(page.locator('.ring-2.ring-blue-400, [class*="ring-blue"]').first()).toBeVisible()
    } else {
      // Camera-based scanner — just verify scan mode activated (button state changed)
      const scanBtn = page.getByRole('button', { name: /📷|מצב סריקה|scan/i })
      await expect(scanBtn).toHaveClass(/btn-primary/)
    }
  })
})
