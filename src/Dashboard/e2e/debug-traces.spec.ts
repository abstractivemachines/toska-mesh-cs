import { test, expect } from '@playwright/test';

test.describe('Debug Trace Navigation', () => {
  test('trace row click should navigate to detail', async ({ page }) => {
    // Navigate to traces page
    await page.goto('/#/traces');
    await page.waitForTimeout(3000);

    console.log('Initial URL:', page.url());

    // Check if trace rows exist
    const traceRows = await page.locator('tr.trace-row').all();
    console.log(`Found ${traceRows.length} trace rows`);

    if (traceRows.length === 0) {
      // Maybe different selector
      const altRows = await page.locator('.trace-row').all();
      console.log(`Alt selector found ${altRows.length} rows`);

      const tableRows = await page.locator('table tbody tr').all();
      console.log(`Table rows found ${tableRows.length}`);
    }

    // Get the first trace row
    const firstRow = page.locator('tr.trace-row').first();

    if (await firstRow.isVisible()) {
      // Get trace ID from the row before clicking
      const traceIdCell = firstRow.locator('td').first();
      const traceIdText = await traceIdCell.textContent();
      console.log('Trace ID text:', traceIdText);

      // Click the row
      console.log('Clicking trace row...');
      await firstRow.click();

      // Wait for navigation
      await page.waitForTimeout(2000);

      // Check URL after click
      const newUrl = page.url();
      console.log('URL after click:', newUrl);

      // Take screenshot
      await page.screenshot({ path: 'e2e-results/debug-trace-after-click.png', fullPage: true });

      // Check if URL changed to trace detail
      if (newUrl.includes('/traces/')) {
        console.log('[OK] Navigation to trace detail worked');

        // Check for waterfall
        await page.waitForTimeout(2000);
        const waterfall = page.locator('.trace-waterfall, .waterfall-panel');
        const hasWaterfall = await waterfall.first().isVisible();
        console.log('Waterfall visible:', hasWaterfall);

        // Check for error state
        const errorState = page.locator('.error-state');
        if (await errorState.isVisible()) {
          const errorText = await errorState.textContent();
          console.log('[ERROR] Error state visible:', errorText);
        }

        await page.screenshot({ path: 'e2e-results/debug-trace-detail.png', fullPage: true });
      } else {
        console.log('[FAIL] URL did not change to trace detail');
        console.log('Expected URL to contain /traces/<traceId>');
      }
    } else {
      console.log('[SKIP] No trace rows visible');
    }

    // Also check console errors
    const consoleErrors: string[] = [];
    page.on('console', msg => {
      if (msg.type() === 'error') {
        consoleErrors.push(msg.text());
      }
    });

    // Reload and check for JS errors
    await page.goto('/#/traces');
    await page.waitForTimeout(2000);

    if (consoleErrors.length > 0) {
      console.log('Console errors:', consoleErrors);
    }
  });

  test('manually navigate to trace detail URL', async ({ page }) => {
    // First get a trace ID
    await page.goto('/#/traces');
    await page.waitForTimeout(3000);

    // Get trace ID from first row
    const firstRow = page.locator('tr.trace-row').first();
    const traceIdCell = firstRow.locator('td code').first();
    const traceIdText = await traceIdCell.textContent();

    // Extract actual trace ID (remove "...")
    const traceId = traceIdText?.replace('...', '');
    console.log('Extracted trace ID prefix:', traceId);

    // Try to get full trace ID from API
    const response = await page.request.get('http://talos:30080/api/dashboard/traces');
    const data = await response.json();
    const fullTraceId = data.traces?.[0]?.traceId;
    console.log('Full trace ID from API:', fullTraceId);

    if (fullTraceId) {
      // Manually navigate to trace detail
      await page.goto(`/#/traces/${fullTraceId}`);
      await page.waitForTimeout(3000);

      console.log('Direct navigation URL:', page.url());

      await page.screenshot({ path: 'e2e-results/debug-direct-trace-detail.png', fullPage: true });

      // Check what's displayed
      const content = await page.content();

      if (content.includes('Loading')) {
        console.log('Page shows loading state');
      }
      if (content.includes('Error') || content.includes('error')) {
        console.log('Page shows error');
        const errorText = await page.locator('.error-state, .error').first().textContent();
        console.log('Error text:', errorText);
      }
      if (content.includes('Waterfall')) {
        console.log('Waterfall section exists');
      }
      if (content.includes('not found')) {
        console.log('Trace not found error');
      }
    }
  });
});
