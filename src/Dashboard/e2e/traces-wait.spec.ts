import { test } from '@playwright/test';
test('traces page loads data', async ({ page }) => {
  await page.goto('/#/traces');
  await page.waitForTimeout(8000);
  await page.screenshot({ path: 'e2e-results/traces-wait.png', fullPage: true });
  const hasTable = await page.locator('table.data-table').isVisible();
  console.log('Table visible:', hasTable);
});
