/* The Google map of the complex (/contact, the home page «Как добраться», the footer, the CRM preview). The address and the
   point come only from the API (GET /public/brand: address, latitude, longitude — the complex's own point or the centre of
   its blocks); nothing about the place is written in the code. No address and no point — no map.
   Privacy: until the visitor clicks «Показать карту» nothing is loaded from Google — a static card of the site with the
   address and two links (Google Maps «search» and «dir», the official URL format). The choice is remembered
   (localStorage sitora.mapOk). The iframe is the Maps Embed API with the key from the API (mapsEmbedKey, Maps__EmbedKey);
   without a key — only the links. CSP: frame-src https://www.google.com (vercel.json and SecurityHeadersMiddleware). */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (k, p) => S.t(k, p);
  const KEY = 'sitora.mapOk';
  const store = { get() { try { return localStorage.getItem(KEY) === '1'; } catch (e) { return false; } }, set() { try { localStorage.setItem(KEY, '1'); } catch (e) { /* private mode */ } } };

  // The point from the brand: { address, lat, lon, key } or null (nothing to show).
  function place(brand) {
    if (!brand) return null;
    const lat = brand.latitude != null ? Number(brand.latitude) : null, lon = brand.longitude != null ? Number(brand.longitude) : null;
    const hasPoint = Number.isFinite(lat) && Number.isFinite(lon);
    const address = String(brand.address || '').trim();
    if (!hasPoint && !address) return null;
    return { address, lat: hasPoint ? lat : null, lon: hasPoint ? lon : null, key: brand.mapsEmbedKey || null };
  }
  const query = (p) => (p.lat != null ? p.lat + ',' + p.lon : p.address);
  const searchUrl = (p) => 'https://www.google.com/maps/search/?api=1&query=' + encodeURIComponent(query(p));
  const routeUrl = (p) => 'https://www.google.com/maps/dir/?api=1&destination=' + encodeURIComponent(query(p));
  // The map's language: Russian for ru and tg (Google has no Tajik), English for en.
  const embedUrl = (p) => 'https://www.google.com/maps/embed/v1/place?key=' + encodeURIComponent(p.key) + '&q=' + encodeURIComponent(query(p)) + '&zoom=16&language=' + (S.lang === 'en' ? 'en' : 'ru');

  function iframe(p) {
    return h('iframe', { class: 'nr-map-frame', src: embedUrl(p), loading: 'lazy', referrerpolicy: 'no-referrer-when-downgrade', allowfullscreen: '', title: t('map.frameTitle', { a: p.address || query(p) }) });
  }
  function links(p) {
    return h('div', { class: 'nr-map-links' },
      h('a', { class: 'nr-link', href: searchUrl(p), target: '_blank', rel: 'noopener' }, t('map.open')),
      h('a', { class: 'nr-link', href: routeUrl(p), target: '_blank', rel: 'noopener' }, t('map.route')));
  }

  // The map block: opts.size 'large' (/contact, 16:9 up to 520 px) or 'band' (the home page, 360 px). Returns the element
  // (hidden until the brand answers; stays hidden without an address and a point).
  function block(opts) {
    const o = opts || {};
    const box = h('div', { class: 'nr-map nr-dark nr-map--' + (o.size || 'large'), hidden: true });
    const draw = (brand) => {
      const p = place(brand);
      box.hidden = !p;
      if (!p) { box.replaceChildren(); return; }
      if (p.key && store.get()) { box.replaceChildren(h('div', { class: 'nr-map-media' }, iframe(p)), h('div', { class: 'nr-map-bar' }, h('b', { class: 'nr-map-addr-sm' }, p.address), links(p))); return; }
      const show = p.key ? h('button', { class: 'btn nr-btn nr-btn--primary nr-map-show', type: 'button', onclick: () => { store.set(); draw(brand); const f = box.querySelector('iframe'); if (f) f.focus({ preventScroll: true }); } }, t('map.show')) : null;
      box.replaceChildren(h('div', { class: 'nr-map-card' },
        h('span', { class: 'nr-kicker nr-kicker--light' }, t('map.kicker')),
        p.address ? h('p', { class: 'nr-map-addr' }, p.address) : null,
        h('div', { class: 'nr-map-acts' }, show, links(p)),
        p.key ? h('p', { class: 'nr-map-note' }, t('map.note')) : null));
    };
    box.draw = draw;
    let off = null;
    // A page redrawn (language) leaves the old block: it stops listening.
    const onBrand = (b) => { if (!box.isConnected && off) { off(); off = null; return; } draw(b); };
    if (S.brandInfo) draw(S.brandInfo);
    else if (S.api && S.api.publicBrand) S.api.publicBrand().then(draw).catch(() => draw(null));
    if (S.bus) off = S.bus.on('brand', onBrand);
    return box;
  }

  // The footer's link «Открыть на карте»: shown when the brand has an address or a point.
  function footerLink() {
    const a = h('a', { class: 'foot-map', target: '_blank', rel: 'noopener', hidden: true }, t('foot.openMap'));
    const draw = (b) => { const p = place(b); a.hidden = !p; if (p) a.href = searchUrl(p); };
    if (S.brandInfo) draw(S.brandInfo); else if (S.api && S.api.publicBrand) S.api.publicBrand().then(draw).catch(() => {});
    return a;
  }

  S.map = { place, searchUrl, routeUrl, embedUrl, block, iframe, links, footerLink };
})();
