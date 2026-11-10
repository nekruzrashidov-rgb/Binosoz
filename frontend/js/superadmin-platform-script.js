/* Super admin's section: platform dashboard, developers (organizations), users, reviews, tariffs,
   applications, working "as owner" of an organization, authenticator app. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  S.views = S.views || {};

  S.views.platform = function (root) {
    if (!S.api.isAuthed) return S.views.login(root);
    if (!S.api.isPlatform()) { root.appendChild(h('div', { class: 'wrap page-in' }, h('div', { class: 'card login-card login' }, h('h2', null, t('plat.only')), h('div', { class: 'row', style: { marginTop: '14px' } }, h('button', { class: 'btn', onclick: () => S.go(S.homeFor(S.api.user)) }, t('cab.toCrm')))))); return () => {}; }
    const page = h('div', { class: 'page-in' }); root.appendChild(page);
    const items = [
      { key: 'dashboard', label: t('plat.dashboard'), icon: 'chart' }, { key: 'orgs', label: t('plat.orgs'), icon: 'building' },
      { key: 'users', label: t('plat.usersTitle'), icon: 'users' }, { key: 'reviews', label: t('plat.reviews'), icon: 'star' },
      { key: 'tariffs', label: t('plat.tariffs'), icon: 'wallet' }, { key: 'apps', label: t('apps.title'), icon: 'inbox' },
      { key: 'activity', label: t('pact.title'), icon: 'clock' }, { key: 'notify', label: t('pnot.titleNav'), icon: 'bell' },
      { key: 'psettings', label: t('pset.title'), icon: 'gear' },
      { key: 'security', label: t('plat.security'), icon: 'shield' }];
    // The number of applications waiting for a decision, on the section's label.
    let focusOrg = null;
    const appsItem = items.find((i) => i.key === 'apps');
    const countPending = () => S.api.applications('Pending').then((l) => { appsItem.badge = l.length || null; ws.redraw(); }).catch(() => {});
    const ws = S.workspace(items, 'dashboard', (k, main) => { S.clear(main); main.append(h('h1', null, items.find((i) => i.key === k).label), h('p', { class: 'sub' }, t('plat.sub.' + k))); const body = h('div', { class: 'stack' }); main.appendChild(body); views[k](body); });
    page.appendChild(ws.el);
    queueMicrotask(() => ws.go('dashboard'));
    countPending();

    const money = (v) => S.fmt.money(v, 'TJS');
    // Long values (sums with the currency) get a smaller font so they stay on one line, as in the CRM.
    const kpi = (label, value, color, sub) => h('div', { class: 'card kpi', style: { '--k': color } }, h('span', null, label),
      h('b', { style: String(value).length > 8 ? { fontSize: '22px' } : null }, value), sub ? h('div', { class: 'muted' }, sub) : null, h('i'));
    const fail = (holder, e) => holder.replaceChildren(h('div', { class: 'form-err' }, e.message));
    const stars = (n) => h('span', { class: 'stars', title: n + '/5', 'aria-label': n + '/5' }, '★'.repeat(n) + '☆'.repeat(5 - n));
    const tariffChip = (tr) => (tr ? S.chip(tr.isPaid ? 'good' : 'warn', tr.tariffName + (tr.isPaid ? '' : ' · ' + t('plat.unpaid'))) : h('span', { class: 'muted' }, t('plat.noTariff')));
    let chart = null;

    const views = {
      // Platform totals, deals by month (Chart.js from cdnjs) and the five organizations with the most deals.
      dashboard(body) {
        const holder = h('div', { class: 'stack' }, S.skeleton(3, 90));
        body.replaceChildren(holder);
        (async () => {
          try {
            const d = await S.api.platformDashboard();
            const canvas = h('canvas', { height: 260, role: 'img', 'aria-label': t('plat.dealsByMonth') });
            holder.replaceChildren(
              h('div', { class: 'kpis' },
                kpi(t('plat.kpiOrgs'), S.fmt.n(d.totalOrganizations), 'var(--accent)', t('plat.kpiOrgsSub', { a: S.fmt.n(d.activeOrganizations), n: S.fmt.n(d.newOrganizationsMonth) })),
                kpi(t('plat.kpiBuildings'), S.fmt.n(d.totalBuildings), 'var(--info)'),
                kpi(t('plat.kpiDeals'), S.fmt.n(d.totalDeals), 'var(--good)', t('plat.kpiNewMonth', { n: S.fmt.n(d.newDealsMonth) })),
                kpi(t('plat.kpiRevenue'), money(d.platformRevenueMonth), 'var(--gold)')),
              h('div', { class: 'kpis' },
                kpi(t('plat.kpiPayments'), money(d.totalPaymentsSum), 'var(--good)'),
                kpi(t('plat.kpiCustomers'), S.fmt.n(d.totalCustomers), 'var(--info)'),
                kpi(t('plat.kpiStaff'), S.fmt.n(d.totalStaff), 'var(--accent)')),
              h('div', { class: 'card' }, h('h3', null, t('plat.dealsByMonth')), h('div', { class: 'plat-chart' }, canvas)),
              h('div', { class: 'card' }, h('h3', null, t('plat.topOrgs')), S.tbl([
                { label: '#', render: (o) => d.topOrganizations.indexOf(o) + 1 },
                { label: t('plat.name'), render: (o) => h('b', null, o.name) },
                { label: t('plat.deals'), r: true, num: true, render: (o) => S.fmt.n(o.dealsCount) },
                { label: t('plat.paymentsSum'), r: true, num: true, render: (o) => money(o.paymentsSum) }], d.topOrganizations, { empty: t('plat.none') })));
            drawChart(canvas, d.dealsByMonth);
          } catch (e) { fail(holder, e); }
        })();
      },

      orgs(body) {
        const holder = h('div');
        const search = S.input('search', { type: 'search', placeholder: t('plat.search'), 'aria-label': t('plat.search') });
        let list = [], focus = null;
        // The logo is served by the API: a relative address needs the API's host when the site is elsewhere.
        const src = (u) => (u && u.charAt(0) === '/' ? S.apiBase().replace(/\/api\/v1\/?$/, '') + u : u);
        const own = (fn) => (e) => { e.stopPropagation(); fn(e); };
        const toggle = (o) => own((e) => S.busy(e.currentTarget, async () => {
          if (o.active && !(await S.confirm(t('plat.suspendText', { o: o.name }), t('plat.suspend'), true))) return;
          try { await S.api.setOrganizationActive(o.id, !o.active); S.toast(t(o.active ? 'plat.suspended' : 'plat.activated', { o: o.name }), 'ok'); load(); } catch (er) { S.err(er); }
        }));
        const draw = () => {
          const q = search.value.trim().toLowerCase();
          const rows = list.filter((o) => !q || o.name.toLowerCase().includes(q) || o.slug.includes(q) || o.owners.some((e) => e.toLowerCase().includes(q)));
          holder.replaceChildren(S.tbl([
            { label: t('plat.logo'), render: (o) => (o.logoUrl ? h('img', { class: 'org-logo', src: src(o.logoUrl), alt: '' }) : h('span', { class: 'avatar org-logo' }, S.fmt.initials(o.name))) },
            { label: t('plat.name'), render: (o) => h('div', null, h('b', null, o.name), h('div', { class: 'hint' }, o.ownerInfos.length ? o.ownerInfos[0].fullName + (o.ownerInfos[0].email ? ' · ' + o.ownerInfos[0].email : '') : t('plat.noOwner'))) },
            { label: t('plat.slug'), render: (o) => h('code', null, o.slug) },
            { label: t('deal.stage'), render: (o) => S.chip(o.active ? 'Active ok' : 'Blocked', t(o.active ? 'plat.active' : 'plat.off')) },
            { label: t('plat.tariff'), render: (o) => tariffChip(o.tariff) },
            { label: t('plat.buildings'), r: true, num: true, render: (o) => o.buildings },
            { label: t('plat.deals'), r: true, num: true, render: (o) => o.deals },
            { label: t('plat.customers'), r: true, num: true, render: (o) => o.customers },
            { label: t('plat.createdAt'), render: (o) => S.fmt.date(o.createdAt) },
            { label: '', r: true, render: (o) => h('div', { class: 'row', style: { gap: '6px', justifyContent: 'flex-end', flexWrap: 'nowrap' } },
              o.active ? h('button', { class: 'btn soft sm', onclick: own((e) => S.busy(e.currentTarget, async () => { try { await S.api.impersonate(o.id); S.toast(t('imp.started', { o: o.name }), 'ok'); S.go('crm'); } catch (er) { S.err(er); } })) }, t('plat.enter')) : null,
              h('button', { class: 'btn ghost sm', onclick: own(() => inviteOwnerForm(o, load)) }, t('plat.inviteOwner')),
              h('button', { class: 'btn sm ' + (o.active ? 'ghost' : ''), onclick: toggle(o) }, t(o.active ? 'plat.suspend' : 'plat.activate'))) },
          ], rows, { empty: t(q ? 'plat.noMatch' : 'plat.none'), rowClass: (o) => 'app-row has-note' + (o.id === focus ? ' row-focus' : ''), onRow: (o) => orgCard(o.id, load) }));
        };
        const load = async () => {
          holder.replaceChildren(S.skeleton(4, 38));
          try {
            list = await S.api.organizations();
            // Arrived from an approved application: its organization is highlighted and scrolled into view.
            if (focusOrg) { focus = focusOrg; focusOrg = null; }
            draw();
            const row = S.$('.row-focus', holder); if (row) row.scrollIntoView({ block: 'center' });
          } catch (e) { fail(holder, e); }
        };
        search.addEventListener('input', draw);
        body.replaceChildren(h('p', { class: 'hint' }, t('plat.enterHint')), h('div', { class: 'toolbar' }, h('div', { style: { minWidth: '240px', flex: '1', maxWidth: '380px' } }, search), h('span', { class: 'spacer' }), h('button', { class: 'btn', onclick: () => orgForm(load) }, t('plat.new'))), holder);
        load();
      },

      // Everyone on the platform: filter by role and organization, search; block, unblock, a new password by e-mail.
      users(body) {
        const holder = h('div');
        const ROLES = ['SuperAdmin', 'Owner', 'ObjectAdmin', 'Manager', 'Accountant', 'Content', 'Customer'];
        const role = S.select('role', [['', t('plat.allRoles')]].concat(ROLES.map((r) => [r, t('role.' + r, null, r)])), '');
        const org = S.select('organizationId', [['', t('plat.allOrgs')]], '');
        const search = S.input('search', { type: 'search', placeholder: t('plat.userSearch'), 'aria-label': t('plat.userSearch') });
        let page = 1;
        S.api.organizations().then((l) => l.forEach((o) => org.appendChild(h('option', { value: o.id }, o.name)))).catch(() => {});
        const act = (fn, done) => (e) => { e.stopPropagation(); S.busy(e.currentTarget, async () => { try { await fn(); S.toast(done, 'ok'); load(); } catch (er) { S.err(er); } }); };
        const load = async () => {
          holder.replaceChildren(S.skeleton(5, 38));
          try {
            const r = await S.api.platformUsers({ role: role.value, organizationId: org.value, search: search.value.trim(), page });
            const pages = Math.max(1, Math.ceil(r.totalCount / r.pageSize));
            holder.replaceChildren(S.tbl([
              { label: t('f.fullName'), render: (u) => h('div', null, h('b', null, u.fullName), h('div', { class: 'hint' }, u.email || u.phone || '—')) },
              { label: t('users.role'), render: (u) => t('role.' + u.role, null, u.role || '—') },
              { label: t('plat.org'), render: (u) => u.organizationName || '—' },
              { label: t('plat.createdAt'), render: (u) => S.fmt.date(u.createdAt) },
              { label: t('plat.lastLogin'), render: (u) => (u.lastLoginAt ? S.fmt.dt(u.lastLoginAt) : '—') },
              { label: t('deal.stage'), render: (u) => S.chip(u.status === 'Active' ? 'Active ok' : 'Blocked', t(u.status === 'Active' ? 'plat.userActive' : 'plat.userBlocked')) },
              { label: t('plat.deals'), r: true, num: true, render: (u) => (u.role === 'Customer' ? u.dealsCount : '—') },
              { label: '', r: true, render: (u) => (u.role === 'SuperAdmin' ? '' : h('div', { class: 'row', style: { gap: '6px', justifyContent: 'flex-end', flexWrap: 'nowrap' } },
                u.status === 'Active'
                  ? h('button', { class: 'btn ghost sm', onclick: async (e) => {
                    e.stopPropagation(); const btn = e.currentTarget;
                    if (!(await S.confirm(t('plat.blockText', { u: u.fullName }), t('plat.block'), true))) return;
                    await S.busy(btn, async () => { try { await S.api.blockUser(u.id); S.toast(t('plat.blocked'), 'ok'); load(); } catch (er) { S.err(er); } });
                  } }, t('plat.block'))
                  : h('button', { class: 'btn sm', onclick: act(() => S.api.unblockUser(u.id), t('plat.unblocked')) }, t('plat.unblock')),
                u.role !== 'Customer' ? h('button', { class: 'btn ghost sm', onclick: act(() => S.api.resetUserPassword(u.id), t('plat.passwordSent', { e: u.email })) }, t('plat.resetPassword')) : null)) },
            ], r.items, { empty: t('plat.noUsers') }),
            pages > 1 ? h('div', { class: 'row', style: { justifyContent: 'center', gap: '10px', marginTop: '12px' } },
              h('button', { class: 'btn ghost sm', disabled: page <= 1, onclick: () => { page--; load(); } }, '←'),
              h('span', { class: 'muted' }, t('plat.page', { p: page, n: pages })),
              h('button', { class: 'btn ghost sm', disabled: page >= pages, onclick: () => { page++; load(); } }, '→')) : null);
          } catch (e) { fail(holder, e); }
        };
        const reload = () => { page = 1; load(); };
        role.addEventListener('change', reload); org.addEventListener('change', reload); search.addEventListener('input', S.debounce(reload, 300));
        body.replaceChildren(h('div', { class: 'toolbar' }, h('div', { style: { minWidth: '220px', flex: '1', maxWidth: '340px' } }, search), role, org), holder);
        load();
      },

      // All reviews of the platform: stars, published or hidden, delete (the copy stays in the audit log).
      reviews(body) {
        const holder = h('div');
        const load = async () => {
          holder.replaceChildren(S.skeleton(5, 38));
          try {
            const r = await S.api.platformReviews(1);
            holder.replaceChildren(S.tbl([
              { label: t('plat.rating'), render: (v) => stars(v.rating) },
              { label: t('plat.reviewText'), render: (v) => h('div', { class: 'review-cell' }, h('div', null, v.text), v.ownerReply ? h('div', { class: 'hint' }, '↳ ' + v.ownerReply) : null) },
              { label: t('plat.building'), render: (v) => h('div', null, v.buildingName || '—', h('div', { class: 'hint' }, v.organizationName || '')) },
              { label: t('plat.author'), render: (v) => v.customerFullName || v.authorName },
              { label: t('deal.stage'), render: (v) => S.chip(v.isPublished ? 'good' : 'warn', t(v.isPublished ? 'plat.published' : 'plat.hidden')) },
              { label: t('plat.createdAt'), render: (v) => S.fmt.date(v.createdAt) },
              { label: '', r: true, render: (v) => h('button', { class: 'btn ghost sm', onclick: async (e) => {
                const btn = e.currentTarget;
                if (!(await S.confirm(t('plat.deleteReviewText'), t('plat.deleteReview'), true))) return;
                await S.busy(btn, async () => { try { await S.api.deleteReview(v.id); S.toast(t('plat.reviewDeleted'), 'ok'); load(); } catch (er) { S.err(er); } });
              } }, t('plat.deleteReview')) },
            ], r.items, { empty: t('plat.noReviews') }));
          } catch (e) { fail(holder, e); }
        };
        body.replaceChildren(holder);
        load();
      },

      // Tariff cards; a new tariff, editing one and giving one to an organization.
      tariffs(body) {
        const holder = h('div', { class: 'stack' });
        const limit = (n, key) => (n === null || n === undefined ? t(key + 'Unlimited') : t(key, { n }));
        const load = async () => {
          holder.replaceChildren(S.skeleton(3, 120));
          try {
            const list = await S.api.tariffs();
            holder.replaceChildren(h('div', { class: 'tariff-grid' }, list.map((p) => h('div', { class: 'card tariff-card' + (p.isActive ? '' : ' off') },
              h('div', { class: 'row between' }, h('h3', null, p.name), p.isActive ? null : S.chip('Blocked', t('plat.tariffOff'))),
              h('div', { class: 'tariff-price' }, S.fmt.money(p.price, p.currency), h('small', null, ' ' + t('plat.perMonth'))),
              h('ul', null, h('li', null, limit(p.maxBuildings, 'plat.maxBuildings')), h('li', null, limit(p.maxUsers, 'plat.maxUsers')), p.features.map((f) => h('li', null, f))),
              h('div', { class: 'hint' }, t('plat.tariffOrgs', { n: p.organizationsCount })),
              h('div', { class: 'row', style: { gap: '6px' } }, h('button', { class: 'btn ghost sm', onclick: () => tariffForm(p, load) }, t('common.edit')))))));
          } catch (e) { fail(holder, e); }
        };
        body.replaceChildren(h('div', { class: 'toolbar' }, h('span', { class: 'spacer' }),
          h('button', { class: 'btn ghost', onclick: () => assignForm(null, load) }, t('plat.assignTariff')),
          h('button', { class: 'btn', onclick: () => tariffForm(null, load) }, t('plat.newTariff'))), holder);
        load();
      },

      // Developers' applications from /register: approve (creates the organization and invites the owner) or reject.
      apps(body) {
        const STATUS = { Pending: ['warn', 'apps.pending'], Approved: ['good', 'apps.approved'], Rejected: ['bad', 'apps.rejected'] };
        const EMPTY = { '': ['apps.none', 'inbox'], Pending: ['apps.nonePending', 'file'], Approved: ['apps.noneApproved', 'building'], Rejected: ['apps.noneRejected', 'x'] };
        let filter = 'Pending', list = [];
        const opened = new Set();
        const holder = h('div');
        const seg = h('div', { class: 'seg', role: 'group', 'aria-label': t('apps.filter') }, [['', 'apps.all'], ['Pending', 'apps.pending'], ['Approved', 'apps.approvedMany'], ['Rejected', 'apps.rejectedMany']]
          .map(([k, key]) => h('button', { class: filter === k ? 'on' : '', 'data-k': k, onclick: () => setFilter(k) }, t(key))));
        const setFilter = (k) => { filter = k; S.$$('button', seg).forEach((b) => b.classList.toggle('on', b.dataset.k === k)); load(); };
        // Buttons and links inside a row do their own thing; a click elsewhere on the row opens its comment.
        const own = (fn) => (e) => { e.stopPropagation(); if (fn) fn(e); };
        const draw = () => holder.replaceChildren(S.tbl([
          { label: t('apps.company'), render: (a) => h('div', null, h('b', null, a.companyName),
            a.comment ? h('div', { class: 'app-note-toggle', 'aria-expanded': String(opened.has(a.id)) }, S.icon('file'), t(opened.has(a.id) ? 'apps.hideComment' : 'apps.showComment')) : null,
            a.comment && opened.has(a.id) ? h('div', { class: 'app-note' }, a.comment) : null) },
          { label: t('apps.owner'), render: (a) => a.ownerFullName },
          { label: t('login.email'), render: (a) => h('a', { href: 'mailto:' + a.contactEmail, onclick: own() }, a.contactEmail) },
          { label: t('f.phone'), render: (a) => h('a', { href: 'tel:' + a.contactPhone, onclick: own() }, a.contactPhone) },
          { label: t('apps.date'), render: (a) => S.fmt.dt(a.createdAt) },
          { label: t('deal.stage'), render: (a) => h('div', null, S.chip(STATUS[a.status][0], t(STATUS[a.status][1])), a.reviewNote ? h('div', { class: 'hint' }, a.reviewNote) : null) },
          { label: '', r: true, render: (a) => (a.status === 'Pending' ? h('div', { class: 'row', style: { gap: '6px', justifyContent: 'flex-end', flexWrap: 'nowrap' } },
            h('button', { class: 'btn sm', onclick: own(() => approveForm(a, approved)) }, t('apps.approve')),
            h('button', { class: 'btn ghost sm', onclick: own(() => rejectForm(a, done)) }, t('apps.reject')))
            : a.status === 'Approved' && a.organizationId ? h('button', { class: 'btn soft sm', onclick: own(() => { focusOrg = a.organizationId; ws.go('orgs'); }) }, t('apps.openOrg')) : '') },
        ], list, { empty: t(EMPTY[filter][0]), emptyIcon: EMPTY[filter][1], rowClass: (a) => (a.comment ? 'app-row has-note' : 'app-row'), onRow: (a) => { if (!a.comment) return; if (opened.has(a.id)) opened.delete(a.id); else opened.add(a.id); draw(); } }));
        const load = async () => {
          holder.replaceChildren(S.skeleton(4, 38));
          try { list = await S.api.applications(filter); draw(); } catch (e) { holder.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
        };
        const done = () => { load(); countPending(); };
        // After approving, show the result: the approved list with the new organization's row.
        const approved = () => { setFilter('Approved'); countPending(); };
        body.replaceChildren(h('div', { class: 'toolbar' }, seg), holder);
        load(); countPending();
      },

      // 2FA: a QR code (qrcode.min.js from cdnjs) and the key for manual entry; turned on and off with a code from the app.
      // The API answers 409 to "setup" while 2FA is on — then only the "turn off" form is shown.
      security(body) {
        const holder = h('div', { class: 'card stack', style: { maxWidth: '520px' } }, S.skeleton(3, 30));
        body.replaceChildren(holder);
        const codeForm = (label, action, done) => {
          const code = S.input('code', { inputmode: 'numeric', maxlength: 6, autocomplete: 'one-time-code' });
          const btn = h('button', { class: 'btn', type: 'submit' }, label);
          const form = h('form', { class: 'stack', novalidate: true }, S.field(t('plat.totpCode'), code), btn);
          form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await action(code.value.replace(/\s/g, '')); S.toast(done, 'ok'); draw(); } catch (er) { S.showErrors(form, er); } }); });
          return form;
        };
        const qr = (uri) => {
          const box = h('div', { class: 'qr-box', role: 'img', 'aria-label': t('plat.totpQr') });
          if (window.QRCode) new QRCode(box, { text: uri, width: 180, height: 180, correctLevel: QRCode.CorrectLevel.M });
          else box.replaceChildren(h('a', { href: uri, class: 'hint' }, t('plat.totpLink')));
          return box;
        };
        async function draw() {
          holder.replaceChildren(S.skeleton(3, 30));
          try {
            const k = await S.api.totpSetup();
            holder.replaceChildren(h('h3', null, t('plat.totp')), h('p', { class: 'muted' }, t('plat.totpText')), qr(k.qrCodeUri),
              S.field(t('plat.totpKey'), h('code', { class: 'paper', style: { display: 'block', wordBreak: 'break-all', userSelect: 'all' } }, k.manualKey)),
              h('a', { href: k.qrCodeUri, class: 'hint' }, t('plat.totpLink')), codeForm(t('plat.totpOn'), S.api.totpEnable, t('plat.totpDone')));
          } catch (e) {
            if (e.status !== 409) { holder.replaceChildren(h('div', { class: 'form-err' }, e.message)); return; }
            holder.replaceChildren(h('h3', null, t('plat.totp')), h('div', { class: 'form-ok' }, t('plat.totpIsOn')), h('p', { class: 'muted' }, t('plat.totpOffText')),
              codeForm(t('plat.totpOff'), S.api.totpDisable, t('plat.totpOffDone')));
          }
        }
        draw();
      },

      // Activity of the whole platform: who did what, in which organization, from which address. Filters and pages.
      async activity(body) {
        const q = { organizationId: '', action: '', from: '', to: '', page: 1 };
        const holder = h('div');
        const pager = h('div', { class: 'row between', style: { marginTop: '12px' } });
        const [orgs, actions] = await Promise.all([S.api.organizations().catch(() => []), S.api.platformActivityActions().catch(() => [])]);
        const org = S.select('org', [['', t('pact.allOrgs')], ...orgs.map((o) => [o.id, o.name])], '');
        const act = S.select('action', [['', t('pact.allActions')], ...actions.map((a) => [a, t('audit.act.' + a, null, a)])], '');
        const from = S.input('from', { type: 'date' }), to = S.input('to', { type: 'date' });
        // The date filter is by Dushanbe days: [from 00:00; to + 1 day 00:00) in UTC+5.
        const utc = (d, addDay) => { if (!d) return ''; const x = new Date(d + 'T00:00:00+05:00'); if (addDay) x.setUTCDate(x.getUTCDate() + 1); return x.toISOString(); };
        const apply = () => { q.organizationId = org.value; q.action = act.value; q.from = utc(from.value); q.to = utc(to.value, true); q.page = 1; load(); };
        [org, act, from, to].forEach((el) => el.addEventListener('change', apply));
        const kind = (a) => (/creat|add|import|register|approv|activat|sent/i.test(a) ? 'good' : /delet|remov|cancel|suspend|revers|reject|block/i.test(a) ? 'bad' : /login|sign|auth|code|token|password/i.test(a) ? 'muted' : 'info');
        async function load() {
          holder.replaceChildren(S.skeleton(6, 34));
          try {
            const r = await S.api.platformActivity(q);
            holder.replaceChildren(S.tbl([
              { label: t('pact.time'), render: (x) => S.fmt.dt(x.at) },
              { label: t('pact.action'), render: (x) => h('span', { class: 'chip act-' + kind(x.action) }, t('audit.act.' + x.action, null, x.action)) },
              { label: t('pact.entity'), render: (x) => t('audit.ent.' + x.entity, null, x.entity) },
              { label: t('pact.org'), render: (x) => x.organizationName || h('span', { class: 'muted' }, t('pact.platform')) },
              { label: t('pact.user'), render: (x) => [x.userEmail || h('span', { class: 'muted' }, t('audit.system', null, 'система')), x.impersonated ? h('small', { class: 'muted' }, ' · ' + t('pact.asOwner')) : null] },
              { label: 'IP', render: (x) => h('code', null, x.ip || '—') }], r.items, { empty: t('pact.none') }));
            const pages = Math.max(1, Math.ceil(r.totalCount / r.pageSize));
            pager.replaceChildren(h('span', { class: 'muted' }, t('pact.total', { n: r.totalCount })),
              h('div', { class: 'row', style: { gap: '8px' } },
                h('button', { class: 'btn ghost sm', type: 'button', disabled: q.page <= 1, onclick: () => { q.page--; load(); } }, '‹'),
                h('span', null, q.page + ' / ' + pages),
                h('button', { class: 'btn ghost sm', type: 'button', disabled: q.page >= pages, onclick: () => { q.page++; load(); } }, '›')));
          } catch (e) { fail(holder, e); }
        }
        body.replaceChildren(h('div', { class: 'card filters-row' }, S.field(t('pact.org'), org), S.field(t('pact.action'), act), S.field(t('pact.from'), from), S.field(t('pact.to'), to)), holder, pager);
        load();
      },

      // A message to the bell of every user of one organization or of all organizations.
      async notify(body) {
        const orgs = await S.api.organizations().catch(() => []);
        const to = S.select('to', [['', t('pnot.all')], ...orgs.map((o) => [o.id, o.name])], '');
        const type = S.select('type', [['info', t('pnot.info')], ['warning', t('pnot.warning')], ['error', t('pnot.error')]], 'info');
        const title = S.input('title', { maxlength: 200 });
        const message = h('textarea', { class: 'inp', name: 'message', rows: 5, maxlength: 2000 });
        const send = h('button', { class: 'btn', type: 'submit' }, t('pnot.send'));
        const form = h('form', { class: 'card stack', style: { maxWidth: '620px' }, novalidate: true },
          S.field(t('pnot.to'), to), S.field(t('pnot.type'), type), S.field(t('pnot.title'), title), S.field(t('pnot.message'), message), send);
        form.addEventListener('submit', (e) => {
          e.preventDefault(); S.showErrors(form, null);
          S.busy(send, async () => {
            try {
              const r = await S.api.platformNotify({ organizationId: to.value, type: type.value, title: title.value, message: message.value });
              S.toast(t('pnot.sent', { n: r.recipients }), 'ok'); title.value = ''; message.value = '';
            } catch (er) { S.showErrors(form, er); }
          });
        });
        body.replaceChildren(form);
      },

      // Platform settings: organization limit, demo mode, version (read only), contact email.
      async psettings(body) {
        const holder = h('div', { class: 'card stack', style: { maxWidth: '560px' } }, S.skeleton(4, 30));
        body.replaceChildren(holder);
        let s;
        try { s = await S.api.platformSettings(); } catch (e) { fail(holder, e); return; }
        const max = S.input('max', { type: 'number', min: 1, step: 1, value: s.maxOrganizations == null ? '' : s.maxOrganizations, placeholder: t('pset.noLimit') });
        const demo = h('input', { type: 'checkbox', class: 'switch', checked: s.demoMode, 'aria-label': t('pset.demo') });
        const version = S.input('version', { value: s.version, readonly: true });
        const email = S.input('email', { type: 'email', value: s.contactEmail || '', placeholder: 'support@binosoz.tj' });
        const save = h('button', { class: 'btn', type: 'submit' }, t('common.save'));
        const form = h('form', { class: 'stack', novalidate: true },
          S.field(t('pset.max'), max, t('pset.maxHint', { n: s.organizations })),
          h('label', { class: 'switch-row' }, demo, h('span', null, t('pset.demo')), h('small', { class: 'muted' }, t('pset.demoHint'))),
          S.field(t('pset.version'), version), S.field(t('pset.email'), email), save);
        form.addEventListener('submit', (e) => {
          e.preventDefault(); S.showErrors(form, null);
          S.busy(save, async () => {
            try {
              const v = max.value.trim();
              s = await S.api.savePlatformSettings({ maxOrganizations: v ? +v : null, clearMaxOrganizations: !v, demoMode: demo.checked, contactEmail: email.value.trim() });
              S.toast(t('common.saved'), 'ok');
            } catch (er) { S.showErrors(form, er); }
          });
        });
        holder.replaceChildren(form);
      },
    };

    // Approving creates the organization: its short name goes into the site address, suggested from the company name.
    function approveForm(a, done) {
      const slug = S.input('organizationSlug', { value: S.slugify(a.companyName), placeholder: 'my-company' });
      const form = h('form', { class: 'stack', id: 'approveform', novalidate: true }, h('p', { class: 'muted' }, t('apps.approveText', { c: a.companyName, e: a.contactEmail })), S.field(t('plat.slug'), slug, t('plat.slugHint')));
      const btn = h('button', { class: 'btn', type: 'submit', form: 'approveform' }, t('apps.approve'));
      const md = S.modal({ title: t('apps.approveTitle'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { const r = await S.api.approveApplication(a.id, slug.value.trim()); S.toast(r.message || t('apps.approvedToast'), 'ok'); md.close(); done(); } catch (er) { S.showErrors(form, er); } }); });
    }
    function rejectForm(a, done) {
      const reason = h('textarea', { class: 'inp', name: 'reason', id: 'reject-reason', maxlength: 500, rows: 3 });
      const form = h('form', { class: 'stack', id: 'rejectform', novalidate: true }, h('p', { class: 'muted' }, t('apps.rejectText', { c: a.companyName })), S.field(t('apps.reason'), reason));
      const btn = h('button', { class: 'btn danger', type: 'submit', form: 'rejectform' }, t('apps.reject'));
      const md = S.modal({ title: t('apps.rejectTitle'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await S.api.rejectApplication(a.id, reason.value.trim()); S.toast(t('apps.rejectedToast'), 'ok'); md.close(); done(); } catch (er) { S.showErrors(form, er); } }); });
    }

    // Deals by month: bars — how many, line — for how much (TJS, right axis). Colours follow the theme.
    function drawChart(canvas, rows) {
      if (chart) { chart.destroy(); chart = null; }
      if (!window.Chart) { canvas.replaceWith(h('p', { class: 'muted' }, t('plat.noChart'))); return; }
      const css = getComputedStyle(document.documentElement);
      const gold = css.getPropertyValue('--gold').trim() || '#c9a227', info = css.getPropertyValue('--info').trim() || '#8d9bff';
      const ink = css.getPropertyValue('--muted').trim() || '#8894ba', line = css.getPropertyValue('--line').trim() || '#26325e';
      const months = t('stats.months').split(',');
      chart = new window.Chart(canvas, {
        data: {
          labels: rows.map((r) => months[+r.month.slice(5) - 1] + ' ' + r.month.slice(2, 4)),
          datasets: [
            { type: 'bar', label: t('plat.dealsCount'), data: rows.map((r) => r.count), backgroundColor: gold, borderRadius: 6, yAxisID: 'y' },
            { type: 'line', label: t('plat.dealsSum'), data: rows.map((r) => r.sum), borderColor: info, backgroundColor: info, tension: 0.3, yAxisID: 'sum' }],
        },
        options: {
          responsive: true, maintainAspectRatio: false,
          plugins: { legend: { labels: { color: ink } }, tooltip: { callbacks: { label: (c) => c.dataset.yAxisID === 'sum' ? money(c.parsed.y) : c.dataset.label + ': ' + c.parsed.y } } },
          scales: {
            x: { ticks: { color: ink }, grid: { color: line } },
            y: { beginAtZero: true, ticks: { color: ink, precision: 0 }, grid: { color: line } },
            sum: { position: 'right', beginAtZero: true, ticks: { color: ink, callback: (v) => S.fmt.n(v) }, grid: { drawOnChartArea: false } } },
        },
      });
    }

    // Organization card: totals, buildings, owners, tariff history; rename, give a tariff, invite an owner.
    async function orgCard(id, done) {
      const body = h('div', { class: 'stack' }, S.skeleton(4, 30));
      const md = S.modal({ title: t('plat.orgCard'), body, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.close'))] });
      try {
        const d = await S.api.organizationDetails(id);
        const o = d.organization;
        const kv = (k, v) => [h('dt', null, k), h('dd', null, v === null || v === undefined || v === '' ? '—' : v)];
        body.replaceChildren(
          h('div', { class: 'row between' }, h('h3', null, o.name), S.chip(o.active ? 'Active ok' : 'Blocked', t(o.active ? 'plat.active' : 'plat.off'))),
          h('dl', { class: 'kv' }, kv(t('plat.slug'), o.slug), kv(t('plat.tariff'), tariffChip(o.tariff)), kv(t('plat.createdAt'), S.fmt.date(o.createdAt)),
            kv(t('plat.lastActivity'), o.lastActivityAt ? S.fmt.dt(o.lastActivityAt) : null), kv(t('plat.buildings'), o.buildings), kv(t('plat.deals'), o.deals),
            kv(t('plat.customers'), o.customers), kv(t('plat.users'), o.users), kv(t('plat.apartments'), t('plat.apartmentsLine', { a: d.apartmentsCount, f: d.freeApartments, s: d.soldApartments })),
            kv(t('plat.paymentsSum'), money(d.paymentsSumTjs))),
          h('h4', null, t('plat.owners')), o.ownerInfos.length ? h('ul', null, o.ownerInfos.map((w) => h('li', null, h('b', null, w.fullName), ' · ', w.email ? h('a', { href: 'mailto:' + w.email }, w.email) : '—', w.phone ? ' · ' + w.phone : ''))) : h('p', { class: 'muted' }, t('plat.noOwner')),
          h('h4', null, t('plat.buildingList')), d.buildings.length ? h('ul', null, d.buildings.map((b) => h('li', null, b.name + ' — ' + t('plat.apartmentsShort', { a: b.apartmentsCount, f: b.freeApartments })))) : h('p', { class: 'muted' }, t('plat.noBuildings')),
          d.tariffHistory.length ? h('h4', null, t('plat.tariffHistory')) : null,
          d.tariffHistory.length ? h('ul', null, d.tariffHistory.map((th) => h('li', null, th.tariffName + ' · ' + S.fmt.date(th.startsAt) + ' — ' + (th.endsAt ? S.fmt.date(th.endsAt) : t('plat.noEnd')) + (th.isPaid ? ' · ' + t('plat.paid') : '')))) : null,
          h('div', { class: 'row', style: { gap: '8px', flexWrap: 'wrap' } },
            h('button', { class: 'btn ghost sm', onclick: () => { md.close(); renameForm(o, done); } }, t('plat.rename')),
            h('button', { class: 'btn ghost sm', onclick: () => { md.close(); assignForm(o, done); } }, t('plat.assignTariff')),
            h('button', { class: 'btn ghost sm', onclick: () => { md.close(); inviteOwnerForm(o, done); } }, t('plat.inviteOwner'))));
      } catch (e) { fail(body, e); }
    }

    function renameForm(o, done) {
      const name = S.input('name', { value: o.name }), slug = S.input('slug', { value: o.slug });
      const form = h('form', { class: 'stack', id: 'renameform', novalidate: true }, S.field(t('plat.name'), name), S.field(t('plat.slug'), slug, t('plat.slugHint')));
      const btn = h('button', { class: 'btn', type: 'submit', form: 'renameform' }, t('common.save'));
      const md = S.modal({ title: t('plat.rename'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await S.api.updateOrganization(o.id, { name: name.value.trim(), slug: slug.value.trim() }); S.toast(t('common.saved'), 'ok'); md.close(); done(); } catch (er) { S.showErrors(form, er); } }); });
    }

    function inviteOwnerForm(o, done) {
      const first = S.input('firstName'), last = S.input('lastName'), email = S.input('email', { type: 'email' });
      const form = h('form', { class: 'stack', id: 'ownerform', novalidate: true }, h('p', { class: 'muted' }, t('plat.inviteOwnerText', { o: o.name })),
        h('div', { class: 'grid g2' }, S.field(t('reg.firstName'), first), S.field(t('reg.lastName'), last)), S.field(t('login.email'), email));
      const btn = h('button', { class: 'btn', type: 'submit', form: 'ownerform' }, t('plat.inviteOwner'));
      const md = S.modal({ title: t('plat.inviteOwner'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await S.api.inviteOwner(o.id, { email: email.value.trim(), firstName: first.value.trim(), lastName: last.value.trim() }); S.toast(t('plat.ownerInvited', { e: email.value.trim() }), 'ok'); md.close(); done(); } catch (er) { S.showErrors(form, er); } }); });
    }

    function tariffForm(p, done) {
      const name = S.input('name', { value: p ? p.name : '' }), price = S.input('price', { type: 'number', min: 0, step: '0.01', value: p ? p.price : 0 });
      const currency = S.select('currency', [['TJS', 'TJS'], ['USD', 'USD']], p ? p.currency : 'TJS');
      const maxB = S.input('maxBuildings', { type: 'number', min: 1, value: p && p.maxBuildings !== null ? p.maxBuildings : '', placeholder: t('plat.unlimited') });
      const maxU = S.input('maxUsers', { type: 'number', min: 1, value: p && p.maxUsers !== null ? p.maxUsers : '', placeholder: t('plat.unlimited') });
      const features = h('textarea', { class: 'inp', name: 'features', rows: 4 }, p ? p.features.join('\n') : '');
      const active = h('input', { type: 'checkbox', checked: p ? p.isActive : true });
      const form = h('form', { class: 'stack', id: 'tariffform', novalidate: true }, S.field(t('plat.name'), name),
        h('div', { class: 'grid g2' }, S.field(t('plat.price'), price), S.field(t('plat.currency'), currency)),
        h('div', { class: 'grid g2' }, S.field(t('plat.maxBuildingsLabel'), maxB, t('plat.emptyUnlimited')), S.field(t('plat.maxUsersLabel'), maxU)),
        S.field(t('plat.features'), features, t('plat.featuresHint')), h('label', { class: 'check' }, active, ' ', t('plat.tariffActive')));
      const btn = h('button', { class: 'btn', type: 'submit', form: 'tariffform' }, t('common.save'));
      const md = S.modal({ title: p ? p.name : t('plat.newTariff'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      const intOrNull = (el) => (el.value.trim() === '' ? null : Number(el.value));
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => {
        try {
          await S.api.saveTariff(p && p.id, { name: name.value.trim(), price: Number(price.value || 0), currency: currency.value, maxBuildings: intOrNull(maxB), maxUsers: intOrNull(maxU),
            features: features.value.split('\n').map((f) => f.trim()).filter(Boolean), isActive: active.checked });
          S.toast(t('common.saved'), 'ok'); md.close(); done();
        } catch (er) { S.showErrors(form, er); }
      }); });
    }

    // Give a tariff to an organization from today; the previous one ends now.
    async function assignForm(o, done) {
      const [orgs, plans] = await Promise.all([o ? Promise.resolve([o]) : S.api.organizations(), S.api.tariffs()]);
      const org = S.select('organizationId', orgs.map((x) => [x.id, x.name]), o ? o.id : undefined);
      const plan = S.select('tariffPlanId', plans.filter((p) => p.isActive).map((p) => [p.id, p.name + ' · ' + S.fmt.money(p.price, p.currency)]));
      const ends = S.input('endsAt', { type: 'date' });
      const paid = h('input', { type: 'checkbox' });
      const form = h('form', { class: 'stack', id: 'assignform', novalidate: true }, S.field(t('plat.org'), org), S.field(t('plat.tariff'), plan),
        S.field(t('plat.endsAt'), ends, t('plat.endsAtHint')), h('label', { class: 'check' }, paid, ' ', t('plat.paid')));
      const btn = h('button', { class: 'btn', type: 'submit', form: 'assignform' }, t('plat.assignTariff'));
      const md = S.modal({ title: t('plat.assignTariff'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => {
        try {
          await S.api.assignTariff(org.value, { tariffPlanId: plan.value, endsAt: ends.value ? new Date(ends.value + 'T23:59:59+05:00').toISOString() : null, isPaid: paid.checked });
          S.toast(t('plat.tariffAssigned'), 'ok'); md.close(); done();
        } catch (er) { S.showErrors(form, er); }
      }); });
    }

    function orgForm(done) {
      const name = S.input('name'), slug = S.input('slug', { placeholder: 'my-company' }), email = S.input('ownerEmail', { type: 'email' }), owner = S.input('ownerFullName');
      // The short name goes into the public site address: suggest one from the name, latin letters only.
      name.addEventListener('input', () => { if (!slug.dataset.touched) slug.value = S.slugify(name.value); });
      slug.addEventListener('input', () => { slug.dataset.touched = '1'; });
      const form = h('form', { class: 'stack', id: 'orgform', novalidate: true }, S.field(t('plat.name'), name), S.field(t('plat.slug'), slug, t('plat.slugHint')), S.field(t('plat.ownerEmail'), email, t('plat.ownerHint')), S.field(t('plat.ownerName'), owner));
      const btn = h('button', { class: 'btn', type: 'submit', form: 'orgform' }, t('common.save'));
      const md = S.modal({ title: t('plat.new'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await S.api.createOrganization({ name: name.value.trim(), slug: slug.value.trim(), ownerEmail: email.value.trim(), ownerFullName: owner.value.trim() }); S.toast(t('plat.created'), 'ok'); md.close(); done(); } catch (er) { S.showErrors(form, er); } }); });
    }
    return () => {};
  };

  const LAT = { а: 'a', б: 'b', в: 'v', г: 'g', ғ: 'gh', д: 'd', е: 'e', ё: 'yo', ж: 'zh', з: 'z', и: 'i', ӣ: 'i', й: 'y', к: 'k', қ: 'q', л: 'l', м: 'm', н: 'n', о: 'o', п: 'p', р: 'r', с: 's', т: 't', у: 'u', ӯ: 'u', ф: 'f', х: 'kh', ҳ: 'h', ц: 'ts', ч: 'ch', ҷ: 'j', ш: 'sh', щ: 'sh', ъ: '', ы: 'y', ь: '', э: 'e', ю: 'yu', я: 'ya' };
  S.slugify = (v) => String(v || '').toLowerCase().split('').map((c) => (LAT[c] !== undefined ? LAT[c] : c)).join('').replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 64);
})();
