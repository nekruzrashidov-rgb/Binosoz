// Group «languages» of the audit: the skip link follows the language (A-09), the building's texts in English come from
// the *En fields or fall back to Russian with a note (A-10), the offline page speaks the chosen language (A-25).
const { test, expect } = require('@playwright/test');

test('skip link is translated on TJ and EN', async ({ page }) => {
  await page.goto('/contact?lang=en');
  await expect(page.locator('.skip-link')).toHaveText('Skip to content');
  await page.goto('/contact?lang=tg');
  await expect(page.locator('.skip-link')).toHaveText('Гузариш ба мундариҷа');
});

test('offline page in the chosen language, Russian by default', async ({ page }) => {
  await page.goto('/offline.html');
  await expect(page.locator('h1')).toHaveText('Нет подключения');
  await page.addInitScript(() => { try { localStorage.setItem('sitora.lang', 'en'); } catch (e) { /* private mode */ } });
  await page.goto('/offline.html');
  await expect(page.locator('h1')).toHaveText('No connection');
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
  await expect(page.locator('[data-fallback]').first()).toBeHidden();
});

test('English building texts: *En, or Russian marked «in Russian»', async ({ page }) => {
  await page.goto('/contact?lang=en');
  await page.waitForFunction(() => window.S && window.S.localText && window.S.lang === 'en');
  const v = await page.evaluate(() => {
    const S = window.S, b = { descriptionRu: 'Жилой дом', descriptionTg: 'Бинои истиқоматӣ', descriptionEn: 'A residential building' };
    const out = { en: S.localText(b, 'description'), enFallback: S.localText({ descriptionRu: 'Жилой дом' }, 'description') };
    S.lang = 'tg'; out.tg = S.localText(b, 'description');
    S.lang = 'ru'; out.ru = S.localText(b, 'description');
    return out;
  });
  expect(v).toEqual({ en: 'A residential building', enFallback: 'Жилой дом (in Russian)', tg: 'Бинои истиқоматӣ', ru: 'Жилой дом' });
});
