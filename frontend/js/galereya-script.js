/* Gallery (/gallery, design docs/design/Gallery.dc.html): renders of the site's 3D model (signed as renders), views of
   Dushanbe from Wikimedia Commons (S.site.galleryPhotos, each with author and licence) and the four floor plans drawn on
   a canvas. Filters by category; a click opens a lightbox with a larger picture, arrows and the keyboard (← → Esc).
   No photos of the complex: there are none yet, and a picture of another building must not pass for it. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  S.views = S.views || {};

  const FILTERS = [['all', 'galpg.f.all'], ['renders', 'galpg.f.renders'], ['city', 'galpg.f.city'], ['plans', 'galpg.f.plans']];
  // Renders of the 3D model (frontend/tools/renders): name and the caption key of the time of day.
  const RENDERS = [['tower-dusk-tall', 'nr.render.dusk'], ['tower-day-tall', 'nr.render.day'], ['tower-night-tall', 'nr.render.night']]
    .map(([render, time]) => ({ render, time, cat: 'renders', cap: 'nr.render' }));
  const PLANS = [0, 1, 2, 3].map((n) => ({ plan: n, cat: 'plans', cap: 'layouts.type' + n }));

  // A floor plan on a canvas of W×H CSS pixels.
  function paintPlan(canvas, n, W, H) { canvas.setAttribute('width', W); canvas.setAttribute('height', H); S.site.drawFloorPlan(canvas, n); }

  S.views.gallery = function (root) {
    const site = S.site;
    let filter = 'all', lightbox = null;
    const page = h('div', { class: 'page-in nr gallery-page' });
    root.appendChild(page);

    const head = h('section', { class: 'nr-pagehead' }, h('div', { class: 'nr-wrap' },
      h('nav', { class: 'nr-crumbs', 'aria-label': t('cat.crumbs') }, h('a', { href: '/' }, t('nav.home')), h('span', { 'aria-hidden': 'true' }, '/'), h('span', { 'aria-current': 'page' }, t('nav.gallery'))),
      h('h1', { class: 'nr-page-title' }, t('galpg.title')), h('p', { class: 'nr-page-sub' }, t('galpg.sub'))));

    const bar = h('div', { class: 'nr-pills', role: 'group', 'aria-label': t('galpg.filters') });
    const grid = h('div', { class: 'galpg-grid' });
    const items = RENDERS.concat(site.galleryPhotos.map((p) => ({ photo: p, cat: 'city', cap: 'galpg.p.' + p.id })), PLANS);
    const shown = () => items.filter((it) => filter === 'all' || it.cat === filter);
    // Caption: the title and, for a photo, «Фото: автор, лицензия» with a link to the file page.
    // A render: «Визуализация 3D-модели · закат»; a photo — its place and «Фото: автор, лицензия».
    const title = (it) => (it.render ? t('nr.render') + ' · ' + t(it.time) : t(it.cap));
    const caption = (it) => [h('span', { class: 'galpg-title' }, title(it)), it.photo ? site.galleryCredit(it.photo) : null];
    const picture = (it, sizes, cls) => (it.render ? site.renderImg(it.render, sizes, t('nr.render.alt') + ' — ' + t(it.time)) : site.galleryImg(it.photo, sizes, cls));

    function drawBar() {
      bar.replaceChildren(...FILTERS.map(([k, key]) => h('button', { type: 'button', class: 'nr-pill', 'aria-pressed': String(filter === k), onclick: () => { filter = k; drawBar(); drawGrid(); } }, t(key))));
    }
    function drawGrid() {
      const list = shown();
      grid.replaceChildren(...list.map((it, i) => {
        let pic;
        if (it.photo || it.render) pic = picture(it, '(max-width: 600px) 100vw, (max-width: 1000px) 50vw, 420px');
        else { pic = h('canvas', { role: 'img', 'aria-label': t(it.cap) }); paintPlan(pic, it.plan, 300, 200); }
        return h('figure', { class: 'galpg-card' },
          h('button', { type: 'button', class: 'galpg-shot' + (it.plan != null ? ' is-plan' : ''), 'aria-label': t('galpg.zoom') + ': ' + title(it), onclick: () => open(i) }, pic,
            h('span', { class: 'galpg-hover', 'aria-hidden': 'true' }, h('span', { class: 'galpg-zoom' }, '⤢'), t('galpg.zoom'))),
          h('figcaption', null, ...caption(it)));
      }));
    }

    // Lightbox over the page: the photo (or the plan at 600×400), scaled down on a phone; caption, arrows, Esc.
    function open(index) {
      close();
      let i = index;
      const list = shown();
      const stage = h('div', { class: 'lb-pic' });
      const cap = h('p', { class: 'lb-cap' });
      const count = h('span', { class: 'lb-count' });
      const draw = () => {
        const it = list[i];
        let pic;
        if (it.photo || it.render) { pic = picture(it, '(max-width: 680px) 100vw, 680px', 'lb-img'); pic.classList.add('lb-img'); pic.setAttribute('loading', 'eager'); }
        else { pic = h('canvas', { class: 'lb-canvas', role: 'img', 'aria-label': t(it.cap) }); paintPlan(pic, it.plan, 600, 400); }
        stage.replaceChildren(pic);
        cap.replaceChildren(...caption(it));
        count.textContent = (i + 1) + ' / ' + list.length;
      };
      const step = (d) => { i = (i + d + list.length) % list.length; draw(); };
      const prev = h('button', { type: 'button', class: 'lb-nav prev', 'aria-label': t('galpg.prev'), onclick: () => step(-1) }, '‹');
      const next = h('button', { type: 'button', class: 'lb-nav next', 'aria-label': t('galpg.next'), onclick: () => step(1) }, '›');
      const x = h('button', { type: 'button', class: 'lb-close', 'aria-label': t('common.close'), onclick: close }, '×');
      lightbox = h('div', { class: 'lightbox', role: 'dialog', 'aria-modal': 'true', 'aria-label': t('nav.gallery'), onclick: (e) => { if (e.target === lightbox) close(); } },
        h('div', { class: 'lb-box' }, x, h('div', { class: 'lb-stage' }, prev, stage, next), h('div', { class: 'lb-foot' }, cap, count)));
      lightbox.addEventListener('keydown', (e) => { if (e.key === 'Escape') close(); else if (e.key === 'ArrowLeft') step(-1); else if (e.key === 'ArrowRight') step(1); });
      document.body.appendChild(lightbox);
      document.body.classList.add('menu-open');
      draw();
      x.focus();
    }
    function close() {
      if (!lightbox) return;
      lightbox.remove(); lightbox = null;
      document.body.classList.remove('menu-open');
    }

    drawBar(); drawGrid();
    page.append(head, h('section', { class: 'nr-sec nr-gallery' }, h('div', { class: 'nr-wrap' }, bar, grid, h('p', { class: 'nr-note' }, t('galpg.note')))), site.footer());
    return () => close();
  };
})();
