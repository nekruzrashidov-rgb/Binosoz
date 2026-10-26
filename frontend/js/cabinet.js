/* Login by a code from e-mail (everyone), staff invitations and the buyer cabinet. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  S.views = S.views || {};

  /* ---------- login ---------- */
  // Where each role works: the platform section, the management workspace or the buyer cabinet.
  S.homeFor = (u) => (!u ? 'cabinet' : u.role === 'PlatformOwner' ? 'platform' : u.role === 'Customer' ? 'cabinet' : 'crm');
  const enter = (r) => { S.toast(t('login.welcome'), 'ok'); S.go(S.homeFor(r.user)); };

  // One form for everyone: e-mail, then the code from the letter. Only the platform owner gets a third step.
  S.views.login = function (root) {
    const card = h('div', { class: 'card login-card login' });
    root.appendChild(h('div', { class: 'wrap page-in' }, card));
    let step = 'email', emailVal = '', timer = 0, tick = null, challenge = null;

    const startTick = () => {
      clearInterval(tick); timer = 60;
      tick = setInterval(() => {
        if (!card.isConnected) { clearInterval(tick); return; }
        timer = Math.max(0, timer - 1);
        const b = S.$('[data-resend]', card);
        if (b) { b.disabled = timer > 0; b.textContent = timer > 0 ? t('login.resendIn', { s: timer }) : t('login.resend'); }
        if (!timer) clearInterval(tick);
      }, 1000);
    };
    const send = async () => { await S.api.emailSend(emailVal); startTick(); };

    function draw() {
      const form = h('form', { class: 'stack', novalidate: true });
      const btn = h('button', { class: 'btn lg block', type: 'submit' }, step === 'email' ? t('login.getCode') : t('login.enter'));
      let submit;
      if (step === 'email') {
        const email = S.input('email', { type: 'email', value: emailVal, placeholder: 'name@mail.tj', autocomplete: 'email', inputmode: 'email' });
        // The server answers the same for known and unknown addresses, so registration is offered as a link, not detected.
        form.append(h('h2', null, t('login.title')), h('p', { class: 'muted' }, t('login.sub')), S.field(t('login.email'), email), btn,
          h('p', { class: 'muted', style: { textAlign: 'center' } }, t('reg.ask'), ' ', h('button', { class: 'btn ghost sm', type: 'button', onclick: () => { emailVal = email.value.trim(); step = 'register'; draw(); } }, t('reg.link'))));
        submit = async () => {
          emailVal = email.value.trim();
          if (!/^\S+@\S+\.\S+$/.test(emailVal)) { S.showErrors(form, { message: t('val.email'), errors: { email: [t('val.email')] } }); return; }
          await send(); step = 'code'; draw();
        };
        setTimeout(() => email.focus(), 30);
      } else if (step === 'register') {
        const email = S.input('email', { type: 'email', value: emailVal, placeholder: 'name@mail.tj', autocomplete: 'email', inputmode: 'email' });
        const name = S.input('fullName', { autocomplete: 'name' });
        const phone = S.input('phone', { type: 'tel', value: '+992', autocomplete: 'tel' });
        btn.textContent = t('reg.submit');
        form.append(h('h2', null, t('reg.title')), h('p', { class: 'muted' }, t('reg.sub')), S.field(t('login.email'), email), S.field(t('f.fullName'), name), S.field(t('f.phone'), phone, t('f.phoneHint')), btn,
          h('button', { class: 'btn ghost sm', type: 'button', onclick: () => { step = 'email'; draw(); } }, t('reg.haveAccount')));
        submit = async () => {
          emailVal = email.value.trim();
          if (!/^\S+@\S+\.\S+$/.test(emailVal)) { S.showErrors(form, { message: t('val.email'), errors: { email: [t('val.email')] } }); return; }
          await S.api.register({ email: emailVal, fullName: name.value.trim(), phone: phone.value.replace(/[\s()-]/g, '') });
          startTick(); step = 'code'; draw();
        };
        setTimeout(() => (emailVal ? name : email).focus(), 30);
      } else if (step === 'code') {
        const code = S.input('code', { inputmode: 'numeric', maxlength: 6, placeholder: '••••••', autocomplete: 'one-time-code', style: { letterSpacing: '.4em', fontSize: '20px', textAlign: 'center', fontWeight: 800 } });
        const resend = h('button', { class: 'btn ghost sm', type: 'button', 'data-resend': '', disabled: timer > 0, onclick: async () => { try { await send(); S.toast(t('login.sent'), 'ok'); } catch (er) { S.err(er); } } }, timer > 0 ? t('login.resendIn', { s: timer }) : t('login.resend'));
        form.append(h('h2', null, t('login.codeTitle')), h('p', { class: 'muted' }, t('login.codeSub', { p: emailVal })), S.field(t('login.code'), code, t('login.codeHint')), btn,
          h('div', { class: 'row between' }, h('button', { class: 'btn ghost sm', type: 'button', onclick: () => { step = 'email'; clearInterval(tick); draw(); } }, t('login.changeEmail')), resend));
        submit = async () => {
          const r = await S.api.emailVerify(emailVal, code.value.replace(/\s/g, ''));
          if (r.challenge) { challenge = r.challenge; step = 'second'; clearInterval(tick); draw(); return; }
          enter(r);
        };
        setTimeout(() => code.focus(), 30);
      } else {
        const pass = S.input('password', { type: 'password', autocomplete: 'current-password' });
        form.append(h('h2', null, t('login.secondTitle')), h('p', { class: 'muted' }, t('login.secondSub')), S.field(t('login.secondLabel'), pass, t('login.secondHint')), btn,
          h('button', { class: 'btn ghost sm', type: 'button', onclick: () => { step = 'email'; challenge = null; draw(); } }, t('login.again')));
        submit = async () => {
          // Six digits are a code from the authenticator app, anything else is the password.
          const v = pass.value.trim(); const totp = /^\d{6}$/.test(v);
          enter(await S.api.secondFactor(challenge.token, totp ? null : pass.value, totp ? v : null));
        };
        setTimeout(() => pass.focus(), 30);
      }
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await submit(); } catch (er) { S.showErrors(form, er); } }); });
      card.replaceChildren(form);
    }
    draw();
    return () => clearInterval(tick);
  };

  /* ---------- staff invitation: #invite=<token> ---------- */
  S.views.invite = function (root) {
    const token = (location.hash.match(/invite=([0-9A-Fa-f]+)/) || [])[1] || '';
    const card = h('div', { class: 'card login-card login' });
    root.appendChild(h('div', { class: 'wrap page-in' }, card));
    const btn = h('button', { class: 'btn lg block' }, t('invite.accept'));
    const msg = h('div');
    btn.addEventListener('click', () => S.busy(btn, async () => {
      msg.replaceChildren();
      try { enter(await S.api.acceptInvitation(token)); } catch (er) { msg.replaceChildren(h('div', { class: 'form-err' }, er.message)); }
    }));
    card.append(h('h2', null, t('invite.title')), h('p', { class: 'muted', style: { margin: '8px 0 16px' } }, t('invite.text')), token ? btn : h('div', { class: 'form-err' }, t('invite.bad')), msg);
    return () => {};
  };

  /* ---------- buyer cabinet ---------- */
  S.views.cabinet = function (root) {
    if (!S.api.isAuthed) return S.views.login(root);
    if (!S.api.isCustomer()) {
      const w = h('div', { class: 'wrap page-in' }, h('div', { class: 'card login-card login' }, h('h2', null, t('cab.staffTitle')), h('p', { class: 'muted', style: { margin: '8px 0 16px' } }, t('cab.staffText')), h('div', { class: 'row' }, h('button', { class: 'btn', onclick: () => S.go(S.homeFor(S.api.user)) }, t('cab.toCrm')), h('button', { class: 'btn ghost', onclick: async () => { await S.api.logout(); S.go('cabinet'); } }, t('nav.logout')))));
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
