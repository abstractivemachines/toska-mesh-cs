import { test, expect } from '@playwright/test';

test.describe('Final Backend Fixes Verification', () => {
  test('service detail shows correct instance count in metadata', async ({ page }) => {
    await page.goto('/#/');
    await page.waitForTimeout(3000);

    // Click on first service card
    const serviceCard = page.locator('.service-card').first();
    await expect(serviceCard).toBeVisible();
    await serviceCard.click();
    await page.waitForTimeout(2000);

    // Check metadata summary
    const metadataSection = page.locator('.metadata-summary, text=Metadata Summary').first();
    await expect(metadataSection).toBeVisible();

    // Verify instance count is not 0
    const instanceCountText = await page.locator('text=/\\d+ instance\\(s\\)/i').first().textContent();
    console.log(`[INFO] Metadata instance count: ${instanceCountText}`);

    // Should show at least 1 instance
    expect(instanceCountText).not.toContain('0 instance');

    await page.screenshot({ path: 'e2e-results/final-metadata-fixed.png', fullPage: true });
  });

  test('traces page loads successfully without 500 error', async ({ page }) => {
    await page.goto('/#/traces');
    await page.waitForTimeout(3000);

    // Check for error message
    const error500 = page.locator('text=500 Internal Server Error');
    const hasError = await error500.isVisible();

    if (hasError) {
      console.log('[FAIL] Traces page still shows 500 error');
    } else {
      console.log('[OK] Traces page does not show 500 error');
    }

    expect(hasError).toBe(false);

    // Check for trace data
    const traceRows = page.locator('.trace-row, [class*="trace-row"], tr');
    const count = await traceRows.count();
    console.log(`[INFO] Found ${count} trace items`);

    await page.screenshot({ path: 'e2e-results/final-traces-fixed.png' });
  });

  test('trace waterfall can be viewed', async ({ page }) => {
    await page.goto('/#/traces');
    await page.waitForTimeout(3000);

    // Click on first trace if available
    const traceRow = page.locator('.trace-row, [class*="trace-row"]').first();

    if (await traceRow.isVisible()) {
      await traceRow.click();
      await page.waitForTimeout(2000);

      const url = page.url();
      if (url.includes('/traces/') && url !== '/#/traces') {
        console.log('[OK] Navigated to trace detail');

        // Check for waterfall
        const waterfall = page.locator('.trace-waterfall, [class*="waterfall"]');
        if (await waterfall.isVisible()) {
          console.log('[OK] Trace waterfall visible');
        }

        await page.screenshot({ path: 'e2e-results/final-trace-waterfall.png', fullPage: true });
      }
    } else {
      console.log('[INFO] No trace rows available');
    }
  });
});
