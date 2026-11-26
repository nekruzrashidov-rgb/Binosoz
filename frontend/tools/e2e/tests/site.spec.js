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
  // No numbers, layouts, installment or «about» block without the API; the hero picture is signed as a render.
  await expect(page.locator('.nr-hero-stats')).toBeHidden();
  await expect(page.locator('#layouts')).toBeHidden();
  await expect(page.locator('#installment')).toBeHidden();
  await expect(page.locator('#about')).toBeHidden();
  await expect(page.locator('.nr-hero .nr-render-note')).toHaveText('Визуализация 3D-модели');
  await expect(page.locator('h1')).toHaveText('Rich Tower');
});

// The complex is «Rich Tower» (docs/DECISIONS.md): the old name is nowhere — texts, titles, descriptions, in every language.
// (The manager's own name on the contact page is a person, not the brand, and stays as config.js has it.)
const OLD_BRAND = /Некр[уӯ]з\s+Резиденс|Nekruz\s+Residence|НЕКРУЗ/;
test('no old brand name on the public pages, in every language', async ({ page }) => {
  for (const lang of ['ru', 'tg', 'en']) {
    await page.addInitScript((l) => { try { localStorage.setItem('sitora.lang', l); } catch (e) { /* private mode */ } }, lang);
    for (const url of PUBLIC) {
      await open(page, url);
      const text = await page.evaluate(() => document.body.innerText + ' ' + document.title + ' ' + ((document.querySelector('meta[name=description]') || {}).content || '')
        + ' ' + [...document.querySelectorAll('meta[property^="og:"]')].map((m) => m.content).join(' '));
      expect(text, lang + ' ' + url).not.toMatch(OLD_BRAND);
    }
  }
});

// The name comes from the API (the organization, GET /public/brand): another name there — the page shows it.
test('the complex name comes from the API', async ({ page }) => {
  await page.route('**/api/v1/public/brand**', (route) => route.fulfill({ status: 200, contentType: 'application/json',
    body: JSON.stringify({ organizationId: '01a20000-0000-7000-8000-000000000001', name: 'Тест Тауэр', slug: 'nekruz', buildings: [] }) }));
  await open(page, '/');
  await expect(page.locator('h1')).toHaveText('Тест Тауэр');
  await expect(page.locator('header .brand-word')).toContainText('Тест');
  await expect(page).toHaveTitle(/Тест Тауэр/);
  await expect(page.locator('.foot-brand b')).toHaveText('Тест Тауэр');
});

// Claims that are not in the data must not come back on any public page (the redesign removed them).
const INVENTED = ['бизнес-класса', 'Элитный', '10 лет на рынке', '5 минут от', 'Ключи в срок', 'Штрафные санкции', 'Охрана 24/7',
  'Бассейн на крыше', 'Otis', 'в течение 15 минут', 'за 15 минут', 'Варзоб', 'Руководитель проекта', '[Имя'];
test('no invented claims on the public pages (without data)', async ({ page }) => {
  for (const url of ['/', '/apartments', '/about', '/gallery', '/contact', '/login', '/register']) {
    await open(page, url);
    const text = await page.evaluate(() => document.body.innerText + ' ' + document.title + ' ' + (document.querySelector('meta[name=description]') || {}).content);
    for (const phrase of INVENTED) expect(text, url + ': «' + phrase + '»').not.toContain(phrase);
  }
});

test('about without data: no amenities, no construction progress, no team', async ({ page }) => {
  await open(page, '/about');
  await expect(page.locator('#amenities')).toBeHidden();
  await expect(page.locator('#timeline')).toBeHidden();
  await expect(page.locator('#team')).toHaveCount(0);
});

test('catalog without data: one empty card, no 3D stage', async ({ page }) => {
  await open(page, '/apartments');
  await expect(page.locator('.soon-block')).toHaveCount(1);
  await expect(page.locator('#hero-top')).toBeHidden();
  await expect(page.locator('#calc')).toBeHidden();
  expect(await page.evaluate(() => !!window.THREE)).toBe(false);
});

test('without data: no eco passport and no energy costs (nothing filled in — nothing shown)', async ({ page }) => {
  await open(page, '/apartments');
  await expect(page.locator('#eco')).toBeHidden();
  await expect(page.locator('.eco-card')).toHaveCount(0);
  await expect(page.locator('.util-card')).toHaveCount(0);
  await open(page, '/');
  await expect(page.locator('.eco-card')).toHaveCount(0);
});

test('without data: the «Зелёный эффект» section is not shown (no invented counters)', async ({ page }) => {
  await open(page, '/');
  await expect(page.locator('#green')).toBeHidden();
  await expect(page.locator('.green-tile')).toHaveCount(0);
  await open(page, '/about');
  await expect(page.locator('#green-model')).toBeVisible();
});

test('login: the buyer tab is chosen by default', async ({ page }) => {
  await open(page, '/login');
  await expect(page.locator('.login-tabs button.on')).toHaveText('Покупатель');
});

// Blocks that fade in (.reveal) must never stay invisible: none at opacity 0 inside the screen, also after /#why.
for (const url of ['/', '/#why', '/apartments', '/about']) {
  test(`reveal: nothing on screen stays invisible (${url})`, async ({ page }) => {
    // The site turns the fade-in off for robots (navigator.webdriver): pretend to be a person, or nothing would be tested.
    await page.addInitScript(() => Object.defineProperty(Navigator.prototype, 'webdriver', { get: () => false }));
    await open(page, url);
    await expect(page.locator('html')).toHaveClass(/motion-ok/);
    await page.waitForTimeout(1200);
    const hidden = async () => page.evaluate(() => [...document.querySelectorAll('.reveal, .section-reveal')].filter((el) => {
      const r = el.getBoundingClientRect();
      return r.height > 0 && r.top < innerHeight && r.bottom > 0 && getComputedStyle(el).opacity === '0';
    }).map((el) => el.className));
    expect(await hidden()).toEqual([]);
    // Scrolled to the bottom step by step: whatever came into view has appeared.
    for (let y = 0; y < 12; y++) {
      await page.mouse.wheel(0, 700);
      await page.waitForTimeout(250);
    }
    await page.waitForTimeout(900);
    expect(await hidden()).toEqual([]);
  });
}

// WCAG 1.4.11: the borders of fields, PIN cells and outline buttons — at least 3:1 against what is behind them.
for (const theme of ['light', 'dark']) {
  test(`login: field borders have 3:1 contrast (${theme} theme)`, async ({ page }) => {
    await page.goto('/');
    await page.evaluate((t) => localStorage.setItem('sitora.theme', t), theme);
    await open(page, '/login');
    const low = await page.evaluate(() => {
      const rgb = (c) => (c.match(/[\d.]+/g) || []).slice(0, 3).map(Number);
      const lum = (c) => { const v = rgb(c).map((x) => { x /= 255; return x <= 0.03928 ? x / 12.92 : Math.pow((x + 0.055) / 1.055, 2.4); }); return 0.2126 * v[0] + 0.7152 * v[1] + 0.0722 * v[2]; };
      const bgOf = (el) => { for (let e = el.parentElement; e; e = e.parentElement) { const b = getComputedStyle(e).backgroundColor; if (b && !/rgba\(.*, 0\)$/.test(b) && b !== 'transparent') return b; } return 'rgb(255,255,255)'; };
      return [...document.querySelectorAll('.card.login input.inp, .card.login .pin-cell, .card.login .btn.ghost')].map((el) => {
        const a = lum(getComputedStyle(el).borderTopColor), b = lum(bgOf(el));
        return [el.className, Math.round(((Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)) * 100) / 100];
      }).filter(([, ratio]) => ratio < 3);
    });
    expect(low).toEqual([]);
  });
}

test('login: the six code cells fill the whole row', async ({ page }) => {
  await open(page, '/login');
  const widths = await page.evaluate(() => {
    const row = document.querySelector('.pin-cells').getBoundingClientRect();
    const cells = [...document.querySelectorAll('.pin-cells .pin-cell')].map((c) => c.getBoundingClientRect());
    return { row: Math.round(row.width), field: Math.round(document.querySelector('.card.login input[name=email]').getBoundingClientRect().width), right: Math.round(cells[5].right - row.left), first: Math.round(cells[0].width), last: Math.round(cells[5].width) };
  });
  expect(Math.abs(widths.row - widths.field)).toBeLessThanOrEqual(2);
  expect(Math.abs(widths.right - widths.row)).toBeLessThanOrEqual(1);
  expect(widths.first).toBe(widths.last);
});

test('login: a theme switch in the top bar', async ({ page }) => {
  await open(page, '/login');
  const before = await page.locator('html').getAttribute('data-theme');
  await page.locator('.lg-theme').click();
  await expect(page.locator('html')).not.toHaveAttribute('data-theme', before || 'light');
});

test('404: the page in the language of the design', async ({ page }) => {
  await open(page, '/404.html');
  await expect(page.locator('.nr-err .nr-kicker')).toHaveText('Ошибка 404');
  await expect(page.locator('.nr-err h1')).toHaveText('Страница не найдена');
  await expect(page.locator('.nr-err .nr-btn')).toHaveCount(2);
});

// «1 документ, 2 документа, 5 документов»: one rule for every number before a word (S.plural; ru, tg, en).
test('plural forms: Russian, Tajik and English', async ({ page }) => {
  await open(page, '/');
  const forms = await page.evaluate(() => {
    const f = ['документ', 'документа', 'документов'];
    const ru = [1, 2, 5, 11, 12, 21, 22, 25, 101, 111].map((n) => window.S.plural(n, f));
    window.S.lang = 'en';
    const en = [1, 2, 0].map((n) => window.S.plural(n, ['document', 'documents']));
    window.S.lang = 'tg';
    const tg = window.S.plural(5, ['ҳуҷҷат']);
    window.S.lang = 'ru';
    const viaT = window.S.t('green.pagesN', { n: 3 });
    return { ru, en, tg, viaT };
  });
  expect(forms.ru).toEqual(['документ', 'документа', 'документов', 'документов', 'документов', 'документ', 'документа', 'документов', 'документ', 'документов']);
  expect(forms.en).toEqual(['document', 'documents', 'documents']);
  expect(forms.tg).toBe('ҳуҷҷат');
  expect(forms.viaT).toBe('3 листа');
});

// Keyboard and screen readers: the content is the <main> landmark, the first Tab shows «Перейти к содержимому».
test('landmark: <main> and a skip link on the public pages', async ({ page }) => {
  for (const url of ['/', '/apartments', '/contact']) {
    await open(page, url);
    await expect(page.locator('main#view')).toHaveCount(1);
    await page.keyboard.press('Tab');
    await expect(page.locator('.skip-link')).toBeFocused();
    await expect(page.locator('.skip-link')).toHaveAttribute('href', '#view');
  }
});

test('footer: every link is at least 44 px high on a phone', async ({ page, isMobile }) => {
  test.skip(!isMobile, 'touch targets are measured at the phone width');
  await open(page, '/contact');
  const small = await page.evaluate(() => [...document.querySelectorAll('.lux-footer a')]
    .filter((a) => a.offsetParent !== null).map((a) => [a.textContent.trim().slice(0, 30), Math.round(a.getBoundingClientRect().height)])
    .filter(([, hgt]) => hgt < 44));
  expect(small).toEqual([]);
});

// The buyer signs in with e-mail and an own 6-digit code: six cells, digits only, paste fills all, Backspace goes back.
test('login: the buyer form has an e-mail and six digit cells', async ({ page }) => {
  await open(page, '/login');
  const cells = page.locator('.pin-cell[data-pin="pin"]');
  await expect(page.locator('input[name=email]')).toBeVisible();
  await expect(cells).toHaveCount(6);
  await expect(cells.first()).toHaveAttribute('inputmode', 'numeric');
  await expect(cells.first()).toHaveAttribute('autocomplete', 'current-password');
  await expect(cells.first()).toHaveAttribute('type', 'password');
  await expect(page.getByRole('button', { name: 'Забыли код?' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Регистрация' })).toBeVisible();

  // Letters are not taken; digits move to the next cell.
  await cells.first().click();
  await page.keyboard.type('4a8-2x9');
  expect(await cells.evaluateAll((els) => els.map((e) => e.value).join(''))).toBe('4829');
  await expect(cells.nth(4)).toBeFocused();
  // Backspace in an empty cell clears the previous one and goes back.
  await page.keyboard.press('Backspace');
  expect(await cells.evaluateAll((els) => els.map((e) => e.value).join(''))).toBe('482');
  await expect(cells.nth(3)).toBeFocused();

  // Pasting 6 digits (with spaces) fills every cell.
  await cells.nth(2).evaluate((el) => {
    const data = new DataTransfer();
    data.setData('text/plain', '25 08 17');
    el.dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }));
  });
  expect(await cells.evaluateAll((els) => els.map((e) => e.value).join(''))).toBe('250817');

  // «Показать код» shows the digits, «Скрыть код» hides them again.
  await page.getByRole('button', { name: 'Показать код' }).click();
  await expect(cells.first()).toHaveAttribute('type', 'text');
  await page.getByRole('button', { name: 'Скрыть код' }).click();
  await expect(cells.first()).toHaveAttribute('type', 'password');
});

test('login: «Забыли код?» opens the reset by a code from e-mail', async ({ page }) => {
  await open(page, '/login');
  await page.locator('input[name=email]').fill('buyer@mail.tj');
  await page.getByRole('button', { name: 'Забыли код?' }).click();
  await expect(page.getByRole('heading', { name: 'Сброс кода входа' })).toBeVisible();
  await expect(page.locator('input[name=email]')).toHaveValue('buyer@mail.tj');
  await expect(page.getByRole('button', { name: 'Получить код' })).toBeVisible();
  await page.getByRole('button', { name: '← Ко входу' }).click();
  await expect(page.getByRole('heading', { name: 'Вход в кабинет' })).toBeVisible();
});

test('registration: the code twice, a weak code is explained before sending', async ({ page }) => {
  await open(page, '/login');
  await page.getByRole('button', { name: 'Регистрация' }).click();
  await expect(page.getByRole('heading', { name: 'Регистрация' })).toBeVisible();
  const pin = page.locator('.pin-cell[data-pin="pin"]');
  const repeat = page.locator('.pin-cell[data-pin="pinRepeat"]');
  await expect(pin).toHaveCount(6);
  await expect(repeat).toHaveCount(6);
  await expect(pin.first()).toHaveAttribute('autocomplete', 'new-password');

  await pin.first().click();
  await page.keyboard.type('123456');
  await expect(page.locator('.pin-hint.bad')).toContainText('цифры подряд');
  for (let i = 0; i < 6; i++) await page.keyboard.press('Backspace');
  await page.keyboard.type('777777');
  await expect(page.locator('.pin-hint.bad')).toContainText('одинаковые');

  // Without the API: the form still checks names, e-mail and the repeat before sending anything.
  for (let i = 0; i < 6; i++) await page.keyboard.press('Backspace');
  await page.keyboard.type('482915');
  await expect(page.locator('.pin-hint.bad')).toHaveCount(0);
  await expect(repeat.first()).toBeFocused();
  await page.keyboard.type('482916');
  await page.locator('input[name=firstName]').fill('Фарход');
  await page.locator('input[name=lastName]').fill('Каримов');
  await page.locator('input[name=email]').fill('farhod@mail.tj');
  await page.getByRole('button', { name: 'Продолжить' }).click();
  await expect(page.locator('.field[data-name="pinRepeat"] .ferr')).toHaveText('Коды не совпадают');
});

// The reset and registration screens of /login: every text passes AA in both themes, no horizontal scroll.
for (const theme of ['light', 'dark']) {
  test(`login screens: contrast AA and no horizontal scroll (${theme} theme)`, async ({ page }) => {
    await page.goto('/');
    await page.evaluate((t) => localStorage.setItem('sitora.theme', t), theme);
    await open(page, '/login');
    await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
    await page.addScriptTag({ path: path.join(__dirname, '..', '..', 'check-contrast.js') });
    const check = async (name) => {
      const res = await page.evaluate(() => window.checkContrastDocument(document, window));
      expect({ name, fails: res.fails, darkOnImage: res.darkOnImage, hscroll: res.hscroll }).toEqual({ name, fails: [], darkOnImage: [], hscroll: false });
    };
    await check('login');
    await page.getByRole('button', { name: 'Регистрация' }).click();
    await check('register');
    await page.getByRole('button', { name: 'Уже есть учётная запись — войти' }).click();
    await page.getByRole('button', { name: 'Забыли код?' }).click();
    await check('forgot');
  });
}

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
