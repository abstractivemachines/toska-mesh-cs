import { test, expect } from '@playwright/test';

test('tab highlighting switches correctly', async ({ page }) => {
  // Go to services page (default)
  await page.goto('/#/');
  await page.waitForTimeout(1000);

  // Verify Services tab is active
  const servicesTab = page.locator('.nav-tab', { hasText: 'Services' });
  const tracesTab = page.locator('.nav-tab', { hasText: 'Traces' });

  await expect(servicesTab).toHaveClass(/nav-tab-active/);
  await expect(tracesTab).not.toHaveClass(/nav-tab-active/);

  // Screenshot before clicking
  await page.screenshot({ path: 'e2e-results/tab-services-active.png', fullPage: true });

  // Click on Traces tab
  await tracesTab.click();
  await page.waitForTimeout(500);

  // Verify Traces tab is now active
  await expect(tracesTab).toHaveClass(/nav-tab-active/);
  await expect(servicesTab).not.toHaveClass(/nav-tab-active/);

  // Screenshot after clicking
  await page.screenshot({ path: 'e2e-results/tab-traces-active.png', fullPage: true });

  // Click back to Services
  await servicesTab.click();
  await page.waitForTimeout(500);

  // Verify Services tab is active again
  await expect(servicesTab).toHaveClass(/nav-tab-active/);
  await expect(tracesTab).not.toHaveClass(/nav-tab-active/);

  console.log('Tab highlighting works correctly!');
});
