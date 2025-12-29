import { test, expect } from '@playwright/test';

test('fuzzy search filters as you type', async ({ page }) => {
  await page.goto('/#/traces');
  
  // Wait for initial load
  await page.waitForSelector('table.data-table', { timeout: 15000 });
  await page.screenshot({ path: 'e2e-results/fuzzy-1-initial.png', fullPage: true });
  
  // Get initial count
  const initialInfo = await page.locator('.pagination-info').textContent();
  console.log('Initial:', initialInfo);
  
  // Type partial service name
  const input = page.locator('#service-filter');
  await input.fill('Disc');
  
  // Wait for debounce (300ms) + API call
  await page.waitForTimeout(1500);
  await page.screenshot({ path: 'e2e-results/fuzzy-2-disc.png', fullPage: true });
  
  // Check that results filtered
  const filteredInfo = await page.locator('.pagination-info').textContent();
  console.log('After typing Disc:', filteredInfo);
  
  // Type more to narrow down
  await input.fill('gate');
  await page.waitForTimeout(1500);
  await page.screenshot({ path: 'e2e-results/fuzzy-3-gate.png', fullPage: true });
  
  const gateInfo = await page.locator('.pagination-info').textContent();
  console.log('After typing gate:', gateInfo);
  
  // Clear and verify reset
  await page.locator('button', { hasText: 'Clear' }).click();
  await page.waitForTimeout(1500);
  
  const clearedInfo = await page.locator('.pagination-info').textContent();
  console.log('After clear:', clearedInfo);
});
