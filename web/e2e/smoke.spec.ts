import { test, expect } from '@playwright/test'

test('the PWA shell loads', async ({ page }) => {
  await page.goto('/')
  await expect(page).toHaveTitle('Orbit Work Management')
  await expect(page.locator('#root')).not.toBeEmpty()
})
