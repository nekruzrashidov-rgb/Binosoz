// The AI assistant on the public pages for guests, with a mocked API: the bubble only when the server says it is on,
// a quick question goes to the public endpoint, links in the reply, "busy" (429) with the manager's contacts,
// Esc and focus, the full-screen 3D without the bubble, contrast AA in both themes and no horizontal scroll.
const { test, expect } = require('@playwright/test');
const path = require('path');

const CHAT = '/public/nekruz/ai/chat';
const STATUS = '/public/nekruz/ai/status';
const REPLY = 'Свободны 1- и 2-комнатные квартиры, цены — в каталоге /apartments. Вопросы — на странице /contact.';

async function mockApi(page, o) {
  const calls = [];
  await page.route('**/health/live', (route) => route.fulfill({ status: 200, contentType: 'text/plain', body: 'Healthy' }));
  await page.route('**/api/v1/**', (route) => {
    const req = route.request();
    const p = new URL(req.url()).pathname.replace(/^\/api\/v1/, '');
    const json = (status, body) => route.fulfill({ status, contentType: status >= 400 ? 'application/problem+json' : 'application/json', body: JSON.stringify(body) });
    if (p === STATUS) return json(200, { enabled: o.enabled });
    if (p === CHAT && req.method() === 'POST') {
      calls.push({ body: req.postDataJSON(), auth: req.headers().authorization || null });
      if (o.busy) return json(429, { status: 429, title: 'Слишком много запросов', detail: 'Помощник сейчас занят, напишите менеджеру', code: 'ai_public_busy', contacts: { phone: '+992 00 111 22 33', telegram: '@test_sales' } });
      return json(200, { reply: REPLY });
    }
    return json(404, { title: 'Не найдено' });
  });
  return calls;
}

const fab = (page) => page.locator('.ai-fab');

test('the bubble shows on / and /apartments when the assistant is on', async ({ page }) => {
  await mockApi(page, { enabled: true });
  for (const url of ['/', '/apartments']) {
    await page.goto(url);
    await expect(fab(page)).toBeVisible();
  }
});

test('no bubble when the server says the assistant is off', async ({ page }) => {
  await mockApi(page, { enabled: false });
  for (const url of ['/', '/apartments']) {
    await page.goto(url);
    await page.waitForLoadState('networkidle');
    await expect(fab(page)).toBeHidden();
  }
});

test('a quick question goes to the public endpoint; site links in the reply are clickable', async ({ page }) => {
  const calls = await mockApi(page, { enabled: true });
  await page.goto('/');
  await fab(page).click();
  const dialog = page.getByRole('dialog', { name: 'Помощник' });
  await expect(dialog).toBeVisible();
  await expect(dialog.locator('.ai-input')).toBeFocused();
  await expect(dialog.locator('.ai-note')).toContainText('может ошибаться');
  await expect(dialog.locator('.ai-note a')).toHaveAttribute('href', '/privacy');

  await dialog.getByRole('button', { name: /Какие квартиры свободны/ }).click();
  await expect(dialog.locator('.ai-msg.bot.rich')).toContainText('Свободны 1- и 2-комнатные');
  expect(calls).toHaveLength(1);
  expect(calls[0].body).toEqual({ message: 'Какие квартиры свободны?', history: [] });
  expect(calls[0].auth).toBeNull();
  await expect(dialog.locator('a.ai-link[href="/apartments"]')).toBeVisible();
  await expect(dialog.locator('a.ai-link[href="/contact"]')).toBeVisible();

  // The next question carries the history.
  await dialog.locator('.ai-input').fill('А рассрочка?');
  await dialog.locator('.ai-input').press('Enter');
  await expect(dialog.locator('.ai-msg.bot.rich')).toHaveCount(2);
  expect(calls[1].body.history).toEqual([{ role: 'user', content: 'Какие квартиры свободны?' }, { role: 'assistant', content: REPLY }]);
});

test('"busy" (429) shows the text and the manager\'s contacts', async ({ page }) => {
  await mockApi(page, { enabled: true, busy: true });
  await page.goto('/apartments');
  await fab(page).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: /Как забронировать/ }).click();
  await expect(dialog.locator('.ai-msg.err')).toHaveText('Помощник сейчас занят, напишите менеджеру');
  await expect(dialog.locator('.ai-contacts a[href^="tel:"]')).toHaveAttribute('href', 'tel:+992001112233');
  await expect(dialog.locator('.ai-contacts a[href="https://t.me/test_sales"]')).toBeVisible();
  await expect(dialog.locator('.ai-contacts a[href="/contact"]')).toBeVisible();
});

test('Esc closes the panel and gives the focus back to the bubble', async ({ page }) => {
  await mockApi(page, { enabled: true });
  await page.goto('/');
  await fab(page).click();
  await expect(page.locator('.ai-input')).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(page.locator('.ai-panel')).not.toHaveClass(/open/);
  await expect(fab(page)).toBeFocused();
});

test('the full-screen 3D hides the bubble', async ({ page }) => {
  await mockApi(page, { enabled: true });
  await page.goto('/apartments');
  await expect(fab(page)).toBeVisible();
  // The same element the 3D view puts on the page (.h3d-full); its own scene is checked in house3d.spec.js.
  await page.evaluate(() => { const d = document.createElement('div'); d.className = 'h3d-full'; document.body.appendChild(d); });
  await expect(fab(page)).toBeHidden();
  await page.evaluate(() => document.querySelector('.h3d-full').remove());
  await expect(fab(page)).toBeVisible();
});

for (const theme of ['light', 'dark']) {
  test(`the open chat: contrast AA and no horizontal scroll (${theme} theme)`, async ({ page }) => {
    await mockApi(page, { enabled: true, busy: true });
    await page.goto('/');
    await page.evaluate((t) => localStorage.setItem('sitora.theme', t), theme);
    await page.goto('/');
    await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
    await fab(page).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: /рассрочка/ }).click();
    await expect(dialog.locator('.ai-contacts')).toBeVisible();
    await page.waitForTimeout(400); // the panel slides in
    await page.addScriptTag({ path: path.join(__dirname, '..', '..', 'check-contrast.js') });
    // The whole page with the chat open: the panel's texts and the page under it.
    const res = await page.evaluate(() => window.checkContrastDocument(document, window));
    expect({ fails: res.fails, hscroll: res.hscroll }).toEqual({ fails: [], hscroll: false });
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow).toBeLessThanOrEqual(0);
  });
}
