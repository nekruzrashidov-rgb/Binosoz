/* Management workspace: owner, building admin and the positions inside an organization (manager, accountant, content). */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  S.views = S.views || {};
  const STAGES = ['Lead', 'Showing', 'Booking', 'Contract', 'Payment', 'Completed', 'Cancelled'];
  const has = (...r) => S.api.has(...r);
  // Sales actions (create deals, move stages, build plans) and money actions; leaders can do both.
  const sales = () => has('Manager');
  const money = () => has('Accountant');
  const STAFF_POSITIONS = ['ObjectAdmin', 'Manager', 'Accountant', 'Content'];

  // Ids are GUIDs in the real API and numbers in the demo: show a short readable part.
  // The API issues UUIDv7 — they start with a timestamp, so the tail is what tells two ids apart.
  const shortId = (id) => (typeof id === 'string' && id.length > 12 ? id.replace(/-/g, '').slice(-8).toUpperCase() : id);
  function toObj(x) { if (!x) return {}; if (Array.isArray(x)) { const o = {}; x.forEach((i) => { o[S.pick(i, ['stage', 'status', 'name', 'key'], '')] = S.pick(i, ['count', 'value', 'total'], 0); }); return o; } const o = {}; Object.keys(x).forEach((k) => { o[k.charAt(0).toUpperCase() + k.slice(1)] = x[k]; }); return o; }

  S.views.crm = function (root) {
    if (!S.api.isAuthed) return S.views.login(root);
    if (!S.api.isStaff()) { root.appendChild(h('div', { class: 'wrap page-in' }, h('div', { class: 'card login-card login' }, h('h2', null, t('crm.notStaff')), h('div', { class: 'row', style: { marginTop: '14px' } }, h('button', { class: 'btn', onclick: () => S.go(S.homeFor(S.api.user)) }, S.api.isPlatform() ? '← ' + t('nav.platform') : t('cab.toCrm')))))); return () => {}; }
    // A new owner without buildings gets the setup wizard instead of an empty workspace — once per organization.
    const wizardKey = 'binosoz.wizard.done.' + S.api.user.organizationId;
    if (S.api.isOwner() && !S.store.get(wizardKey)) {
      const box = h('div', { class: 'wrap page-in' }, S.skeleton(3, 60)); root.appendChild(box);
      const finish = () => { S.store.set(wizardKey, '1'); S.clear(root); S.views.crm(root); };
      S.api.buildings().then((bs) => (bs.length ? finish() : wizard(box, finish)), finish);
      return () => {};
    }
    const page = h('div', { class: 'page-in' }); root.appendChild(page);
    // Which developer and which buildings this workspace is about.
    const ctx = h('div', { class: 'wrap ctx-line' });
    page.appendChild(ctx);
    S.api.buildings().then((bs) => { S.crmBuildings = bs; const u = S.api.user; ctx.replaceChildren(h('b', null, u.organizationName || ''), h('span', { class: 'muted' }, ' · ' + t('role.' + u.role, null, u.role) + ' · ' + (u.buildingIds.length ? bs.filter((b) => u.buildingIds.includes(b.id)).map((b) => b.name).join(', ') : t('crm.allBuildings')))); }).catch(() => {});
    const items = [];
    const add = (key, label, icon, group, ok) => { if (ok) items.push({ key, label, icon, group }); };
    add('dashboard', t('crm.dashboard'), 'chart', t('crm.g.sales'), has('Manager', 'Accountant'));
    add('pipeline', t('crm.pipeline'), 'kanban', t('crm.g.sales'), has('Manager'));
    add('leads', t('crm.leads'), 'inbox', t('crm.g.sales'), has('Manager'));
    // An accountant reads deals too (to post the first payment); creating deals and moving stages stays with sales.
    add('deals', t('crm.deals'), 'file', t('crm.g.sales'), has('Manager', 'Accountant'));
    add('customers', t('crm.customers'), 'users', t('crm.g.sales'), has('Manager'));
    add('templates', t('tpl.title'), 'send', t('crm.g.sales'), has('Manager') && !S.isDemo());
    add('chats', t('chats.title'), 'send', t('crm.g.sales'), has('Manager', 'Accountant'));
    add('payments', t('crm.payments'), 'wallet', t('crm.g.finance'), has('Accountant'));
    add('debts', t('crm.debts'), 'shield', t('crm.g.finance'), has('Accountant'));
    add('cash', t('cash.title'), 'wallet', t('crm.g.finance'), has('Accountant') && !S.isDemo());
    add('rate', t('crm.rate'), 'chart', t('crm.g.finance'), has('Accountant'));
    // The developer's view of a building: construction, sales and money on one card (owner and building admin).
    add('project', t('proj.title'), 'building', t('crm.g.catalog'), S.api.isLeader() && !S.isDemo());
    // Leaders manage buildings; a manager uses the apartment lists to book and sell.
    add('objects', t('obj.title'), 'home', t('crm.g.catalog'), S.api.isLeader() || has('Manager'));
    // Floor plans: leaders and managers book from the drawing (the demo backend has no plans).
    add('plan', t('fplan.title'), 'grid', t('crm.g.catalog'), (S.api.isLeader() || has('Manager')) && !S.isDemo());
    add('catalog', t('crm.catalog'), 'building', t('crm.g.catalog'), has('Content'));
    add('construction', t('crm.construction'), 'hammer', t('crm.g.catalog'), has('Content'));
    add('users', t('crm.users'), 'users', t('crm.g.system'), S.api.isLeader());
    add('audit', t('crm.audit'), 'shield', t('crm.g.system'), S.api.isOwner());
    add('settings', t('set.title'), 'gear', t('crm.g.system'), S.api.isOwner());
    // Emoji of the top tab bar.
    const EMOJI = { dashboard: '📊', pipeline: '🧭', leads: '👥', deals: '📋', customers: '👤', templates: '✉️', chats: '💬', payments: '💰', debts: '⏰', cash: '🏦', rate: '💱',
      project: '🏗️', objects: '🏠', plan: '🗺️', catalog: '🏢', construction: '🧱', users: '🧑‍💼', audit: '🛡️', settings: '⚙️' };
    items.forEach((it) => { it.emoji = EMOJI[it.key]; });
    const user = S.api.user;
    const ws = S.workspace(items, items[0].key, (k, main) => { S.clear(main); main.append(h('h1', null, items.find((i) => i.key === k).label), h('p', { class: 'sub' }, t('crm.sub.' + k))); const body = h('div', { class: 'stack' }); main.appendChild(body); try { views[k](body); } catch (e) { body.appendChild(h('div', { class: 'form-err' }, e.message)); console.error(e); } }, { topTabs: true });
    page.appendChild(ws.el);
    // The section from the address (/crm#leads) or the first one.
    queueMicrotask(() => ws.go(ws.current));
    const openTab = (k) => ws.go(k);
    S.crmGo = openTab;
    // Unread messages from buyers across all conversations, on the section's label.
    const chatsItem = items.find((i) => i.key === 'chats');
    S.crmChatBadge = () => { if (chatsItem) S.api.chats().then((l) => { chatsItem.badge = l.reduce((s, c) => s + (c.unread || 0), 0) || null; ws.redraw(); }).catch(() => {}); };
    S.crmChatBadge();

    /* ---------- views ---------- */
    const views = {
      async dashboard(body) {
        body.replaceChildren(S.skeleton(4, 80));
        // Statistics for everyone who sees the overview; the sales funnel below only for sales (an accountant has no access to it).
        const [st, salesRows] = await Promise.all([S.api.stats(), S.api.statsSales().catch(() => [])]);
        const statsPart = statsBlock(st, salesRows);
        if (!has('Manager')) { body.replaceChildren(...statsPart); return; }
        const [sumRaw, rate] = await Promise.all([S.api.summary(), S.api.rate().catch(() => null)]);
        const r = sumRaw || {};
        const stages = toObj(S.pick(r, ['dealsByStage', 'stages', 'deals'], {}));
        const k = (key, def) => S.pick(r, key, def);
        const kp = [[t('dash.leads'), k(['openLeads', 'leads', 'leadsCount', 'newLeads'], 0), 'var(--info)'], [t('dash.bookings'), k(['activeBookings', 'bookings'], 0), 'var(--warn)'], [t('dash.expiring'), k(['expiringBookings', 'expiring', 'expiringSoon'], 0), 'var(--bad)']];
        const total = Object.values(stages).reduce((s, x) => s + (+x || 0), 0) || 1;
        body.replaceChildren(...statsPart,
          h('h3', { class: 'dash-section' }, t('stats.funnel')),
          h('div', { class: 'kpis' }, kp.map(([l, v, c]) => h('div', { class: 'card kpi', style: { '--k': c } }, h('span', null, l), h('b', null, v), h('i')))),
          h('div', { class: 'card' }, h('h3', null, t('dash.byStage')), STAGES.map((s) => { const n = +stages[s] || 0; return h('div', { class: 'hbar' }, S.stageChip(s), S.bar((n / total) * 100), h('b', { class: 'num', style: { textAlign: 'right' } }, n)); }),
            rate && rate.rate ? h('p', { class: 'hint', style: { marginTop: '14px' } }, t('dash.rate', { r: rate.rate, d: S.fmt.date(rate.date) })) : h('p', { class: 'hint', style: { marginTop: '14px' } }, t('dash.noRate')),
            r.salesNote === 'usdWithoutRate' ? h('p', { class: 'hint' }, t('dash.usdNoRate')) : null),
          S.isDemo() ? null : salesCharts(salesRows),
          // «Сэкономлено бумаги»: pages of the PDFs issued online and uploaded, by month (GET /stats/green).
          S.eco && !S.isDemo() ? S.eco.paperCard() : null);
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
        // Call or write straight from the list; the click on the row itself still opens the lead.
        const digits = (p) => String(p || '').replace(/\D/g, '');
        const contact = (l) => (digits(l.phone) ? h('span', { class: 'row', style: { gap: '6px', flexWrap: 'nowrap', justifyContent: 'flex-end' } },
          h('a', { class: 'btn soft sm', href: 'tel:+' + digits(l.phone), onclick: (e) => e.stopPropagation() }, t('lead.call')),
          h('a', { class: 'btn ghost sm', href: 'https://t.me/+' + digits(l.phone), target: '_blank', rel: 'noopener', onclick: (e) => e.stopPropagation() }, t('lead.write'))) : '');
        let list = []; const draw = () => holder.replaceChildren(S.tbl([{ label: t('lead.name'), render: (l) => h('b', null, l.name) }, { label: t('f.phone'), render: (l) => l.phone }, { label: t('lead.message'), render: (l) => (l.comment ? h('span', { class: 'lead-msg', title: l.comment }, l.comment) : '') }, { label: t('lead.date'), render: (l) => S.fmt.dt(l.createdAt) }, { label: t('lead.source'), render: (l) => t('lead.src.' + l.source, null, l.source) }, { label: t('lead.assignee'), render: (l) => l.assignee }, { label: t('deal.stage'), render: (l) => S.chip(l.status, t('lead.st.' + l.status, null, l.status)) }, { label: '', r: true, render: contact }], list.filter((l) => filter === 'all' || l.status === filter), { onRow: (l) => leadModal(l.id, load), empty: t('lead.none') }));
        const load = async () => { holder.replaceChildren(S.skeleton(5, 38)); list = await S.api.leads(); draw(); }; load();
      },

      async deals(body) {
        let stage = 'all', q = ''; const holder = h('div'); let list = [];
        const search = S.input('q', { type: 'search', placeholder: t('common.search'), 'aria-label': t('common.search') }); search.addEventListener('input', S.debounce(() => { q = search.value.toLowerCase(); draw(); }, 150));
        const bar = h('div', { class: 'toolbar' }, search, h('div', { class: 'pill-group' }, ['all', ...STAGES].map((s) => h('button', { class: 'pill' + (stage === s ? ' on' : ''), onclick: (e) => { stage = s; S.$$('.pill', bar).forEach((p) => p.classList.remove('on')); e.currentTarget.classList.add('on'); draw(); } }, s === 'all' ? t('filter.all') : t('stage.' + s)))), h('span', { class: 'spacer' }), sales() ? h('button', { class: 'btn', onclick: () => newDealModal(load) }, S.icon('plus'), t('deal.new')) : null);
        S.$$('.btn svg', bar).forEach((s) => { s.style.cssText = 'width:16px;height:16px;stroke:currentColor;fill:none;stroke-width:2'; });
        body.replaceChildren(bar, holder);
        const draw = () => holder.replaceChildren(S.tbl([
          { label: '#', render: (d) => shortId(d.id) }, { label: t('deal.apt'), render: (d) => h('b', null, '№ ' + d.apartmentNumber) }, { label: t('deal.customer'), render: (d) => d.customerName }, { label: t('deal.stage'), render: (d) => S.stageChip(d.stage) },
          { label: t('deal.price'), r: true, num: true, render: (d) => S.fmt.money(d.price - d.discount, d.currency) },
          // The real API's list has no paid sum (it is in each deal's schedule): the column shows only when it is known.
          list.some((d) => d.paid !== null) ? { label: t('sch.paid'), r: true, num: true, render: (d) => S.fmt.money(d.paid, d.currency) } : null,
          { label: t('deal.manager'), render: (d) => d.managerName || managers[d.managerId] || '' }].filter(Boolean),
          list.filter((d) => (stage === 'all' || d.stage === stage) && (!q || (d.customerName + ' ' + d.apartmentNumber + ' ' + d.id).toLowerCase().includes(q))), { onRow: (d) => dealDrawer(d.id, load), empty: t('deal.none') }));
        // The list carries the manager's id only: names come from the staff list (leaders may read it) and from the signed-in user.
        let managers = {};
        const loadManagers = async () => {
          const me = S.api.user || {}; const names = me.id ? { [me.id]: me.name } : {};
          if (S.api.isLeader()) (await S.api.users().catch(() => [])).forEach((u) => { names[u.id] = u.name; });
          return names;
        };
        async function load() { holder.replaceChildren(S.skeleton(5, 38)); [list, managers] = await Promise.all([S.api.deals(), loadManagers()]); draw(); } load();
        S.reloadDeals = load;
      },

      async customers(body) {
        const holder = h('div'); const search = S.input('q', { type: 'search', placeholder: t('cust.search'), 'aria-label': t('cust.search') });
        const load = async () => { holder.replaceChildren(S.skeleton(5, 38)); const l = await S.api.customers(search.value.trim()); holder.replaceChildren(S.tbl([{ label: t('f.fullName'), render: (c) => h('b', null, c.fullName) }, { label: t('f.phone'), render: (c) => c.phone }, { label: t('f.email'), render: (c) => c.email }, { label: t('crm.deals'), r: true, render: (c) => c.dealsCount }, { label: t('lead.date'), render: (c) => S.fmt.date(c.createdAt) }], l, { onRow: (c) => customerModal(c.id), empty: t('cust.none') })); };
        search.addEventListener('input', S.debounce(load, 250));
        body.replaceChildren(h('div', { class: 'toolbar' }, search, h('span', { class: 'spacer' }),
          S.isDemo() ? null : h('button', { class: 'btn ghost', type: 'button', onclick: () => importClientsModal(load) }, '📥 ', t('cimp.btn')),
          h('button', { class: 'btn', onclick: () => customerForm(null, load) }, t('cust.new'))), holder); load();
      },

      // Message templates (SMS / WhatsApp) with {name}, {apt}, {amount}: a list, then a form in a dialog.
      async templates(body) {
        const holder = h('div');
        const TYPES = ['payment', 'booking', 'welcome'];
        const load = async () => {
          holder.replaceChildren(S.skeleton(3, 60));
          const list = await S.api.templates();
          holder.replaceChildren(list.length ? h('div', { class: 'tpl-grid' }, list.map((tp) => h('article', { class: 'card tpl-card' },
            h('div', { class: 'row between' }, h('b', null, tp.name), S.chip(tp.type, t('tpl.type.' + tp.type))),
            h('p', { class: 'tpl-text' }, highlightVars(tp.text)),
            h('div', { class: 'row', style: { gap: '8px' } },
              h('button', { class: 'btn soft sm', type: 'button', onclick: () => form(tp) }, t('common.edit')),
              h('button', { class: 'btn ghost sm', type: 'button', onclick: async (e) => { if (!(await S.confirm(t('tpl.delAsk', { n: tp.name }), t('common.delete'), true))) return; await S.busy(e.currentTarget, () => S.api.deleteTemplate(tp.id)); S.toast(t('tpl.deleted'), 'ok'); load(); } }, t('common.delete'))))))
            : h('div', { class: 'empty card' }, t('tpl.none')));
        };
        function highlightVars(text) { return String(text).split(/(\{(?:name|apt|amount)\})/).map((p) => (/^\{(name|apt|amount)\}$/.test(p) ? h('code', null, p) : p)); }
        function form(tp) {
          const name = S.input('name', { value: tp ? tp.name : '', maxlength: 120 });
          const text = h('textarea', { class: 'inp', rows: 5, maxlength: 1000 }, tp ? tp.text : '');
          const type = S.select('type', TYPES.map((k) => [k, t('tpl.type.' + k)]), tp ? tp.type : 'payment');
          const err = h('div', { class: 'form-err', hidden: true });
          const insert = (v) => { const at = text.selectionStart ?? text.value.length; text.value = text.value.slice(0, at) + v + text.value.slice(text.selectionEnd ?? at); text.focus(); };
          const save = h('button', { class: 'btn', type: 'button', onclick: (e) => S.busy(e.currentTarget, async () => {
            err.hidden = true;
            try { await S.api.saveTemplate(tp && tp.id, { name: name.value, text: text.value, type: type.value }); md.close(); S.toast(t('common.saved'), 'ok'); load(); } catch (ex) { err.textContent = ex.message; err.hidden = false; }
          }) }, t('common.save'));
          const md = S.modal({ title: tp ? t('tpl.edit') : t('tpl.new'), body: h('div', { class: 'stack' },
            S.field(t('tpl.name'), name), S.field(t('tpl.type'), type), S.field(t('tpl.text'), text, t('tpl.hint')),
            h('div', { class: 'row', style: { gap: '6px' } }, ['{name}', '{apt}', '{amount}'].map((v) => h('button', { class: 'btn soft sm', type: 'button', onclick: () => insert(v) }, v))), err), actions: [save] });
        }
        body.replaceChildren(h('div', { class: 'toolbar' }, h('p', { class: 'hint', style: { margin: 0 } }, t('tpl.sub')), h('span', { class: 'spacer' }), h('button', { class: 'btn', type: 'button', onclick: () => form(null) }, t('tpl.new'))), holder);
        load();
      },

      // Project card per building (developer's view) and three small tiles under it.
      async project(body) {
        body.replaceChildren(S.skeleton(2, 220));
        const cards = await S.api.projectCards();
        if (!cards.length) { body.replaceChildren(h('div', { class: 'empty card' }, t('proj.none'))); return; }
        body.replaceChildren(...cards.map((c) => {
          const soldPct = c.totalApartments ? Math.round((c.sold * 100) / c.totalApartments) : 0;
          const revPct = c.plan > 0 ? Math.min(100, Math.round((c.revenue * 100) / c.plan)) : 0;
          return h('section', { class: 'proj-wrap' },
            h('div', { class: 'proj-card' },
              h('span', { class: 'proj-corner', 'aria-hidden': 'true' }),
              h('div', { class: 'proj-brand' }, '🏗️ ', c.name),
              h('div', { class: 'proj-dev' }, t('proj.developer'), ': ', h('b', null, c.developer)),
              h('div', { class: 'proj-label' }, t('proj.construction'), h('b', null, c.constructionPercent + '%')),
              h('div', { class: 'pay-card-bar-track' }, h('div', { class: 'pay-card-bar-fill', style: { width: c.constructionPercent + '%' } })),
              h('dl', { class: 'proj-stats' },
                h('div', null, h('dt', null, t('proj.sold')), h('dd', null, c.sold + ' / ' + c.totalApartments + ' ', h('small', null, soldPct + '%'))),
                h('div', null, h('dt', null, t('proj.booked')), h('dd', null, String(c.booked))),
                h('div', null, h('dt', null, t('proj.free')), h('dd', null, String(c.free))),
                h('div', null, h('dt', null, t('proj.revenue')), h('dd', { class: 'gold' }, S.fmt.money(c.revenue, c.currency))),
                h('div', null, h('dt', null, t('proj.plan')), h('dd', null, S.fmt.money(c.plan, c.currency), ' ', h('small', null, revPct + '%'))))),
            h('div', { class: 'proj-tiles' },
              h('div', { class: 'card proj-tile' }, h('span', { 'aria-hidden': 'true' }, '📅'), h('small', null, t('proj.next')), h('b', null, c.nextStage || (c.completionYear ? t('proj.delivery', { y: c.completionYear }) : '—'))),
              // Crews and tasks are not tracked yet: example numbers, marked as such.
              h('div', { class: 'card proj-tile' }, h('span', { 'aria-hidden': 'true' }, '👷'), h('small', null, t('proj.crews')), h('b', null, '8'), h('em', null, t('proj.example'))),
              h('div', { class: 'card proj-tile' }, h('span', { 'aria-hidden': 'true' }, '📋'), h('small', null, t('proj.tasks')), h('b', null, '3'), h('em', null, t('proj.example')))));
        }));
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

      // Cash book: income and expenses of a building; entries are never edited, a mistake is reversed.
      async plan(body) {
        const bs = S.crmBuildings || (await S.api.buildings());
        if (!bs.length) { body.replaceChildren(h('p', { class: 'muted' }, t('cash.noBuildings'))); return; }
        const bsel = S.select('building', bs.map((b) => [b.id, b.name]), bs[0].id);
        const floorsRow = h('div', { class: 'fp-floors', role: 'group', 'aria-label': t('fplan.floor') });
        const stage = h('div', { class: 'fp-stage card flat' });
        const tip = h('div', { class: 'fp-tip', hidden: true, role: 'tooltip' });
        const legend = h('div', { class: 'fp-legend' }, [['free', '🟢'], ['booked', '🟡'], ['sold', '🔴'], ['onrequest', '⚫']].map(([k, dot]) => h('span', null, dot + ' ' + t('fplan.st.' + k))));
        let floor = null, withPlan = [];
        const building = () => bs.find((b) => String(b.id) === String(bsel.value));
        async function loadFloors() {
          withPlan = await S.api.floorPlanFloors(bsel.value).catch(() => []);
          const n = Math.max(building().floorsCount || 0, ...withPlan, 1);
          floor = withPlan[0] || 1;
          floorsRow.replaceChildren(...Array.from({ length: n }, (_, i) => i + 1).map((f) => h('button', { type: 'button', class: 'btn sm ' + (withPlan.includes(f) ? 'ghost has-plan' : 'ghost'), 'data-floor': f, onclick: () => { floor = f; draw(); } }, String(f))));
          draw();
        }
        async function draw() {
          floorsRow.querySelectorAll('button').forEach((b) => b.classList.toggle('on', +b.dataset.floor === floor));
          tip.hidden = true;
          if (!withPlan.includes(floor)) { stage.replaceChildren(h('p', { class: 'muted' }, t('fplan.none'))); return; }
          stage.replaceChildren(S.skeleton(1, 120));
          try {
            // The server colours the drawing and strips scripts when the template is saved; parse it as SVG, not HTML.
            const doc = new DOMParser().parseFromString(await S.api.floorPlan(bsel.value, floor), 'image/svg+xml');
            if (doc.querySelector('parsererror')) throw new Error(t('fplan.bad'));
            const svg = document.importNode(doc.documentElement, true);
            svg.querySelectorAll('.fp-apt').forEach((r) => { r.setAttribute('tabindex', '0'); r.setAttribute('role', 'button'); r.setAttribute('aria-label', (r.querySelector('title') || {}).textContent || ''); });
            stage.replaceChildren(svg);
          } catch (e) { stage.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
        }
        function open(r) {
          const st = r.dataset.status, dealId = r.dataset.dealId;
          if (st === 'free') bookForm({ id: r.dataset.apartmentId, number: r.dataset.number }, draw);
          else if (dealId) dealDrawer(dealId, draw);
          else S.toast(st === 'booked' ? t('fplan.noDeal') : t('fplan.unavailable'), 'info');
        }
        stage.addEventListener('click', (e) => { const r = e.target.closest('.fp-apt'); if (r) open(r); });
        stage.addEventListener('keydown', (e) => { const r = e.target.closest && e.target.closest('.fp-apt'); if (r && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); open(r); } });
        stage.addEventListener('mousemove', (e) => {
          const r = e.target.closest('.fp-apt');
          if (!r) { tip.hidden = true; return; }
          tip.textContent = (r.querySelector('title') || {}).textContent || '';
          const box = tip.parentNode.getBoundingClientRect();
          tip.style.left = (e.clientX - box.left + 12) + 'px'; tip.style.top = (e.clientY - box.top + 12) + 'px'; tip.hidden = false;
        });
        stage.addEventListener('mouseleave', () => { tip.hidden = true; });
        bsel.addEventListener('change', loadFloors);
        body.replaceChildren(h('div', { class: 'toolbar' }, bs.length > 1 ? S.field(t('cash.building'), bsel) : null), h('div', { class: 'fp-bar' }, h('b', null, t('fplan.floor')), floorsRow), legend, h('div', { class: 'fp-host' }, stage, tip));
        await loadFloors();
      },

      async cash(body) {
        const bs = S.crmBuildings || (await S.api.buildings());
        if (!bs.length) { body.replaceChildren(h('p', { class: 'muted' }, t('cash.noBuildings'))); return; }
        const bsel = S.select('building', bs.map((b) => [b.id, b.name]), bs[0].id);
        const from = S.input('from', { type: 'date', value: S.today().slice(0, 8) + '01' }), to = S.input('to', { type: 'date', value: S.today() });
        const holder = h('div');
        const range = () => ({ from: from.value || undefined, to: to.value || undefined });
        const go = h('button', { class: 'btn', onclick: (e) => S.busy(e.currentTarget, load) }, t('rep.show'));
        const xl = h('button', { class: 'btn ghost', onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/buildings/' + bsel.value + '/cash/export.xlsx', range(), 'Excel').catch(S.err)) }, t('cash.excel'));
        const plus = h('button', { class: 'btn gold', onclick: () => addForm() }, '+ ' + t('cash.add'));
        bsel.addEventListener('change', load);
        body.replaceChildren(h('div', { class: 'toolbar' }, bs.length > 1 ? S.field(t('cash.building'), bsel) : null, S.field(t('rep.from'), from), S.field(t('rep.to'), to),
          h('div', { style: { alignSelf: 'flex-end', display: 'flex', gap: '8px', flexWrap: 'wrap' } }, go, xl, plus)), holder);

        async function load() {
          holder.replaceChildren(S.skeleton(5, 38));
          try {
            const r = await S.api.cashBook(bsel.value, range().from, range().to);
            const kpi = (label, v, color) => h('div', { class: 'card kpi', style: { '--k': color } }, h('span', null, label), h('b', { style: { fontSize: '22px' } }, S.fmt.money(v, 'TJS')), h('i'));
            const signed = (x) => (x.amount < 0 ? '− ' : '+ ') + S.fmt.money(Math.abs(x.amount), x.currency);
            holder.replaceChildren(
              h('div', { class: 'grid g3', style: { marginBottom: '14px' } }, kpi(t('cash.income'), r.totalIncomeTjs, 'var(--good)'), kpi(t('cash.expense'), r.totalExpenseTjs, 'var(--bad)'), kpi(t('cash.balance'), r.balanceTjs, 'var(--gold)')),
              S.tbl([
                { label: t('doc.date'), render: (x) => S.fmt.date(x.operationDate) },
                { label: t('cash.direction'), render: (x) => (x.direction === 'Income' ? '🟢 ' + t('cash.income') : '🔴 ' + t('cash.expense')) },
                { label: t('cash.category'), render: (x) => t('cash.cat.' + x.category) },
                { label: t('cash.description'), render: (x) => h('span', null, x.isReversal ? h('b', null, t('cash.reverse') + ': ') : null, x.description) },
                { label: t('pay.amount'), r: true, num: true, render: (x) => h('span', { title: x.currency !== 'TJS' ? S.fmt.money(x.amountTjs, 'TJS') : null }, signed(x)) },
                { label: '', r: true, render: (x) => h('div', { class: 'row', style: { gap: '6px', justifyContent: 'flex-end', flexWrap: 'nowrap' } },
                  h('button', { class: 'btn ghost sm', onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/buildings/' + bsel.value + '/cash/' + x.id + '/receipt.pdf', null, 'PDF').catch(S.err)) }, '📄 ' + t('cash.receipt')),
                  !x.isReversal && !x.isReversed ? h('button', { class: 'btn danger sm', onclick: () => reverse(x) }, '↩ ' + t('cash.reverse')) : null) },
              ], r.transactions, { rowClass: (x) => (x.isReversal || x.isReversed ? 'rev cash-rev' : ''), empty: t('cash.none') }));
          } catch (e) { holder.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
        }

        async function reverse(x) {
          const reason = await S.askReason(t('cash.reverse'), t('cash.reverseConfirm'));
          if (reason === null) return;
          try { await S.api.reverseCash(bsel.value, x.id, reason); S.toast(t('cash.reversed'), 'ok'); load(); } catch (er) { S.err(er); }
        }

        function addForm() {
          const INCOME = ['ApartmentPayment', 'Rent', 'OtherIncome'], EXPENSE = ['Construction', 'Materials', 'Salaries', 'Marketing', 'Utilities', 'Tax', 'OtherExpense'];
          const dir = S.select('direction', [['Income', t('cash.income')], ['Expense', t('cash.expense')]], 'Income');
          const cat = S.select('category', [], undefined);
          const fillCats = () => cat.replaceChildren(...(dir.value === 'Income' ? INCOME : EXPENSE).map((c) => h('option', { value: c }, t('cash.cat.' + c))));
          dir.addEventListener('change', fillCats); fillCats();
          const amount = S.input('amount', { type: 'number', step: '0.01', min: '0.01', inputmode: 'decimal' });
          const cur = S.select('currency', [['TJS', 'TJS'], ['USD', 'USD']], 'TJS');
          const date = S.input('operationDate', { type: 'date', value: S.today(), max: S.today() });
          const desc = h('textarea', { class: 'inp', name: 'description', maxlength: 500, rows: 3 });
          const deal = S.input('dealId', { placeholder: t('cash.dealHint') });
          const btn = h('button', { class: 'btn', type: 'submit' }, t('common.save'));
          const form = h('form', { class: 'stack', novalidate: true },
            h('div', { class: 'grid g2' }, S.field(t('cash.direction'), dir), S.field(t('cash.category'), cat), S.field(t('pay.amount'), amount), S.field(t('cash.currency'), cur)),
            S.field(t('doc.date'), date), S.field(t('cash.description'), desc), S.field(t('cash.deal'), deal), btn);
          const md = S.modal({ title: t('cash.add'), body: form });
          form.addEventListener('submit', async (e) => {
            e.preventDefault(); S.showErrors(form, null);
            await S.busy(btn, async () => {
              try {
                await S.api.addCash(bsel.value, { amount: +amount.value, currency: cur.value, direction: dir.value, category: cat.value, description: desc.value.trim(), operationDate: date.value, dealId: deal.value.trim() || null });
                md.close(); S.toast(t('cash.added'), 'ok'); load();
              } catch (er) { S.showErrors(form, er); }
            });
          });
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
        const bs = await S.api.buildings();
        if (!bs.length) { body.replaceChildren(S.empty(t('cat.noBuildings'), 'building'), S.api.isOwner() ? h('div', { class: 'row' }, h('button', { class: 'btn', onclick: () => buildingForm(() => S.crmGo('catalog')) }, t('cat.newBuilding'))) : null); return; }
        const bid = pickBuilding(bs);
        const b = await S.api.building(bid).catch(() => bs.find((x) => x.id === bid)); const floors = await S.api.floors(b.id);
        if (!floors.length) { body.replaceChildren(buildingBar(bs, bid, 'catalog'), noFloors(b, () => S.crmGo('catalog'))); return; }
        let floorId = (floors.find((f) => !f.commercial) || floors[0]).id; const holder = h('div');
        const nameRu = S.input('nameRu', { value: b.nameRu || b.name }), nameTg = S.input('nameTg', { value: b.nameTg }), addr = S.input('address', { value: b.address });
        const dRu = h('textarea', { class: 'inp', name: 'descriptionRu', id: 'b-dru' }); dRu.value = b.descriptionRu || b.description || ''; const dTg = h('textarea', { class: 'inp', name: 'descriptionTg', id: 'b-dtg' }); dTg.value = b.descriptionTg || '';
        const bbtn = h('button', { class: 'btn', type: 'submit' }, t('common.save'));
        const bform = h('form', { class: 'card stack', novalidate: true }, h('h3', null, t('cat.building')), h('div', { class: 'grid g2' }, S.field(t('cat.nameRu'), nameRu), S.field(t('cat.nameTg'), nameTg)), S.field(t('f.address'), addr), h('div', { class: 'grid g2' }, S.field(t('cat.descRu'), dRu), S.field(t('cat.descTg'), dTg, t('cat.tgHint'))), h('div', null, bbtn));
        bform.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(bform, null); await S.busy(bbtn, async () => { try { await S.api.saveBuilding(b.id, { nameRu: nameRu.value, nameTg: nameTg.value, descriptionRu: dRu.value, descriptionTg: dTg.value, address: addr.value }); S.toast(t('common.saved'), 'ok'); } catch (er) { S.showErrors(bform, er); } }); });
        const sel = S.select('floor', floors.map((f) => [f.id, f.commercial ? t('floor.commercial') : t('floor.n', { n: f.number })]), floorId); sel.addEventListener('change', () => { floorId = +sel.value || sel.value; drawApts(); });
        const imp = h('input', { type: 'file', id: 'imp-file', accept: '.xlsx', hidden: true });
        imp.addEventListener('change', async () => { const f = imp.files[0]; if (!f) return; try { const r = await S.api.importApartments(b.id, f); S.toast(r && r.message ? r.message : t('cat.imported'), 'ok'); drawApts(); } catch (er) { S.modal({ title: t('cat.importErr'), body: h('div', { class: 'stack' }, h('p', null, er.message), er.errors ? h('pre', { class: 'paper', style: { whiteSpace: 'pre-wrap' } }, JSON.stringify(er.errors, null, 1)) : null) }); } imp.value = ''; });
        const tools = h('div', { class: 'toolbar' }, h('div', { style: { minWidth: '200px' } }, S.field(t('cat.floor'), sel)), h('span', { class: 'spacer' }), S.isDemo() ? null : h('button', { class: 'btn ghost', type: 'button', title: t('cat.templateHint'), onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/apartments/import/template', null, t('cat.template')).catch(S.err)) }, t('cat.template')), imp, h('label', { class: 'btn ghost', for: 'imp-file' }, t('cat.import')), h('button', { class: 'btn', onclick: () => aptForm(null, floors.find((f) => String(f.id) === String(floorId)), drawApts) }, t('cat.addApt')));
        // Eco passport of the building: Content and leaders (the catalog section is theirs).
        const ecoForm = S.eco ? S.eco.form(b, (r) => { b.eco = r && r.eco; }) : null;
        body.replaceChildren(buildingBar(bs, bid, 'catalog'), bform, ecoForm, h('h3', { style: { marginTop: '10px' } }, t('cat.apts')), tools, holder);
        async function drawApts() {
          const fl = floors.find((f) => String(f.id) === String(floorId)); holder.replaceChildren(S.skeleton(4, 38));
          const list = (await S.api.floorApartments(fl)).sort((a, c) => (a.planSlot || 0) - (c.planSlot || 0));
          holder.replaceChildren(S.tbl([{ label: '№', render: (a) => h('b', null, a.number) }, { label: t('cat.slot'), render: (a) => a.planSlot }, { label: t('f.rooms'), render: (a) => S.fmt.rooms(a.rooms) }, { label: t('f.area'), r: true, num: true, render: (a) => S.fmt.area(a.area) }, { label: t('cat.pps'), r: true, num: true, render: (a) => S.fmt.n(a.pricePerSqm) }, { label: t('deal.price'), r: true, num: true, render: (a) => S.fmt.money(a.price, 'TJS') }, { label: t('deal.stage'), render: (a) => S.statusChip(a.status) }, { label: '', r: true, render: (a) => h('span', { class: 'row', style: { justifyContent: 'flex-end', gap: '6px', flexWrap: 'nowrap' } }, h('button', { class: 'btn ghost sm', onclick: () => aptForm(a, fl, drawApts) }, t('common.edit')), a.status === 'free' || a.status === 'onrequest' ? h('button', { class: 'btn soft sm', onclick: async (e) => { await S.busy(e.currentTarget, async () => { try { await S.api.onRequest(a.id, a.status === 'free'); drawApts(); } catch (er) { S.err(er); } }); } }, a.status === 'free' ? t('cat.toRequest') : t('cat.toFree')) : null) }], list, { empty: t('cat.noApts'), rowClass: (a) => 'apt-st st-' + a.status }));
        }
        drawApts();
      },

      async construction(body) {
        const holder = h('div', { class: 'stack' }); const bs = await S.api.buildings();
        if (!bs.length) { body.replaceChildren(S.empty(t('cat.noBuildings'), 'building')); return; }
        const bid = pickBuilding(bs);
        const load = async () => {
          holder.replaceChildren(S.skeleton(4, 60)); const st = await S.api.stages(bid);
          holder.replaceChildren(S.tbl([{ label: t('con.stage'), render: (s) => h('b', null, s.title) }, { label: t('con.progress'), render: (s) => h('div', { style: { minWidth: '160px' } }, S.bar(s.percent)) }, { label: '%', r: true, num: true, render: (s) => s.percent + '%' }, { label: t('doc.date'), render: (s) => S.fmt.date(s.date) }, { label: t('con.photos'), r: true, num: true, render: (s) => s.photos.length }, { label: '', r: true, render: (s) => h('button', { class: 'btn ghost sm', onclick: () => stageForm(s, bid, load) }, t('common.edit')) }], st, { empty: t('con.none') }));
        };
        body.replaceChildren(buildingBar(bs, bid, 'construction'), h('div', { class: 'toolbar' }, h('span', { class: 'spacer' }), h('button', { class: 'btn', onclick: () => stageForm(null, bid, load) }, t('con.add'))), holder); load();
      },

      async users(body) {
        const holder = h('div');
        const pending = h('div');
        const bname = (ids) => (ids.length ? ids.map((id) => ((S.crmBuildings || []).find((b) => b.id === id) || { name: '…' }).name).join(', ') : t('crm.allBuildings'));
        const load = async () => { holder.replaceChildren(S.skeleton(4, 38)); const [l, inv] = await Promise.all([S.api.users(), S.api.invitations().catch(() => [])]); S.crmBuildings = S.crmBuildings || (await S.api.buildings());
          pending.replaceChildren(...(inv.length ? [h('h3', { style: { margin: '18px 0 8px' } }, t('inv.pending')), S.tbl([{ label: t('login.email'), render: (i) => h('b', null, i.email) }, { label: t('f.fullName'), render: (i) => i.name }, { label: t('users.role'), render: (i) => t('role.' + i.role, null, i.role) }, { label: t('users.buildings'), render: (i) => bname(i.buildingIds) }, { label: t('inv.until'), render: (i) => S.fmt.date(i.expiresAt) }, { label: '', r: true, render: (i) => h('button', { class: 'btn ghost sm', onclick: (e) => S.busy(e.currentTarget, async () => { try { await S.api.revokeInvitation(i.id); S.toast(t('inv.revoked'), 'ok'); load(); } catch (er) { S.err(er); } }) }, t('inv.revoke')) }], inv)] : []));
          holder.replaceChildren(S.tbl([{ label: t('f.fullName'), render: (u) => h('b', null, u.name) }, { label: t('login.email'), render: (u) => u.email }, { label: t('users.role'), render: (u) => u.roles.map((r) => t('role.' + r)).join(', ') }, { label: t('users.buildings'), render: (u) => (S.api.isOwner() && u.id !== S.api.user.id && !u.roles.includes('Owner') ? h('button', { class: 'btn ghost sm', title: t('users.setBuildings'), onclick: () => buildingsForm(u, load) }, bname(u.buildingIds)) : bname(u.buildingIds)) }, { label: t('deal.stage'), render: (u) => S.chip(u.blocked ? 'Blocked' : 'Active ok', t(u.blocked ? 'users.blocked' : 'users.active')) }, { label: '', r: true, render: (u) => (u.id === (S.api.user && S.api.user.id) ? '' : h('button', { class: 'btn ghost sm', onclick: async (e) => { await S.busy(e.currentTarget, async () => { try { await S.api.setUserBlocked(u.id, !u.blocked); S.toast(t('common.saved'), 'ok'); load(); } catch (er) { S.err(er); } }); } }, u.blocked ? t('users.unblock') : t('users.block'))) }], l)); };
        body.replaceChildren(h('div', { class: 'toolbar' }, h('p', { class: 'hint' }, t('inv.hint')), h('span', { class: 'spacer' }), h('button', { class: 'btn', onclick: () => userForm(load) }, t('inv.new'))), holder, pending); load();
      },

      async audit(body) {
        // Entity names as the server writes them (class names); the demo calls users "User".
        const userEntity = S.isDemo() ? 'User' : 'AppUser';
        const holder = h('div'); const sel = S.select('entity', [['', t('filter.all')], ['Deal', 'Deal'], ['Payment', 'Payment'], ['Apartment', 'Apartment'], ['Booking', 'Booking'], ['Lead', 'Lead'], [userEntity, 'User'], ['Impersonation', t('audit.viaPlatform')], ['Invitation', 'Invitation']], '');
        const load = async () => { holder.replaceChildren(S.skeleton(6, 34)); const l = await S.api.audit(sel.value); holder.replaceChildren(S.tbl([{ label: t('audit.when'), render: (a) => S.fmt.dt(a.at) }, { label: t('audit.who'), render: (a) => (a.viaPlatform ? h('span', null, a.user, ' ', S.chip('warn', t('audit.viaPlatform'))) : a.user) }, { label: t('audit.action'), render: (a) => h('b', null, a.action) }, { label: t('audit.entity'), render: (a) => a.entity + ' #' + shortId(a.entityId) }, { label: t('audit.change'), render: (a) => h('code', { style: { fontSize: '12px', color: 'var(--ink-2)' } }, [a.before ? JSON.stringify(a.before) : '', a.before && a.after ? ' → ' : '', a.after ? JSON.stringify(a.after) : ''].join('')) }], l, { empty: t('audit.none') })); };
        sel.addEventListener('change', load); body.replaceChildren(h('div', { class: 'toolbar' }, h('div', { style: { minWidth: '200px' } }, S.field(t('audit.entity'), sel))), holder); load();
      },

      // Conversations with buyers: the freshest first, unread messages counted, search by name or phone.
      async chats(body) {
        const search = S.input('search', { type: 'search', placeholder: t('chats.search'), 'aria-label': t('chats.search') });
        const holder = h('div');
        let timer = null;
        const load = async () => {
          holder.replaceChildren(S.skeleton(5, 56));
          try {
            const list = await S.api.chats(search.value.trim());
            holder.replaceChildren(list.length ? h('div', { class: 'chat-list' }, list.map((c) => h('button', { class: 'chat-row' + (c.unread ? ' unread' : ''), type: 'button', onclick: () => dealDrawer(c.dealId, () => { load(); S.crmChatBadge(); }, null, 'chat') },
              h('span', { class: 'avatar' }, S.fmt.initials(c.customerName)),
              h('span', { class: 'chat-row-main' },
                h('span', { class: 'row between', style: { flexWrap: 'nowrap', gap: '8px' } }, h('b', null, c.customerName), h('small', { class: 'muted' }, S.fmt.dt(c.lastMessageAt))),
                h('small', { class: 'muted' }, [c.customerPhone, c.buildingName, t('apt.title', { n: c.apartmentNumber })].filter(Boolean).join(' · ')),
                h('span', { class: 'chat-last' }, (c.lastFromCustomer ? '' : t('chats.you') + ' ') + c.lastMessage)),
              c.unread ? h('span', { class: 'badge', 'aria-label': t('chats.unread', { n: c.unread }) }, c.unread) : null)))
              : S.empty(t(search.value.trim() ? 'chats.noMatch' : 'chats.none'), 'send'));
          } catch (e) { holder.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
        };
        search.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(load, 300); });
        body.replaceChildren(h('div', { class: 'toolbar' }, h('div', { style: { minWidth: '260px', flex: '1', maxWidth: '420px' } }, search)), holder);
        load(); S.crmChatBadge();
      },

      // The organization's buildings as cards; a card opens the building: apartments, its people and the numbers.
      async objects(body) {
        let tab = 'apts';
        const list = async () => {
          body.replaceChildren(S.skeleton(3, 80));
          const bs = await S.api.buildings(); S.crmBuildings = bs;
          const add = S.api.isOwner() ? h('button', { class: 'btn', onclick: () => buildingForm(() => S.crmGo('objects')) }, t('cat.newBuilding')) : null;
          if (!bs.length) { body.replaceChildren(S.empty(t('cat.noBuildings'), 'building'), add ? h('div', { class: 'row' }, add) : null); return; }
          body.replaceChildren(h('div', { class: 'toolbar' }, h('p', { class: 'hint' }, t('obj.hint')), h('span', { class: 'spacer' }), add),
            h('div', { class: 'obj-grid' }, bs.map((b) => h('button', { class: 'card obj-card', type: 'button', onclick: () => detail(b) },
              h('b', { class: 'obj-name' }, b.name), h('span', { class: 'muted' }, b.address),
              h('span', { class: 'obj-meta' }, h('span', null, t('obj.apts', { n: b.total }) + (b.plannedApartments ? ' / ' + b.plannedApartments : '')), h('span', null, t('obj.free', { n: b.free })), b.completionYear ? h('span', null, t('obj.year', { y: b.completionYear })) : null),
              h('span', { class: 'obj-open' }, t('obj.open'))))));
        };
        const detail = (b) => {
          const pane = h('div', { class: 'stack' });
          const seg = h('div', { class: 'seg obj-tabs', role: 'tablist' }, [['apts', 'obj.tab.apts'], ['staff', 'obj.tab.staff'], ['stats', 'obj.tab.stats']].filter(([k]) => k !== 'staff' || S.api.isLeader()).map(([k, key]) =>
            h('button', { class: tab === k ? 'on' : '', role: 'tab', 'aria-selected': String(tab === k), 'data-k': k, onclick: () => { tab = k; S.$$('button', seg).forEach((x) => { x.classList.toggle('on', x.dataset.k === k); x.setAttribute('aria-selected', String(x.dataset.k === k)); }); show(); } }, t(key))));
          const show = () => panes[tab](pane, b, () => detail(b)).catch((e) => pane.replaceChildren(h('div', { class: 'form-err' }, e.message)));
          body.replaceChildren(h('button', { class: 'btn ghost sm', style: { alignSelf: 'flex-start' }, onclick: () => list() }, '← ' + t('obj.all')),
            h('div', null, h('h2', { style: { margin: '0 0 4px' } }, b.name), h('p', { class: 'muted' }, [b.address, b.completionYear ? t('obj.year', { y: b.completionYear }) : '', t('obj.floors', { n: b.floorsCount })].filter(Boolean).join(' · '))),
            seg, pane);
          show();
        };
        const panes = {
          async apts(pane, b, again) {
            pane.replaceChildren(S.skeleton(5, 38));
            const floors = await S.api.floors(b.id);
            if (!floors.length) { pane.replaceChildren(noFloors(b, again)); return; }
            const apts = await S.api.allApartments(floors);
            const fsel = S.select('floor', [['', t('obj.allFloors')]].concat(floors.map((f) => [String(f.number), f.commercial ? t('floor.commercial') : t('floor.n', { n: f.number })])), '');
            const ssel = S.select('status', [['', t('filter.all')]].concat(['free', 'booked', 'sold', 'onrequest'].map((s) => [s, t('status.' + s)])), '');
            const holder = h('div');
            const edit = has('Content');
            const draw = () => {
              const l = apts.filter((a) => (!fsel.value || String(a.floor) === fsel.value) && (!ssel.value || a.status === ssel.value)).sort((x, y) => x.floor - y.floor || x.number - y.number);
              holder.replaceChildren(S.tbl([
                { label: t('cat.floor'), render: (a) => (a.floor === 0 ? t('floor.commercial') : a.floor) },
                { label: '№', render: (a) => h('b', null, a.number) },
                { label: t('f.rooms'), render: (a) => S.fmt.rooms(a.rooms) },
                { label: t('f.area'), r: true, num: true, render: (a) => S.fmt.area(a.area) },
                { label: t('deal.price'), r: true, num: true, render: (a) => S.fmt.money(a.price, 'TJS') },
                { label: t('deal.stage'), render: (a) => S.statusChip(a.status) },
                edit ? { label: '', r: true, render: (a) => h('button', { class: 'btn ghost sm', onclick: (e) => { e.stopPropagation(); aptForm(a, floors.find((f) => f.number === a.floor), again); } }, t('common.edit')) } : null,
              ].filter(Boolean), l, { empty: t(apts.length ? 'obj.noMatch' : 'cat.noApts'), onRow: (a) => aptPanel(a, again), rowClass: (a) => 'apt-st st-' + a.status }));
            };
            [fsel, ssel].forEach((s) => s.addEventListener('change', draw));
            const imp = h('input', { type: 'file', id: 'obj-imp', accept: '.xlsx', hidden: true });
            imp.addEventListener('change', async () => { const f = imp.files[0]; imp.value = ''; if (!f) return; try { const r = await S.api.importApartments(b.id, f); S.toast(r && r.message ? r.message : t('cat.imported'), 'ok'); again(); } catch (er) { S.modal({ title: t('cat.importErr'), body: h('div', { class: 'stack' }, h('p', null, er.message), er.errors ? h('pre', { class: 'paper', style: { whiteSpace: 'pre-wrap' } }, JSON.stringify(er.errors, null, 1)) : null) }); } });
            const floorFor = () => floors.find((f) => String(f.number) === fsel.value) || floors.find((f) => !f.commercial) || floors[0];
            pane.replaceChildren(h('div', { class: 'toolbar' }, h('div', { style: { minWidth: '160px' } }, S.field(t('cat.floor'), fsel)), h('div', { style: { minWidth: '160px' } }, S.field(t('deal.stage'), ssel)), h('span', { class: 'spacer' }),
              edit && !S.isDemo() ? h('button', { class: 'btn ghost', type: 'button', title: t('cat.templateHint'), onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/apartments/import/template', null, t('cat.template')).catch(S.err)) }, t('cat.template')) : null, edit ? imp : null, edit ? h('label', { class: 'btn ghost', for: 'obj-imp' }, t('cat.import')) : null, edit ? h('button', { class: 'btn', onclick: () => aptForm(null, floorFor(), again) }, t('cat.addApt')) : null), holder);
            draw();
          },

          // Everyone who works in this building: people with access to it (or to all buildings) and invitations not yet accepted.
          async staff(pane, b, again) {
            pane.replaceChildren(S.skeleton(4, 38));
            const [users, inv] = await Promise.all([S.api.users(), S.api.invitations().catch(() => [])]);
            const here = (ids) => !ids || !ids.length || ids.map(String).includes(String(b.id));
            const rows = users.filter((u) => here(u.buildingIds)).map((u) => ({ kind: 'user', id: u.id, name: u.name, email: u.email, role: u.roles.map((r) => t('role.' + r)).join(', '), status: u.blocked ? 'blocked' : 'active', u }))
              .concat(inv.filter((i) => here(i.buildingIds)).map((i) => ({ kind: 'inv', id: i.id, name: i.name, email: i.email, role: t('role.' + i.role, null, i.role), status: 'invited' })));
            const ST = { active: ['Active ok', 'obj.st.active'], invited: ['warn', 'obj.st.invited'], blocked: ['Blocked', 'obj.st.blocked'] };
            const run = (fn) => (e) => S.busy(e.currentTarget, async () => { try { await fn(); S.toast(t('common.saved'), 'ok'); again(); } catch (er) { S.err(er); } });
            const act = (r) => (r.kind === 'inv' ? h('button', { class: 'btn ghost sm', onclick: run(() => S.api.revokeInvitation(r.id)) }, t('inv.revoke'))
              : r.id === S.api.user.id || r.u.roles.includes('Owner') ? '' : h('button', { class: 'btn ghost sm', onclick: run(() => S.api.setUserBlocked(r.id, !r.u.blocked)) }, t(r.u.blocked ? 'obj.restore' : 'obj.revokeAccess')));
            S.crmBuildings = S.crmBuildings || (await S.api.buildings());
            pane.replaceChildren(h('div', { class: 'toolbar' }, h('p', { class: 'hint' }, t('obj.staffHint')), h('span', { class: 'spacer' }), h('button', { class: 'btn', onclick: () => userForm(again, b.id) }, t('inv.new'))),
              S.tbl([{ label: t('f.fullName'), render: (r) => h('b', null, r.name) }, { label: t('login.email'), render: (r) => r.email }, { label: t('users.role'), render: (r) => r.role }, { label: t('deal.stage'), render: (r) => S.chip(ST[r.status][0], t(ST[r.status][1])) }, { label: '', r: true, render: act }], rows, { empty: t('obj.noStaff') }));
          },

          // Leaders get the building's figures from /stats/buildings (with money); a manager — the apartments from the catalog.
          async stats(pane, b) {
            pane.replaceChildren(S.skeleton(3, 60));
            let row = null;
            if (S.api.isLeader()) { try { row = (await S.api.statsBuildings()).find((x) => String(x.buildingId) === String(b.id)) || null; } catch (e) { row = null; } }
            if (!row) {
              const floors = await S.api.floors(b.id);
              const apts = floors.length ? await S.api.allApartments(floors) : [];
              const n = (s) => apts.filter((a) => a.status === s).length;
              row = { totalApartments: apts.length, free: n('free'), booked: n('booked'), sold: n('sold'), onRequest: n('onrequest'), occupancyRate: apts.length ? (n('booked') + n('sold')) / apts.length : 0, revenue: null, collected: null };
            }
            const total = row.totalApartments;
            const pct = (v) => (total ? Math.round((v * 1000) / total) / 10 : 0) + '%';
            const kpi = (label, value, color, sub) => h('div', { class: 'card kpi', style: { '--k': color } }, h('span', null, label), h('b', null, value), sub ? h('div', { class: 'muted' }, sub) : null, h('i'));
            const parts = [['free', row.free, 'var(--good)'], ['booked', row.booked, 'var(--warn)'], ['sold', row.sold, 'var(--info)'], ['onrequest', row.onRequest, 'var(--muted)']];
            // Occupancy donut: arcs of a conic gradient, one per status, in proportion to the apartments.
            let at = 0;
            const stops = parts.filter(([, v]) => v > 0).map(([, v, col]) => { const from = at; at += (v * 100) / (total || 1); return col + ' ' + from + '% ' + at + '%'; });
            const donut = h('div', { class: 'occ-donut', style: { background: total ? 'conic-gradient(' + stops.join(', ') + ')' : 'var(--surface-3)' }, role: 'img', 'aria-label': parts.map(([k, v]) => t('status.' + k) + ': ' + v).join(', ') },
              h('div', null, h('b', null, Math.round((+row.occupancyRate || 0) * 100) + '%'), h('small', null, t('stats.occupancy'))));
            let money;
            if (row.revenue) {
              const sum = statTjs(row.revenue), got = statTjs(row.collected) || 0;
              const share = sum ? Math.min(100, Math.round((got * 100) / sum)) : 0;
              money = h('div', { class: 'card stack' }, h('h3', null, t('stats.money')),
                h('div', { class: 'row between' }, h('span', null, t('stats.collected') + ': ', h('b', null, statMoney(row.collected))), h('b', { class: 'num' }, share + '%')),
                S.bar(share, 'gold'), h('div', { class: 'muted' }, t('stats.contracts') + ': ' + statMoney(row.revenue)));
            } else money = h('div', { class: 'card flat hint' }, t('stats.moneyLeaders'));
            pane.replaceChildren(
              h('div', { class: 'grid g4' }, kpi(t('obj.total'), total, 'var(--accent)', b.plannedApartments ? t('obj.ofPlanned', { n: b.plannedApartments }) : null),
                kpi(t('status.free'), row.free, 'var(--good)', pct(row.free)), kpi(t('status.booked'), row.booked, 'var(--warn)', pct(row.booked)), kpi(t('status.sold'), row.sold, 'var(--info)', pct(row.sold))),
              h('div', { class: 'grid g2' },
                h('div', { class: 'card' }, h('h3', null, t('obj.split')), h('div', { class: 'occ-wrap' }, donut,
                  h('div', { class: 'obj-legend stack' }, parts.map(([k, v, col]) => h('span', null, h('i', { style: { background: col } }), t('status.' + k) + ' — ' + v))))),
                money));
          },
        };
        list();
      },

      // The owner's settings: the developer's brand on the public site (name, contacts, colour, logo) and the plan.
      async settings(body) {
        body.replaceChildren(S.skeleton(4, 50));
        let br;
        try { br = await S.api.ownerBrand(); } catch (e) { body.replaceChildren(h('div', { class: 'form-err' }, e.message)); return; }
        const bs = await S.api.buildings().catch(() => []);
        const inp = (n, v, o) => S.input(n, Object.assign({ value: v || '' }, o || {}));
        const name = inp('name', br.name), slug = inp('slug', br.slug), phone = inp('phone', br.phone, { type: 'tel', placeholder: '+992 …' }), tg = inp('telegram', br.telegram, { placeholder: '@company' }), addr = inp('address', br.address), email = inp('email', br.email, { type: 'email' });
        const color = h('input', { type: 'color', class: 'color-inp', name: 'primaryColor', id: 'set-color', value: br.primaryColor || '#c9a227' });
        const noColor = h('input', { type: 'checkbox', id: 'set-nocolor', checked: !br.primaryColor });
        color.addEventListener('input', () => { noColor.checked = false; });
        const btn = h('button', { class: 'btn', type: 'submit' }, t('common.save'));
        const form = h('form', { class: 'card stack', novalidate: true }, h('h3', null, t('set.company')),
          h('div', { class: 'grid g2' }, S.field(t('set.name'), name), S.field(t('plat.slug'), slug, t('set.slugHint'))),
          h('div', { class: 'grid g2' }, S.field(t('f.phone'), phone), S.field('Telegram', tg)),
          h('div', { class: 'grid g2' }, S.field(t('set.address'), addr), S.field(t('set.email'), email)),
          h('div', { class: 'field', dataset: { name: 'primaryColor' } }, h('label', { for: 'set-color' }, t('set.color')), h('div', { class: 'row', style: { gap: '14px' } }, color, h('label', { class: 'row', style: { gap: '6px', fontWeight: 600 }, for: 'set-nocolor' }, noColor, t('set.noColor'))), h('div', { class: 'ferr' })),
          h('div', null, btn));
        form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try {
          br = await S.api.saveOwnerBrand({ name: name.value.trim(), slug: slug.value.trim(), primaryColor: noColor.checked ? null : color.value, phone: phone.value.trim() || null, telegram: tg.value.trim() || null, address: addr.value.trim() || null, email: email.value.trim() || null });
          S.toast(t('common.saved'), 'ok');
        } catch (er) { S.showErrors(form, er); } }); });

        // The logo is served by the API: a relative address needs the API's host when the site is elsewhere.
        const src = (u) => (u && u.charAt(0) === '/' ? S.apiBase().replace(/\/api\/v1\/?$/, '') + u : u);
        const logoBox = h('div', { class: 'logo-box' });
        const drawLogo = () => logoBox.replaceChildren(br.logoUrl ? h('img', { src: src(br.logoUrl), alt: t('set.logo') }) : h('span', { class: 'muted' }, t('set.noLogo')));
        const file = h('input', { type: 'file', id: 'set-logo', accept: 'image/png,image/jpeg', hidden: true });
        const upload = h('label', { class: 'btn soft', for: 'set-logo' }, t('set.logoUpload'));
        file.addEventListener('change', async () => {
          const f = file.files[0]; file.value = ''; if (!f) return;
          if (!/^image\/(png|jpeg)$/.test(f.type)) { S.err({ message: t('set.logoType') }); return; }
          if (f.size > 2 * 1024 * 1024) { S.err({ message: t('set.logoSize') }); return; }
          try { br = await S.api.uploadLogo(f); drawLogo(); S.toast(t('set.logoDone'), 'ok'); } catch (er) { S.err(er); }
        });
        const del = h('button', { class: 'btn ghost', type: 'button', onclick: (e) => S.busy(e.currentTarget, async () => { try { br = await S.api.deleteLogo(); drawLogo(); } catch (er) { S.err(er); } }) }, t('set.logoDelete'));
        drawLogo();

        const LIMIT = 3;
        body.replaceChildren(h('div', { class: 'set-grid' }, form, h('div', { class: 'stack' },
          h('div', { class: 'card stack' }, h('h3', null, t('set.logo')), logoBox, h('p', { class: 'hint' }, t('set.logoHint')), h('div', { class: 'row' }, file, upload, del)),
          h('div', { class: 'card stack' }, h('h3', null, t('set.plan')),
            h('dl', { class: 'kv' }, h('dt', null, t('set.tariff')), h('dd', null, t('set.standard')), h('dt', null, t('set.objects')), h('dd', null, t('set.objectsN', { n: bs.length, m: LIMIT }))),
            S.bar((bs.length * 100) / LIMIT, bs.length >= LIMIT ? 'warn' : ''), h('p', { class: 'hint' }, t('set.planHint'))))));
        // Tariffs and coefficients of the green features (owner): the utility calculator and the CO₂ figure use only these.
        if (S.eco) body.append(S.eco.settingsForm());
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

    // Clients from Excel (Имя | Телефон | Email | Комментарий): the file is checked first (nothing is saved),
    // the preview shows each row's status, then «Загрузить N клиентов» saves the new ones.
    function importClientsModal(done) {
      let file = null;
      const input = h('input', { type: 'file', class: 'inp', accept: '.xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', 'aria-label': t('cimp.file') });
      const out = h('div', { class: 'stack' });
      const confirmBtn = h('button', { class: 'btn', type: 'button', disabled: true }, t('cimp.load', { n: 0 }));
      const STATUS = { new: 'good', duplicate: 'warn', invalid: 'bad' };
      input.addEventListener('change', () => S.busy(confirmBtn, async () => {
        file = input.files && input.files[0];
        confirmBtn.disabled = true; out.replaceChildren();
        if (!file) return;
        try {
          const r = await S.api.importClients(file, true);
          out.replaceChildren(
            h('p', { class: 'hint' }, t('cimp.summary', { a: r.added, s: r.skipped, i: r.invalid })),
            S.tbl([{ label: '#', render: (x) => x.row }, { label: t('f.fullName'), render: (x) => x.name }, { label: t('f.phone'), render: (x) => x.phone },
              { label: t('f.email'), render: (x) => x.email || '' }, { label: t('cimp.status'), render: (x) => S.chip(STATUS[x.status], t('cimp.st.' + x.status) + (x.error ? ': ' + x.error : '')) }], r.rows, { empty: t('cimp.empty') }));
          confirmBtn.textContent = t('cimp.load', { n: r.added });
          confirmBtn.disabled = r.added === 0;
        } catch (e) { out.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
      }));
      confirmBtn.addEventListener('click', () => S.busy(confirmBtn, async () => {
        try {
          const r = await S.api.importClients(file, false);
          md.close();
          S.toast(t('cimp.done', { a: r.added, s: r.skipped + r.invalid }), 'ok');
          done && done();
        } catch (e) { out.prepend(h('div', { class: 'form-err' }, e.message)); }
      }));
      const md = S.modal({ title: t('cimp.title'), wide: true, body: h('div', { class: 'stack' }, h('p', { class: 'hint' }, t('cimp.hint')), S.field(t('cimp.file'), input), out), actions: [confirmBtn] });
    }

    function customerForm(pre, done) {
      pre = pre || {};
      const name = S.input('fullName', { value: pre.fullName || '', autocomplete: 'off' }), phone = S.input('phone', { type: 'tel', value: pre.phone || '+992' }), email = S.input('email', { type: 'email' }), pass = S.input('passport');
      const bs = S.crmBuildings || [];
      const building = bs.length > 1 ? S.select('buildingId', [['', t('cust.pickBuilding')]].concat(bs.map((b) => [b.id, b.name])), pre.buildingId || '') : null;
      const form = h('form', { class: 'stack', id: 'custform', novalidate: true }, S.field(t('f.fullName'), name), S.field(t('f.phone'), phone, t('f.phoneHint')), S.field(t('f.email'), email, t('cust.emailHint')), S.field(t('f.passport'), pass, t('f.passportHint')), building ? S.field(t('cust.building'), building, t('cust.buildingHint')) : null);
      const btn = h('button', { class: 'btn', type: 'submit', form: 'custform' }, t('common.save'));
      const md = S.modal({ title: t('cust.new'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await S.api.createCustomer({ fullName: name.value.trim(), phone: phone.value.replace(/[\s()-]/g, ''), email: email.value.trim(), passport: pass.value.trim(), leadId: pre.leadId, buildingId: building ? building.value || null : null }); S.toast(t('common.saved'), 'ok'); md.close(); done && done(); } catch (er) { S.showErrors(form, er); } }); });
    }

    async function customerModal(id) {
      const holder = h('div', null, S.skeleton(3)); S.modal({ title: t('cust.card'), body: holder, wide: false });
      try {
        const c = await S.api.customer(id);
        holder.replaceChildren(h('dl', { class: 'kv' }, h('dt', null, t('f.fullName')), h('dd', null, c.fullName), h('dt', null, t('f.phone')), h('dd', null, c.phone), h('dt', null, t('f.email')), h('dd', null, c.email || '—'), h('dt', null, t('f.passport')), h('dd', null, c.passport || '—')),
          h('h3', { style: { margin: '16px 0 8px' } }, t('crm.deals')), S.tbl([{ label: t('deal.apt'), render: (d) => '№ ' + d.apartmentNumber }, { label: t('deal.stage'), render: (d) => S.stageChip(d.stage) }, { label: t('deal.price'), r: true, num: true, render: (d) => S.fmt.money(d.price - d.discount, d.currency) }], c.deals, { onRow: (d) => dealDrawer(d.id, null), empty: t('deal.none') }));
      } catch (e) { holder.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
    }

    // An apartment from a building's list: its data, the booking or deal on it, and the next step for sales.
    // The booking is found among the pipeline cards — there is no separate "booking of an apartment" request.
    async function aptPanel(a, again) {
      const body = h('div', { class: 'stack' }, S.skeleton(4, 40));
      const md = S.modal({ side: true, body, noFocus: true });
      let card = null, stage = null;
      if (a.status !== 'free' && sales()) {
        try {
          for (const col of await S.api.pipeline()) {
            const it = col.items.find((x) => String(x.apartmentId) === String(a.id) && (x.kind === 'booking' || x.kind === 'deal') && col.stage !== 'Cancelled');
            if (it) { card = it; stage = col.stage; break; }
          }
        } catch (e) { /* the panel still shows the apartment */ }
      }
      const kv = (k, v) => [h('dt', null, k), h('dd', null, v === undefined || v === null || v === '' ? '—' : v)];
      const close = (fn) => () => { md.close(); fn(); };
      const run = (label, fn, cls) => h('button', { class: 'btn ' + (cls || ''), type: 'button', onclick: (e) => S.busy(e.currentTarget, async () => { try { await fn(); } catch (er) { S.err(er); } }) }, label);
      const actions = [];
      if (sales()) {
        if (a.status === 'free') actions.push(h('button', { class: 'btn', type: 'button', onclick: close(() => bookForm(a, again)) }, t('apt.book')), h('button', { class: 'btn ghost', type: 'button', onclick: close(() => newDealModal(again, { apartmentId: a.id })) }, t('deal.new')));
        if (card && card.kind === 'booking') {
          actions.push(h('button', { class: 'btn', type: 'button', onclick: close(() => newDealModal(again, card)) }, t('deal.fromBooking')),
            run(t('book.extend'), async () => { await S.api.extendBooking(card.id); S.toast(t('book.extended'), 'ok'); md.close(); again(); }, 'ghost'),
            run(t('apt.unbook'), async () => { const r = await S.askReason(t('book.cancelTitle'), t('book.cancelReason')); if (r === null) return; await S.api.cancelBooking(card.id, r); S.toast(t('common.saved'), 'ok'); md.close(); again(); }, 'danger'));
        }
        if (card && card.kind === 'deal') actions.push(h('button', { class: 'btn', type: 'button', onclick: close(() => dealDrawer(card.dealId, again)) }, t('apt.openDeal')));
      }
      body.replaceChildren(
        h('div', { class: 'row between', style: { paddingRight: '40px' } }, h('h2', { style: { margin: 0 } }, t('apt.title', { n: a.number })), S.statusChip(a.status)),
        h('dl', { class: 'kv' }, kv(t('floor.nLabel'), a.floor === 0 ? t('floor.commercial') : a.floor), kv(t('f.rooms'), S.fmt.rooms(a.rooms)), kv(t('f.area'), S.fmt.area(a.area)), kv(t('cat.pps'), S.fmt.money(a.pricePerSqm, 'TJS')), kv(t('deal.price'), h('b', null, S.fmt.money(a.price, 'TJS')))),
        card ? h('div', { class: 'card flat stack' }, h('h3', null, t(card.kind === 'booking' ? 'apt.booking' : 'apt.deal')),
          h('dl', { class: 'kv' }, kv(t('deal.customer'), pickName(card)), kv(t('f.phone'), card.subtitle || card.phone),
            kv(t('apt.since'), S.fmt.dt(card.createdAt)), card.kind === 'booking' ? kv(t('book.left'), card.expiresAt ? S.cd(card.expiresAt) : '—') : kv(t('deal.stage'), S.stageChip(stage)))) : null,
        a.status === 'booked' && !card && sales() ? h('p', { class: 'hint' }, t('apt.noCard')) : null,
        actions.length ? h('div', { class: 'row apt-actions' }, actions) : null);
    }
    const pickName = (c) => c.clientName || c.customerName || c.name || c.title || '—';

    function bookForm(a, done) {
      const name = S.input('name', { autocomplete: 'off' }), phone = S.input('phone', { type: 'tel', value: '+992' }), cm = h('textarea', { class: 'inp', name: 'comment', id: 'book-cm', rows: 2 });
      const form = h('form', { class: 'stack', id: 'bookform', novalidate: true }, h('p', { class: 'muted' }, t('apt.bookText', { n: a.number })), S.field(t('f.fullName'), name), S.field(t('f.phone'), phone, t('f.phoneHint')), S.field(t('f.comment'), cm, null, 'book-cm'));
      const btn = h('button', { class: 'btn', type: 'submit', form: 'bookform' }, t('apt.book'));
      const md = S.modal({ title: t('apt.bookTitle', { n: a.number }), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try {
        await S.api.book(a.id, { name: name.value.trim(), phone: phone.value.replace(/[\s()-]/g, ''), comment: cm.value.trim() });
        S.toast(t('apt.booked'), 'ok'); md.close(); done && done();
      } catch (er) { S.showErrors(form, er); } }); });
    }

    async function newDealModal(done, booking) {
      const holder = h('div', null, S.skeleton(3)); const md = S.modal({ title: t('deal.new'), body: holder });
      try {
        const [customers, bs] = await Promise.all([S.api.customers(''), S.api.buildings()]);
        const perBuilding = await Promise.all(bs.map(async (b) => (await S.api.allApartments(await S.api.floors(b.id))).map((a) => Object.assign(a, { buildingName: b.name }))));
        const apts = perBuilding.flat().filter((a) => a.status === 'free' || (booking && String(a.id) === String(booking.apartmentId)));
        const cust = S.select('customerId', [['', t('deal.pickCustomer')]].concat(customers.map((c) => [c.id, c.fullName + ' · ' + c.phone])), '');
        const apt = S.select('apartmentId', [['', t('deal.pickApt')]].concat(apts.sort((a, b) => a.number - b.number).map((a) => [a.id, (bs.length > 1 ? a.buildingName + ' · ' : '') + '№ ' + a.number + ' · ' + t('floor.n', { n: a.floor }) + ' · ' + S.fmt.rooms(a.rooms) + ' · ' + S.fmt.money(a.price, 'TJS')])), booking && booking.apartmentId ? booking.apartmentId : '');
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

    // Invite a person by e-mail: they get a link and then sign in with a code. A building admin invites only into their building.
    // Money from the statistics: in somoni when it can be counted, otherwise by currency ("120 000 TJS + 5 000 USD").
    const statMoney = (m) => (m && m.tjs !== null && m.tjs !== undefined ? S.fmt.money(m.tjs, 'TJS')
      : m && m.byCurrency && m.byCurrency.length ? m.byCurrency.map((x) => S.fmt.money(x.amount, x.currency)).join(' + ') : S.fmt.money(0, 'TJS'));
    const statTjs = (m) => (m && m.tjs !== null && m.tjs !== undefined ? +m.tjs : null);
    const canExport = () => S.api.isOwner() || (S.api.user.roles || []).includes('Accountant');

    // Excel export for a period: the current year up to today by default.
    function exportRange() {
      const today = new Date().toISOString().slice(0, 10);
      const from = h('input', { type: 'date', class: 'input', value: today.slice(0, 4) + '-01-01', max: today, 'aria-label': t('stats.from') });
      const to = h('input', { type: 'date', class: 'input', value: today, max: today, 'aria-label': t('stats.to') });
      return h('span', { class: 'export-range' }, from, '—', to,
        h('button', { class: 'btn ghost', onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/stats/export', { from: from.value || undefined, to: to.value || undefined }, 'Excel').catch(S.err)) }, t('stats.export')));
    }

    // The overview's statistics: apartments, contract money (a CSS ring), sales by month (CSS bars), overdue payments.
    // One KPI tile. o: { gold, fmt, note, delta: { up, text }, series } — the number counts up (800 ms, ease-out) when the
    // tile first comes into view; the sparkline is drawn when there is a series of at least two points.
    function kpiCard(icon, label, n, o) {
      o = o || {};
      const fmt = o.fmt || ((v) => S.fmt.n(v));
      const num = h('div', { class: 'kpi-num' + (o.gold ? ' gold' : '') }, fmt(0));
      const spark = o.series && o.series.length > 1 ? h('canvas', { class: 'kpi-spark', 'aria-hidden': 'true' }) : null;
      const el = h('div', { class: 'card kpi-card' },
        h('div', { class: 'kpi-icon', 'aria-hidden': 'true' }, icon), num, h('div', { class: 'kpi-label' }, label),
        o.delta ? h('span', { class: 'kpi-delta ' + (o.delta.up ? 'up' : 'down') }, o.delta.text) : o.note ? h('div', { class: 'kpi-note' }, o.note) : null,
        spark);
      const reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      const run = () => {
        if (spark) drawSparkline(spark, o.series, getComputedStyle(document.documentElement).getPropertyValue('--gold').trim() || '#c9a227');
        if (reduce) { num.textContent = fmt(n); return; }
        const t0 = performance.now();
        let finished = false;
        const step = (now) => { if (finished) return; const p = Math.min(1, (now - t0) / 800); num.textContent = fmt(Math.round(n * (1 - Math.pow(1 - p, 3)))); if (p < 1) requestAnimationFrame(step); else finished = true; };
        requestAnimationFrame(step);
        // Without frames (a background tab) the final number still appears.
        setTimeout(() => { finished = true; num.textContent = fmt(n); }, 1100);
      };
      if ('IntersectionObserver' in window) {
        const io = new IntersectionObserver((es) => { if (es.some((e) => e.isIntersecting)) { io.disconnect(); run(); } }, { threshold: 0.3 });
        requestAnimationFrame(() => io.observe(el));
      } else requestAnimationFrame(run);
      return el;
    }
    // Sparkline: the line of the series, a soft fill under it and a dot on the last point.
    function drawSparkline(canvas, data, color) {
      const W = Math.max(80, canvas.parentNode.clientWidth - 40), H = 60, dpr = Math.min(window.devicePixelRatio || 1, 2);
      canvas.width = W * dpr; canvas.height = H * dpr; canvas.style.width = W + 'px'; canvas.style.height = H + 'px';
      const ctx = canvas.getContext('2d'); ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      const max = Math.max(...data), min = Math.min(...data), range = max - min || 1;
      const pts = data.map((v, i) => [(i / (data.length - 1)) * (W - 6) + 3, H - ((v - min) / range) * H * 0.8 - H * 0.1]);
      ctx.beginPath(); ctx.moveTo(pts[0][0], H); pts.forEach(([x, y]) => ctx.lineTo(x, y)); ctx.lineTo(pts[pts.length - 1][0], H); ctx.closePath();
      const grad = ctx.createLinearGradient(0, 0, 0, H); grad.addColorStop(0, color + '40'); grad.addColorStop(1, color + '00');
      ctx.fillStyle = grad; ctx.fill();
      ctx.beginPath(); ctx.moveTo(pts[0][0], pts[0][1]); pts.forEach(([x, y]) => ctx.lineTo(x, y)); ctx.strokeStyle = color; ctx.lineWidth = 2; ctx.lineJoin = 'round'; ctx.stroke();
      const [lx, ly] = pts[pts.length - 1]; ctx.beginPath(); ctx.arc(lx, ly, 3, 0, Math.PI * 2); ctx.fillStyle = color; ctx.fill();
    }

    // Sales analytics on the dashboard (Canvas 2D, no chart library): deals per month — bars grow in turn, the current month
    // in gold, a tooltip with the month, deals, completed and money; the funnel leads → bookings → deals → completed.
    // `money` — rows of /stats/sales (month, deals, revenue) for the tooltip's sums.
    function salesCharts(money) {
      const months = h('canvas', { class: 'chart-canvas', role: 'img', 'aria-label': t('an.months') });
      const funnel = h('canvas', { class: 'chart-canvas', role: 'img', 'aria-label': t('an.funnel') });
      const tip = h('div', { class: 'chart-tip', hidden: true });
      const box = h('div', { class: 'grid g2 an-grid' },
        h('div', { class: 'card' }, h('h3', null, t('an.months')), h('div', { class: 'chart-box' }, months, tip),
          h('p', { class: 'hint chart-legend' }, h('i', { class: 'lg-a' }), t('wow.an.current'), h('i', { class: 'lg-b' }), t('wow.an.past'))),
        h('div', { class: 'card' }, h('h3', null, t('an.funnel')), h('div', { class: 'chart-box' }, funnel)));
      S.api.salesAnalytics().then((data) => {
        let animated = false;
        const draw = (animate) => {
          if (!box.isConnected) { window.removeEventListener('resize', onResize); return; }
          drawMonths(months, data.months, animate); drawFunnel(funnel, data.funnel);
        };
        const onResize = S.debounce(() => draw(false), 150);
        window.addEventListener('resize', onResize);
        requestAnimationFrame(() => { draw(!animated); animated = true; });
        // Tooltip: the month under the pointer.
        const byMonth = {}; (money || []).forEach((r) => { byMonth[r.month] = r; });
        const names = t('top.months').split(',');
        months.addEventListener('pointermove', (e) => {
          const r = months.getBoundingClientRect(), slot = (r.width - 34) / data.months.length;
          const i = Math.floor((e.clientX - r.left - 28) / slot);
          const row = data.months[i];
          if (!row) { tip.hidden = true; return; }
          const m = byMonth[row.month];
          tip.replaceChildren(h('b', null, names[+row.month.slice(5) - 1] + ' ' + row.month.slice(0, 4)),
            h('span', null, t('an.signed') + ': ' + row.deals), h('span', null, t('an.completed') + ': ' + row.completed),
            m ? h('span', null, t('wow.an.money') + ': ' + statMoney(m.revenue)) : null);
          tip.hidden = false;
          const x = Math.min(r.width - 150, Math.max(0, e.clientX - r.left + 12));
          tip.style.left = x + 'px'; tip.style.top = Math.max(0, e.clientY - r.top - 70) + 'px';
        });
        months.addEventListener('pointerleave', () => { tip.hidden = true; });
      }).catch((e) => box.replaceChildren(h('div', { class: 'form-err' }, e.message)));
      return box;
    }
    // A canvas the width of its box, sharp on high-density screens; the colours follow the theme.
    function prepare(canvas, height) {
      const w = Math.max(240, canvas.parentNode.clientWidth), dpr = Math.min(window.devicePixelRatio || 1, 2);
      canvas.width = w * dpr; canvas.height = height * dpr; canvas.style.width = w + 'px'; canvas.style.height = height + 'px';
      const g = canvas.getContext('2d'); g.setTransform(dpr, 0, 0, dpr, 0, 0);
      const css = getComputedStyle(document.documentElement);
      return { g, w, h: height, ink: css.getPropertyValue('--ink-2').trim() || '#888', muted: css.getPropertyValue('--muted').trim() || '#888', line: css.getPropertyValue('--line').trim() || '#ccc', gold: css.getPropertyValue('--gold').trim() || '#c9a227' };
    }
    // Bars of signed deals: they grow from the bottom one after another (600 ms each, 50 ms apart); the last one is the current month.
    function drawMonths(canvas, rows, animate) {
      const reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      const t0 = performance.now(), D = 600, GAP = 50, total = D + GAP * rows.length;
      const frame = () => {
        const c = prepare(canvas, 220), g = c.g, padL = 28, padB = 26, top = 12;
        const max = Math.max(1, ...rows.map((r) => r.deals));
        const plotW = c.w - padL - 6, plotH = c.h - padB - top, slot = plotW / rows.length, bw = Math.max(6, slot * 0.56);
        g.font = '11px Inter, system-ui, sans-serif'; g.fillStyle = c.muted; g.strokeStyle = c.line; g.lineWidth = 1;
        [0, 0.5, 1].forEach((f) => { const y = top + plotH * (1 - f); g.beginPath(); g.moveTo(padL, y); g.lineTo(c.w - 6, y); g.stroke(); g.textAlign = 'right'; g.textBaseline = 'middle'; g.fillText(String(Math.round(max * f)), padL - 6, y); });
        const names = t('top.months').split(',');
        const el = performance.now() - t0;
        rows.forEach((r, i) => {
          const x = padL + slot * i + slot / 2;
          const p = !animate || reduce ? 1 : Math.max(0, Math.min(1, (el - i * GAP) / D));
          const k = 1 - Math.pow(1 - p, 3);
          const hh = (r.deals / max) * plotH * k;
          const last = i === rows.length - 1;
          g.fillStyle = last ? c.gold : c.muted; g.globalAlpha = last ? 1 : 0.45;
          const y = top + plotH - hh, rad = Math.min(4, bw / 2, hh);
          g.beginPath(); g.moveTo(x - bw / 2, top + plotH); g.lineTo(x - bw / 2, y + rad); g.quadraticCurveTo(x - bw / 2, y, x - bw / 2 + rad, y);
          g.lineTo(x + bw / 2 - rad, y); g.quadraticCurveTo(x + bw / 2, y, x + bw / 2, y + rad); g.lineTo(x + bw / 2, top + plotH); g.closePath(); g.fill();
          g.globalAlpha = 1;
          // Narrow chart: every other month, counted from the current one so that it always has its label.
          if (slot > 26 || (rows.length - 1 - i) % 2 === 0) { g.fillStyle = last ? c.gold : c.muted; g.textAlign = 'center'; g.textBaseline = 'top'; g.fillText(names[+r.month.slice(5) - 1] || r.month.slice(5), x, top + plotH + 6); }
        });
        if (animate && !reduce && el < total && canvas.isConnected) requestAnimationFrame(frame);
      };
      frame();
    }
    // Funnel: one rounded bar per step, centred, its width by the count, a slope down to the next one; blue, amber, gold, green.
    function drawFunnel(canvas, steps) {
      const COLORS = { leads: '#3b82f6', bookings: '#f59e0b', deals: '#c9a227', completed: '#22c55e' };
      const c = prepare(canvas, 220), g = c.g, W = c.w, H = c.h, n = steps.length, rowH = H / n, gap = 8, barH = rowH - gap;
      const maxCount = Math.max(1, ...steps.map((s) => s.count));
      // The narrowest bar still holds its label.
      const width = (s) => Math.max(W * 0.34, (s.count / maxCount) * W * 0.9);
      g.font = '700 13px Inter, system-ui, sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
      steps.forEach((s, i) => {
        const y = i * rowH, w = width(s), x = (W - w) / 2;
        if (i < n - 1) {
          const nw = width(steps[i + 1]), nx = (W - nw) / 2;
          g.globalAlpha = 0.25; g.fillStyle = COLORS[s.key] || c.gold;
          g.beginPath(); g.moveTo(x + 8, y + barH); g.lineTo(x + w - 8, y + barH); g.lineTo(nx + nw - 8, y + barH + gap); g.lineTo(nx + 8, y + barH + gap); g.closePath(); g.fill();
          g.globalAlpha = 1;
        }
        g.fillStyle = COLORS[s.key] || c.gold;
        g.beginPath();
        if (g.roundRect) g.roundRect(x, y, w, barH, 8); else g.rect(x, y, w, barH);
        g.fill();
        g.fillStyle = '#fff';
        g.fillText(t('an.step.' + s.key) + ' · ' + s.count + ' (' + s.percent + '%)', W / 2, y + barH / 2);
      });
    }

    function statsBlock(st, rows) {
      const total = statTjs(st.totalRevenue), got = statTjs(st.collectedRevenue);
      const pct = total ? Math.min(100, Math.round(((got || 0) * 100) / total)) : 0;
      const ring = h('div', { class: 'money-ring', style: { '--p': pct }, role: 'img', 'aria-label': t('stats.collectedPct', { p: pct }) }, h('b', null, pct + '%'), h('small', null, t('stats.collected')));
      const months = t('stats.months').split(',');
      const value = (row) => statTjs(row.revenue) || 0;
      const max = Math.max(...rows.map(value), 0);
      const bars = h('div', { class: 'sales-bars', role: 'list' }, rows.map((row) => {
        const [y, m] = row.month.split('-').map(Number);
        const height = max ? Math.max(4, Math.round((value(row) * 100) / max)) : 4;
        return h('div', { class: 'sales-bar', role: 'listitem', title: t('stats.barTitle', { m: months[m - 1] + ' ' + y, d: row.deals, s: statMoney(row.revenue) }) },
          h('span', { class: 'sales-bar-n' }, row.deals), h('i', { style: { height: height + '%' } }), h('small', null, months[m - 1]));
      }));
      const head = h('div', { class: 'toolbar' },
        st.overduePayments > 0 ? S.chip('Overdue', t('stats.overdue', { n: st.overduePayments })) : S.chip('Active ok', t('stats.noOverdue')),
        h('span', { class: 'hint' }, t('stats.week', { l: st.newLeadsThisWeek, d: st.newDealsThisMonth, c: Math.round((+st.conversionRate || 0) * 100) })),
        h('span', { class: 'spacer' }),
        canExport() ? exportRange() : null);
      // KPI tiles: an icon, the number counting up, a note under it; where a monthly series exists — the change against the
      // previous month and a sparkline of the last 7 months. Apartment statuses have no history: their note is the share of all.
      const share = (n) => (st.totalApartments ? t('wow.kpi.share', { p: Math.round((n * 100) / st.totalApartments) }) : null);
      const last7 = rows.slice(-7);
      const dealsSeries = last7.map((r) => r.deals), moneySeries = last7.map(value);
      const change = (series) => {
        if (series.length < 2) return null;
        const cur = series[series.length - 1], prev = series[series.length - 2];
        if (!prev) return null;
        const p = Math.round(((cur - prev) * 100) / prev);
        return { up: p >= 0, text: (p >= 0 ? '↑ +' : '↓ ') + p + '% ' + t('wow.kpi.month') };
      };
      const thisMonth = rows.length ? rows[rows.length - 1] : null;
      return [head,
        h('div', { class: 'kpis' },
          kpiCard('🏢', t('stats.total'), st.totalApartments, { note: t('wow.kpi.inBuildings') }),
          kpiCard('🟢', t('status.free'), st.free, { note: share(st.free) }),
          kpiCard('🟡', t('status.booked'), st.booked, { note: share(st.booked) }),
          kpiCard('🔑', t('status.sold'), st.sold, { gold: true, note: share(st.sold) })),
        rows.length ? h('div', { class: 'kpis' },
          kpiCard('📋', t('wow.kpi.deals'), thisMonth ? thisMonth.deals : 0, { delta: change(dealsSeries), series: dealsSeries }),
          kpiCard('💰', t('wow.kpi.money'), thisMonth ? value(thisMonth) : 0, { gold: true, fmt: (v) => S.fmt.money(v, 'TJS'), delta: change(moneySeries), series: moneySeries }),
          kpiCard('📥', t('wow.kpi.leads'), st.newLeadsThisWeek || 0, { note: t('wow.kpi.week') }),
          kpiCard('🎯', t('wow.kpi.conversion'), Math.round((+st.conversionRate || 0) * 100), { fmt: (v) => v + '%', note: t('wow.kpi.convNote') })) : null,
        h('div', { class: 'grid g2' },
          h('div', { class: 'card' }, h('h3', null, t('stats.money')), h('div', { class: 'money-wrap' }, ring,
            h('dl', { class: 'kv money-legend' }, h('dt', null, t('stats.contracts')), h('dd', null, statMoney(st.totalRevenue)),
              h('dt', null, h('i', { class: 'dot gold' }), t('stats.collected')), h('dd', null, statMoney(st.collectedRevenue)),
              h('dt', null, h('i', { class: 'dot' }), t('stats.remaining')), h('dd', null, statMoney(st.remaining)))),
            total === null ? h('p', { class: 'hint' }, t('dash.usdNoRate')) : null),
          h('div', { class: 'card' }, h('h3', null, t('stats.sales')), rows.length ? bars : S.empty(t('stats.noSales'), 'chart'), h('p', { class: 'hint' }, t('stats.salesHint'))))];
    }

    // A building without floors (created before floors were made automatically): offer to create 1..N in one go.
    function noFloors(b, done) {
      const btn = h('button', { class: 'btn', onclick: () => S.busy(btn, async () => {
        try { await S.api.createFloors(b.id, b.floorsCount, (n) => { btn.lastChild.textContent = t('wiz.floorsN', { n, c: b.floorsCount }); }); S.toast(t('common.saved'), 'ok'); done(); } catch (er) { S.err(er); }
      }) }, h('span', null, t('obj.makeFloors', { n: b.floorsCount })));
      return h('div', { class: 'card stack', style: { alignItems: 'flex-start' } }, S.empty(t('obj.noFloors'), 'building'), btn);
    }

    function userForm(done, presetBuilding) {
      const u = S.api.user; const bs = S.crmBuildings || [];
      const roles = (S.api.isOwner() ? STAFF_POSITIONS : STAFF_POSITIONS.filter((r) => r !== 'ObjectAdmin'));
      const first = S.input('firstName', { autocomplete: 'off' }), last = S.input('lastName', { autocomplete: 'off' });
      const email = S.input('email', { type: 'email' }), role = S.select('role', roles.map((r) => [r, t('role.' + r)]), 'Manager');
      const own = u.buildingIds.length ? bs.filter((b) => u.buildingIds.includes(b.id)) : bs;
      const boxes = h('div', { class: 'stack', style: { gap: '6px' } }, own.map((b) => h('label', { class: 'row', style: { gap: '8px', flexWrap: 'nowrap' } }, h('input', { type: 'checkbox', value: b.id, checked: own.length === 1 || String(b.id) === String(presetBuilding) }), b.name)));
      const hint = h('div', { class: 'hint' });
      const syncHint = () => { hint.textContent = role.value === 'ObjectAdmin' ? t('inv.oneBuilding') : t('inv.allHint'); };
      role.addEventListener('change', syncHint); syncHint();
      const form = h('form', { class: 'stack', id: 'userform', novalidate: true }, h('div', { class: 'grid g2' }, S.field(t('reg.firstName'), first), S.field(t('reg.lastName'), last)),
        S.field(t('login.email'), email, t('inv.emailHint')), S.field(t('users.role'), role),
        h('div', { class: 'field', dataset: { name: 'buildingIds' } }, h('label', null, t('users.buildings')), boxes, hint, h('div', { class: 'ferr' })));
      const btn = h('button', { class: 'btn', type: 'submit', form: 'userform' }, t('inv.send'));
      const md = S.modal({ title: t('inv.new'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try {
        const buildingIds = S.$$('input[type=checkbox]:checked', boxes).map((c) => c.value);
        await S.api.invite({ firstName: first.value.trim(), lastName: last.value.trim(), email: email.value.trim(), role: role.value, buildingIds });
        S.toast(t('inv.sent', { e: email.value.trim() }), 'ok'); md.close(); done && done(); } catch (er) { S.showErrors(form, er); } }); });
    }

    // Owner only: which buildings a person works in (empty — all). Their sessions end, new rights apply on the next sign-in.
    function buildingsForm(u, done) {
      const bs = S.crmBuildings || [];
      const boxes = h('div', { class: 'stack', style: { gap: '6px' } }, bs.map((b) => h('label', { class: 'row', style: { gap: '8px', flexWrap: 'nowrap' } }, h('input', { type: 'checkbox', value: b.id, checked: u.buildingIds.includes(b.id) }), b.name)));
      const form = h('form', { class: 'stack', id: 'bldform', novalidate: true }, h('p', { class: 'muted' }, u.name + ' · ' + u.roles.map((r) => t('role.' + r)).join(', ')),
        h('div', { class: 'field', dataset: { name: 'buildingIds' } }, boxes, h('div', { class: 'hint' }, u.roles.includes('ObjectAdmin') ? t('inv.oneBuilding') : t('inv.allHint')), h('div', { class: 'ferr' })), h('p', { class: 'hint' }, t('users.buildingsNote')));
      const btn = h('button', { class: 'btn', type: 'submit', form: 'bldform' }, t('common.save'));
      const md = S.modal({ title: t('users.setBuildings'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await S.api.setUserBuildings(u.id, S.$$('input:checked', boxes).map((c) => c.value)); S.toast(t('common.saved'), 'ok'); md.close(); done && done(); } catch (er) { S.showErrors(form, er); } }); });
    }

    // Owner only: a new building of the organization.
    function buildingForm(done) {
      const nameRu = S.input('nameRu'), nameTg = S.input('nameTg'), addr = S.input('address'), floors = S.input('floorsCount', { type: 'number', min: 1, max: 100, value: 9 });
      const form = h('form', { class: 'stack', id: 'newbform', novalidate: true }, h('div', { class: 'grid g2' }, S.field(t('cat.nameRu'), nameRu), S.field(t('cat.nameTg'), nameTg)), S.field(t('f.address'), addr), S.field(t('cat.floorsCount'), floors));
      const btn = h('button', { class: 'btn', type: 'submit', form: 'newbform' }, t('common.save'));
      const md = S.modal({ title: t('cat.newBuilding'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { const b = await S.api.createBuilding({ nameRu: nameRu.value.trim(), nameTg: nameTg.value.trim() || nameRu.value.trim(), address: addr.value.trim(), floorsCount: +floors.value }); await S.api.createFloors(b.id, b.floorsCount || +floors.value); S.crmBuildings = null; S.store.set('sitora.crmBuilding', b.id); S.toast(t('common.saved'), 'ok'); md.close(); done && done(); } catch (er) { S.showErrors(form, er); } }); });
    }

    // The building the catalog and construction sections work on (remembered between visits).
    function pickBuilding(bs) {
      const saved = S.store.get('sitora.crmBuilding');
      return (bs.find((b) => String(b.id) === String(saved)) || bs[0]).id;
    }
    function buildingBar(bs, bid, tab) {
      if (bs.length < 2 && !S.api.isOwner()) return null;
      const sel = S.select('building', bs.map((b) => [b.id, b.name]), bid, { 'aria-label': t('cust.building') });
      sel.addEventListener('change', () => { S.store.set('sitora.crmBuilding', sel.value); S.crmGo(tab); });
      return h('div', { class: 'toolbar' }, bs.length > 1 ? h('div', { style: { minWidth: '240px' } }, S.field(t('cust.building'), sel)) : null, h('span', { class: 'spacer' }),
        S.api.isOwner() ? h('button', { class: 'btn ghost', onclick: () => buildingForm(() => S.crmGo(tab)) }, t('cat.newBuilding')) : null);
    }

    /* ---------- deal drawer ---------- */
    // hint — a report row with the client and apartment: an accountant cannot open the deal card itself.
    function dealDrawer(id, done, hint, firstTab) {
      let d = null, sched = null; let tab = firstTab || 'overview';
      // The AI conversation about this deal lives only while the card is open.
      const aiHistory = [];
      const body = h('div', null, S.skeleton(4, 60));
      const md = S.modal({ side: true, body, onClose: () => done && done(), noFocus: true });
      (async function init() {
        try { d = await S.api.deal(id, hint); } catch (e) { body.replaceChildren(h('div', { class: 'form-err' }, e.message)); return; }
        draw();
      })();
      async function reload() { d = await S.api.deal(id, hint); sched = null; draw(); }
      async function getSched() { if (sched) return sched; try { sched = await S.api.schedule(id); } catch (e) { sched = { none: true }; } return sched; }
      const tabsDef = () => [['overview', t('deal.tab.overview')], ['schedule', t('deal.tab.schedule')], ['payments', t('deal.tab.payments')], ['docs', t('deal.tab.docs')], ['chat', t('deal.tab.chat')], ['ai', t('deal.tab.ai')]];
      function draw() {
        const content = h('div', { class: 'stack' });
        body.replaceChildren(h('div', { class: 'row between', style: { paddingRight: '40px', marginBottom: '14px' } }, h('div', null, h('h2', { style: { margin: 0, padding: 0 } }, t('deal.title', { n: d.apartmentNumber })), h('div', { class: 'muted', style: { fontWeight: 600 } }, (d.customerName || '—') + ' · #' + shortId(d.id))), d.readOnly ? null : S.stageChip(d.stage)),
          h('div', { class: 'tabs' }, tabsDef().map(([k, l]) => h('button', { class: tab === k ? 'on' : '', onclick: () => { tab = k; draw(); } }, l))), content);
        panes[tab](content);
      }
      const panes = {
        overview(c) {
          const stageSel = S.select('stage', STAGES.map((s) => [s, t('stage.' + s)]), d.stage); const reason = S.input('reason', { placeholder: t('deal.reasonPh') });
          const canMove = S.api.has('Manager') && d.stage !== 'Completed' && d.stage !== 'Cancelled' && sales();
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
          if (!s.none) { c.replaceChildren(S.panels.schedule(s), h('div', { class: 'row' }, h('button', { class: 'btn ghost', onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/deals/' + id + '/schedule.pdf', null, 'PDF').catch(S.err)) }, t('cab.pdfSchedule')), h('button', { class: 'btn ghost', onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/deals/' + id + '/statement.pdf', { date: S.today() }, 'PDF').catch(S.err)) }, t('cab.pdfAct')), s.paid === 0 && sales() ? h('button', { class: 'btn soft', onclick: () => planForm(true) }, t('plan.rebuild')) : null)); return; }
          c.replaceChildren(sales() ? planFormEl() : S.empty(t('plan.none'), 'wallet'));
        },
        async payments(c) {
          c.replaceChildren(S.skeleton(3, 40));
          const canPay = money();
          const parts = [receiptCard(canPay)];
          if (canPay) {
            const sc = await getSched();
            if (sc.none) { c.replaceChildren(...parts, S.empty(t('pay.needPlan'), 'wallet')); return; }
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
        docs(c) { c.append(S.panels.documents(id, true, { verify: sales() })); },
        chat(c) { c.append(h('div', { class: 'card flat' }, S.panels.chat(id, 'staff'))); },
        // The assistant already knows this deal: the server puts the buyer, apartment, price, stage and payments into its prompt.
        ai(c) {
          const log = h('div', { class: 'chat ai-log', 'aria-live': 'polite' }, h('div', { class: 'muted', style: { padding: '14px', textAlign: 'center' } }, t('ai.deal.hint')));
          const input = h('textarea', { class: 'inp', id: 'ai-in-' + id, rows: 2, maxlength: 2000, placeholder: t('ai.deal.ph') });
          const btn = h('button', { class: 'btn', type: 'submit', 'aria-label': t('chat.send') }, S.icon('send'));
          const form = h('form', { class: 'row', style: { flexWrap: 'nowrap', marginTop: '10px', alignItems: 'flex-end' } }, input, btn);
          const add = (role, text) => { if (!aiHistory.length) log.replaceChildren(); log.appendChild(h('div', { class: 'msg' + (role === 'user' ? ' me' : '') }, text)); log.scrollTop = log.scrollHeight; };
          aiHistory.forEach((m) => log.appendChild(h('div', { class: 'msg' + (m.role === 'user' ? ' me' : '') }, m.content)));
          form.addEventListener('submit', async (e) => {
            e.preventDefault(); const text = input.value.trim(); if (!text) return;
            add('user', text); input.value = '';
            await S.busy(btn, async () => {
              try { const r = await S.api.aiChat(text, aiHistory.slice(), id); aiHistory.push({ role: 'user', content: text }, { role: 'assistant', content: r.reply }); add('assistant', r.reply); }
              catch (er) { log.appendChild(h('div', { class: 'form-err' }, er.message)); }
            });
          });
          input.addEventListener('keydown', (e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); form.requestSubmit(); } });
          c.append(h('div', { class: 'card flat' }, log, form, h('p', { class: 'hint' }, t('ai.deal.note'))));
          setTimeout(() => input.focus(), 30);
        },
      };
      const kv = (k, v) => [h('dt', null, k), h('dd', null, v === undefined || v === null || v === '' ? '—' : v)];

      // A photo of a bank receipt: the AI reads the amount, date and bank and finds the unpaid schedule line it pays.
      // The payment itself is posted only by the accountant's click.
      function receiptCard(canPay) {
        const out = h('div');
        const inp = h('input', { type: 'file', id: 'rcpt-' + id, accept: 'image/png,image/jpeg,image/gif,image/webp,application/pdf', hidden: true });
        inp.addEventListener('change', async () => {
          const f = inp.files[0]; inp.value = ''; if (!f) return;
          if (f.size > 10 * 1024 * 1024) { S.err({ message: t('ai.rc.tooBig') }); return; }
          out.replaceChildren(S.skeleton(2, 28));
          try {
            const r = await S.api.readReceipt(id, f); const rc = r.receipt;
            if (!rc.isReadable) { out.replaceChildren(h('div', { class: 'form-err' }, t('ai.rc.unread'))); return; }
            const items = [h('b', null, [t('ai.rc.amount') + ': ' + (rc.amount != null ? S.fmt.money(rc.amount, rc.currency || d.currency) : '—'),
              t('ai.rc.date') + ': ' + (rc.paymentDate ? S.fmt.date(rc.paymentDate) : '—'), t('ai.rc.bank') + ': ' + (rc.bankName || '—')].join(' | ')),
              rc.description ? h('div', { class: 'muted' }, rc.description) : null];
            if (r.suggestedScheduleItemId) {
              items.push(h('div', { class: 'hint' }, t('ai.rc.match', { d: S.fmt.date(r.suggestedDueDate), s: S.fmt.money(r.suggestedAmount, r.suggestedCurrency) })));
              items.push(canPay
                ? h('div', null, h('button', { class: 'btn', type: 'button', onclick: (e) => S.busy(e.currentTarget, async () => {
                  try {
                    await S.api.addPayment(id, { amount: rc.amount, currency: rc.currency || d.currency, date: rc.paymentDate && rc.paymentDate <= S.today() ? rc.paymentDate : S.today(), method: 'BankTransfer', comment: t('ai.rc.comment', { b: rc.bankName || '' }).trim() });
                    S.toast(t('pay.added'), 'ok'); await reload();
                  } catch (er) { S.err(er); }
                }) }, '✅ ' + t('ai.rc.confirm')))
                : h('div', { class: 'hint' }, t('ai.rc.toAccountant')));
            } else items.push(h('div', { class: 'hint' }, t('ai.rc.noMatch')));
            out.replaceChildren(h('div', { class: 'card flat stack' }, ...items));
          } catch (er) { out.replaceChildren(h('div', { class: 'form-err' }, er.message)); }
        });
        return h('div', { class: 'stack' }, h('div', { class: 'row' }, inp, h('label', { class: 'btn ghost', for: inp.id }, '📷 ' + t('ai.rc.upload')), h('span', { class: 'hint' }, t('ai.rc.hint'))), out);
      }
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

  /* ---------- first sign-in of a new owner ---------- */
  // Welcome → the first building (with its floors, so apartments can be added at once) → invite a manager and an accountant.
  const CITIES = ['dushanbe', 'khujand', 'bokhtar', 'other'];
  function wizard(box, finish) {
    const u = S.api.user;
    let building = null, floorsDone = 0;
    const steps = (n) => h('ol', { class: 'wiz-steps', 'aria-label': t('wiz.progress') }, [1, 2, 3].map((i) =>
      h('li', { class: i < n ? 'done' : i === n ? 'on' : '', 'aria-current': i === n ? 'step' : null }, h('b', null, i < n ? '✓' : String(i)), h('span', null, t('wiz.step' + i)))));
    const shell = (n, ...content) => { box.replaceChildren(h('div', { class: 'card wiz' }, steps(n), ...content)); window.scrollTo(0, 0); };

    function welcome() {
      shell(1, h('div', { class: 'wiz-hero' }, h('div', { class: 'wiz-ok', 'aria-hidden': 'true' }, '✓'), h('h1', null, t('wiz.welcome')),
        h('p', { class: 'muted' }, t('wiz.approved', { o: u.organizationName || '' })),
        h('button', { class: 'btn lg', type: 'button', onclick: buildingStep }, t('wiz.start'))));
    }

    function buildingStep() {
      const nameRu = S.input('nameRu', { placeholder: 'Некруз Резиденс' }), nameTg = S.input('nameTg', { placeholder: 'Некруз Резиденс' });
      const city = S.select('city', CITIES.map((c) => [c, t('wiz.city.' + c)]), 'dushanbe');
      const cityOther = S.input('cityOther');
      const other = h('div', { hidden: true }, S.field(t('wiz.cityName') + ' *', cityOther));
      city.addEventListener('change', () => { other.hidden = city.value !== 'other'; });
      const addr = S.input('address', { placeholder: t('wiz.addrPh') });
      const floors = S.input('floorsCount', { type: 'number', min: 1, max: 50, value: 12, inputmode: 'numeric' });
      const apts = S.input('plannedApartments', { type: 'number', min: 1, max: 1000, value: 96, inputmode: 'numeric' });
      const year = S.input('completionYear', { type: 'number', min: 2024, max: 2035, value: Math.min(2035, new Date().getFullYear() + 2), inputmode: 'numeric' });
      const desc = h('textarea', { class: 'inp', name: 'descriptionRu', id: 'wiz-desc', rows: 3, maxlength: 4000 });
      const btn = h('button', { class: 'btn lg', type: 'submit' }, h('span', null, t('wiz.create')));
      const form = h('form', { class: 'stack', novalidate: true },
        h('div', { class: 'grid g2' }, S.field(t('cat.nameRu') + ' *', nameRu), S.field(t('cat.nameTg') + ' *', nameTg, t('wiz.tgHint'))),
        h('div', { class: 'grid g2' }, S.field(t('wiz.city') + ' *', city), S.field(t('f.address') + ' *', addr)), other,
        h('div', { class: 'grid g3' }, S.field(t('cat.floorsCount') + ' *', floors, '1–50'), S.field(t('wiz.apts') + ' *', apts, '1–1000'), S.field(t('wiz.year') + ' *', year, '2024–2035')),
        S.field(t('wiz.desc'), desc, null, 'wiz-desc'),
        h('div', { class: 'row between' }, h('button', { class: 'btn ghost', type: 'button', onclick: welcome }, '← ' + t('wiz.back')), btn));
      const int = (el, min, max) => { const v = Number(el.value); return Number.isInteger(v) && v >= min && v <= max ? v : null; };
      form.addEventListener('submit', async (e) => {
        e.preventDefault(); S.showErrors(form, null);
        const errors = {};
        if (!nameRu.value.trim()) errors.nameRu = [t('wiz.errName')];
        if (!nameTg.value.trim()) errors.nameTg = [t('wiz.errName')];
        if (city.value === 'other' && !cityOther.value.trim()) errors.cityOther = [t('wiz.errCity')];
        if (!addr.value.trim()) errors.address = [t('wiz.errAddr')];
        if (int(floors, 1, 50) === null) errors.floorsCount = [t('wiz.errRange', { a: 1, b: 50 })];
        if (int(apts, 1, 1000) === null) errors.plannedApartments = [t('wiz.errRange', { a: 1, b: 1000 })];
        if (int(year, 2024, 2035) === null) errors.completionYear = [t('wiz.errRange', { a: 2024, b: 2035 })];
        if (Object.keys(errors).length) { S.showErrors(form, { message: t('wiz.errForm'), errors }); return; }
        const cityName = city.value === 'other' ? cityOther.value.trim() : t('wiz.city.' + city.value);
        await S.busy(btn, async () => {
          try {
            // A retry after a failure goes on from where it stopped instead of creating a second building.
            if (!building) {
              building = await S.api.createBuilding({ nameRu: nameRu.value.trim(), nameTg: nameTg.value.trim(), address: cityName + ', ' + addr.value.trim(), floorsCount: int(floors, 1, 50),
                plannedApartments: int(apts, 1, 1000), completionYear: int(year, 2024, 2035), descriptionRu: desc.value.trim() });
            }
            await S.api.createFloors(building.id, building.floorsCount, (n) => { floorsDone = n; btn.lastChild.textContent = t('wiz.floorsN', { n, c: building.floorsCount }); }, floorsDone + 1);
            S.crmBuildings = null; S.store.set('sitora.crmBuilding', building.id);
            inviteStep();
          } catch (er) { S.showErrors(form, er); }
        });
      });
      shell(2, h('h2', null, t('wiz.buildingTitle')), h('p', { class: 'muted' }, t('wiz.buildingSub')), form);
      setTimeout(() => nameRu.focus(), 30);
    }

    function inviteStep() {
      const card = (role) => {
        const first = S.input('firstName'), last = S.input('lastName'), email = S.input('email', { type: 'email', placeholder: 'name@company.tj', inputmode: 'email' });
        const btn = h('button', { class: 'btn', type: 'submit' }, t('wiz.invite'));
        const f = h('form', { class: 'card flat stack wiz-inv', novalidate: true }, h('h3', null, t('role.' + role)), h('p', { class: 'hint' }, t('wiz.inv.' + role)),
          S.field(t('reg.firstName'), first), S.field(t('reg.lastName'), last), S.field(t('login.email'), email), btn);
        f.addEventListener('submit', async (e) => {
          e.preventDefault(); S.showErrors(f, null);
          const errors = {};
          if (!first.value.trim()) errors.firstName = [t('wiz.errPerson')];
          if (!last.value.trim()) errors.lastName = [t('wiz.errPerson')];
          if (!/^\S+@\S+\.\S+$/.test(email.value.trim())) errors.email = [t('val.email')];
          if (Object.keys(errors).length) { S.showErrors(f, { message: t('wiz.errForm'), errors }); return; }
          await S.busy(btn, async () => {
            try {
              await S.api.invite({ firstName: first.value.trim(), lastName: last.value.trim(), email: email.value.trim(), role, buildingIds: [building.id] });
              f.replaceChildren(h('h3', null, t('role.' + role)), h('div', { class: 'wiz-sent', role: 'status' }, '✓ ' + t('wiz.sent', { e: email.value.trim() })));
            } catch (er) { S.showErrors(f, er); }
          });
        });
        return f;
      };
      shell(3, h('h2', null, t('wiz.created')), h('p', { class: 'muted' }, t('wiz.inviteSub', { b: building.name })),
        h('div', { class: 'grid g2' }, card('Manager'), card('Accountant')),
        h('div', { class: 'row between wiz-foot' }, h('button', { class: 'btn ghost', type: 'button', onclick: finish }, t('wiz.skip')), h('button', { class: 'btn', type: 'button', onclick: finish }, t('wiz.done'))));
    }

    welcome();
  }
})();
