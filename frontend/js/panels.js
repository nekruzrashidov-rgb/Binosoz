/* Shared panels used by the buyer cabinet and the manager CRM: workspace shell, schedule, documents, chat, construction. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  S.panels = {};

  /* Workspace shell: side menu on desktop, scrolling tabs on phones. */
  S.workspace = (items, initial, onSelect) => {
    let cur = initial || items.filter((i) => !i.group)[0].key;
    const main = h('main', { class: 'main', id: 'ws-main' });
    const side = h('nav', { class: 'side', 'aria-label': t('nav.sections') });
    const tabs = h('div', { class: 'tabs mobile-only', style: { margin: 0, padding: '0 12px', background: 'var(--surface)' }, role: 'tablist' });
    const draw = () => {
      side.replaceChildren(); let lastGroup = null;
      items.forEach((it) => {
        if (it.group !== undefined && it.group !== lastGroup) { lastGroup = it.group; if (it.group) side.appendChild(h('div', { class: 'grp' }, it.group)); }
        side.appendChild(h('button', { class: it.key === cur ? 'on' : '', onclick: () => go(it.key), 'aria-current': it.key === cur ? 'page' : null }, S.icon(it.icon || 'grid'), h('span', { class: 'grow' }, it.label), it.badge ? h('span', { class: 'badge' }, it.badge) : null));
      });
      tabs.replaceChildren(...items.map((it) => h('button', { class: it.key === cur ? 'on' : '', role: 'tab', onclick: () => go(it.key) }, it.label, it.badge ? h('span', { class: 'badge' }, it.badge) : null)));
    };
    const go = (k) => { cur = k; draw(); onSelect(k, main); const m = document.getElementById('ws-main'); if (m && window.scrollY > 140) window.scrollTo({ top: 0, behavior: 'smooth' }); };
    const el = h('div', null, tabs, h('div', { class: 'ws' }, side, main));
    draw();
    return { el, main, go, get current() { return cur; }, redraw: draw, setItems(n) { items = n; draw(); } };
  };

  /* Schedule table + summary */
  S.panels.schedule = (sc) => {
    const eq = sc.eq; const cur = sc.currency;
    const head = h('div', { class: 'grid g4', style: { marginBottom: '14px' } },
      kpi(t('sch.total'), S.fmt.money(sc.total, cur), eq && eq.total != null ? '≈ ' + S.fmt.money(eq.total, eq.currency) : null),
      kpi(t('sch.paid'), S.fmt.money(sc.paid, cur), eq && eq.paid != null ? '≈ ' + S.fmt.money(eq.paid, eq.currency) : null),
      kpi(t('sch.left'), S.fmt.money(sc.remaining, cur)),
      kpi(t('sch.terms'), (sc.down !== null && sc.down !== undefined ? sc.down + '% · ' : '') + (sc.months || sc.items.length - 1) + ' ' + t('u.monthsShort') + (sc.day ? ' · ' + t('sch.day', { d: sc.day }) : '')));
    const cols = [
      { label: '#', render: (r) => (r.kind === 'Down' || r.number === 0 ? t('sch.down') : r.number) },
      { label: t('sch.due'), render: (r) => S.fmt.date(r.dueDate) },
      { label: t('sch.amount'), r: true, num: true, render: (r) => S.fmt.money(r.amountDue, cur) },
    ];
    if (eq) cols.push({ label: t('sch.eq'), r: true, num: true, render: (r) => (r.eqDue != null ? '≈ ' + S.fmt.money(r.eqDue, eq.currency) : '—') });
    cols.push({ label: t('sch.paidCol'), r: true, num: true, render: (r) => S.fmt.money(r.amountPaid, cur) }, { label: t('sch.status'), render: (r) => S.chip(r.status, t('pay.' + r.status)) });
    const rows = h('div', { style: { maxHeight: '520px', overflow: 'auto' } }, S.tbl(cols, sc.items, { rowClass: (r) => (r.status === 'Paid' ? 'dim' : '') }));
    return h('div', null, head, eq ? h('p', { class: 'hint', style: { marginBottom: '10px' } }, t('sch.eqNote', { rate: eq.rate, date: S.fmt.date(eq.date) })) : null, rows);
  };
  const kpi = (l, v, s) => h('div', { class: 'card kpi' }, h('span', null, l), h('b', { style: { fontSize: '20px' } }, v), s ? h('div', { class: 'muted', style: { fontSize: '12.5px', fontWeight: 600 } }, s) : null, h('i'));
  S.kpi = kpi;

  /* Documents */
  const DOC_TYPES = ['Contract', 'Annex', 'Act', 'Invoice', 'Receipt', 'Other'];
  const DOC_TYPE_RU = { Contract: 'Договор', Annex: 'Приложение', Act: 'Акт', Invoice: 'Счёт', Receipt: 'Квитанция', Other: 'Другое' };
  S.panels.documents = (dealId, canUpload) => {
    const box = h('div', { class: 'stack' });
    const list = h('div');
    const load = async () => {
      list.replaceChildren(S.skeleton(3, 38));
      try {
        const docs = await S.api.documents(dealId);
        list.replaceChildren(S.tbl([
          { label: t('doc.name'), render: (d) => h('span', { class: 'row', style: { gap: '8px', flexWrap: 'nowrap' } }, S.icon('file'), d.name, DOC_TYPE_RU[d.type] ? h('span', { class: 'muted', style: { fontSize: '12.5px' } }, t('doc.type.' + d.type, null, DOC_TYPE_RU[d.type])) : null) },
          { label: t('doc.date'), render: (d) => S.fmt.date(d.createdAt) },
          { label: t('doc.size'), render: (d) => S.fmt.size(d.size) },
          { label: '', r: true, render: (d) => h('button', { class: 'btn soft sm', onclick: (e) => S.busy(e.currentTarget, async () => { try { await S.api.openSigned(d.id, d.name); } catch (er) { S.err(er); } }) }, t('doc.open')) },
        ], docs, { empty: t('doc.none') }));
      } catch (e) { list.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
    };
    if (canUpload) {
      const inp = h('input', { type: 'file', id: 'doc-file-' + dealId, accept: '.pdf,.png,.jpg,.jpeg,.docx', hidden: true });
      // Document type is stored with the file (contract, act, receipt...); the buyer sees it in the cabinet.
      const type = S.select('type', DOC_TYPES.map((k) => [k, t('doc.type.' + k, null, DOC_TYPE_RU[k])]), 'Contract', { 'aria-label': t('doc.typeLabel', null, 'Тип документа'), style: { width: 'auto' } });
      inp.addEventListener('change', async () => { const f = inp.files[0]; if (!f) return; if (f.size > 20 * 1024 * 1024) { S.toast(t('doc.tooBig'), 'bad'); return; } try { await S.api.uploadDocument(dealId, f, type.value); S.toast(t('doc.uploaded'), 'ok'); inp.value = ''; load(); } catch (e) { S.err(e); inp.value = ''; } });
      box.appendChild(h('div', { class: 'row' }, inp, type, h('label', { class: 'btn ghost sm', for: inp.id }, S.icon('clip', ''), t('doc.upload')), h('span', { class: 'hint' }, t('doc.hint'))));
      S.$$('svg', box).forEach((s) => { s.style.cssText = 'width:16px;height:16px;stroke:currentColor;fill:none;stroke-width:1.8'; });
    }
    box.appendChild(list); load();
    return box;
  };

  /* Chat: live through SignalR when the real API is connected, polling otherwise */
  S.panels.chat = (dealId, side) => {
    const log = h('div', { class: 'chat', 'aria-live': 'polite' });
    const input = h('input', { class: 'inp', id: 'chat-in-' + dealId, placeholder: t('chat.ph'), maxlength: 1000, autocomplete: 'off' });
    const btn = h('button', { class: 'btn', type: 'submit', 'aria-label': t('chat.send') }, S.icon('send'));
    const form = h('form', { class: 'row', style: { flexWrap: 'nowrap', marginTop: '10px' } }, input, btn);
    const box = h('div', null, log, form);
    let last = '';
    const mine = (m) => (m.mine !== undefined && m.mine !== null ? m.mine : m.sender === side || (side === 'staff' && m.sender !== 'customer' && m.sender !== 'client'));
    const draw = (msgs) => {
      const sig = msgs.map((m) => m.id).join(',');
      if (sig === last) return; last = sig;
      const nodes = msgs.length ? msgs.map((m) => h('div', { class: 'msg' + (mine(m) ? ' me' : '') }, m.text, h('small', null, (m.senderName || '') + ' · ' + S.fmt.dt(m.createdAt)))) : [h('div', { class: 'muted', style: { textAlign: 'center', padding: '24px' } }, t('chat.empty'))];
      log.replaceChildren(...nodes);
      log.scrollTop = log.scrollHeight;
    };
    const load = async () => { try { const m = await S.api.messages(dealId); draw(m.slice().sort((a, b) => new Date(a.createdAt) - new Date(b.createdAt))); } catch (e) { /* keep last */ } };
    form.addEventListener('submit', async (e) => { e.preventDefault(); const text = input.value.trim(); if (!text) return; await S.busy(btn, async () => { try { await S.api.sendMessage(dealId, text); input.value = ''; await load(); } catch (er) { S.err(er); } }); });
    load();
    const timer = setInterval(() => { if (!box.isConnected) { clearInterval(timer); stopLive(); return; } if (!document.hidden) load(); }, S.isDemo() ? 4000 : 9000);
    const off = S.bus.on('chat', (id) => { if (id === dealId) load(); });
    let conn = null;
    function stopLive() { off(); if (conn) { try { conn.stop(); } catch (e) { /* closed */ } conn = null; } }
    if (!S.isDemo() && window.signalR) {
      try {
        conn = new window.signalR.HubConnectionBuilder().withUrl(S.hubUrl(), { accessTokenFactory: () => S.accessToken() }).withAutomaticReconnect().configureLogging(window.signalR.LogLevel.None).build();
        conn.on('messageReceived', () => load());
        // Deal ids are GUIDs: pass the id as is (Number() would turn it into NaN).
        conn.onreconnected(() => conn.invoke('JoinDeal', dealId).catch(() => {}));
        conn.start().then(() => conn.invoke('JoinDeal', dealId)).catch(() => { /* polling covers it */ });
      } catch (e) { conn = null; }
    }
    return box;
  };

  /* Construction progress */
  S.panels.stages = (buildingId) => {
    const box = h('div', { class: 'stack' });
    (async () => {
      box.replaceChildren(S.skeleton(4, 60));
      try {
        const st = await S.api.stages(buildingId);
        if (!st.length) { box.replaceChildren(S.empty(t('con.none'), 'hammer')); return; }
        const total = Math.round(st.reduce((s, x) => s + x.percent, 0) / st.length);
        box.replaceChildren(
          h('div', { class: 'card flat' }, h('div', { class: 'row between' }, h('b', null, t('con.total')), h('b', { class: 'num' }, total + '%')), S.bar(total, 'gold')),
          h('div', { class: 'timeline' }, st.map((s) => h('div', { class: 'tl-item' }, S.ring(s.percent), h('div', null, h('div', { class: 'row between' }, h('b', null, s.title), s.date ? h('span', { class: 'muted num', style: { fontSize: '12.5px' } }, S.fmt.date(s.date)) : null), s.description ? h('p', { class: 'muted' }, s.description) : null, s.photos.length ? h('div', { class: 'photos' }, s.photos.map((p) => h('a', { href: S.fileSrc(p.url), target: '_blank', rel: 'noopener' }, h('img', { src: S.fileSrc(p.url), alt: s.title, loading: 'lazy' })))) : null)))));
      } catch (e) { box.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
    })();
    return box;
  };
})();
