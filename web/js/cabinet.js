/* Login (buyers by SMS, staff by e-mail) and the buyer cabinet. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  S.views = S.views || {};

  /* ---------- login ---------- */
  S.views.login = function (root, mode) {
    let tab = mode || 'customer';
    const wrap = h('div', { class: 'wrap page-in' });
    root.appendChild(wrap);
    const card = h('div', { class: 'card login-card login' });
    wrap.appendChild(card);

    function draw() {
      const seg = h('div', { class: 'seg', style: { marginBottom: '18px' } }, [['customer', t('login.buyer')], ['staff', t('login.staff')]].map(([k, l]) => h('button', { class: tab === k ? 'on' : '', onclick: () => { tab = k; draw(); } }, l)));
      card.replaceChildren(seg, tab === 'customer' ? buyerForm() : staffForm());
    }

    function buyerForm() {
      let step = 1, phoneVal = '+992', timer = 0, tick;
      const box = h('div');
      const render = () => {
        const form = h('form', { class: 'stack', novalidate: true });
        if (step === 1) {
          const phone = S.input('phone', { type: 'tel', value: phoneVal, placeholder: '+992 9XX XX XX XX', autocomplete: 'tel' });
          const btn = h('button', { class: 'btn lg block', type: 'submit' }, t('login.getCode'));
          form.append(h('h2', null, t('login.buyerTitle')), h('p', { class: 'muted' }, t('login.buyerSub')), S.field(t('f.phone'), phone, t('f.phoneHint')), btn);
          form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); phoneVal = phone.value.replace(/[\s()-]/g, ''); await S.busy(btn, async () => { try { await S.api.smsSend(phoneVal); step = 2; timer = 60; startTick(); render(); } catch (er) { S.showErrors(form, er); } }); });
        } else {
          const code = S.input('code', { inputmode: 'numeric', maxlength: 6, placeholder: '••••••', autocomplete: 'one-time-code', style: { letterSpacing: '.4em', fontSize: '20px', textAlign: 'center', fontWeight: 800 } });
          const btn = h('button', { class: 'btn lg block', type: 'submit' }, t('login.enter'));
          const resend = h('button', { class: 'btn ghost sm', type: 'button', disabled: timer > 0, onclick: async () => { try { await S.api.smsSend(phoneVal); timer = 60; startTick(); render(); S.toast(t('login.sent'), 'ok'); } catch (er) { S.err(er); } } }, timer > 0 ? t('login.resendIn', { s: timer }) : t('login.resend'));
          form.append(h('h2', null, t('login.codeTitle')), h('p', { class: 'muted' }, t('login.codeSub', { p: phoneVal })), S.field(t('login.code'), code), btn, h('div', { class: 'row between' }, h('button', { class: 'btn ghost sm', type: 'button', onclick: () => { step = 1; clearInterval(tick); render(); } }, t('login.changePhone')), resend));
          form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await S.api.smsVerify(phoneVal, code.value.trim()); S.toast(t('login.welcome'), 'ok'); S.go(S.api.isStaff() ? 'crm' : 'cabinet'); } catch (er) { S.showErrors(form, er); } }); });
          setTimeout(() => code.focus(), 30);
        }
        box.replaceChildren(form);
        if (S.isDemo()) box.appendChild(h('div', { class: 'demo-box' }, t('login.demoBuyer'), ' ', h('button', { type: 'button', onclick: () => { phoneVal = S.mock.demoCustomerPhone; step === 1 ? (S.$('input[name=phone]', box).value = phoneVal) : null; if (step === 2) { S.$('input[name=code]', box).value = '123456'; } else { S.toast(t('login.demoFilled')); } } }, t('login.demoFill'))));
      };
      const startTick = () => { clearInterval(tick); tick = setInterval(() => { if (!box.isConnected) { clearInterval(tick); return; } timer = Math.max(0, timer - 1); const b = S.$('button.btn.ghost.sm:last-child', box); if (b && step === 2) { b.disabled = timer > 0; b.textContent = timer > 0 ? t('login.resendIn', { s: timer }) : t('login.resend'); } if (!timer) clearInterval(tick); }, 1000); };
      render();
      return box;
    }

    function staffForm() {
      const email = S.input('email', { type: 'email', placeholder: 'name@company.tj', autocomplete: 'username' });
      const pass = S.input('password', { type: 'password', autocomplete: 'current-password' });
      const btn = h('button', { class: 'btn lg block', type: 'submit' }, t('login.enter'));
      const form = h('form', { class: 'stack', novalidate: true }, h('h2', null, t('login.staffTitle')), h('p', { class: 'muted' }, t('login.staffSub')), S.field(t('login.email'), email), S.field(t('login.password'), pass), btn);
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await S.api.login(email.value.trim(), pass.value); S.toast(t('login.welcome'), 'ok'); S.go(S.api.isStaff() ? 'crm' : 'cabinet'); } catch (er) { S.showErrors(form, er); } }); });
      const box = h('div', null, form);
      if (S.isDemo()) box.appendChild(h('div', { class: 'demo-box' }, h('div', null, t('login.demoStaff')), h('div', { style: { marginTop: '6px', display: 'flex', flexWrap: 'wrap', gap: '4px 12px' } }, S.mock.demoUsers.map((u) => h('button', { type: 'button', onclick: () => { email.value = u.email; pass.value = u.password; } }, t('role.' + u.role))))));
      return box;
    }
    draw();
    return () => {};
  };

  /* ---------- buyer cabinet ---------- */
  S.views.cabinet = function (root) {
    if (!S.api.isAuthed) return S.views.login(root, 'customer');
    if (!S.api.isCustomer()) {
      const w = h('div', { class: 'wrap page-in' }, h('div', { class: 'card login-card login' }, h('h2', null, t('cab.staffTitle')), h('p', { class: 'muted', style: { margin: '8px 0 16px' } }, t('cab.staffText')), h('div', { class: 'row' }, h('button', { class: 'btn', onclick: () => S.go('crm') }, t('cab.toCrm')), h('button', { class: 'btn ghost', onclick: async () => { await S.api.logout(); S.go('cabinet'); } }, t('nav.logout')))));
      root.appendChild(w); return () => {};
    }
    const page = h('div', { class: 'page-in' }); root.appendChild(page);
    let deals = [], deal = null, sched = null, stopSel = false;
    const items = () => [
      { key: 'overview', label: t('cab.overview'), icon: 'home' }, { key: 'schedule', label: t('cab.schedule'), icon: 'wallet' }, { key: 'docs', label: t('cab.docs'), icon: 'file' },
      { key: 'chat', label: t('cab.chat'), icon: 'send' }, { key: 'build', label: t('cab.build'), icon: 'hammer' },
    ];
    let ws = null;
    (async () => {
      page.appendChild(h('div', { class: 'wrap', style: { paddingTop: '40px' } }, S.skeleton(4, 60)));
      try { deals = await S.api.myDeals(); } catch (e) { page.replaceChildren(h('div', { class: 'wrap', style: { paddingTop: '40px' } }, h('div', { class: 'form-err' }, e.message))); return; }
      if (stopSel) return;
      if (!deals.length) { page.replaceChildren(h('div', { class: 'wrap' }, h('div', { class: 'card login-card login', style: { textAlign: 'center' } }, S.empty(t('cab.noDeals'), 'building'), h('button', { class: 'btn', onclick: () => S.go('home') }, t('cab.pick')))));  return; }
      deal = deals[0];
      ws = S.workspace(items(), 'overview', (k, main) => show(k, main));
      page.replaceChildren(ws.el);
      show('overview', ws.main);
    })();

    async function ensureSchedule() { if (sched && sched.dealId === deal.id) return sched; try { sched = await S.api.schedule(deal.id); sched.dealId = deal.id; } catch (e) { sched = { dealId: deal.id, none: true, error: e }; } return sched; }

    function dealPicker() {
      if (deals.length < 2) return null;
      // Real ids are long GUIDs (UUIDv7, the tail tells them apart): show 8 characters.
      const short = (id) => (typeof id === 'string' && id.length > 12 ? id.replace(/-/g, '').slice(-8).toUpperCase() : id);
      const sel = S.select('deal', deals.map((d) => [d.id, t('cab.dealOpt', { n: d.apartmentNumber, id: short(d.id) })]), deal.id); sel.addEventListener('change', () => { deal = deals.find((d) => String(d.id) === sel.value); sched = null; ws.go(ws.current); });
      return h('div', { style: { maxWidth: '320px', marginBottom: '14px' } }, S.field(t('cab.deal'), sel));
    }

    async function show(k, main) {
      main.replaceChildren(h('h1', null, items().find((i) => i.key === k).label), h('p', { class: 'sub' }, t('cab.hello', { name: (S.api.user && S.api.user.name) || '' }) + ' · ' + t('cab.aptLine', { n: deal.apartmentNumber, f: deal.floor })), dealPicker());
      const body = h('div', { class: 'stack' }, S.skeleton(3, 80)); main.appendChild(body);
      try {
        if (k === 'overview') body.replaceChildren(...(await overview()));
        else if (k === 'schedule') { const s = await ensureSchedule(); body.replaceChildren(s.none ? S.empty(t('cab.noSchedule'), 'wallet') : h('div', null, S.panels.schedule(s), h('div', { class: 'row', style: { marginTop: '14px' } }, pdfBtn()))); }
        else if (k === 'docs') body.replaceChildren(S.panels.documents(deal.id, false));
        else if (k === 'chat') body.replaceChildren(h('div', { class: 'card' }, S.panels.chat(deal.id, 'customer')));
        else if (k === 'build') body.replaceChildren(S.panels.stages(deal.buildingId || 1));
      } catch (e) { body.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
    }

    function pdfBtn() {
      const date = S.input('date', { type: 'date', value: S.today(), 'aria-label': t('cab.actDate') }); date.style.maxWidth = '170px';
      return h('div', { class: 'row' }, h('button', { class: 'btn', onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/deals/' + deal.id + '/schedule.pdf', null, t('cab.pdfSchedule')).catch(S.err)) }, t('cab.pdfSchedule')), h('button', { class: 'btn ghost', onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/deals/' + deal.id + '/statement.pdf', { date: date.value }, t('cab.pdfAct')).catch(S.err)) }, t('cab.pdfAct')), h('span', { class: 'hint' }, t('cab.actDate')), date);
    }

    async function overview() {
      const s = await ensureSchedule();
      const price = deal.price - deal.discount;
      const paid = s.none ? deal.paid : s.paid; const pct = price ? Math.min(100, Math.round((paid / price) * 100)) : 0;
      const next = s.none || deal.stage === 'Cancelled' ? null : s.items.find((i) => i.status !== 'Paid');
      const c = 2 * Math.PI * 46;
      const ring = h('div', { class: 'pay-ring' }); ring.innerHTML = '<svg viewBox="0 0 108 108"><defs><linearGradient id="gold-g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#f2c46a"/><stop offset="1" stop-color="#cf9a33"/></linearGradient></defs><circle class="bg" cx="54" cy="54" r="46"/><circle class="fg" cx="54" cy="54" r="46" stroke-dasharray="' + c + '" stroke-dashoffset="' + c + '"/></svg><b>' + pct + '%</b>';
      requestAnimationFrame(() => requestAnimationFrame(() => { const fg = S.$('.fg', ring); if (fg) fg.style.strokeDashoffset = c * (1 - pct / 100); }));
      const left = deal.remaining !== undefined && !s.none ? s.remaining : price - paid;
      const info = h('div', { class: 'card' }, h('div', { class: 'row between' }, h('h3', null, t('apt.title', { n: deal.apartmentNumber })), S.stageChip(deal.stage)),
        h('dl', { class: 'kv' }, kv(t('floor.nLabel'), deal.floor), kv(t('f.rooms'), S.fmt.rooms(deal.rooms)), kv(t('f.area'), S.fmt.area(deal.area)), kv(t('deal.price'), S.fmt.money(deal.price, deal.currency)), deal.discount ? kv(t('deal.discount'), '− ' + S.fmt.money(deal.discount, deal.currency)) : null, kv(t('deal.manager'), deal.managerName || '—'), kv(t('deal.since'), S.fmt.date(deal.createdAt))));
      const pay = h('div', { class: 'card' }, h('h3', null, t('cab.payments')), h('div', { class: 'paybig' }, ring, h('div', { class: 'stack', style: { gap: '4px' } }, h('div', { class: 'muted' }, t('sch.paid')), h('div', { class: 'big', style: { fontSize: '24px' } }, S.fmt.money(paid, deal.currency)), h('div', { class: 'muted' }, t('sch.left') + ': ' + S.fmt.money(left, deal.currency)), s.eq ? h('div', { class: 'hint' }, '≈ ' + S.fmt.money(s.eq.paid, s.eq.currency) + ' · ' + t('price.estimate')) : null)),
        next ? h('div', { class: 'next', style: { marginTop: '14px' } }, h('div', null, h('div', { class: 'muted', style: { fontSize: '12px', fontWeight: 800, textTransform: 'uppercase' } }, t('cab.next')), h('b', null, S.fmt.date(next.dueDate)), next.status === 'Overdue' ? h('span', { style: { marginLeft: '8px' } }, S.chip('Overdue', t('pay.Overdue'))) : null), h('b', { class: 'big', style: { fontSize: '20px' } }, S.fmt.money(next.amountDue - next.amountPaid, deal.currency))) : null);
      return [h('div', { class: 'deal-head' }, info, pay), s.none ? null : h('div', { class: 'row' }, pdfBtn()), s.none ? h('div', { class: 'card flat' }, t('cab.noSchedule')) : null];
    }
    const kv = (k, v) => [h('dt', null, k), h('dd', null, v === undefined || v === null || v === '' ? '—' : v)];
    return () => { stopSel = true; };
  };
})();
