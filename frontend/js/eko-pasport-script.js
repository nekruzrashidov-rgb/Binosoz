/* Eco passport of a building (S.eco): the public block on the site and the form in the CRM.
   Data: GET /buildings/{id} → eco (only filled fields come; no eco — no block at all, no «—» or placeholders).
   Edited by Content and leaders: PUT /buildings/{id}/eco. Texts: eco.* in js/i18n/*.js. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);

  // Energy classes from the best to the worst; the scale colours are status tokens (good / warn / bad).
  const CLASSES = [['APlusPlus', 'A++', 'good'], ['APlus', 'A+', 'good'], ['A', 'A', 'good'], ['B', 'B', 'warn'], ['C', 'C', 'warn'], ['D', 'D', 'bad']];
  const HEATING = ['Central', 'IndividualGas', 'HeatPump', 'Electric'];
  const FEATURES = ['hasHeatMeters', 'hasWaterMeters', 'hasLedCommonAreas', 'hasEvCharging', 'hasRainwaterReuse', 'hasWasteSorting'];
  const ICON = {
    leaf: '<path d="M5 19c0-8 5-13 14-14-1 9-6 14-14 14z"/><path d="M5 19l7-7"/>',
    bolt: '<path d="M13 3L5 13h6l-1 8 8-10h-6l1-8z"/>',
    check: '<path d="M5 12l4 4 10-10"/>',
  };
  const icon = (n) => { const s = document.createElementNS('http://www.w3.org/2000/svg', 'svg'); s.setAttribute('viewBox', '0 0 24 24'); s.setAttribute('width', 18); s.setAttribute('height', 18); s.setAttribute('fill', 'none'); s.setAttribute('stroke', 'currentColor'); s.setAttribute('stroke-width', 1.9); s.setAttribute('stroke-linecap', 'round'); s.setAttribute('stroke-linejoin', 'round'); s.setAttribute('aria-hidden', 'true'); s.innerHTML = ICON[n]; return s; };

  const label = (cls) => (CLASSES.find((c) => c[0] === cls) || [])[1] || '';
  const rank = (cls) => CLASSES.findIndex((c) => c[0] === cls);
  const has = (v) => v !== null && v !== undefined && v !== '';
  const nf = (v) => S.fmt.n(v);

  // Anything to show? Booleans count only when true (a feature the building has).
  function filled(eco) {
    if (!eco) return false;
    return Object.keys(eco).some((k) => (FEATURES.includes(k) ? eco[k] === true : has(eco[k]) && !/(Ru|Tg)$/.test(k)));
  }

  // The scale A++ … D with the building's class marked (the others dimmed); the class also said in words.
  function scale(cls) {
    const i = rank(cls);
    return h('div', { class: 'eco-scale-wrap' },
      h('div', { class: 'eco-scale', role: 'img', 'aria-label': t('eco.classIs', { c: label(cls) }) },
        CLASSES.map(([k, l, tone], j) => h('span', { class: 'eco-step ' + tone + (j === i ? ' on' : ''), 'aria-hidden': 'true' }, l))),
      h('p', { class: 'eco-class-text' }, t('eco.classIs', { c: label(cls) })));
  }

  // The public block: scale, facts with numbers, features the building has.
  function block(eco, opts) {
    if (!filled(eco)) return null;
    const o = opts || {};
    const facts = [];
    if (has(eco.wallInsulation)) facts.push([t('eco.wall'), eco.wallInsulation]);
    if (has(eco.windows)) facts.push([t('eco.windows'), eco.windows]);
    if (has(eco.heatingType)) facts.push([t('eco.heating'), t('eco.heat.' + eco.heatingType)]);
    if (has(eco.estimatedKwhPerM2Year)) facts.push([t('eco.kwh'), t('eco.kwhValue', { n: nf(eco.estimatedKwhPerM2Year) })]);
    if (has(eco.solarPanelsKw)) facts.push([t('eco.solar'), t('eco.kw', { n: nf(eco.solarPanelsKw) })]);
    if (has(eco.greenAreaM2)) facts.push([t('eco.green'), nf(eco.greenAreaM2) + ' ' + t('u.m2')]);
    const features = FEATURES.filter((k) => eco[k] === true);
    return h('div', { class: 'eco-card card' + (o.compact ? ' compact' : '') },
      h('div', { class: 'eco-head' }, h('span', { class: 'eco-ic' }, icon('leaf')), h('h3', null, t('eco.title')), o.demo ? h('span', { class: 'demo-badge' }, t('demo.badge')) : null),
      has(eco.energyClass) ? scale(eco.energyClass) : null,
      facts.length ? h('dl', { class: 'eco-facts' }, facts.map(([k, v]) => h('div', null, h('dt', null, k), h('dd', null, v)))) : null,
      features.length ? h('ul', { class: 'eco-features' }, features.map((k) => h('li', null, icon('check'), t('eco.f.' + k)))) : null,
      h('p', { class: 'eco-note' }, t('eco.note')));
  }

  // A small badge «Энергокласс A+» (apartment sheet, cards).
  const badge = (eco) => (eco && has(eco.energyClass) ? h('span', { class: 'eco-badge ' + CLASSES[rank(eco.energyClass)][2] }, icon('leaf'), t('eco.badge', { c: label(eco.energyClass) })) : null);

  // CRM form: the whole passport at once; empty fields mean «not filled». Booleans: not set / yes / no.
  function form(b, onSaved) {
    const e = b.eco || {};
    const sel = (name, options, v) => S.select(name, [['', t('eco.notSet')]].concat(options), v === null || v === undefined ? '' : String(v));
    const yn = (name) => sel(name, [['true', t('common.yes')], ['false', t('common.no')]], e[name]);
    const num = (name, step) => S.input(name, { type: 'number', min: 0, step: step || 'any', value: has(e[name]) ? e[name] : '' });
    const txt = (name) => S.input(name, { value: e[name] || '', maxlength: 200 });
    const f = {
      energyClass: sel('energyClass', CLASSES.map(([k, l]) => [k, l]), e.energyClass),
      heatingType: sel('heatingType', HEATING.map((k) => [k, t('eco.heat.' + k)]), e.heatingType),
      wallInsulationRu: txt('wallInsulationRu'), wallInsulationTg: txt('wallInsulationTg'),
      windowsRu: txt('windowsRu'), windowsTg: txt('windowsTg'),
      estimatedKwhPerM2Year: num('estimatedKwhPerM2Year'), solarPanelsKw: num('solarPanelsKw'), greenAreaM2: num('greenAreaM2'),
    };
    FEATURES.forEach((k) => { f[k] = yn(k); });
    const btn = h('button', { class: 'btn', type: 'submit' }, t('common.save'));
    const el = h('form', { class: 'card stack eco-form', novalidate: true },
      h('h3', null, t('eco.title')), h('p', { class: 'muted' }, t('eco.formHint')),
      h('div', { class: 'grid g2' }, S.field(t('eco.class'), f.energyClass), S.field(t('eco.heating'), f.heatingType)),
      h('div', { class: 'grid g2' }, S.field(t('eco.wall') + ' (RU)', f.wallInsulationRu), S.field(t('eco.wall') + ' (TJ)', f.wallInsulationTg)),
      h('div', { class: 'grid g2' }, S.field(t('eco.windows') + ' (RU)', f.windowsRu), S.field(t('eco.windows') + ' (TJ)', f.windowsTg)),
      h('div', { class: 'grid g3' }, S.field(t('eco.kwhField'), f.estimatedKwhPerM2Year), S.field(t('eco.solarField'), f.solarPanelsKw), S.field(t('eco.greenField'), f.greenAreaM2)),
      h('div', { class: 'grid g3' }, FEATURES.map((k) => S.field(t('eco.f.' + k), f[k]))),
      h('div', null, btn));
    el.addEventListener('submit', async (ev) => {
      ev.preventDefault(); S.showErrors(el, null);
      const v = (k) => f[k].value.trim();
      const body = {
        energyClass: v('energyClass') || null, heatingType: v('heatingType') || null,
        wallInsulationRu: v('wallInsulationRu') || null, wallInsulationTg: v('wallInsulationTg') || null,
        windowsRu: v('windowsRu') || null, windowsTg: v('windowsTg') || null,
        estimatedKwhPerM2Year: v('estimatedKwhPerM2Year') === '' ? null : +v('estimatedKwhPerM2Year'),
        solarPanelsKw: v('solarPanelsKw') === '' ? null : +v('solarPanelsKw'),
        greenAreaM2: v('greenAreaM2') === '' ? null : +v('greenAreaM2'),
      };
      FEATURES.forEach((k) => { body[k] = v(k) === '' ? null : v(k) === 'true'; });
      await S.busy(btn, async () => { try { const r = await S.api.saveBuildingEco(b.id, body); S.toast(t('common.saved'), 'ok'); if (onSaved) onSaved(r); } catch (er) { S.showErrors(el, er); } });
    });
    return el;
  }

  // Energy costs of an apartment (GET /apartments/{id}/utility): kWh and TJS per year and month, the comparison with a
  // typical building when the developer gave one (with its source, small, under the numbers).
  function utility(r, apt) {
    const money = (v) => S.fmt.money(v, 'TJS');
    const kwh = (v) => t('util.kwh', { n: S.fmt.n(v) });
    const row = (k, v) => h('div', { class: 'util-row' }, h('span', null, k), h('b', null, v));
    const base = r.baselineKwhPerYear !== null && r.baselineKwhPerYear !== undefined;
    const save = base && r.savingCostPerYearTjs;
    return h('div', { class: 'util-card card' },
      h('div', { class: 'eco-head' }, h('span', { class: 'eco-ic' }, icon('bolt')), h('h3', null, t('util.title', { n: apt.number })), r.isDemo ? h('span', { class: 'demo-badge' }, t('demo.badge')) : null),
      h('p', { class: 'muted' }, t('util.basis', { a: S.fmt.area(r.area), k: S.fmt.n(r.kwhPerM2Year), tr: String(r.tariffKwhTjs).replace('.', ',') })),
      h('div', { class: 'util-grid' },
        row(t('util.perMonth'), kwh(r.kwhPerMonth) + ' · ' + money(r.costPerMonthTjs)),
        row(t('util.perYear'), kwh(r.kwhPerYear) + ' · ' + money(r.costPerYearTjs)),
        base ? row(t('util.baseline'), kwh(r.baselineKwhPerYear) + ' · ' + money(r.baselineCostPerYearTjs)) : null,
        save ? row(save > 0 ? t('util.saving') : t('util.more'), kwh(Math.abs(r.savingKwhPerYear)) + ' · ' + money(Math.abs(r.savingCostPerYearTjs))) : null),
      h('p', { class: 'eco-note' }, t('util.note')),
      base && r.baselineSource ? h('p', { class: 'eco-note' }, t('util.source', { s: r.baselineSource })) : null);
  }

  // CRM → settings: the developer's tariff and coefficients, each with its source; nothing is invented by the site.
  function settingsForm() {
    const box = h('div', { class: 'card stack green-settings' }, h('h3', null, t('gset.title')), S.skeleton ? S.skeleton(2, 40) : '');
    S.api.greenSettings().then((g) => {
      const num = (name, v, step) => S.input(name, { type: 'number', min: 0, step: step || 'any', value: v === null || v === undefined ? '' : v });
      const tariff = num('tariffKwhTjs', g.tariffKwhTjs, '0.0001'), baseline = num('baselineKwhPerM2Year', g.baselineKwhPerM2Year), co2 = num('co2KgPerKgPaper', g.co2KgPerKgPaper, '0.0001');
      const bSrc = S.input('baselineSource', { value: g.baselineSource || '', maxlength: 500 }), cSrc = S.input('co2Source', { value: g.co2Source || '', maxlength: 500 });
      const demo = h('input', { type: 'checkbox', checked: !!g.isDemo });
      const btn = h('button', { class: 'btn', type: 'submit' }, t('common.save'));
      const f = h('form', { class: 'stack', novalidate: true }, h('p', { class: 'hint' }, t('gset.hint')),
        h('div', { class: 'grid g2' }, S.field(t('gset.tariff'), tariff), S.field(t('gset.baseline'), baseline)), S.field(t('gset.baselineSource'), bSrc),
        h('div', { class: 'grid g2' }, S.field(t('gset.co2'), co2), S.field(t('gset.co2Source'), cSrc)),
        h('label', { class: 'check' }, demo, t('gset.demo')), h('div', null, btn));
      const val = (el) => (el.value.trim() === '' ? null : +el.value);
      f.addEventListener('submit', async (e) => {
        e.preventDefault(); S.showErrors(f, null);
        await S.busy(btn, async () => { try { await S.api.saveGreenSettings({ tariffKwhTjs: val(tariff), baselineKwhPerM2Year: val(baseline), baselineSource: bSrc.value.trim() || null, co2KgPerKgPaper: val(co2), co2Source: cSrc.value.trim() || null, isDemo: demo.checked }); S.toast(t('common.saved'), 'ok'); } catch (er) { S.showErrors(f, er); } });
      });
      box.replaceChildren(h('h3', null, t('gset.title')), f);
    }).catch((e) => box.replaceChildren(h('h3', null, t('gset.title')), h('div', { class: 'form-err' }, e.message)));
    return box;
  }

  // ---------- «Зелёный эффект» ----------
  // Weight: under 1 kg — in grams (one A4 sheet is 5 g: «0 кг» for a few sheets would hide the real number), else kg with one decimal.
  const weight = (kg) => (kg < 1 ? t('green.gV', { n: S.fmt.n(Math.round(kg * 1000)) }) : t('green.kgV', { n: String(Math.round(kg * 10) / 10).replace('.', ',') }));
  // Paper from the sheets themselves (5 g each), not from the rounded kilograms.
  const paper = (g) => weight(g.pagesSaved * 0.005);
  const anything = (g) => !!g && (g.documentsDigital > 0 || g.pagesSaved > 0 || g.onlineActions > 0);

  // Landing: a dark section with 3–4 counters from GET /public/green-impact. Nothing to show (all zeros or the API did
  // not answer) — the section stays hidden. CO₂ only when the developer set a coefficient with its source.
  function impactSection() {
    const sec = h('section', { class: 'lux-green section', id: 'green', hidden: true });
    S.api.greenImpact().then((g) => {
      if (!anything(g)) return;
      const tile = (v, l) => h('div', { class: 'green-tile' }, h('b', null, v), h('span', null, l));
      sec.replaceChildren(h('div', { class: 'wrap' },
        h('div', { class: 'lux-head' }, h('span', { class: 'green-kicker' }, icon('leaf'), t('green.kicker')), h('h2', null, t('green.title')), h('p', null, t('green.sub')), g.isDemo ? h('span', { class: 'demo-badge' }, t('demo.badge')) : null),
        h('div', { class: 'green-tiles' },
          tile(S.fmt.n(g.documentsDigital), t('green.docs')),
          tile(S.fmt.n(g.pagesSaved), t('green.pages')),
          tile(paper(g), t('green.paper')),
          g.co2KgSaved !== undefined && g.co2KgSaved !== null ? tile(weight(g.co2KgSaved), t('green.co2')) : tile(S.fmt.n(g.onlineActions), t('green.actions'))),
        h('p', { class: 'green-note' }, t('green.note'), g.co2Source ? ' ' + t('green.co2Source', { s: g.co2Source }) : '')));
      sec.hidden = false;
    }).catch(() => {});
    return sec;
  }

  // CRM dashboard: «Сэкономлено бумаги» — sheets, kg, CO₂ (with the coefficient), a bar per month (SVG, no library).
  function paperCard() {
    const card = h('div', { class: 'card green-card', hidden: true });
    S.api.greenStats().then((s) => {
      const g = s && s.totals;
      if (!g) return;
      const months = s.months || [];
      const max = Math.max(1, ...months.map((m) => m.pages));
      const ns = 'http://www.w3.org/2000/svg', W = 600, H = 160, bw = W / Math.max(months.length, 1);
      const svg = document.createElementNS(ns, 'svg'); svg.setAttribute('viewBox', '0 0 ' + W + ' ' + (H + 22)); svg.setAttribute('class', 'green-chart');
      svg.setAttribute('role', 'img'); svg.setAttribute('aria-label', t('green.chartLabel'));
      months.forEach((m, i) => {
        const bh = Math.round((m.pages / max) * H);
        const r = document.createElementNS(ns, 'rect'); r.setAttribute('x', i * bw + 6); r.setAttribute('y', H - bh); r.setAttribute('width', bw - 12); r.setAttribute('height', Math.max(bh, m.pages ? 2 : 0)); r.setAttribute('rx', 4);
        const tt = document.createElementNS(ns, 'title'); tt.textContent = m.month + ': ' + t('green.pagesN', { n: m.pages }); r.appendChild(tt);
        svg.appendChild(r);
        const tx = document.createElementNS(ns, 'text'); tx.setAttribute('x', i * bw + bw / 2); tx.setAttribute('y', H + 16); tx.setAttribute('text-anchor', 'middle'); tx.textContent = m.month.slice(5); svg.appendChild(tx);
      });
      const kpi = (v, l) => h('div', null, h('b', null, v), h('span', null, l));
      card.replaceChildren(
        h('div', { class: 'eco-head' }, h('span', { class: 'eco-ic' }, icon('leaf')), h('h3', null, t('green.paperTitle')), g.isDemo ? h('span', { class: 'demo-badge' }, t('demo.badge')) : null),
        h('div', { class: 'green-kpis' }, kpi(S.fmt.n(g.pagesSaved), t('green.pages')), kpi(paper(g), t('green.paper')),
          g.co2KgSaved !== undefined && g.co2KgSaved !== null ? kpi(weight(g.co2KgSaved), t('green.co2')) : null,
          kpi(S.fmt.n(g.onlineActions), t('green.actions'))),
        svg,
        h('p', { class: 'hint' }, t('green.cardNote', { b: s.onlineBookings, p: s.onlinePayments, c: s.chatMessages }), g.co2Source ? ' ' + t('green.co2Source', { s: g.co2Source }) : ''));
      card.hidden = false;
    }).catch(() => {});
    return card;
  }

  S.eco = { block, badge, form, filled, rank, classes: CLASSES, label, utility, settingsForm, impactSection, paperCard };
})();
