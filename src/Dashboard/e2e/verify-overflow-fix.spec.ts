import { test, expect } from '@playwright/test';

test('trace detail text should not overflow', async ({ page }) => {
  // Navigate to traces
  await page.goto('/#/traces');
  await page.waitForTimeout(3000);

  // Click on first trace row
  const firstRow = page.locator('tr.trace-row').first();
  await firstRow.click();
  await page.waitForTimeout(3000);

  // Screenshot the trace detail
  await page.screenshot({ path: 'e2e-results/verify-overflow-fix.png', fullPage: true });

  // Check that we're on the trace detail page
  const url = page.url();
  console.log('URL:', url);
  expect(url).toContain('/traces/');

  // Get the summary items
  const summaryItems = await page.locator('.trace-summary dd').allTextContents();
  console.log('Summary values:');
  summaryItems.forEach((item, i) => {
    console.log(`  ${i}: ${item.substring(0, 50)}${item.length > 50 ? '...' : ''}`);
  });

  // The ROOT SERVICE should be a proper service name, not contain trace ID
  // Trace IDs are 32 hex chars, service names are readable words
  const rootService = summaryItems[1]; // Second dd should be ROOT SERVICE
  console.log('ROOT SERVICE value:', rootService);

  // Service name should be short and readable (not a hex trace ID)
  expect(rootService.length).toBeLessThan(50);
  expect(rootService).not.toMatch(/^[a-f0-9]{20,}/); // Should not start with long hex

  console.log('[OK] Text overflow appears to be fixed');
});
