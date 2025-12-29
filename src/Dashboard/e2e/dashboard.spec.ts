import { test, expect, Page } from '@playwright/test';

// Utility to capture issues found during testing
interface Issue {
  page: string;
  type: 'error' | 'warning' | 'visual' | 'functional' | 'accessibility';
  description: string;
  element?: string;
}

const issues: Issue[] = [];

function reportIssue(issue: Issue) {
  issues.push(issue);
  console.log(`[ISSUE] ${issue.type.toUpperCase()} on ${issue.page}: ${issue.description}`);
}

// Capture console errors
async function setupConsoleCapture(page: Page, pageName: string) {
  page.on('console', (msg) => {
    if (msg.type() === 'error') {
      reportIssue({
        page: pageName,
        type: 'error',
        description: `Console error: ${msg.text()}`,
      });
    }
  });

  page.on('pageerror', (err) => {
    reportIssue({
      page: pageName,
      type: 'error',
      description: `Page error: ${err.message}`,
    });
  });
}

test.describe('Dashboard UI Examination', () => {
  test.describe('Home Page - Service Catalog', () => {
    test('loads and displays header correctly', async ({ page }) => {
      await setupConsoleCapture(page, 'Home');
      await page.goto('/#/');

      // Check header elements
      const title = page.locator('h1');
      await expect(title).toBeVisible();
      const titleText = await title.textContent();
      expect(titleText).toContain('Dashboard');

      // Check gateway URL display
      const gatewayInfo = page.locator('text=Gateway');
      if (await gatewayInfo.isVisible()) {
        console.log('[OK] Gateway info displayed');
      } else {
        reportIssue({
          page: 'Home',
          type: 'visual',
          description: 'Gateway info not visible in header',
        });
      }

      // Check navigation tabs
      const servicesTab = page.locator('text=Services');
      const tracesTab = page.locator('text=Traces');
      await expect(servicesTab).toBeVisible();
      await expect(tracesTab).toBeVisible();

      await page.screenshot({ path: 'e2e-results/home-header.png' });
    });

    test('loads service catalog data', async ({ page }) => {
      await setupConsoleCapture(page, 'Home - Services');
      await page.goto('/#/');

      // Wait for either services to load or error message
      await page.waitForTimeout(3000);

      // Check for loading state
      const loading = page.locator('text=Loading');
      const error = page.locator('.error-state, [class*="error"]');
      const serviceCards = page.locator('.service-card, [class*="service-card"]');

      if (await loading.isVisible()) {
        reportIssue({
          page: 'Home',
          type: 'functional',
          description: 'Still showing loading state after 3 seconds',
        });
      }

      // Check for error messages
      const errorText = page.locator('text=/Unable to load|Error|NetworkError/i');
      if (await errorText.isVisible()) {
        const text = await errorText.textContent();
        reportIssue({
          page: 'Home',
          type: 'error',
          description: `Error displayed: ${text}`,
        });
      }

      // Check if services loaded
      const serviceCount = await serviceCards.count();
      console.log(`[INFO] Found ${serviceCount} service cards`);

      if (serviceCount === 0) {
        // Check for empty state
        const emptyState = page.locator('text=/No services|empty/i');
        if (await emptyState.isVisible()) {
          console.log('[OK] Empty state displayed correctly');
        } else {
          reportIssue({
            page: 'Home',
            type: 'visual',
            description: 'No services and no empty state message',
          });
        }
      }

      await page.screenshot({ path: 'e2e-results/home-services.png' });
    });

    test('service cards have correct structure', async ({ page }) => {
      await setupConsoleCapture(page, 'Home - Service Cards');
      await page.goto('/#/');
      await page.waitForTimeout(3000);

      const serviceCards = page.locator('.service-card');
      const count = await serviceCards.count();

      for (let i = 0; i < Math.min(count, 3); i++) {
        const card = serviceCards.nth(i);

        // Check card is clickable (has cursor pointer or is link)
        const cursor = await card.evaluate((el) => window.getComputedStyle(el).cursor);
        if (cursor !== 'pointer') {
          reportIssue({
            page: 'Home',
            type: 'visual',
            description: `Service card ${i} doesn't have pointer cursor`,
            element: 'service-card',
          });
        }

        // Check for service name
        const nameElement = card.locator('h3, .service-name, [class*="name"]').first();
        if (!(await nameElement.isVisible())) {
          reportIssue({
            page: 'Home',
            type: 'visual',
            description: `Service card ${i} missing service name`,
            element: 'service-card',
          });
        }

        // Check for status indicator
        const statusBadge = card.locator('.status-badge, [class*="status"], [class*="badge"]');
        if (!(await statusBadge.first().isVisible())) {
          reportIssue({
            page: 'Home',
            type: 'visual',
            description: `Service card ${i} missing status indicator`,
            element: 'service-card',
          });
        }
      }

      await page.screenshot({ path: 'e2e-results/home-service-cards.png' });
    });

    test('clicking service card navigates to detail', async ({ page }) => {
      await setupConsoleCapture(page, 'Home - Navigation');
      await page.goto('/#/');
      await page.waitForTimeout(3000);

      const serviceCards = page.locator('.service-card');
      const count = await serviceCards.count();

      if (count > 0) {
        const firstCard = serviceCards.first();
        const serviceName = await firstCard.locator('h3, .service-name').first().textContent();

        await firstCard.click();
        await page.waitForTimeout(1000);

        const url = page.url();
        if (!url.includes('/services/')) {
          reportIssue({
            page: 'Home',
            type: 'functional',
            description: `Clicking service card did not navigate. URL: ${url}`,
          });
        } else {
          console.log(`[OK] Navigated to service detail: ${url}`);
        }

        await page.screenshot({ path: 'e2e-results/service-detail-navigation.png' });
      } else {
        console.log('[SKIP] No service cards to test navigation');
      }
    });
  });

  test.describe('Service Detail Page', () => {
    test('displays service information', async ({ page }) => {
      await setupConsoleCapture(page, 'Service Detail');

      // First get a service name from home page
      await page.goto('/#/');
      await page.waitForTimeout(3000);

      const serviceCards = page.locator('.service-card');
      const count = await serviceCards.count();

      if (count > 0) {
        const firstCard = serviceCards.first();
        await firstCard.click();
        await page.waitForTimeout(2000);

        // Check for service detail components
        const backButton = page.locator('text=/Back|←|back/i, a[href="#/"]');
        if (!(await backButton.first().isVisible())) {
          reportIssue({
            page: 'Service Detail',
            type: 'visual',
            description: 'Missing back navigation button',
          });
        }

        // Check for instance table
        const instanceSection = page.locator('text=/Instances|Instance/i');
        if (await instanceSection.isVisible()) {
          console.log('[OK] Instance section visible');
        }

        // Check for metrics panel
        const metricsSection = page.locator('text=/Metrics|Request Rate|Error Rate|Latency/i');
        if (await metricsSection.first().isVisible()) {
          console.log('[OK] Metrics section visible');
        } else {
          reportIssue({
            page: 'Service Detail',
            type: 'visual',
            description: 'Metrics panel not visible',
          });
        }

        // Check for health history
        const healthSection = page.locator('text=/Health|History/i');
        if (await healthSection.first().isVisible()) {
          console.log('[OK] Health section visible');
        }

        await page.screenshot({ path: 'e2e-results/service-detail.png', fullPage: true });
      } else {
        console.log('[SKIP] No services available to test detail page');
      }
    });

    test('metrics panel time range selector works', async ({ page }) => {
      await setupConsoleCapture(page, 'Service Detail - Time Range');
      await page.goto('/#/');
      await page.waitForTimeout(3000);

      const serviceCards = page.locator('.service-card');
      if (await serviceCards.count() > 0) {
        await serviceCards.first().click();
        await page.waitForTimeout(2000);

        // Find time range selector
        const timeSelector = page.locator('.time-range-selector, [class*="time-range"], button:has-text("15m"), button:has-text("1h")');
        if (await timeSelector.first().isVisible()) {
          console.log('[OK] Time range selector visible');

          // Try clicking different ranges
          const ranges = page.locator('button:has-text("1h"), button:has-text("6h"), button:has-text("24h")');
          const rangeCount = await ranges.count();

          for (let i = 0; i < rangeCount; i++) {
            await ranges.nth(i).click();
            await page.waitForTimeout(500);
          }

          await page.screenshot({ path: 'e2e-results/service-metrics-timerange.png' });
        } else {
          reportIssue({
            page: 'Service Detail',
            type: 'visual',
            description: 'Time range selector not found',
          });
        }
      }
    });
  });

  test.describe('Traces Page', () => {
    test('loads traces page', async ({ page }) => {
      await setupConsoleCapture(page, 'Traces');
      await page.goto('/#/traces');
      await page.waitForTimeout(3000);

      // Check page title/header
      const header = page.locator('h2, h1').filter({ hasText: /Traces/i });
      if (await header.isVisible()) {
        console.log('[OK] Traces page header visible');
      } else {
        reportIssue({
          page: 'Traces',
          type: 'visual',
          description: 'Traces page header not found',
        });
      }

      await page.screenshot({ path: 'e2e-results/traces-page.png' });
    });

    test('trace filters are present', async ({ page }) => {
      await setupConsoleCapture(page, 'Traces - Filters');
      await page.goto('/#/traces');
      await page.waitForTimeout(2000);

      // Check for filter elements
      const serviceFilter = page.locator('select, input[placeholder*="service" i], [class*="filter"]');
      const filters = await serviceFilter.count();

      if (filters > 0) {
        console.log(`[OK] Found ${filters} filter elements`);
      } else {
        reportIssue({
          page: 'Traces',
          type: 'functional',
          description: 'No trace filters found',
        });
      }

      // Check for search button
      const searchButton = page.locator('button:has-text("Search"), button:has-text("Filter"), button[type="submit"]');
      if (await searchButton.first().isVisible()) {
        console.log('[OK] Search/filter button found');
      }

      await page.screenshot({ path: 'e2e-results/traces-filters.png' });
    });

    test('trace list displays correctly', async ({ page }) => {
      await setupConsoleCapture(page, 'Traces - List');
      await page.goto('/#/traces');
      await page.waitForTimeout(3000);

      // Check for trace rows
      const traceRows = page.locator('.trace-row, [class*="trace-row"], tr, .trace-item');
      const rowCount = await traceRows.count();

      console.log(`[INFO] Found ${rowCount} trace items`);

      // Check for loading or error states
      const loading = page.locator('text=Loading');
      const error = page.locator('text=/Error|Unable|Failed/i');
      const noData = page.locator('text=/No traces|No results|empty/i');

      if (await loading.isVisible()) {
        reportIssue({
          page: 'Traces',
          type: 'functional',
          description: 'Still loading after 3 seconds',
        });
      }

      if (await error.isVisible()) {
        const errorText = await error.textContent();
        reportIssue({
          page: 'Traces',
          type: 'error',
          description: `Error displayed: ${errorText}`,
        });
      }

      if (await noData.isVisible()) {
        console.log('[INFO] No traces message displayed');
      }

      await page.screenshot({ path: 'e2e-results/traces-list.png' });
    });

    test('clicking trace navigates to detail', async ({ page }) => {
      await setupConsoleCapture(page, 'Traces - Navigation');
      await page.goto('/#/traces');
      await page.waitForTimeout(3000);

      const traceRows = page.locator('.trace-row, [class*="trace-row"], .trace-item').first();

      if (await traceRows.isVisible()) {
        await traceRows.click();
        await page.waitForTimeout(1000);

        const url = page.url();
        if (url.includes('/traces/') && url !== '/#/traces') {
          console.log(`[OK] Navigated to trace detail: ${url}`);
        } else {
          reportIssue({
            page: 'Traces',
            type: 'functional',
            description: 'Clicking trace row did not navigate to detail',
          });
        }

        await page.screenshot({ path: 'e2e-results/trace-detail-navigation.png' });
      } else {
        console.log('[SKIP] No trace rows to test navigation');
      }
    });
  });

  test.describe('Trace Detail Page', () => {
    test('displays waterfall visualization', async ({ page }) => {
      await setupConsoleCapture(page, 'Trace Detail');

      // Navigate via traces list
      await page.goto('/#/traces');
      await page.waitForTimeout(3000);

      const traceRow = page.locator('.trace-row, [class*="trace-row"], .trace-item').first();

      if (await traceRow.isVisible()) {
        await traceRow.click();
        await page.waitForTimeout(2000);

        // Check for waterfall components
        const waterfall = page.locator('.trace-waterfall, [class*="waterfall"]');
        const spanBars = page.locator('.span-bar, [class*="span-bar"], [class*="span"]');
        const timeline = page.locator('.timeline, [class*="timeline"]');

        if (await waterfall.isVisible()) {
          console.log('[OK] Waterfall visualization visible');
        } else {
          reportIssue({
            page: 'Trace Detail',
            type: 'visual',
            description: 'Waterfall visualization not visible',
          });
        }

        const spanCount = await spanBars.count();
        console.log(`[INFO] Found ${spanCount} span bars in waterfall`);

        // Check for trace summary info
        const summary = page.locator('text=/Duration|Spans|Service/i');
        if (await summary.first().isVisible()) {
          console.log('[OK] Trace summary info visible');
        }

        await page.screenshot({ path: 'e2e-results/trace-detail-waterfall.png', fullPage: true });
      } else {
        console.log('[SKIP] No traces available for waterfall test');
      }
    });

    test('span details are expandable', async ({ page }) => {
      await setupConsoleCapture(page, 'Trace Detail - Span Details');

      await page.goto('/#/traces');
      await page.waitForTimeout(3000);

      const traceRow = page.locator('.trace-row, [class*="trace-row"], .trace-item').first();

      if (await traceRow.isVisible()) {
        await traceRow.click();
        await page.waitForTimeout(2000);

        // Try clicking on a span to expand details
        const spanBars = page.locator('.span-bar, [class*="span-bar"], .span-row');

        if (await spanBars.first().isVisible()) {
          await spanBars.first().click();
          await page.waitForTimeout(500);

          // Check if details appeared
          const details = page.locator('.span-details, [class*="span-details"], [class*="attributes"]');
          if (await details.isVisible()) {
            console.log('[OK] Span details expandable');
          }

          await page.screenshot({ path: 'e2e-results/trace-span-details.png' });
        }
      }
    });
  });

  test.describe('Navigation and Routing', () => {
    test('tab navigation works correctly', async ({ page }) => {
      await setupConsoleCapture(page, 'Navigation');
      await page.goto('/#/');

      // Click Traces tab
      const tracesTab = page.locator('a:has-text("Traces"), button:has-text("Traces"), nav >> text=Traces');
      await tracesTab.first().click();
      await page.waitForTimeout(500);

      let url = page.url();
      if (!url.includes('/traces')) {
        reportIssue({
          page: 'Navigation',
          type: 'functional',
          description: 'Traces tab did not navigate to /traces',
        });
      }

      // Click Services tab
      const servicesTab = page.locator('a:has-text("Services"), button:has-text("Services"), nav >> text=Services');
      await servicesTab.first().click();
      await page.waitForTimeout(500);

      url = page.url();
      if (!url.includes('/#/') || url.includes('/traces')) {
        reportIssue({
          page: 'Navigation',
          type: 'functional',
          description: 'Services tab did not navigate correctly',
        });
      }

      await page.screenshot({ path: 'e2e-results/navigation.png' });
    });

    test('browser back/forward works', async ({ page }) => {
      await setupConsoleCapture(page, 'Browser Navigation');
      await page.goto('/#/');
      await page.waitForTimeout(1000);

      // Navigate to traces
      await page.goto('/#/traces');
      await page.waitForTimeout(500);

      // Go back
      await page.goBack();
      await page.waitForTimeout(500);

      const url = page.url();
      if (!url.endsWith('/#/') && !url.endsWith('/#')) {
        reportIssue({
          page: 'Browser Navigation',
          type: 'functional',
          description: `Browser back did not work correctly. URL: ${url}`,
        });
      } else {
        console.log('[OK] Browser back navigation works');
      }
    });

    test('direct URL access works', async ({ page }) => {
      await setupConsoleCapture(page, 'Direct URL');

      // Test direct access to traces page
      await page.goto('/#/traces');
      await page.waitForTimeout(1000);

      const tracesHeader = page.locator('h2, h1').filter({ hasText: /Traces/i });
      if (!(await tracesHeader.isVisible())) {
        reportIssue({
          page: 'Direct URL',
          type: 'functional',
          description: 'Direct URL to /traces did not load traces page',
        });
      } else {
        console.log('[OK] Direct URL access works');
      }
    });
  });

  test.describe('Accessibility Checks', () => {
    test('pages have proper headings', async ({ page }) => {
      await setupConsoleCapture(page, 'Accessibility - Headings');

      // Check home page
      await page.goto('/#/');
      await page.waitForTimeout(1000);

      const h1Count = await page.locator('h1').count();
      if (h1Count === 0) {
        reportIssue({
          page: 'Home',
          type: 'accessibility',
          description: 'No h1 heading found',
        });
      } else if (h1Count > 1) {
        reportIssue({
          page: 'Home',
          type: 'accessibility',
          description: `Multiple h1 headings found (${h1Count})`,
        });
      }

      // Check traces page
      await page.goto('/#/traces');
      await page.waitForTimeout(1000);

      const tracesH1 = await page.locator('h1').count();
      const tracesH2 = await page.locator('h2').count();

      if (tracesH1 === 0 && tracesH2 === 0) {
        reportIssue({
          page: 'Traces',
          type: 'accessibility',
          description: 'No h1 or h2 heading found',
        });
      }
    });

    test('interactive elements are focusable', async ({ page }) => {
      await setupConsoleCapture(page, 'Accessibility - Focus');
      await page.goto('/#/');
      await page.waitForTimeout(2000);

      // Check service cards are focusable
      const cards = page.locator('.service-card');
      const cardCount = await cards.count();

      for (let i = 0; i < Math.min(cardCount, 3); i++) {
        const card = cards.nth(i);
        const tabIndex = await card.getAttribute('tabindex');
        const role = await card.getAttribute('role');
        const isButton = await card.evaluate((el) => el.tagName.toLowerCase() === 'button');
        const isLink = await card.evaluate((el) => el.tagName.toLowerCase() === 'a');

        if (tabIndex === null && !isButton && !isLink && role !== 'button') {
          reportIssue({
            page: 'Home',
            type: 'accessibility',
            description: `Service card ${i} may not be keyboard accessible (no tabindex, not a button/link)`,
            element: 'service-card',
          });
        }
      }
    });

    test('buttons have accessible names', async ({ page }) => {
      await setupConsoleCapture(page, 'Accessibility - Button Names');
      await page.goto('/#/');
      await page.waitForTimeout(2000);

      const buttons = page.locator('button');
      const buttonCount = await buttons.count();

      for (let i = 0; i < buttonCount; i++) {
        const button = buttons.nth(i);
        const text = await button.textContent();
        const ariaLabel = await button.getAttribute('aria-label');
        const title = await button.getAttribute('title');

        if (!text?.trim() && !ariaLabel && !title) {
          reportIssue({
            page: 'Home',
            type: 'accessibility',
            description: `Button ${i} has no accessible name`,
            element: 'button',
          });
        }
      }
    });
  });

  test.describe('Responsive Design', () => {
    test('layout adapts to mobile viewport', async ({ page }) => {
      await setupConsoleCapture(page, 'Responsive - Mobile');

      // Set mobile viewport
      await page.setViewportSize({ width: 375, height: 667 });
      await page.goto('/#/');
      await page.waitForTimeout(2000);

      // Check header is still visible
      const header = page.locator('header, .header, [class*="header"]');
      if (await header.first().isVisible()) {
        console.log('[OK] Header visible on mobile');
      }

      // Check for horizontal overflow
      const hasHorizontalScroll = await page.evaluate(() => {
        return document.documentElement.scrollWidth > document.documentElement.clientWidth;
      });

      if (hasHorizontalScroll) {
        reportIssue({
          page: 'Home',
          type: 'visual',
          description: 'Horizontal scroll present on mobile viewport',
        });
      }

      await page.screenshot({ path: 'e2e-results/responsive-mobile.png' });
    });

    test('layout adapts to tablet viewport', async ({ page }) => {
      await setupConsoleCapture(page, 'Responsive - Tablet');

      await page.setViewportSize({ width: 768, height: 1024 });
      await page.goto('/#/');
      await page.waitForTimeout(2000);

      // Check service cards layout
      const cards = page.locator('.service-card');
      const cardCount = await cards.count();

      if (cardCount > 1) {
        const firstBox = await cards.first().boundingBox();
        const secondBox = await cards.nth(1).boundingBox();

        if (firstBox && secondBox) {
          // Check if cards are laid out properly (not overlapping)
          const overlap = firstBox.x + firstBox.width > secondBox.x &&
                         firstBox.y + firstBox.height > secondBox.y &&
                         secondBox.x + secondBox.width > firstBox.x &&
                         secondBox.y + secondBox.height > firstBox.y;

          if (overlap) {
            reportIssue({
              page: 'Home',
              type: 'visual',
              description: 'Service cards overlapping on tablet viewport',
            });
          }
        }
      }

      await page.screenshot({ path: 'e2e-results/responsive-tablet.png' });
    });
  });

  test.describe('Error Handling', () => {
    test('retry button works on error', async ({ page }) => {
      await setupConsoleCapture(page, 'Error Handling');
      await page.goto('/#/');
      await page.waitForTimeout(3000);

      const retryButton = page.locator('button:has-text("Retry"), button:has-text("Try again")');

      if (await retryButton.isVisible()) {
        console.log('[INFO] Error state with retry button present');
        await retryButton.click();
        await page.waitForTimeout(2000);

        // Check if retry triggered a reload
        const stillHasRetry = await retryButton.isVisible();
        console.log(`[INFO] After retry, retry button ${stillHasRetry ? 'still' : 'no longer'} visible`);

        await page.screenshot({ path: 'e2e-results/error-retry.png' });
      } else {
        console.log('[OK] No error state present (services loaded successfully)');
      }
    });
  });

  test.afterAll(async () => {
    console.log('\n========== ISSUES SUMMARY ==========');
    if (issues.length === 0) {
      console.log('No issues found!');
    } else {
      console.log(`Found ${issues.length} issue(s):\n`);
      issues.forEach((issue, index) => {
        console.log(`${index + 1}. [${issue.type.toUpperCase()}] ${issue.page}`);
        console.log(`   ${issue.description}`);
        if (issue.element) {
          console.log(`   Element: ${issue.element}`);
        }
        console.log('');
      });
    }
    console.log('====================================\n');
  });
});
