/* About the project (/about, design docs/design/About.dc.html): a dark hero with the building's numbers, the complex
   (its description and address), the amenities, the construction progress, why the platform is a green model and a
   call to action. Everything about the house comes from the API: no invented amenities, dates or team members —
   a block without data is not shown. The footer, icons and headings come from site.js (S.site). */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  S.views = S.views || {};

  S.views.about = function (root) {
    const site = S.site;
    let alive = true;
    const page = h('div', { class: 'page-in nr about-page' });
    root.appendChild(page);

    // 1. Hero: the render of the 3D model, the name and the numbers of the building (each only when the API has it).
    const title = h('h1', { class: 'nr-page-title nr-about-title', 'data-brand': '' }, S.brandName());
    const chips = h('ul', { class: 'nr-chips' });
    const hero = h('section', { class: 'nr-about-hero nr-dark' },
      h('figure', { class: 'nr-hero-media' }, site.renderImg('tower-dusk-wide', '100vw', t('nr.render.alt'), true),
        h('figcaption', { class: 'nr-render-note' }, t('nr.render'))),
      h('div', { class: 'nr-hero-shade', 'aria-hidden': 'true' }),
      h('div', { class: 'nr-wrap nr-about-hero-in' },
        h('nav', { class: 'nr-crumbs', 'aria-label': t('cat.crumbs') }, h('a', { href: '/' }, t('nav.home')), h('span', { 'aria-hidden': 'true' }, '/'), h('span', { 'aria-current': 'page' }, t('nav.project'))),
        title, h('p', { class: 'nr-hero-sub' }, t('aboutpg.hero.sub')), chips));

    // 2. The complex: the developer's description and the address.
    const text = h('div', { class: 'nr-about-text' });
    // A-08: the complex and the amenities keep their place while the API answers (class is-wait, a min-height), so the
    // blocks below do not jump when the data arrives; without data they are hidden.
    const complex = h('section', { class: 'nr-sec is-wait', id: 'complex' }, h('div', { class: 'nr-wrap nr-split nr-split--top' },
      h('div', null, site.kicker(t('aboutpg.complex')), h('h2', { class: 'nr-h2' }, t('nr.about.title'))), text));

    // 3. Amenities: only those the content manager ticked in the CRM.
    const amenGrid = h('div', { class: 'nr-amen-grid' });
    const amenities = h('section', { class: 'nr-sec nr-sec--tight is-wait', id: 'amenities' }, h('div', { class: 'nr-wrap' }, amenGrid));

    // 4. Construction progress (GET /public/buildings/{id}/progress): stages and percents as the developer keeps them.
    const stages = h('ol', { class: 'nr-stages' });
    const overall = h('p', { class: 'nr-lead' });
    const progress = h('section', { class: 'nr-sec nr-dark', id: 'timeline', hidden: true }, h('div', { class: 'nr-wrap' },
      site.kicker(t('nr.about.progressKicker')), h('h2', { class: 'nr-h2' }, t('aboutpg.timeline')), overall, stages));

    // 5. Why this is a green business model: paper → digital documents, trips → 3D and the online account,
    //    energy-efficient buildings → eco passport and the cost calculator. A link to the live counters on the landing.
    const green = h('section', { class: 'nr-sec nr-band', id: 'green-model' }, h('div', { class: 'nr-wrap nr-split nr-split--top' },
      h('div', null, site.kicker(t('green.kicker')), h('h2', { class: 'nr-h2' }, t('aboutpg.green.title')), h('p', { class: 'nr-lead' }, t('aboutpg.green.text')),
        h('a', { class: 'nr-link', href: '/#green' }, t('aboutpg.green.link'), site.arrow())),
      h('ul', { class: 'nr-green-list' }, ['paper', 'trips', 'energy'].map((k) => h('li', null, h('h3', null, t('aboutpg.green.' + k + '.h')), h('p', null, t('aboutpg.green.' + k)))))));

    // 6. Call to action.
    const cta = h('section', { class: 'nr-sec nr-cta' }, h('div', { class: 'nr-wrap nr-cta-in' },
      h('h2', { class: 'nr-h2' }, t('aboutpg.cta')),
      h('div', { class: 'nr-actions' },
        h('a', { class: 'btn nr-btn nr-btn--primary', href: '/apartments' }, t('aboutpg.cta.apts'), site.arrow()),
        h('a', { class: 'btn nr-btn nr-btn--dark', href: '/contact' }, t('aboutpg.cta.contact')))));

    page.append(hero, complex, amenities, progress, green, cta, site.footer());
    const stopReveal = site.reveal(page);

    (async () => {
      try {
        const id = await site.building();
        if (!id || !alive) { settle(); return; }
        const b = await S.api.building(id);
        if (!alive) return;
        title.textContent = S.brandName();
        const apts = b.plannedApartments || b.total || 0;
        const desc = S.localText(b, 'description');
        const address = S.brandAddress(b.address) || site.contacts().address;
        text.replaceChildren(...[desc ? h('p', { class: 'nr-lead' }, desc) : null,
          address ? h('p', { class: 'nr-about-address' }, site.icon('pin'), h('span', null, t('nr.about.address', { a: address }))) : null].filter(Boolean));
        amenGrid.replaceChildren(...(b.amenities || []).map((k) => h('div', { class: 'nr-amen-card' }, site.icon(site.amenIcon(k)), h('h3', null, t('amen.' + k)))));
        settle();
        // The year of completion is a plan until the construction has stages with progress (A-04).
        const p = await S.api.publicProgress(id).catch(() => null);
        if (!alive) return;
        const planned = !p || !p.stages || !p.stages.length || !p.overallPercent;
        chips.replaceChildren(...[
          b.floorsCount ? t('aboutpg.chip.floors', { n: b.floorsCount }) : null,
          apts ? t('aboutpg.chip.apts', { n: apts }) : null,
          b.completionYear ? t(planned ? 'aboutpg.chip.planned' : 'aboutpg.chip.delivery', { y: b.completionYear }) : null,
        ].filter(Boolean).map((x) => h('li', null, x)), ...(b.isDemo ? [h('li', { class: 'nr-chip-demo' }, h('span', { class: 'demo-badge' }, t('demo.badge')))] : []));
        if (!p || !p.stages || !p.stages.length) return;
        overall.textContent = t('nr.about.overall', { n: p.overallPercent });
        stages.replaceChildren(...p.stages.map((s) => h('li', { class: 'nr-stage-item' + (s.percent >= 100 ? ' done' : '') },
          h('span', { class: 'nr-stage-dot', 'aria-hidden': 'true' }),
          h('div', { class: 'nr-stage-top' }, h('h3', null, s.name), h('b', null, s.percent + '%')),
          h('div', { class: 'nr-stage-bar', role: 'progressbar', 'aria-valuemin': 0, 'aria-valuemax': 100, 'aria-valuenow': s.percent, 'aria-label': s.name }, h('i', { style: { width: Math.max(0, Math.min(100, s.percent)) + '%' } })))));
        progress.hidden = false;
      } catch (e) { settle(); /* no data: the empty blocks are hidden */ }
    })();
    // The reserved blocks get their real height: shown with data, hidden without it.
    function settle() {
      complex.classList.remove('is-wait'); amenities.classList.remove('is-wait');
      complex.hidden = !text.children.length;
      amenities.hidden = !amenGrid.children.length;
    }

    return () => { alive = false; stopReveal(); };
  };
})();
