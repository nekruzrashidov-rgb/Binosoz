/* Manager / accountant / content / admin workspace. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  S.views = S.views || {};
  const STAGES = ['Lead', 'Showing', 'Booking', 'Contract', 'Payment', 'Completed', 'Cancelled'];
  const has = (...r) => S.api.has(...r);

  // Ids are GUIDs in the real API and numbers in the demo: show a short readable part.
  // The API issues UUIDv7 — they start with a timestamp, so the tail is what tells two ids apart.
  const shortId = (id) => (typeof id === 'string' && id.length > 12 ? id.replace(/-/g, '').slice(-8).toUpperCase() : id);
  function toObj(x) { if (!x) return {}; if (Array.isArray(x)) { const o = {}; x.forEach((i) => { o[S.pick(i, ['stage', 'status', 'name', 'key'], '')] = S.pick(i, ['count', 'value', 'total'], 0); }); return o; } const o = {}; Object.keys(x).forEach((k) => { o[k.charAt(0).toUpperCase() + k.slice(1)] = x[k]; }); return o; }

  S.views.crm = function (root) {
    if (!S.api.isAuthed || !S.api.isStaff()) return S.views.login(root, 'staff');
    const page = h('div', { class: 'page-in' }); root.appendChild(page);
    const items = [];
    const add = (key, label, icon, group, ok) => { if (ok) items.push({ key, label, icon, group }); };
    add('dashboard', t('crm.dashboard'), 'chart', t('crm.g.sales'), has('Manager'));
    add('pipeline', t('crm.pipeline'), 'kanban', t('crm.g.sales'), has('Manager'));
    add('leads', t('crm.leads'), 'inbox', t('crm.g.sales'), has('Manager'));
    // An accountant reads deals too (to post the first payment); creating deals and moving stages stays with sales.
    add('deals', t('crm.deals'), 'file', t('crm.g.sales'), has('Manager', 'Accountant'));
    add('customers', t('crm.customers'), 'users', t('crm.g.sales'), has('Manager'));
    add('payments', t('crm.payments'), 'wallet', t('crm.g.finance'), has('Accountant'));
    add('debts', t('crm.debts'), 'shield', t('crm.g.finance'), has('Accountant'));
    add('rate', t('crm.rate'), 'chart', t('crm.g.finance'), has('Accountant'));
    add('catalog', t('crm.catalog'), 'building', t('crm.g.catalog'), has('Content'));
    add('construction', t('crm.construction'), 'hammer', t('crm.g.catalog'), has('Content'));
    add('users', t('crm.users'), 'users', t('crm.g.system'), has('__admin__'));
    add('audit', t('crm.audit'), 'shield', t('crm.g.system'), has('__admin__'));
    const user = S.api.user;
    const ws = S.workspace(items, items[0].key, (k, main) => { S.clear(main); main.append(h('h1', null, items.find((i) => i.key === k).label), h('p', { class: 'sub' }, t('crm.sub.' + k))); const body = h('div', { class: 'stack' }); main.appendChild(body); try { views[k](body); } catch (e) { body.appendChild(h('div', { class: 'form-err' }, e.message)); console.error(e); } });
    page.appendChild(ws.el);
    queueMicrotask(() => ws.go(items[0].key));
    const openTab = (k) => ws.go(k);
    S.crmGo = openTab;

    /* ---------- views ---------- */
    const views = {
      async dashboard(body) {
        body.replaceChildren(S.skeleton(4, 80));
        const [sumRaw, rate] = await Promise.all([S.api.summary(), S.api.rate().catch(() => null)]);
        const r = sumRaw || {};
        const stages = toObj(S.pick(r, ['dealsByStage', 'stages', 'deals'], {}));
        const apts = toObj(S.pick(r, ['apartmentsByStatus', 'apartments'], {}));
        const k = (key, def) => S.pick(r, key, def);
        const sales = k(['salesTotalTjs', 'salesTotal', 'totalSales', 'salesAmount'], 0);
        const kp = [[t('dash.leads'), k(['openLeads', 'leads', 'leadsCount', 'newLeads'], 0), 'var(--info)'], [t('dash.bookings'), k(['activeBookings', 'bookings'], 0), 'var(--warn)'], [t('dash.expiring'), k(['expiringBookings', 'expiring', 'expiringSoon'], 0), 'var(--bad)'], [t('dash.sales'), S.fmt.money(sales, 'TJS'), 'var(--good)']];
        const total = Object.values(stages).reduce((s, x) => s + (+x || 0), 0) || 1;
        const aptTotal = Object.values(apts).reduce((s, x) => s + (+x || 0), 0) || 1;
        const col = { Free: '#2fd6a3', Booked: '#ffb030', Sold: '#6a7696', Onrequest: '#6f8bff', OnRequest: '#6f8bff' };
        body.replaceChildren(
          h('div', { class: 'kpis' }, kp.map(([l, v, c]) => h('div', { class: 'card kpi', style: { '--k': c } }, h('span', null, l), h('b', null, v), h('i')))),
          h('div', { class: 'grid g2' },
            h('div', { class: 'card' }, h('h3', null, t('dash.byStage')), STAGES.map((s) => { const n = +stages[s] || 0; return h('div', { class: 'hbar' }, S.stageChip(s), S.bar((n / total) * 100), h('b', { class: 'num', style: { textAlign: 'right' } }, n)); })),
            h('div', { class: 'card' }, h('h3', null, t('dash.byStatus')), h('div', { style: { display: 'flex', height: '16px', borderRadius: '8px', overflow: 'hidden', margin: '10px 0 16px', background: 'var(--surface-3)' } }, ['Free', 'Booked', 'Sold', 'OnRequest'].map((s) => h('div', { title: t('status.' + s.toLowerCase()), style: { width: ((+(apts[s] || apts[s === 'OnRequest' ? 'Onrequest' : s]) || 0) / aptTotal) * 100 + '%', background: col[s], transition: 'width 1s' } }))),
              h('div', { class: 'grid g2' }, ['Free', 'Booked', 'Sold', 'OnRequest'].map((s) => h('div', { class: 'row', style: { gap: '8px' } }, h('i', { style: { width: '11px', height: '11px', borderRadius: '3px', background: col[s], display: 'inline-block' } }), h('span', null, t('status.' + s.toLowerCase())), h('b', { class: 'num', style: { marginLeft: 'auto' } }, apts[s] || apts[s === 'OnRequest' ? 'Onrequest' : s] || 0)))),
              rate && rate.rate ? h('p', { class: 'hint', style: { marginTop: '14px' } }, t('dash.rate', { r: rate.rate, d: S.fmt.date(rate.date) })) : h('p', { class: 'hint', style: { marginTop: '14px' } }, t('dash.noRate')),
              r.salesNote === 'usdWithoutRate' ? h('p', { class: 'hint' }, t('dash.usdNoRate')) : null)));
      },

      async pipeline(body) {
        body.replaceChildren(S.skeleton(4, 120));
        const cols = await S.api.pipeline();
        const board = h('div', { class: 'kanban' }, cols.filter((c) => c.stage !== 'Cancelled').map((c) => h('div', { class: 'col' }, h('h4', null, h('span', null, t('stage.' + c.stage)), h('span', { class: 'badge' }, c.items.length)),
          c.items.length ? c.items.map((it) => card(it, c.stage)) : h('div', { class: 'hint', style: { padding: '10px 6px' } }, t('pipe.empty')))));
        body.replaceChildren(board);
        function card(it, stage) {
          const kind = it.kind || (stage === 'Lead' && !it.dealId && !it.apartmentNumber ? 'lead' : stage === 'Booking' && (it.expiresAt || it.expires) ? 'booking' : 'deal');
          const title = it.title || it.name || it.customerName || '#' + it.id;
          const sub = it.subtitle || it.phone || '';
          return h('div', { class: 'kcard', tabindex: 0, role: 'button', onclick: () => open(kind, it), onkeydown: (e) => { if (e.key === 'Enter') open(kind, it); } },
            h('b', null, title), sub ? h('small', null, sub) : null, it.comment ? h('small', null, it.comment) : null,
            it.price ? h('small', { class: 'num' }, S.fmt.money(it.price, it.currency)) : null, it.expiresAt ? h('div', { style: { marginTop: '6px' } }, S.cd(it.expiresAt)) : null);
        }
        function open(kind, it) {
          if (kind === 'lead') return leadModal(it.id, () => ws.go('pipeline'));
          if (kind === 'booking') return bookingModal(it, () => ws.go('pipeline'));
          return dealDrawer(it.dealId || it.id, () => ws.go('pipeline'));
        }
      },

      async leads(body) {
        let filter = 'all'; const holder = h('div');
        const bar = h('div', { class: 'toolbar' }, h('div', { class: 'pill-group' }, ['all', 'New', 'InProgress', 'Closed'].map((s) => h('button', { class: 'pill' + (filter === s ? ' on' : ''), onclick: (e) => { filter = s; S.$$('.pill', bar).forEach((p) => p.classList.remove('on')); e.currentTarget.classList.add('on'); draw(); } }, s === 'all' ? t('filter.all') : t('lead.st.' + s)))));
        body.replaceChildren(bar, holder);
        let list = []; const draw = () => holder.replaceChildren(S.tbl([{ label: t('f.name'), render: (l) => h('b', null, l.name) }, { label: t('f.phone'), render: (l) => l.phone }, { label: t('f.comment'), render: (l) => l.comment }, { label: t('lead.assignee'), render: (l) => l.assignee }, { label: t('lead.date'), render: (l) => S.fmt.dt(l.createdAt) }, { label: t('deal.stage'), render: (l) => S.chip(l.status, t('lead.st.' + l.status, null, l.status)) }], list.filter((l) => filter === 'all' || l.status === filter), { onRow: (l) => leadModal(l.id, load), empty: t('lead.none') }));
        const load = async () => { holder.replaceChildren(S.skeleton(5, 38)); list = await S.api.leads(); draw(); }; load();
      },

      async deals(body) {
        let stage = 'all', q = ''; const holder = h('div'); let list = [];
        const search = S.input('q', { type: 'search', placeholder: t('common.search'), 'aria-label': t('common.search') }); search.addEventListener('input', S.debounce(() => { q = search.value.toLowerCase(); draw(); }, 150));
        const bar = h('div', { class: 'toolbar' }, search, h('div', { class: 'pill-group' }, ['all', ...STAGES].map((s) => h('button', { class: 'pill' + (stage === s ? ' on' : ''), onclick: (e) => { stage = s; S.$$('.pill', bar).forEach((p) => p.classList.remove('on')); e.currentTarget.classList.add('on'); draw(); } }, s === 'all' ? t('filter.all') : t('stage.' + s)))), h('span', { class: 'spacer' }), S.api.user.roles.some((r) => r === 'Manager' || r === 'Admin') ? h('button', { class: 'btn', onclick: () => newDealModal(load) }, S.icon('plus'), t('deal.new')) : null);
        S.$$('.btn svg', bar).forEach((s) => { s.style.cssText = 'width:16px;height:16px;stroke:currentColor;fill:none;stroke-width:2'; });
        body.replaceChildren(bar, holder);
        const draw = () => holder.replaceChildren(S.tbl([
          { label: '#', render: (d) => shortId(d.id) }, { label: t('deal.apt'), render: (d) => h('b', null, '№ ' + d.apartmentNumber) }, { label: t('deal.customer'), render: (d) => d.customerName }, { label: t('deal.stage'), render: (d) => S.stageChip(d.stage) },
          { label: t('deal.price'), r: true, num: true, render: (d) => S.fmt.money(d.price - d.discount, d.currency) }, { label: t('sch.paid'), r: true, num: true, render: (d) => S.fmt.money(d.paid, d.currency) }, { label: t('deal.manager'), render: (d) => d.managerName }],
          list.filter((d) => (stage === 'all' || d.stage === stage) && (!q || (d.customerName + ' ' + d.apartmentNumber + ' ' + d.id).toLowerCase().includes(q))), { onRow: (d) => dealDrawer(d.id, load), empty: t('deal.none') }));
        async function load() { holder.replaceChildren(S.skeleton(5, 38)); list = await S.api.deals(); draw(); } load();
        S.reloadDeals = load;
      },

      async customers(body) {
        const holder = h('div'); const search = S.input('q', { type: 'search', placeholder: t('cust.search'), 'aria-label': t('cust.search') });
        const load = async () => { holder.replaceChildren(S.skeleton(5, 38)); const l = await S.api.customers(search.value.trim()); holder.replaceChildren(S.tbl([{ label: t('f.fullName'), render: (c) => h('b', null, c.fullName) }, { label: t('f.phone'), render: (c) => c.phone }, { label: t('f.email'), render: (c) => c.email }, { label: t('crm.deals'), r: true, render: (c) => c.dealsCount }, { label: t('lead.date'), render: (c) => S.fmt.date(c.createdAt) }], l, { onRow: (c) => customerModal(c.id), empty: t('cust.none') })); };
        search.addEventListener('input', S.debounce(load, 250));
        body.replaceChildren(h('div', { class: 'toolbar' }, search, h('span', { class: 'spacer' }), h('button', { class: 'btn', onclick: () => customerForm(null, load) }, t('cust.new'))), holder); load();
      },

      async payments(body) {
        const from = S.input('from', { type: 'date', value: S.today().slice(0, 8) + '01' }), to = S.input('to', { type: 'date', value: S.today() });
        const holder = h('div');
        const go = h('button', { class: 'btn', onclick: (e) => S.busy(e.currentTarget, load) }, t('rep.show'));
        const xl = h('button', { class: 'btn ghost', onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/reports/payments.xlsx', { from: from.value, to: to.value }, 'Excel').catch(S.err)) }, t('rep.excel'));
        body.replaceChildren(h('div', { class: 'toolbar' }, S.field(t('rep.from'), from), S.field(t('rep.to'), to), h('div', { style: { alignSelf: 'flex-end', display: 'flex', gap: '8px' } }, go, xl)), holder);
        async function load() {
          holder.replaceChildren(S.skeleton(5, 38));
          try {
            const r = await S.api.reportPayments(from.value, to.value);
            const totals = Object.keys(r.totals || {}).map((c) => h('div', { class: 'card kpi' }, h('span', null, t('rep.total') + ' ' + c), h('b', { style: { fontSize: '22px' } }, S.fmt.money(r.totals[c], c)), h('i')));
            holder.replaceChildren(h('div', { class: 'grid g4', style: { marginBottom: '14px' } }, totals, r.totalTjs !== null && r.totalTjs !== undefined ? h('div', { class: 'card kpi', style: { '--k': 'var(--good)' } }, h('span', null, t('rep.totalTjs')), h('b', { style: { fontSize: '22px' } }, S.fmt.money(r.totalTjs, 'TJS')), h('i')) : null),
              S.tbl([{ label: t('doc.date'), render: (p) => S.fmt.date(p.date) }, { label: t('deal.apt'), render: (p) => (p.apartmentNumber ? '№ ' + p.apartmentNumber : '#' + p.dealId) }, { label: t('deal.customer'), render: (p) => p.customerName }, { label: t('pay.amount'), r: true, num: true, render: (p) => (p.reversal ? '− ' : '') + S.fmt.money(p.amount, p.currency) }, { label: t('pay.tjs'), r: true, num: true, render: (p) => (p.reversal ? '− ' : '') + S.fmt.money(p.amountTjs, 'TJS') }, { label: t('pay.rate'), r: true, num: true, render: (p) => p.rate || '—' }, { label: t('pay.comment'), render: (p) => p.comment }], r.rows, { rowClass: (p) => (p.reversal ? 'rev' : ''), onRow: (p) => dealDrawer(p.dealId, null, p), empty: t('rep.none') }));
          } catch (e) { holder.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
        }
        load();
      },

      async debts(body) {
        const asOf = S.input('asof', { type: 'date', value: S.today() }); const holder = h('div');
        const go = h('button', { class: 'btn', onclick: (e) => S.busy(e.currentTarget, load) }, t('rep.show'));
        const xl = h('button', { class: 'btn ghost', onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/reports/debts.xlsx', { asOf: asOf.value }, 'Excel').catch(S.err)) }, t('rep.excel'));
        body.replaceChildren(h('div', { class: 'toolbar' }, S.field(t('rep.asOf'), asOf), h('div', { style: { alignSelf: 'flex-end', display: 'flex', gap: '8px' } }, go, xl)), holder);
        async function load() {
          holder.replaceChildren(S.skeleton(4, 38));
          try { const r = await S.api.reportDebts(asOf.value); holder.replaceChildren(S.tbl([{ label: t('deal.customer'), render: (d) => h('b', null, d.customerName) }, { label: t('f.phone'), render: (d) => d.phone }, { label: t('deal.apt'), render: (d) => '№ ' + d.apartmentNumber }, { label: t('debt.amount'), r: true, num: true, render: (d) => S.chip('Overdue', S.fmt.money(d.overdue, d.currency)) }, { label: t('debt.since'), render: (d) => S.fmt.date(d.since) }, { label: t('debt.days'), r: true, num: true, render: (d) => d.days }, { label: t('sch.left'), r: true, num: true, render: (d) => S.fmt.money(d.remaining, d.currency) }], r.rows, { onRow: (d) => dealDrawer(d.dealId, null, d), empty: t('debt.none') })); } catch (e) { holder.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
        }
        load();
      },

      async rate(body) {
        const holder = h('div', { class: 'stack' });
        const rate = S.input('rate', { type: 'number', step: '0.0001', min: '0' }), date = S.input('date', { type: 'date', value: S.today() });
        const btn = h('button', { class: 'btn', type: 'submit' }, t('rate.save'));
        const form = h('form', { class: 'card stack', novalidate: true, style: { maxWidth: '420px' } }, h('h3', null, t('rate.set')), S.field(t('rate.value'), rate, t('rate.hint')), S.field(t('rep.asOf'), date, t('rate.dateHint')), btn);
        form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await S.api.setRate(+rate.value, date.value); S.toast(t('rate.saved'), 'ok'); draw(); } catch (er) { S.showErrors(form, er); } }); });
        body.replaceChildren(holder, form);
        async function draw() { try { const r = await S.api.rate(); holder.replaceChildren(h('div', { class: 'card kpi', style: { maxWidth: '420px', '--k': 'var(--gold)' } }, h('span', null, t('rate.current')), h('b', null, r.rate + ' TJS'), h('div', { class: 'muted' }, t('rate.on', { d: S.fmt.date(r.date) })), h('i'))); } catch (e) { holder.replaceChildren(h('div', { class: 'card kpi' }, h('span', null, t('rate.current')), h('b', null, '—'), h('div', { class: 'muted' }, t('rate.none')))); } }
        draw();
      },

      async catalog(body) {
        body.replaceChildren(S.skeleton(4, 60));
        const bs = await S.api.buildings(); const b = await S.api.building(bs[0].id).catch(() => bs[0]); const floors = await S.api.floors(b.id);
        let floorId = (floors.find((f) => !f.commercial) || floors[0]).id; const holder = h('div');
        const nameRu = S.input('nameRu', { value: b.nameRu || b.name }), nameTg = S.input('nameTg', { value: b.nameTg }), addr = S.input('address', { value: b.address });
        const dRu = h('textarea', { class: 'inp', name: 'descriptionRu', id: 'b-dru' }); dRu.value = b.descriptionRu || b.description || ''; const dTg = h('textarea', { class: 'inp', name: 'descriptionTg', id: 'b-dtg' }); dTg.value = b.descriptionTg || '';
        const bbtn = h('button', { class: 'btn', type: 'submit' }, t('common.save'));
        const bform = h('form', { class: 'card stack', novalidate: true }, h('h3', null, t('cat.building')), h('div', { class: 'grid g2' }, S.field(t('cat.nameRu'), nameRu), S.field(t('cat.nameTg'), nameTg)), S.field(t('f.address'), addr), h('div', { class: 'grid g2' }, S.field(t('cat.descRu'), dRu), S.field(t('cat.descTg'), dTg, t('cat.tgHint'))), h('div', null, bbtn));
        bform.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(bform, null); await S.busy(bbtn, async () => { try { await S.api.saveBuilding(b.id, { nameRu: nameRu.value, nameTg: nameTg.value, descriptionRu: dRu.value, descriptionTg: dTg.value, address: addr.value }); S.toast(t('common.saved'), 'ok'); } catch (er) { S.showErrors(bform, er); } }); });
        const sel = S.select('floor', floors.map((f) => [f.id, f.commercial ? t('floor.commercial') : t('floor.n', { n: f.number })]), floorId); sel.addEventListener('change', () => { floorId = +sel.value || sel.value; drawApts(); });
        const imp = h('input', { type: 'file', id: 'imp-file', accept: '.xlsx', hidden: true });
        imp.addEventListener('change', async () => { const f = imp.files[0]; if (!f) return; try { const r = await S.api.importApartments(b.id, f); S.toast(r && r.message ? r.message : t('cat.imported'), 'ok'); drawApts(); } catch (er) { S.modal({ title: t('cat.importErr'), body: h('div', { class: 'stack' }, h('p', null, er.message), er.errors ? h('pre', { class: 'paper', style: { whiteSpace: 'pre-wrap' } }, JSON.stringify(er.errors, null, 1)) : null) }); } imp.value = ''; });
        const tools = h('div', { class: 'toolbar' }, h('div', { style: { minWidth: '200px' } }, S.field(t('cat.floor'), sel)), h('span', { class: 'spacer' }), imp, h('label', { class: 'btn ghost', for: 'imp-file' }, t('cat.import')), h('button', { class: 'btn', onclick: () => aptForm(null, floors.find((f) => String(f.id) === String(floorId)), drawApts) }, t('cat.addApt')));
        body.replaceChildren(bform, h('h3', { style: { marginTop: '10px' } }, t('cat.apts')), tools, holder);
        async function drawApts() {
          const fl = floors.find((f) => String(f.id) === String(floorId)); holder.replaceChildren(S.skeleton(4, 38));
          const list = (await S.api.floorApartments(fl)).sort((a, c) => (a.planSlot || 0) - (c.planSlot || 0));
          holder.replaceChildren(S.tbl([{ label: '№', render: (a) => h('b', null, a.number) }, { label: t('cat.slot'), render: (a) => a.planSlot }, { label: t('f.rooms'), render: (a) => S.fmt.rooms(a.rooms) }, { label: t('f.area'), r: true, num: true, render: (a) => S.fmt.area(a.area) }, { label: t('cat.pps'), r: true, num: true, render: (a) => S.fmt.n(a.pricePerSqm) }, { label: t('deal.price'), r: true, num: true, render: (a) => S.fmt.money(a.price, 'TJS') }, { label: t('deal.stage'), render: (a) => S.statusChip(a.status) }, { label: '', r: true, render: (a) => h('span', { class: 'row', style: { justifyContent: 'flex-end', gap: '6px', flexWrap: 'nowrap' } }, h('button', { class: 'btn ghost sm', onclick: () => aptForm(a, fl, drawApts) }, t('common.edit')), a.status === 'free' || a.status === 'onrequest' ? h('button', { class: 'btn soft sm', onclick: async (e) => { await S.busy(e.currentTarget, async () => { try { await S.api.onRequest(a.id, a.status === 'free'); drawApts(); } catch (er) { S.err(er); } }); } }, a.status === 'free' ? t('cat.toRequest') : t('cat.toFree')) : null) }], list, { empty: t('cat.noApts') }));
        }
        drawApts();
      },

      async construction(body) {
        const holder = h('div', { class: 'stack' }); const bs = await S.api.buildings(); const bid = bs[0].id;
        const load = async () => {
          holder.replaceChildren(S.skeleton(4, 60)); const st = await S.api.stages(bid);
          holder.replaceChildren(S.tbl([{ label: t('con.stage'), render: (s) => h('b', null, s.title) }, { label: t('con.progress'), render: (s) => h('div', { style: { minWidth: '160px' } }, S.bar(s.percent)) }, { label: '%', r: true, num: true, render: (s) => s.percent + '%' }, { label: t('doc.date'), render: (s) => S.fmt.date(s.date) }, { label: t('con.photos'), r: true, num: true, render: (s) => s.photos.length }, { label: '', r: true, render: (s) => h('button', { class: 'btn ghost sm', onclick: () => stageForm(s, bid, load) }, t('common.edit')) }], st, { empty: t('con.none') }));
        };
        body.replaceChildren(h('div', { class: 'toolbar' }, h('span', { class: 'spacer' }), h('button', { class: 'btn', onclick: () => stageForm(null, bid, load) }, t('con.add'))), holder); load();
      },

      async users(body) {
        const holder = h('div');
        const load = async () => { holder.replaceChildren(S.skeleton(4, 38)); const l = await S.api.users(); holder.replaceChildren(S.tbl([{ label: t('f.fullName'), render: (u) => h('b', null, u.name) }, { label: t('login.email'), render: (u) => u.email }, { label: t('users.role'), render: (u) => u.roles.map((r) => t('role.' + r)).join(', ') }, { label: t('deal.stage'), render: (u) => S.chip(u.blocked ? 'Blocked' : 'Active ok', t(u.blocked ? 'users.blocked' : 'users.active')) }, { label: '', r: true, render: (u) => (u.id === (S.api.user && S.api.user.id) ? '' : h('button', { class: 'btn ghost sm', onclick: async (e) => { await S.busy(e.currentTarget, async () => { try { await S.api.setUserBlocked(u.id, !u.blocked); S.toast(t('common.saved'), 'ok'); load(); } catch (er) { S.err(er); } }); } }, u.blocked ? t('users.unblock') : t('users.block'))) }], l)); };
        body.replaceChildren(h('div', { class: 'toolbar' }, h('span', { class: 'spacer' }), h('button', { class: 'btn', onclick: () => userForm(load) }, t('users.new'))), holder); load();
      },

      async audit(body) {
        // Entity names as the server writes them (class names); the demo calls users "User".
        const userEntity = S.isDemo() ? 'User' : 'AppUser';
        const holder = h('div'); const sel = S.select('entity', [['', t('filter.all')], ['Deal', 'Deal'], ['Payment', 'Payment'], ['Apartment', 'Apartment'], ['Booking', 'Booking'], ['Lead', 'Lead'], [userEntity, 'User']], '');
        const load = async () => { holder.replaceChildren(S.skeleton(6, 34)); const l = await S.api.audit(sel.value); holder.replaceChildren(S.tbl([{ label: t('audit.when'), render: (a) => S.fmt.dt(a.at) }, { label: t('audit.who'), render: (a) => a.user }, { label: t('audit.action'), render: (a) => h('b', null, a.action) }, { label: t('audit.entity'), render: (a) => a.entity + ' #' + shortId(a.entityId) }, { label: t('audit.change'), render: (a) => h('code', { style: { fontSize: '12px', color: 'var(--ink-2)' } }, [a.before ? JSON.stringify(a.before) : '', a.before && a.after ? ' → ' : '', a.after ? JSON.stringify(a.after) : ''].join('')) }], l, { empty: t('audit.none') })); };
        sel.addEventListener('change', load); body.replaceChildren(h('div', { class: 'toolbar' }, h('div', { style: { minWidth: '200px' } }, S.field(t('audit.entity'), sel))), holder); load();
      },
    };
    S.views.crm._views = views;

    /* ---------- modals & drawers ---------- */
    function leadModal(id, done) {
      const holder = h('div', null, S.skeleton(3));
      const md = S.modal({ title: t('lead.title2'), body: holder });
      (async () => {
        try {
          const l = (await S.api.leads()).find((x) => String(x.id) === String(id)); if (!l) { holder.replaceChildren(h('div', { class: 'form-err' }, t('lead.gone'))); return; }
          const status = S.select('status', ['New', 'InProgress', 'Closed'].map((s) => [s, t('lead.st.' + s)]), l.status); const cm = h('textarea', { class: 'inp', name: 'comment', id: 'lead-cm' }); cm.value = l.comment || '';
          const form = h('form', { class: 'stack', novalidate: true }, h('dl', { class: 'kv' }, h('dt', null, t('f.name')), h('dd', null, l.name), h('dt', null, t('f.phone')), h('dd', null, l.phone), h('dt', null, t('lead.date')), h('dd', null, S.fmt.dt(l.createdAt))), S.field(t('deal.stage'), status), S.field(t('f.comment'), cm));
          const save = h('button', { class: 'btn', type: 'submit' }, t('common.save'));
          form.addEventListener('submit', async (e) => { e.preventDefault(); await S.busy(save, async () => { try { await S.api.updateLead(l.id, { status: status.value, comment: cm.value, assignee: S.api.user.name }); S.toast(t('common.saved'), 'ok'); md.close(); done && done(); } catch (er) { S.showErrors(form, er); } }); });
          holder.replaceChildren(form, h('div', { class: 'actions' }, h('button', { class: 'btn ghost', type: 'button', onclick: () => { md.close(); customerForm({ fullName: l.name, phone: l.phone, leadId: l.id }, () => done && done()); } }, t('lead.makeCustomer')), save));
        } catch (e) { holder.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
      })();
    }

    function bookingModal(it, done) {
      const reasonBtn = async () => { const r = await S.askReason(t('book.cancelTitle'), t('book.cancelReason')); if (r === null) return; try { await S.api.cancelBooking(it.id, r); S.toast(t('common.saved'), 'ok'); md.close(); done && done(); } catch (e) { S.err(e); } };
      const md = S.modal({ title: t('book.cardTitle'), body: h('div', { class: 'stack' }, h('dl', { class: 'kv' }, h('dt', null, t('deal.apt')), h('dd', null, it.title), h('dt', null, t('f.phone')), h('dd', null, it.subtitle || it.phone || '—'), h('dt', null, t('book.left')), h('dd', null, it.expiresAt ? S.cd(it.expiresAt) : '—'))),
        actions: [h('button', { class: 'btn danger', onclick: reasonBtn }, t('book.cancel')), h('button', { class: 'btn ghost', onclick: async (e) => { await S.busy(e.currentTarget, async () => { try { await S.api.extendBooking(it.id); S.toast(t('book.extended'), 'ok'); md.close(); done && done(); } catch (er) { S.err(er); } }); } }, t('book.extend')), h('button', { class: 'btn', onclick: () => { md.close(); newDealModal(done, it); } }, t('deal.fromBooking'))] });
    }

    function customerForm(pre, done) {
      pre = pre || {};
      const name = S.input('fullName', { value: pre.fullName || '', autocomplete: 'off' }), phone = S.input('phone', { type: 'tel', value: pre.phone || '+992' }), email = S.input('email', { type: 'email' }), pass = S.input('passport');
      const form = h('form', { class: 'stack', id: 'custform', novalidate: true }, S.field(t('f.fullName'), name), S.field(t('f.phone'), phone, t('f.phoneHint')), S.field(t('f.email'), email), S.field(t('f.passport'), pass, t('f.passportHint')));
      const btn = h('button', { class: 'btn', type: 'submit', form: 'custform' }, t('common.save'));
      const md = S.modal({ title: t('cust.new'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await S.api.createCustomer({ fullName: name.value.trim(), phone: phone.value.replace(/[\s()-]/g, ''), email: email.value.trim(), passport: pass.value.trim(), leadId: pre.leadId }); S.toast(t('common.saved'), 'ok'); md.close(); done && done(); } catch (er) { S.showErrors(form, er); } }); });
    }

    async function customerModal(id) {
      const holder = h('div', null, S.skeleton(3)); S.modal({ title: t('cust.card'), body: holder, wide: false });
      try {
        const c = await S.api.customer(id);
        holder.replaceChildren(h('dl', { class: 'kv' }, h('dt', null, t('f.fullName')), h('dd', null, c.fullName), h('dt', null, t('f.phone')), h('dd', null, c.phone), h('dt', null, t('f.email')), h('dd', null, c.email || '—'), h('dt', null, t('f.passport')), h('dd', null, c.passport || '—')),
          h('h3', { style: { margin: '16px 0 8px' } }, t('crm.deals')), S.tbl([{ label: t('deal.apt'), render: (d) => '№ ' + d.apartmentNumber }, { label: t('deal.stage'), render: (d) => S.stageChip(d.stage) }, { label: t('deal.price'), r: true, num: true, render: (d) => S.fmt.money(d.price - d.discount, d.currency) }], c.deals, { onRow: (d) => dealDrawer(d.id, null), empty: t('deal.none') }));
      } catch (e) { holder.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
    }

    async function newDealModal(done, booking) {
      const holder = h('div', null, S.skeleton(3)); const md = S.modal({ title: t('deal.new'), body: holder });
      try {
        const [customers, bs] = await Promise.all([S.api.customers(''), S.api.buildings()]);
        const floors = await S.api.floors(bs[0].id); const apts = (await S.api.allApartments(floors)).filter((a) => a.status === 'free' || (booking && String(a.id) === String(booking.apartmentId)));
        const cust = S.select('customerId', [['', t('deal.pickCustomer')]].concat(customers.map((c) => [c.id, c.fullName + ' · ' + c.phone])), '');
        const apt = S.select('apartmentId', [['', t('deal.pickApt')]].concat(apts.sort((a, b) => a.number - b.number).map((a) => [a.id, '№ ' + a.number + ' · ' + t('floor.n', { n: a.floor }) + ' · ' + S.fmt.rooms(a.rooms) + ' · ' + S.fmt.money(a.price, 'TJS')])), booking && booking.apartmentId ? booking.apartmentId : '');
        const cur = S.select('currency', [['TJS', 'TJS'], ['USD', 'USD']], 'TJS'); const disc = S.input('discount', { type: 'number', min: 0, value: 0 }); const rs = S.input('discountReason', { placeholder: t('deal.reasonPh') });
        const form = h('form', { class: 'stack', id: 'dealform', novalidate: true }, S.field(t('deal.customer'), cust, null), S.field(t('deal.apt'), apt), h('div', { class: 'grid g2' }, S.field(t('deal.currency'), cur, t('deal.currencyHint')), S.field(t('deal.discount'), disc)), S.field(t('deal.discountReason'), rs));
        const btn = h('button', { class: 'btn', type: 'submit', form: 'dealform' }, t('deal.create'));
        holder.replaceChildren(form, h('div', { class: 'actions' }, h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), h('button', { class: 'btn ghost', type: 'button', onclick: () => customerForm(null, () => { md.close(); newDealModal(done, booking); }) }, t('cust.new')), btn));
        form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { const d = await S.api.createDeal({ customerId: cust.value || undefined, apartmentId: apt.value || undefined, currency: cur.value, discount: +disc.value || 0, discountReason: rs.value.trim() }); S.toast(t('deal.created'), 'ok'); md.close(); done && done(); dealDrawer(d.id, done); } catch (er) { S.showErrors(form, er); } }); });
      } catch (e) { holder.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
    }

    function aptForm(a, floor, done) {
      const f = (n, o) => S.input(n, o);
      const num = f('number', { type: 'number', value: a ? a.number : '' }), rooms = f('rooms', { type: 'number', min: 0, max: 6, value: a ? a.rooms : 1 }), area = f('area', { type: 'number', step: '0.1', value: a ? a.area : '' }), pps = f('pricePerSqm', { type: 'number', value: a ? a.pricePerSqm : '' }), slot = f('planSlot', { type: 'number', min: 0, max: 7, value: a && a.planSlot !== null ? a.planSlot : '' }), m3 = f('model3dId', { value: a && a.model3dId ? a.model3dId : '' });
      const form = h('form', { class: 'stack', id: 'aptform', novalidate: true }, h('div', { class: 'grid g2' }, S.field(t('cat.number'), num), S.field(t('f.rooms'), rooms, t('cat.studioHint'))), h('div', { class: 'grid g2' }, S.field(t('f.area') + ' (' + t('u.m2') + ')', area), S.field(t('cat.pps') + ' (TJS)', pps)), h('div', { class: 'grid g2' }, S.field(t('cat.slot'), slot, t('cat.slotHint')), S.field(t('cat.model3d'), m3, t('cat.model3dHint'))));
      const btn = h('button', { class: 'btn', type: 'submit', form: 'aptform' }, t('common.save'));
      const md = S.modal({ title: a ? t('cat.editApt', { n: a.number }) : t('cat.addApt'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { const body = { number: +num.value, rooms: +rooms.value, area: +area.value, pricePerSqm: +pps.value, planSlot: slot.value === '' ? undefined : +slot.value, model3dId: m3.value || null }; if (a) await S.api.updateApartment(a.id, body); else await S.api.createApartment(Object.assign(body, { floorId: floor.id })); S.toast(t('common.saved'), 'ok'); md.close(); done && done(); } catch (er) { S.showErrors(form, er); } }); });
    }

    function stageForm(s, bid, done) {
      const title = S.input('title', { value: s ? s.title : '' }); const pct = h('input', { class: 'range', type: 'range', name: 'percent', id: 'st-pct', min: 0, max: 100, step: 5, value: s ? s.percent : 0 }); const pv = h('b', { class: 'num' }, (s ? s.percent : 0) + '%'); pct.addEventListener('input', () => { pv.textContent = pct.value + '%'; });
      const ds = h('textarea', { class: 'inp', name: 'description', id: 'st-ds' }); ds.value = s ? s.description : '';
      const file = h('input', { type: 'file', id: 'st-photo', accept: 'image/png,image/jpeg' });
      const form = h('form', { class: 'stack', id: 'stform', novalidate: true }, S.field(t('con.stage'), title), h('div', { class: 'field' }, h('label', { for: 'st-pct' }, t('con.progress')), pct, pv), S.field(t('f.comment'), ds), s ? S.field(t('con.addPhoto'), file, t('con.photoHint')) : null);
      const btn = h('button', { class: 'btn', type: 'submit', form: 'stform' }, t('common.save'));
      const md = S.modal({ title: s ? s.title : t('con.add'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try {
        if (s) { await S.api.updateStage(s.id, { title: title.value, percent: +pct.value, description: ds.value, order: s.order }); const f = file.files[0]; if (f) { f.dataUrl = await new Promise((res) => { const r = new FileReader(); r.onload = () => res(r.result); r.readAsDataURL(f); }); await S.api.uploadPhoto(s.id, f); } } else await S.api.createStage(bid, { title: title.value, percent: +pct.value, description: ds.value });
        S.toast(t('common.saved'), 'ok'); md.close(); done && done(); } catch (er) { S.showErrors(form, er); } }); });
    }

    function userForm(done) {
      const name = S.input('name'), email = S.input('email', { type: 'email' }), pass = S.input('password', { type: 'password', autocomplete: 'new-password' }), role = S.select('role', ['Manager', 'Accountant', 'Content', 'Admin'].map((r) => [r, t('role.' + r)]), 'Manager');
      const form = h('form', { class: 'stack', id: 'userform', novalidate: true }, S.field(t('f.fullName'), name), S.field(t('login.email'), email), S.field(t('login.password'), pass, t('users.passHint')), S.field(t('users.role'), role));
      const btn = h('button', { class: 'btn', type: 'submit', form: 'userform' }, t('common.save'));
      const md = S.modal({ title: t('users.new'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await S.api.createUser({ name: name.value.trim(), fullName: name.value.trim(), email: email.value.trim(), password: pass.value, role: role.value, roles: [role.value] }); S.toast(t('common.saved'), 'ok'); md.close(); done && done(); } catch (er) { S.showErrors(form, er); } }); });
    }

    /* ---------- deal drawer ---------- */
    // hint — a report row with the client and apartment: an accountant cannot open the deal card itself.
    function dealDrawer(id, done, hint) {
      let d = null, sched = null; let tab = 'overview';
      const body = h('div', null, S.skeleton(4, 60));
      const md = S.modal({ side: true, body, onClose: () => done && done(), noFocus: true });
      (async function init() {
        try { d = await S.api.deal(id, hint); } catch (e) { body.replaceChildren(h('div', { class: 'form-err' }, e.message)); return; }
        draw();
      })();
      async function reload() { d = await S.api.deal(id, hint); sched = null; draw(); }
      async function getSched() { if (sched) return sched; try { sched = await S.api.schedule(id); } catch (e) { sched = { none: true }; } return sched; }
      const tabsDef = () => [['overview', t('deal.tab.overview')], ['schedule', t('deal.tab.schedule')], ['payments', t('deal.tab.payments')], ['docs', t('deal.tab.docs')], ['chat', t('deal.tab.chat')]];
      function draw() {
        const content = h('div', { class: 'stack' });
        body.replaceChildren(h('div', { class: 'row between', style: { paddingRight: '40px', marginBottom: '14px' } }, h('div', null, h('h2', { style: { margin: 0, padding: 0 } }, t('deal.title', { n: d.apartmentNumber })), h('div', { class: 'muted', style: { fontWeight: 600 } }, (d.customerName || '—') + ' · #' + shortId(d.id))), d.readOnly ? null : S.stageChip(d.stage)),
          h('div', { class: 'tabs' }, tabsDef().map(([k, l]) => h('button', { class: tab === k ? 'on' : '', onclick: () => { tab = k; draw(); } }, l))), content);
        panes[tab](content);
      }
      const panes = {
        overview(c) {
          const stageSel = S.select('stage', STAGES.map((s) => [s, t('stage.' + s)]), d.stage); const reason = S.input('reason', { placeholder: t('deal.reasonPh') });
          const canMove = S.api.has('Manager') && d.stage !== 'Completed' && d.stage !== 'Cancelled' && S.api.user.roles.some((r) => r === 'Manager' || r === 'Admin');
          const btn = h('button', { class: 'btn', type: 'submit' }, t('deal.move'));
          const sf = h('form', { class: 'card flat stack', novalidate: true }, h('h3', null, t('deal.changeStage')), h('div', { class: 'grid g2' }, S.field(t('deal.stage'), stageSel), S.field(t('deal.reason'), reason, t('deal.reasonHint'))), btn);
          sf.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(sf, null); if (stageSel.value === d.stage) { S.toast(t('deal.sameStage')); return; }
            const idx = STAGES.indexOf(stageSel.value) < STAGES.indexOf(d.stage) || stageSel.value === 'Cancelled';
            if (idx && !reason.value.trim()) { S.showErrors(sf, { message: t('val.reason'), errors: { reason: [t('val.reason')] } }); return; }
            if (stageSel.value === 'Cancelled' && !(await S.confirm(t('deal.cancelConfirm'), t('deal.cancelDo'), true))) return;
            await S.busy(btn, async () => { try { await S.api.setStage(d.id, stageSel.value, reason.value.trim()); S.toast(t('common.saved'), 'ok'); await reload(); } catch (er) { S.showErrors(sf, er); } }); });
          c.append(h('div', { class: 'card flat' }, h('dl', { class: 'kv' }, kv(t('deal.customer'), d.customerName), kv(t('f.phone'), d.customerPhone), kv(t('deal.apt'), '№ ' + d.apartmentNumber + ' · ' + t('floor.n', { n: d.floor })), kv(t('f.rooms'), S.fmt.rooms(d.rooms)), kv(t('f.area'), S.fmt.area(d.area)), kv(t('deal.price'), S.fmt.money(d.price, d.currency)), d.discount ? kv(t('deal.discount'), '− ' + S.fmt.money(d.discount, d.currency) + (d.discountReason ? ' (' + d.discountReason + ')' : '')) : null, kv(t('deal.manager'), d.managerName), kv(t('sch.paid'), S.fmt.money(d.paid, d.currency)), kv(t('sch.left'), S.fmt.money(d.remaining, d.currency)), kv(t('deal.since'), S.fmt.date(d.createdAt)))), canMove ? sf : null);
        },
        async schedule(c) {
          c.replaceChildren(S.skeleton(3, 50)); const s = await getSched();
          if (!s.none) { c.replaceChildren(S.panels.schedule(s), h('div', { class: 'row' }, h('button', { class: 'btn ghost', onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/deals/' + id + '/schedule.pdf', null, 'PDF').catch(S.err)) }, t('cab.pdfSchedule')), h('button', { class: 'btn ghost', onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/deals/' + id + '/statement.pdf', { date: S.today() }, 'PDF').catch(S.err)) }, t('cab.pdfAct')), s.paid === 0 && S.api.user.roles.some((r) => r === 'Manager' || r === 'Admin') ? h('button', { class: 'btn soft', onclick: () => planForm(true) }, t('plan.rebuild')) : null)); return; }
          c.replaceChildren(S.api.user.roles.some((r) => r === 'Manager' || r === 'Admin') ? planFormEl() : S.empty(t('plan.none'), 'wallet'));
        },
        async payments(c) {
          c.replaceChildren(S.skeleton(3, 40));
          const canPay = S.api.user.roles.some((r) => r === 'Accountant' || r === 'Admin');
          const parts = [];
          if (canPay) {
            const sc = await getSched();
            if (sc.none) { c.replaceChildren(S.empty(t('pay.needPlan'), 'wallet')); return; }
            const amount = S.input('amount', { type: 'number', min: 1, step: '0.01' }), cur = S.select('currency', [['TJS', 'TJS'], ['USD', 'USD']], d.currency), date = S.input('date', { type: 'date', value: S.today(), max: S.today() }), cm = S.input('comment');
            const btn = h('button', { class: 'btn', type: 'submit' }, t('pay.add'));
            const f = h('form', { class: 'card flat stack', novalidate: true }, h('h3', null, t('pay.new')), h('div', { class: 'grid g2' }, S.field(t('pay.amount'), amount), S.field(t('deal.currency'), cur, d.currency === cur.value ? null : t('pay.convHint'))), h('div', { class: 'grid g2' }, S.field(t('doc.date'), date), S.field(t('pay.comment'), cm)), btn);
            f.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(f, null); await S.busy(btn, async () => { try { await S.api.addPayment(id, { amount: +amount.value, currency: cur.value, date: date.value, comment: cm.value.trim() }); S.toast(t('pay.added'), 'ok'); await reload(); tab = 'payments'; draw(); } catch (er) { S.showErrors(f, er); } }); });
            parts.push(f);
          }
          let list = null;
          try { list = canPay ? await S.api.payments(id) : null; } catch (e) { list = null; }
          if (list) parts.push(S.tbl([{ label: t('doc.date'), render: (p) => S.fmt.date(p.date) }, { label: t('pay.amount'), r: true, num: true, render: (p) => (p.reversal ? '− ' : '') + S.fmt.money(p.amount, p.currency) }, { label: t('pay.inDeal'), r: true, num: true, render: (p) => (p.reversal ? '− ' : '') + S.fmt.money(p.amountDeal, d.currency) }, { label: t('pay.rate'), r: true, num: true, render: (p) => p.rate || '—' }, { label: t('pay.comment'), render: (p) => p.comment }, { label: '', r: true, render: (p) => (!p.reversal && !list.some((x) => String(x.reversedId) === String(p.id)) ? h('button', { class: 'btn danger sm', onclick: async () => { const r = await S.askReason(t('pay.reverseTitle'), t('pay.reverseReason')); if (r === null) return; try { await S.api.reversePayment(p.id, r); S.toast(t('pay.reversed'), 'ok'); await reload(); tab = 'payments'; draw(); } catch (er) { S.err(er); } } }, t('pay.reverse')) : '') }], list, { rowClass: (p) => (p.reversal ? 'rev' : ''), empty: t('pay.none') }));
          else parts.push(h('p', { class: 'muted' }, t('pay.readOnly')), h('div', { class: 'row' }, h('button', { class: 'btn ghost', onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/deals/' + id + '/statement.pdf', { date: S.today() }, 'PDF').catch(S.err)) }, t('cab.pdfAct'))));
          c.replaceChildren(...parts);
        },
        docs(c) { c.append(S.panels.documents(id, true)); },
        chat(c) { c.append(h('div', { class: 'card flat' }, S.panels.chat(id, 'staff'))); },
      };
      const kv = (k, v) => [h('dt', null, k), h('dd', null, v === undefined || v === null || v === '' ? '—' : v)];
      function planFormEl() {
        const down = S.input('downPaymentPercent', { type: 'number', min: 0, max: 90, value: 30 }), months = S.input('months', { type: 'number', min: 1, max: 360, value: 60 }), day = S.input('paymentDay', { type: 'number', min: 1, max: 28, value: 10 });
        const preview = h('div', { class: 'next' }); const btn = h('button', { class: 'btn', type: 'submit' }, t('plan.build'));
        const net = d.price - d.discount; const upd = () => { const dw = Math.round((net * (+down.value || 0)) / 100); const m = Math.round((net - dw) / (+months.value || 1)); preview.replaceChildren(h('span', null, t('plan.preview')), h('b', { class: 'num' }, S.fmt.money(dw, d.currency) + ' + ' + (+months.value || 0) + ' × ' + S.fmt.money(m, d.currency))); };
        [down, months].forEach((el) => el.addEventListener('input', upd)); upd();
        const f = h('form', { class: 'card flat stack', novalidate: true }, h('h3', null, t('plan.title')), h('div', { class: 'grid g3' }, S.field(t('plan.down'), down), S.field(t('plan.months'), months), S.field(t('plan.day'), day, t('plan.dayHint'))), preview, btn);
        f.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(f, null); await S.busy(btn, async () => { try { await S.api.createPlan(id, { downPaymentPercent: +down.value, months: +months.value, paymentDay: +day.value }); S.toast(t('plan.created'), 'ok'); sched = null; tab = 'schedule'; draw(); } catch (er) { S.showErrors(f, er); } }); });
        return f;
      }
      function planForm() { const c = S.$('.stack', body); c.replaceChildren(planFormEl()); }
    }

    return () => {};
  };
})();
