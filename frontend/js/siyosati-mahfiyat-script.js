/* Privacy policy (/privacy): what the site and the Binosoz platform really process — checked against the code
   (entities, localStorage keys, the refresh cookie, token and code lifetimes, third-party services).
   Texts: privacy.* in js/i18n/*.js; `backticks` in a text become <code>. The footer comes from S.site. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  S.views = S.views || {};

  const CONTACT = 'binosoz27@gmail.com';
  // [heading, paragraphs before the list, list items, paragraphs after the list]
  const SECTIONS = [
    ['s1', ['p'], [], []],
    ['s2', [], ['li1', 'li2', 'li3', 'li4', 'li5', 'li6', 'li7', 'li8'], []],
    ['s3', [], ['li1', 'li2', 'li3', 'li4', 'li5'], ['p']],
    ['s4', [], ['li1', 'li2', 'li3', 'li4', 'li5', 'li6', 'li7'], []],
    ['s5', ['p1', 'p2'], [], []],
    ['s6', [], ['li1', 'li2', 'li3', 'li4', 'li5', 'li6', 'li7', 'li8', 'li9'], ['p']],
    ['s7', [], ['li1', 'li2', 'li3', 'li4', 'li5'], []],
    ['s8', ['p'], [], []],
    ['s9', ['p'], [], []],
  ];

  // `code` → <code>, the contact address → a mailto link.
  function rich(text) {
    return String(text).split('`').flatMap((part, i) => {
      if (i % 2) return [h('code', null, part)];
      return part.split(CONTACT).flatMap((p, j) => (j ? [h('a', { href: 'mailto:' + CONTACT }, CONTACT), p] : [p]));
    });
  }

  S.views.privacy = function (root) {
    const key = (s, k) => 'privacy.' + s + '.' + k;
    const page = h('div', { class: 'page-in lux privacy-page' });
    root.appendChild(page);
    page.append(
      h('section', { class: 'lux-pagehead' }, h('div', { class: 'wrap' },
        h('nav', { class: 'crumbs', 'aria-label': t('cat.crumbs') }, h('a', { href: '/' }, t('nav.home')), h('span', { 'aria-hidden': 'true' }, '/'), h('span', { 'aria-current': 'page' }, t('privacy.title'))),
        h('h1', null, S.site.accentFirst(t('privacy.title'))),
        h('p', null, t('privacy.updated')))),
      h('section', { class: 'section' }, h('div', { class: 'wrap' },
        h('article', { class: 'privacy-doc card' },
          h('p', { class: 'privacy-lead' }, ...rich(t('privacy.intro'))),
          ...SECTIONS.map(([s, before, items, after]) => h('section', { class: 'privacy-sec', 'aria-labelledby': 'privacy-' + s },
            h('h2', { id: 'privacy-' + s }, t(key(s, 'h'))),
            ...before.map((k) => h('p', null, ...rich(t(key(s, k))))),
            items.length ? h('ul', null, ...items.map((k) => h('li', null, ...rich(t(key(s, k)))))) : null,
            ...after.map((k) => h('p', null, ...rich(t(key(s, k))))))))),
      ),
      S.site.footer());
    S.site.reveal && S.site.reveal(page);
    return () => {};
  };
})();
