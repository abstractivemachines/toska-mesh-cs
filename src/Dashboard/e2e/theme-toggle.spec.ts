import { test, expect } from '@playwright/test';

test('theme toggle works', async ({ page }) => {
  await page.goto('/#/');
  await page.waitForSelector('.service-card', { timeout: 15000 });
  
  // Screenshot in light mode (default)
  await page.screenshot({ path: 'e2e-results/theme-light.png', fullPage: true });
  
  // Get initial theme
  const initialTheme = await page.locator('html').getAttribute('data-theme');
  console.log('Initial theme:', initialTheme || 'light (default)');
  
  // Click theme toggle
  await page.locator('.theme-toggle').click();
  await page.waitForTimeout(300);
  
  // Screenshot in dark mode
  await page.screenshot({ path: 'e2e-results/theme-dark.png', fullPage: true });
  
  // Verify theme changed
  const newTheme = await page.locator('html').getAttribute('data-theme');
  console.log('After toggle:', newTheme);
  
  expect(newTheme).toBe('dark');
  
  // Toggle back
  await page.locator('.theme-toggle').click();
  await page.waitForTimeout(300);
  
  const backToLight = await page.locator('html').getAttribute('data-theme');
  console.log('After second toggle:', backToLight);
  
  expect(backToLight).toBe('light');
});
