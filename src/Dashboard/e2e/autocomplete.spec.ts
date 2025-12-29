import { test, expect } from '@playwright/test';

test('service autocomplete works', async ({ page }) => {
  await page.goto('/#/traces');
  await page.waitForSelector('table.data-table', { timeout: 15000 });
  
  // Type partial service name
  const input = page.locator('#service-filter');
  await input.fill('Gate');
  await page.waitForTimeout(500);
  
  // Screenshot showing autocomplete dropdown
  await page.screenshot({ path: 'e2e-results/autocomplete-1-dropdown.png', fullPage: true });
  
  // Check suggestions appear
  const suggestions = page.locator('.autocomplete-suggestions li');
  await expect(suggestions).toHaveCount(1);
  await expect(suggestions.first()).toContainText('Gateway');
  
  // Click suggestion
  await suggestions.first().click();
  await page.waitForTimeout(500);
  
  // Verify input has full name and results filtered
  await expect(input).toHaveValue('Gateway');
  await page.screenshot({ path: 'e2e-results/autocomplete-2-selected.png', fullPage: true });
  
  // Check results filtered (should show Gateway traces)
  const paginationInfo = await page.locator('.pagination-info').textContent();
  console.log('After selecting Gateway:', paginationInfo);
  
  // Test status filter auto-apply
  await page.locator('#status-filter').selectOption('Unset');
  await page.waitForTimeout(500);
  
  const afterStatus = await page.locator('.pagination-info').textContent();
  console.log('After selecting Unset status:', afterStatus);
  
  // Clear and verify
  await page.locator('button', { hasText: 'Clear' }).click();
  await page.waitForTimeout(500);
  
  const afterClear = await page.locator('.pagination-info').textContent();
  console.log('After clear:', afterClear);
});
