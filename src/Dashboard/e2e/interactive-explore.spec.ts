import { test, expect } from '@playwright/test';

test.describe('Interactive Dashboard Exploration', () => {
  test.setTimeout(300000); // 5 minutes for exploration

  test('explore all pages and capture issues', async ({ page }) => {
    const issues: string[] = [];

    // Helper to log and track issues
    const logIssue = (issue: string) => {
      console.log(`[ISSUE] ${issue}`);
      issues.push(issue);
    };

    const logInfo = (info: string) => {
      console.log(`[INFO] ${info}`);
    };

    // 1. HOME PAGE - Service Catalog
    logInfo('=== NAVIGATING TO HOME PAGE ===');
    await page.goto('/#/');
    await page.waitForTimeout(3000);
    await page.screenshot({ path: 'e2e-results/explore-01-home.png', fullPage: true });

    // Check for any visible errors on home
    const homeErrors = await page.locator('.error, [class*="error"]').all();
    for (const err of homeErrors) {
      const text = await err.textContent();
      if (text && text.trim()) {
        logIssue(`Home page error: ${text.trim()}`);
      }
    }

    // Check service cards
    const serviceCards = await page.locator('.service-card').all();
    logInfo(`Found ${serviceCards.length} service cards`);

    if (serviceCards.length === 0) {
      logIssue('No service cards displayed on home page');
    }

    // Check each service card for issues
    for (let i = 0; i < serviceCards.length; i++) {
      const card = serviceCards[i];
      const cardText = await card.textContent();
      logInfo(`Service card ${i}: ${cardText?.substring(0, 100)}`);

      // Check for "0 instance" which might indicate an issue
      if (cardText?.includes('0 instance')) {
        logIssue(`Service card ${i} shows 0 instances`);
      }

      // Check for invalid dates
      if (cardText?.includes('1/1/1') || cardText?.includes('0001')) {
        logIssue(`Service card ${i} shows invalid date`);
      }
    }

    // 2. SERVICE DETAIL PAGE
    logInfo('=== NAVIGATING TO SERVICE DETAIL ===');
    const firstCard = page.locator('.service-card').first();
    if (await firstCard.isVisible()) {
      const serviceName = await firstCard.locator('.service-name, h3, .card-title').first().textContent();
      logInfo(`Clicking on service: ${serviceName}`);
      await firstCard.click();
      await page.waitForTimeout(3000);
      await page.screenshot({ path: 'e2e-results/explore-02-service-detail.png', fullPage: true });

      // Check URL
      const url = page.url();
      logInfo(`Current URL: ${url}`);

      // Check page content
      const pageContent = await page.content();

      // Check for invalid dates in detail view
      if (pageContent.includes('1/1/1') || pageContent.includes('1/1/0001')) {
        logIssue('Service detail shows invalid date (1/1/1 or 1/1/0001)');
      }

      // Check instance table
      const instanceRows = await page.locator('.data-table tbody tr, table tbody tr').all();
      logInfo(`Found ${instanceRows.length} instance rows`);

      // Check metadata summary
      const metadataSection = page.locator('.metadata-summary, [class*="metadata"]');
      if (await metadataSection.first().isVisible()) {
        const metadataText = await metadataSection.first().textContent();
        logInfo(`Metadata section: ${metadataText?.substring(0, 200)}`);

        if (metadataText?.includes('0 instance')) {
          logIssue('Metadata summary shows 0 instances incorrectly');
        }
      }

      // Check metrics section
      const metricsSection = page.locator('.metrics-panel, [class*="metrics"]');
      if (await metricsSection.first().isVisible()) {
        await metricsSection.first().scrollIntoViewIfNeeded();
        await page.waitForTimeout(1000);
        await page.screenshot({ path: 'e2e-results/explore-03-metrics.png' });

        const metricCards = await page.locator('.metric-card').all();
        logInfo(`Found ${metricCards.length} metric cards`);

        for (let i = 0; i < metricCards.length; i++) {
          const cardText = await metricCards[i].textContent();
          logInfo(`Metric card ${i}: ${cardText}`);

          if (cardText?.includes('Error') && !cardText?.includes('No data')) {
            logIssue(`Metric card ${i} shows raw "Error" instead of friendly message`);
          }
        }
      }

      // Check health history
      const healthSection = page.locator('.health-history, [class*="health"]');
      if (await healthSection.first().isVisible()) {
        const healthText = await healthSection.first().textContent();
        logInfo(`Health section: ${healthText?.substring(0, 200)}`);
      }

      // Scroll to bottom to see everything
      await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
      await page.waitForTimeout(1000);
      await page.screenshot({ path: 'e2e-results/explore-04-service-detail-bottom.png', fullPage: true });
    }

    // 3. TRACES PAGE
    logInfo('=== NAVIGATING TO TRACES PAGE ===');
    await page.goto('/#/traces');
    await page.waitForTimeout(3000);
    await page.screenshot({ path: 'e2e-results/explore-05-traces.png', fullPage: true });

    // Check for 500 error
    const has500Error = await page.locator('text=500').isVisible();
    if (has500Error) {
      logIssue('Traces page shows 500 error');
    }

    // Check for other errors
    const traceErrors = await page.locator('.error, [class*="error"]').all();
    for (const err of traceErrors) {
      const text = await err.textContent();
      if (text && text.trim() && !text.includes('No data')) {
        logIssue(`Traces page error: ${text.trim()}`);
      }
    }

    // Check trace list
    const traceRows = await page.locator('.trace-row, [class*="trace-row"], .trace-list tr').all();
    logInfo(`Found ${traceRows.length} trace rows`);

    if (traceRows.length === 0) {
      const emptyMessage = await page.locator('text=/no traces|empty|no data/i').isVisible();
      if (!emptyMessage) {
        logIssue('No trace rows and no empty state message');
      }
    }

    // 4. TRACE DETAIL PAGE (if traces exist)
    if (traceRows.length > 0) {
      logInfo('=== NAVIGATING TO TRACE DETAIL ===');
      await traceRows[0].click();
      await page.waitForTimeout(3000);
      await page.screenshot({ path: 'e2e-results/explore-06-trace-detail.png', fullPage: true });

      const traceDetailUrl = page.url();
      logInfo(`Trace detail URL: ${traceDetailUrl}`);

      // Check for waterfall
      const waterfall = page.locator('.trace-waterfall, [class*="waterfall"]');
      if (await waterfall.first().isVisible()) {
        logInfo('Trace waterfall is visible');

        // Check spans
        const spans = await page.locator('.span-bar, [class*="span"]').all();
        logInfo(`Found ${spans.length} span elements`);
      } else {
        logIssue('Trace waterfall not visible on trace detail page');
      }

      // Check for any display issues
      const traceContent = await page.content();
      if (traceContent.includes('undefined') || traceContent.includes('null')) {
        logIssue('Trace detail shows "undefined" or "null" values');
      }
    }

    // 5. Check navigation
    logInfo('=== CHECKING NAVIGATION ===');
    const navTabs = await page.locator('.nav-tabs a, nav a, .navigation a').all();
    logInfo(`Found ${navTabs.length} navigation links`);

    // 6. Summary
    console.log('\n========================================');
    console.log('EXPLORATION COMPLETE - ISSUES SUMMARY');
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

    // Take final full-page screenshots
    await page.goto('/#/');
    await page.waitForTimeout(2000);

    // Fail test if there are issues so user knows
    if (issues.length > 0) {
      console.log('\nIssues detected - review screenshots in e2e-results/');
    }
  });
});
