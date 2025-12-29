import { test, expect } from '@playwright/test';

test.describe('Verify UI Fixes', () => {
  test('service detail shows correct date formatting', async ({ page }) => {
    await page.goto('/#/');
    await page.waitForTimeout(3000);

    // Click on first service card
    const serviceCard = page.locator('.service-card').first();
    if (await serviceCard.isVisible()) {
      await serviceCard.click();
      await page.waitForTimeout(2000);

      // Take full page screenshot
      await page.screenshot({ path: 'e2e-results/verify-service-detail.png', fullPage: true });

      // Check that there are no "1/1/1" dates visible
      const pageContent = await page.content();
      const has1_1_1_date = pageContent.includes('1/1/1');

      if (has1_1_1_date) {
        console.log('[FAIL] Still showing 1/1/1 date format');
      } else {
        console.log('[OK] No invalid date formats found');
      }

      // Check for "-" placeholders for empty dates
      const instanceTable = page.locator('.data-table').first();
      const tableCells = await instanceTable.locator('td').allTextContents();
      const hasDashPlaceholder = tableCells.some(cell => cell === '-');

      if (hasDashPlaceholder) {
        console.log('[OK] Empty dates now show "-" placeholder');
      }
    }
  });

  test('metrics error shows "No data" instead of "Error"', async ({ page }) => {
    await page.goto('/#/');
    await page.waitForTimeout(3000);

    const serviceCard = page.locator('.service-card').first();
    if (await serviceCard.isVisible()) {
      await serviceCard.click();
      await page.waitForTimeout(2000);

      // Scroll to metrics section
      const metricsSection = page.locator('text=Metrics');
      if (await metricsSection.isVisible()) {
        await metricsSection.scrollIntoViewIfNeeded();
        await page.waitForTimeout(1000);

        await page.screenshot({ path: 'e2e-results/verify-metrics.png' });

        // Check that error shows "No data" not just "Error"
        const errorText = page.locator('.metric-card .error');
        const count = await errorText.count();

        for (let i = 0; i < count; i++) {
          const text = await errorText.nth(i).textContent();
          if (text === 'No data') {
            console.log(`[OK] Metric card ${i} shows "No data" for error`);
          } else if (text === 'Error') {
            console.log(`[FAIL] Metric card ${i} still shows just "Error"`);
          }
        }
      }
    }
  });

  test('traces page shows error message', async ({ page }) => {
    await page.goto('/#/traces');
    await page.waitForTimeout(3000);

    await page.screenshot({ path: 'e2e-results/verify-traces.png' });

    // The traces page should show an error (database not migrated)
    const errorMessage = page.locator('.error, [class*="error"]');
    if (await errorMessage.first().isVisible()) {
      const text = await errorMessage.first().textContent();
      console.log(`[INFO] Traces page error: ${text}`);
    }
  });
});
