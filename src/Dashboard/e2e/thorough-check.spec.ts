import { test, expect } from '@playwright/test';

test.describe('Thorough Dashboard Check', () => {
  test.setTimeout(180000);

  test('check all pages for issues', async ({ page }) => {
    const issues: string[] = [];

    const logIssue = (issue: string) => {
      console.log(`[ISSUE] ${issue}`);
      issues.push(issue);
    };

    const logInfo = (info: string) => {
      console.log(`[INFO] ${info}`);
    };

    // Listen for console errors
    page.on('console', msg => {
      if (msg.type() === 'error') {
        logIssue(`Console error: ${msg.text()}`);
      }
    });

    // Listen for network errors
    page.on('response', response => {
      if (response.status() >= 400) {
        logIssue(`HTTP ${response.status()} on ${response.url()}`);
      }
    });

    // === 1. HOME PAGE ===
    logInfo('=== CHECKING HOME PAGE ===');
    await page.goto('/#/');
    await page.waitForTimeout(3000);
    await page.screenshot({ path: 'e2e-results/thorough-01-home.png', fullPage: true });

    // Check header
    const header = page.locator('header, .header, .app-header').first();
    if (!await header.isVisible()) {
      logIssue('Header not visible');
    }

    // Check gateway URL display
    const gatewayInfo = await page.locator('text=/gateway|http:/i').first().textContent();
    logInfo(`Gateway display: ${gatewayInfo}`);

    // Check service cards
    const cards = await page.locator('.service-card').all();
    logInfo(`Found ${cards.length} service cards`);

    for (let i = 0; i < cards.length; i++) {
      const card = cards[i];
      const cardHtml = await card.innerHTML();

      // Check for text overflow issues
      const boundingBox = await card.boundingBox();
      if (boundingBox && boundingBox.width < 100) {
        logIssue(`Service card ${i} might be too narrow: ${boundingBox.width}px`);
      }

      // Check for truncated text
      const allText = await card.allTextContents();
      logInfo(`Card ${i} text: ${allText.join(' | ')}`);
    }

    // === 2. SERVICE DETAIL PAGE ===
    logInfo('=== CHECKING SERVICE DETAIL ===');
    const firstCard = page.locator('.service-card').first();
    await firstCard.click();
    await page.waitForTimeout(3000);
    await page.screenshot({ path: 'e2e-results/thorough-02-service.png', fullPage: true });

    // Check status badge
    const statusBadge = page.locator('.status-badge, [class*="status"]').first();
    if (await statusBadge.isVisible()) {
      const badgeText = await statusBadge.textContent();
      logInfo(`Status badge: ${badgeText}`);
      if (badgeText?.toUpperCase() === 'UNKNOWN') {
        logIssue('Service status shows UNKNOWN - may need better status detection');
      }
    }

    // Check instance table columns
    const tableHeaders = await page.locator('th').allTextContents();
    logInfo(`Table headers: ${tableHeaders.join(', ')}`);

    // Check for "-" placeholders vs empty cells
    const tableCells = await page.locator('td').allTextContents();
    const emptyCells = tableCells.filter(t => t.trim() === '');
    if (emptyCells.length > 0) {
      logIssue(`Found ${emptyCells.length} empty table cells (should show "-")`);
    }

    // Check metadata summary
    const metadataSummary = page.locator('.metadata-summary').first();
    if (await metadataSummary.isVisible()) {
      await metadataSummary.scrollIntoViewIfNeeded();
      const summaryText = await page.locator('.metadata-summary').first().textContent();
      logInfo(`Metadata summary: ${summaryText?.substring(0, 200)}`);

      // Check for "0 instance"
      if (summaryText?.includes('0 instance')) {
        logIssue('Metadata summary shows 0 instances incorrectly');
      }
    }

    // Check metrics display
    const metricsSection = page.locator('.metrics-panel').first();
    if (await metricsSection.isVisible()) {
      await metricsSection.scrollIntoViewIfNeeded();
      await page.waitForTimeout(1000);
      await page.screenshot({ path: 'e2e-results/thorough-03-metrics.png' });

      const metricCards = await page.locator('.metric-card').all();
      for (let i = 0; i < metricCards.length; i++) {
        const cardText = await metricCards[i].textContent();
        logInfo(`Metric ${i}: ${cardText}`);
      }
    }

    // === 3. TRACES PAGE ===
    logInfo('=== CHECKING TRACES PAGE ===');
    await page.goto('/#/traces');
    await page.waitForTimeout(3000);
    await page.screenshot({ path: 'e2e-results/thorough-04-traces.png', fullPage: true });

    // Check for any error messages
    const errorElements = await page.locator('.error, .error-state, [class*="error"]').all();
    for (const el of errorElements) {
      const text = await el.textContent();
      if (text && !text.includes('No data')) {
        logIssue(`Error on traces page: ${text}`);
      }
    }

    // Check trace table
    const traceTable = page.locator('table').first();
    if (await traceTable.isVisible()) {
      const headers = await traceTable.locator('th').allTextContents();
      logInfo(`Trace table headers: ${headers.join(', ')}`);

      // Check first few rows for issues
      const rows = await page.locator('tr.trace-row').all();
      logInfo(`Found ${rows.length} trace rows`);

      if (rows.length > 0) {
        const firstRowCells = await rows[0].locator('td').allTextContents();
        logInfo(`First row: ${firstRowCells.join(' | ')}`);

        // Check for text truncation issues
        for (let i = 0; i < Math.min(3, rows.length); i++) {
          const rowText = await rows[i].textContent();
          if (rowText?.includes('undefined') || rowText?.includes('null')) {
            logIssue(`Trace row ${i} contains undefined/null values`);
          }
        }
      }
    }

    // Check pagination
    const pagination = page.locator('text=/page|showing/i').first();
    if (await pagination.isVisible()) {
      const paginationText = await pagination.textContent();
      logInfo(`Pagination: ${paginationText}`);
    }

    // === 4. TRACE DETAIL PAGE ===
    logInfo('=== CHECKING TRACE DETAIL ===');
    const firstTraceRow = page.locator('tr.trace-row').first();
    if (await firstTraceRow.isVisible()) {
      await firstTraceRow.click();
      await page.waitForTimeout(3000);
      await page.screenshot({ path: 'e2e-results/thorough-05-trace-detail.png', fullPage: true });

      // Check URL changed
      const url = page.url();
      if (!url.includes('/traces/')) {
        logIssue('Clicking trace did not navigate to detail page');
      }

      // Check summary section
      const summaryPanel = page.locator('.detail-panel').first();
      if (await summaryPanel.isVisible()) {
        // Look for overlapping/garbled text
        const summaryItems = await page.locator('.trace-summary dt, .trace-summary dd').allTextContents();
        logInfo(`Summary items: ${summaryItems.join(' | ')}`);

        // Check for text that looks garbled
        for (const item of summaryItems) {
          if (item.length > 50 && !item.includes(' ')) {
            logIssue(`Possible text overflow/garble: ${item.substring(0, 60)}...`);
          }
        }
      }

      // Check waterfall
      const waterfall = page.locator('.waterfall-panel, .trace-waterfall').first();
      if (await waterfall.isVisible()) {
        logInfo('Waterfall panel visible');

        const spans = await page.locator('.span-bar, .span-row').all();
        logInfo(`Found ${spans.length} span bars in waterfall`);

        if (spans.length === 0) {
          logIssue('No spans visible in waterfall');
        }
      } else {
        logIssue('Waterfall panel not visible');
      }

      // Check for loading states stuck
      const loadingState = page.locator('.loading-state');
      if (await loadingState.isVisible()) {
        logIssue('Loading state still visible on trace detail');
      }
    }

    // === 5. RESPONSIVE / LAYOUT CHECK ===
    logInfo('=== CHECKING LAYOUT ===');
    await page.goto('/#/');
    await page.waitForTimeout(2000);

    // Check for horizontal scrollbar (layout overflow)
    const hasHScroll = await page.evaluate(() => {
      return document.documentElement.scrollWidth > document.documentElement.clientWidth;
    });
    if (hasHScroll) {
      logIssue('Page has horizontal scrollbar (layout overflow)');
    }

    // === SUMMARY ===
    console.log('\n========================================');
    console.log('THOROUGH CHECK COMPLETE');
    console.log('========================================');

    if (issues.length === 0) {
      console.log('No issues found!');
    } else {
      console.log(`Found ${issues.length} issues:`);
      issues.forEach((issue, i) => {
        console.log(`  ${i + 1}. ${issue}`);
      });
    }
    console.log('========================================\n');
  });
});
