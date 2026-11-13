// Public site without an API: pages, empty states, login, language, theme, mobile menu, overflow and contrast.
const { test, expect } = require('@playwright/test');
const path = require('path');

const PUBLIC = ['/', '/apartments', '/about', '/gallery', '/contact', '/register', '/login', '/404.html', '/payment.html', '/offline.html'];
// Requests to the (absent) API fail on the static server; that is expected here, every other console error is a bug.
const isApiNoise = (text) => /\/api\/v1|Failed to load resource/.test(text);

async function open(page, url) {
  const errors = [];
  page.on('console', (m) => { if (m.type() === 'error' && !isApiNoise(m.text())) errors.push(m.text()); });
  page.on('pageerror', (e) => errors.push(String(e)));
  await page.goto(url);
  await page.waitForLoadState('networkidle');
  return errors;
}

test.describe('pages', () => {
  for (const url of PUBLIC) {
    test(`${url}: loads without script errors and without horizontal scroll`, async ({ page }) => {
      const errors = await open(page, url);
      expect(errors).toEqual([]);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow).toBeLessThanOrEqual(0);
    });
  }
});

test('320px phone: no horizontal scroll on the public pages', async ({ browser }) => {
  const page = await browser.newPage({ viewport: { width: 320, height: 640 } });
  for (const url of PUBLIC) {
    await page.goto(url);
    await page.waitForLoadState('networkidle');
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow, url).toBeLessThanOrEqual(0);
  }
});

test('home without data: no invented numbers, an honest "sales open soon" block', async ({ page }) => {
  await open(page, '/');
  await expect(page.locator('.soon-block')).toBeVisible();
  await expect(page.locator('.wow-stats')).toBeHidden();
  await expect(page.locator('.float-card')).toBeHidden();
  await expect(page.locator('h1')).toHaveText('Некруз Резиденс');
});

test('catalog without data: one empty card, no 3D stage', async ({ page }) => {
  await open(page, '/apartments');
  await expect(page.locator('.soon-block')).toHaveCount(1);
  await expect(page.locator('#hero-top')).toBeHidden();
  expect(await page.evaluate(() => !!window.THREE)).toBe(false);
});

test('login: the buyer tab is chosen by default', async ({ page }) => {
  await open(page, '/login');
  await expect(page.locator('.login-tabs button.on')).toHaveText('Покупатель');
});

test('language: TJ and EN load on demand and switch the page', async ({ page, isMobile }) => {
  await open(page, '/');
  if (isMobile) await page.locator('.menu-btn').click();
  const lang = (code) => (isMobile ? page.locator('.mob-lang-btn', { hasText: code }) : page.locator('.top-lang button', { hasText: code }));
  await lang('TJ').click();
  await expect(page.locator('html')).toHaveAttribute('lang', 'tg');
  if (isMobile) await page.locator('.menu-btn').click();
  await lang('EN').click();
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
  await expect(page.locator('.lux-nav a').first()).toHaveText('Home');
});

test('theme: the switch turns the dark theme on and off', async ({ page, isMobile }) => {
  await open(page, '/');
  const toggle = async () => {
    if (isMobile) { await page.locator('.menu-btn').click(); await page.locator('.mob-overlay .mob-lang-btn', { hasText: /тему|theme/i }).click(); }
    else await page.locator('.top-theme').click();
  };
  await toggle();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await toggle();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
});

test('mobile menu: opens with the button, closes with Escape', async ({ page, isMobile }) => {
  test.skip(!isMobile, 'the menu button is shown on phones and tablets');
  await open(page, '/');
  await page.locator('.menu-btn').click();
  await expect(page.locator('.mob-overlay')).toHaveClass(/open/);
  await page.keyboard.press('Escape');
  await expect(page.locator('.mob-overlay')).not.toHaveClass(/open/);
});

for (const theme of ['light', 'dark']) {
  test(`contrast: every text of the public pages passes WCAG AA (${theme} theme)`, async ({ page, isMobile }) => {
    test.skip(isMobile, 'measured once, at the desktop width');
    test.setTimeout(120_000);
    await page.goto('/');
    await page.addScriptTag({ path: path.join(__dirname, '..', '..', 'check-contrast.js') });
    const rows = await page.evaluate((t) => window.checkSite({ theme: t }), theme);
    const bad = rows.filter((r) => r.belowAA > 0 || r.darkOnImage).map((r) => `${r.page}: ${r.worst} ${r.darkOnImage}`);
    expect(bad).toEqual([]);
  });
}
