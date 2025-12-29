import { test, expect } from '@playwright/test';

test('debug autocomplete', async ({ page }) => {
  // Track network requests
  const apiCalls: string[] = [];
  page.on('request', req => {
    if (req.url().includes('/api/dashboard/traces') && !req.url().includes('/services')) {
      apiCalls.push(req.url());
    }
  });

  await page.goto('/#/traces');
  await page.waitForSelector('table.data-table', { timeout: 15000 });
  console.log('Initial API calls:', apiCalls.length);
  
  // Type partial name
  await page.locator('#service-filter').fill('Gate');
  await page.waitForTimeout(1000);
  console.log('After typing Gate, API calls:', apiCalls);
  
  // Click suggestion
  await page.locator('.autocomplete-suggestions li').first().click();
  await page.waitForTimeout(1500);
  
  // Check input value
  const inputValue = await page.locator('#service-filter').inputValue();
  console.log('Input value after click:', inputValue);
  console.log('API calls after selection:', apiCalls);
  
  // Wait for loading to finish
  await page.waitForSelector('table.data-table', { timeout: 5000 });
  
  const paginationInfo = await page.locator('.pagination-info').textContent();
  console.log('Pagination:', paginationInfo);
  
  await page.screenshot({ path: 'e2e-results/autocomplete-debug.png', fullPage: true });
});
