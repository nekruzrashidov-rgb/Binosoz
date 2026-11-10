/* About the project (/about): hero with the numbers of the building, the complex and its amenities, the team,
   the construction timeline and a call to action. The footer, icons and headings come from site.js (S.site). */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  S.views = S.views || {};

  const FEATURES = [['🏊', 'aboutpg.f.pool'], ['🏋️', 'aboutpg.f.gym'], ['🚗', 'aboutpg.f.parking'], ['🛡️', 'aboutpg.f.guard'], ['🌳', 'aboutpg.f.playground'], ['🏢', 'aboutpg.f.lifts']];
  const TEAM = [['aboutpg.team.1.n', 'aboutpg.team.1.r'], ['aboutpg.team.2.n', 'aboutpg.team.2.r'], ['aboutpg.team.3.n', 'aboutpg.team.3.r']];
  const MILESTONES = [['2022 Q1', 'aboutpg.tl.1'], ['2023 Q2', 'aboutpg.tl.2'], ['2024 Q4', 'aboutpg.tl.3'], ['2026 Q1', 'aboutpg.tl.4']];

  S.views.about = function (root) {
    const cfg = window.SITORA_CONFIG || {};
    const site = S.site;
    const accent = (text) => site.accentFirst(text);
    // config.js numbers until the building loads (and when the API is unavailable).
    const d = { floors: cfg.DEFAULT_FLOORS || 20, apts: cfg.DEFAULT_APTS || 160, delivery: cfg.DEFAULT_DELIVERY || 'Q1 2026' };
    let alive = true;

    const page = h('div', { class: 'page-in lux about-page' });
    root.appendChild(page);

    // Floors and apartments count up from 0 (1.2 s, ease-out); with less motion or for a robot they are shown at once.
    const chips = h('div', { class: 'about-chips' });
    const floorsChip = h('span', { class: 'about-chip' }), aptsChip = h('span', { class: 'about-chip' }), deliveryChip = h('span', { class: 'about-chip' });
    chips.append(floorsChip, aptsChip, deliveryChip);
    let counting = 0;
    const drawChips = () => {
      deliveryChip.textContent = t('aboutpg.chip.delivery', { y: d.delivery });
      const set = (k) => { floorsChip.textContent = t('aboutpg.chip.floors', { n: Math.round(d.floors * k) }); aptsChip.textContent = t('aboutpg.chip.apts', { n: Math.round(d.apts * k) }); };
      if (!S.motionOk) { set(1); return; }
      const token = ++counting, t0 = performance.now();
      const step = (now) => { if (token !== counting) return; const p = Math.min(1, (now - t0) / 1200); set(1 - Math.pow(1 - p, 3)); if (p < 1) requestAnimationFrame(step); };
      set(0); requestAnimationFrame(step);
      // Without frames (a background tab) the final numbers still appear.
      setTimeout(() => { if (token === counting) { counting++; set(1); } }, 1400);
    };
    drawChips();

    const hero = h('section', { class: 'about-hero' }, h('div', { class: 'wrap' },
      h('nav', { class: 'crumbs', 'aria-label': t('cat.crumbs') }, h('a', { href: '/' }, t('nav.home')), h('span', { 'aria-hidden': 'true' }, '/'), h('span', { 'aria-current': 'page' }, t('nav.project'))),
      h('h1', { class: 'about-title' }, t('login.heroName')),
      h('p', { class: 'about-lead' }, t('aboutpg.hero.sub')),
      chips));

    const complex = h('section', { class: 'section lux-sec', id: 'complex' }, h('div', { class: 'wrap about-cols' },
      h('div', { class: 'about-copy reveal' },
        h('h2', null, accent(t('aboutpg.complex'))),
        h('p', null, t('aboutpg.p1')), h('p', null, t('aboutpg.p2')), h('p', null, t('aboutpg.p3'))),
      h('div', { class: 'about-feats reveal' }, FEATURES.map(([ic, k]) => h('div', { class: 'about-feat' }, h('span', { class: 'about-feat-ic', 'aria-hidden': 'true' }, ic), h('b', null, t(k)))))));

    const team = h('section', { class: 'section lux-sec alt-band', id: 'team' }, h('div', { class: 'wrap' },
      h('div', { class: 'lux-head reveal' }, h('h2', null, accent(t('aboutpg.team')))),
      h('div', { class: 'team-grid' }, TEAM.map(([n, r], i) => h('article', { class: 'team-card reveal', style: { transitionDelay: i * 90 + 'ms' } },
        h('span', { class: 'team-ava', 'aria-hidden': 'true' }, S.fmt.initials(t(n))),
        h('h3', null, t(n)), h('i', { class: 'team-line', 'aria-hidden': 'true' }), h('p', null, t(r)))))));

    const timeline = h('section', { class: 'section lux-sec', id: 'timeline' }, h('div', { class: 'wrap' },
      h('div', { class: 'lux-head reveal' }, h('h2', null, accent(t('aboutpg.timeline')))),
      h('ol', { class: 'timeline' }, MILESTONES.map(([when, k], i) => h('li', { class: 'tl-item reveal' + (i === MILESTONES.length - 1 ? ' last' : ''), style: { transitionDelay: i * 90 + 'ms' } },
        h('span', { class: 'tl-dot', 'aria-hidden': 'true' }),
        h('time', null, when), h('h3', null, t(k)), h('p', null, t(k + '.d')))))));

    const cta = h('section', { class: 'section lux-sec alt-band', id: 'cta' }, h('div', { class: 'wrap about-cta reveal' },
      h('h2', null, accent(t('aboutpg.cta'))),
      h('div', { class: 'about-cta-btns' },
        h('a', { class: 'btn gold lg', href: '/apartments' }, t('aboutpg.cta.apts')),
        h('a', { class: 'btn ghost lg', href: '/contact' }, t('aboutpg.cta.contact')))));

    page.append(hero, complex, team, timeline, cta, site.footer());
    [complex, team, timeline, cta].forEach((sec) => sec.classList.add('section-reveal'));
    const stopReveal = site.reveal(page);

    // The building's own numbers: floors, apartments (planned or counted) and the delivery year.
    (async () => {
      try {
        const id = await site.building();
        if (!id) return;
        const b = await S.api.building(id);
        if (!alive) return;
        if (b.floorsCount) d.floors = b.floorsCount;
        if (b.plannedApartments) d.apts = b.plannedApartments;
        if (b.completionYear) d.delivery = String(b.completionYear);
        drawChips();
      } catch (e) { /* the page stays with the config numbers */ }
    })();

    return () => { alive = false; stopReveal(); };
  };
})();
