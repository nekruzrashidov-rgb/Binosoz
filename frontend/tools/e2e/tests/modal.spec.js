// A-07 and A-17: the booking dialog keeps the focus inside (Tab and Shift+Tab go round, the page behind is inert),
// is named by its title, Escape closes it and the focus returns to the button; an empty form is checked on the page
// and nothing is sent to the server.
const { test, expect } = require('@playwright/test');
const { mockCatalog } = require('./catalog-mock');

const inDialog = (page) => page.evaluate(() => !!document.activeElement && !!document.activeElement.closest('.modal'));

test('booking dialog: focus trap, name, Escape, focus back; empty form is not sent', async ({ page }) => {
  const sent = [];
  await mockCatalog(page, sent);
  await page.goto('/apartments');
  const open = page.locator('button', { hasText: 'Забронировать на 48 часов' }).first();
  await open.click();
  const dialog = page.getByRole('dialog', { name: /Бронь квартиры/ });
  await expect(dialog).toBeVisible();
  await expect.poll(() => inDialog(page)).toBe(true);
  // The page behind cannot be reached.
  expect(await page.evaluate(() => document.querySelector('header, .lux-top').closest('[inert]') !== null)).toBe(true);
  for (let i = 0; i < 12; i++) { await page.keyboard.press('Tab'); expect(await inDialog(page)).toBe(true); }
  for (let i = 0; i < 12; i++) { await page.keyboard.press('Shift+Tab'); expect(await inDialog(page)).toBe(true); }

  // Empty name, only +992: errors under the fields, no request.
  await dialog.locator('button[type=submit], .actions button.gold').last().click();
  await expect(dialog).toContainText('Укажите имя');
  await expect(dialog).toContainText('Неверный номер телефона');
  expect(sent).toEqual([]);

  await page.keyboard.press('Escape');
  await expect(dialog).toHaveCount(0);
  expect(await page.evaluate(() => document.querySelectorAll('[inert]').length)).toBe(0);
  await expect(open).toBeFocused();
});
