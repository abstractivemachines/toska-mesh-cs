import { test } from '@playwright/test';

test('screenshot home page', async ({ page }) => {
  await page.goto('/#/');
  await page.waitForTimeout(3000);
  await page.screenshot({ path: 'e2e-results/home-5-services.png', fullPage: true });
  console.log('Screenshot saved');
});
