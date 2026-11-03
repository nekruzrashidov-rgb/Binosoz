/* Login by a code from e-mail (everyone), staff invitations and the buyer cabinet. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  S.views = S.views || {};

  /* ---------- login ---------- */
  // Where each role works: the platform section, the management workspace or the buyer cabinet.
  S.homeFor = (u) => (!u ? 'cabinet' : u.role === 'SuperAdmin' ? 'platform' : u.role === 'Customer' ? 'cabinet' : 'crm');
  const enter = (r) => { S.toast(t('login.welcome'), 'ok'); S.go(S.homeFor(r.user)); };

  // The login pages have no site header: the logo above the card leads back to the site.
  const loginBrand = () => h('a', { class: 'login-brand', href: '/', 'aria-label': 'Binosoz' }, S.logo(), h('span', null, 'BINOSOZ'));

  // A password with a show/hide button (👁); the label still points at the input itself.
  function passwordField(label, name, autocomplete, hint) {
    const input = S.input(name, { type: 'password', autocomplete });
    const eye = h('button', { type: 'button', class: 'pass-eye', 'aria-label': t('login.showPass'), title: t('login.showPass'), 'aria-pressed': 'false' }, '👁');
    eye.addEventListener('click', () => {
      const show = input.type === 'password';
      input.type = show ? 'text' : 'password';
      const label2 = t(show ? 'login.hidePass' : 'login.showPass');
      eye.setAttribute('aria-pressed', String(show)); eye.setAttribute('aria-label', label2); eye.title = label2;
      input.focus();
    });
    const field = S.field(label, h('div', { class: 'pass-wrap' }, input, eye), hint);
    S.$('label', field).htmlFor = input.id;
    field.dataset.name = name;
    return { field, input };
  }

  // Two tabs: staff sign in with e-mail and password (then a 2FA code if it is on), buyers with a code from e-mail.
  // /login#reset=<token> from the "forgot password" letter opens the new-password form.
  S.views.login = function (root) {
    const card = h('div', { class: 'card login-card login' });
    root.appendChild(h('div', { class: 'wrap page-in' }, loginBrand(), card));
    const tokenInHash = () => (location.hash.match(/reset=([A-Za-z0-9_-]+)/) || [])[1] || '';
    let resetToken = tokenInHash();
    let who = resetToken || S.store.get('sitora.loginAs') !== 'buyer' ? 'staff' : 'buyer';
    let step = resetToken ? 'reset' : who, emailVal = '', timer = 0, tick = null, challenge = null, notice = '';
    const isEmail = (v) => /^\S+@\S+\.\S+$/.test(v);
    const badEmail = (form) => S.showErrors(form, { message: t('val.email'), errors: { email: [t('val.email')] } });
    const tabs = () => h('div', { class: 'login-tabs', role: 'tablist', 'aria-label': t('login.as') }, [['staff', 'login.staff'], ['buyer', 'login.buyer']].map(([k, key]) =>
      h('button', { type: 'button', role: 'tab', class: who === k ? 'on' : '', 'aria-selected': String(who === k), onclick: () => {
        if (who === k && step === k) return;
        who = k; S.store.set('sitora.loginAs', k); step = k; notice = ''; clearInterval(tick); draw();
      } }, t(key))));
    const link = (label, onclick) => h('button', { class: 'btn ghost sm', type: 'button', onclick }, label);

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

    const codeInput = (name) => S.input(name, { inputmode: 'numeric', maxlength: 6, placeholder: '••••••', autocomplete: 'one-time-code', style: { letterSpacing: '.4em', fontSize: '20px', textAlign: 'center', fontWeight: 800 } });

    function draw() {
      const form = h('form', { class: 'stack', novalidate: true });
      const btn = h('button', { class: 'btn lg block', type: 'submit' }, t('login.enter'));
      const info = notice ? h('div', { class: 'form-ok', role: 'status' }, notice) : null;
      let submit, focus;
      if (step === 'staff') {
        const email = S.input('email', { type: 'email', value: emailVal, placeholder: 'name@mail.tj', autocomplete: 'username', inputmode: 'email' });
        const pass = passwordField(t('login.password'), 'password', 'current-password');
        form.append(h('h2', null, t('login.staffTitle')), h('p', { class: 'muted' }, t('login.staffHint')), info, S.field(t('login.email'), email), pass.field, btn,
          h('p', { style: { textAlign: 'center', margin: 0 } }, link(t('login.forgot'), () => { emailVal = email.value.trim(); notice = ''; step = 'forgot'; draw(); })));
        submit = async () => {
          emailVal = email.value.trim();
          if (!isEmail(emailVal)) { badEmail(form); return; }
          try {
            const r = await S.api.passwordLogin(emailVal, pass.input.value);
            if (r.challenge) { challenge = r.challenge; notice = ''; step = '2fa'; draw(); return; }
            enter(r);
          } catch (er) {
            // A buyer's address: they sign in with a code — open that tab with the address already filled in.
            if (er.status === 400 && who === 'staff') { who = 'buyer'; S.store.set('sitora.loginAs', 'buyer'); step = 'buyer'; notice = t('login.useCode'); draw(); return; }
            throw er;
          }
        };
        focus = emailVal ? pass.input : email;
      } else if (step === '2fa') {
        const code = codeInput('code');
        form.append(h('h2', null, t('login.twoFaTitle')), h('p', { class: 'muted' }, t('login.twoFaSub')), S.field(t('login.twoFaLabel'), code), btn,
          link(t('login.again'), () => { challenge = null; step = 'staff'; draw(); }));
        submit = async () => enter(await S.api.twoFactorVerify(challenge.token, code.value.replace(/\s/g, '')));
        focus = code;
      } else if (step === 'forgot') {
        const email = S.input('email', { type: 'email', value: emailVal, placeholder: 'name@mail.tj', autocomplete: 'email', inputmode: 'email' });
        btn.textContent = t('login.forgotSend');
        form.append(h('h2', null, t('login.forgotTitle')), h('p', { class: 'muted' }, t('login.forgotSub')), info, S.field(t('login.email'), email), btn,
          link(t('login.forgotBack'), () => { notice = ''; step = 'staff'; draw(); }));
        submit = async () => {
          emailVal = email.value.trim();
          if (!isEmail(emailVal)) { badEmail(form); return; }
          const r = await S.api.passwordResetRequest(emailVal);
          notice = (r && r.message) || t('login.forgotSent'); draw();
        };
        focus = email;
      } else if (step === 'reset') {
        const pass = passwordField(t('login.newPassword'), 'newPassword', 'new-password', t('login.passRules'));
        const again = passwordField(t('login.repeatPassword'), 'repeat', 'new-password');
        btn.textContent = t('login.resetSave');
        form.append(h('h2', null, t('login.resetTitle')), pass.field, again.field, btn,
          link(t('login.forgotBack'), () => { history.replaceState(null, '', location.pathname); step = 'staff'; draw(); }));
        submit = async () => {
          if (pass.input.value !== again.input.value) { S.showErrors(form, { message: t('login.mismatch'), errors: { repeat: [t('login.mismatch')] } }); return; }
          await S.api.passwordReset(resetToken, pass.input.value);
          history.replaceState(null, '', location.pathname);
          notice = t('login.resetDone'); step = 'staff'; draw();
        };
        focus = pass.input;
      } else if (step === 'buyer') {
        const email = S.input('email', { type: 'email', value: emailVal, placeholder: 'name@mail.tj', autocomplete: 'email', inputmode: 'email' });
        btn.textContent = t('login.sendCode');
        // The server answers the same for known and unknown addresses, so registration is offered as a link, not detected.
        form.append(h('h2', null, t('login.buyerTitle')), h('p', { class: 'muted' }, t('login.hintBuyer')), info, S.field(t('login.email'), email), btn,
          h('p', { class: 'muted', style: { textAlign: 'center', margin: 0 } }, t('reg.ask'), ' ', link(t('reg.link'), () => { emailVal = email.value.trim(); notice = ''; step = 'register'; draw(); })));
        submit = async () => {
          emailVal = email.value.trim();
          if (!isEmail(emailVal)) { badEmail(form); return; }
          await send(); notice = ''; step = 'code'; draw();
        };
        focus = email;
      } else if (step === 'register') {
        const first = S.input('firstName', { autocomplete: 'given-name' });
        const last = S.input('lastName', { autocomplete: 'family-name' });
        const email = S.input('email', { type: 'email', value: emailVal, placeholder: 'name@mail.tj', autocomplete: 'email', inputmode: 'email' });
        const phone = S.input('phone', { type: 'tel', value: '+992', autocomplete: 'tel' });
        btn.textContent = t('login.sendCode');
        form.append(h('h2', null, t('reg.title')), h('p', { class: 'muted' }, t('reg.sub')), S.field(t('reg.firstName'), first), S.field(t('reg.lastName'), last),
          S.field(t('login.email'), email), S.field(t('f.phone'), phone, t('f.phoneHint')), btn,
          link(t('reg.haveAccount'), () => { step = 'buyer'; draw(); }));
        submit = async () => {
          emailVal = email.value.trim();
          if (!first.value.trim() || !last.value.trim()) { S.showErrors(form, { message: t('reg.nameRequired'), errors: Object.assign({}, first.value.trim() ? {} : { firstName: [t('reg.nameRequired')] }, last.value.trim() ? {} : { lastName: [t('reg.nameRequired')] }) }); return; }
          if (!isEmail(emailVal)) { badEmail(form); return; }
          await S.api.register({ email: emailVal, firstName: first.value.trim(), lastName: last.value.trim(), phone: phone.value.replace(/[\s()-]/g, '') });
          startTick(); step = 'code'; draw();
        };
        focus = first;
      } else {
        const code = codeInput('code');
        const resend = h('button', { class: 'btn ghost sm', type: 'button', 'data-resend': '', disabled: timer > 0, onclick: async () => { try { await send(); S.toast(t('login.sent'), 'ok'); } catch (er) { S.err(er); } } }, timer > 0 ? t('login.resendIn', { s: timer }) : t('login.resend'));
        form.append(h('h2', null, t('login.codeTitle')), h('p', { class: 'muted' }, t('login.codeSub', { p: emailVal })), S.field(t('login.code'), code, t('login.codeHint')), btn,
          h('div', { class: 'row between' }, link(t('login.changeEmail'), () => { step = 'buyer'; clearInterval(tick); draw(); }), resend));
        submit = async () => enter(await S.api.emailVerify(emailVal, code.value.replace(/\s/g, '')));
        focus = code;
      }
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await submit(); } catch (er) { S.showErrors(form, er); } }); });
      // Tabs only on the first screens; the code, 2FA and password-reset steps keep their own "back" link.
      card.replaceChildren(step === 'staff' || step === 'buyer' || step === 'register' ? tabs() : null, form);
      setTimeout(() => focus && focus.focus(), 30);
    }
    draw();
    // The reset link opened while this page is already on screen changes only the hash.
    const onHash = () => { const token = tokenInHash(); if (token) { resetToken = token; who = 'staff'; notice = ''; step = 'reset'; draw(); } };
    window.addEventListener('hashchange', onHash);
    return () => { clearInterval(tick); window.removeEventListener('hashchange', onHash); };
  };

  /* ---------- staff invitation: #invite=<token> ---------- */
  S.views.invite = function (root) {
    const token = (location.hash.match(/invite=([0-9A-Fa-f]+)/) || [])[1] || '';
    const card = h('div', { class: 'card login-card login' });
    root.appendChild(h('div', { class: 'wrap page-in' }, loginBrand(), card));
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
    // Unread messages from the manager and whether they are online — without marking anything as read.
    let chat = { unread: 0 };
    const refreshChat = () => S.api.chatUnread(deal.id).then((u) => { chat = u; if (ws) ws.setItems(items()); return u; }).catch(() => chat);
    const items = () => [
      { key: 'overview', label: t('cab.overview'), icon: 'home' }, { key: 'schedule', label: t('cab.schedule'), icon: 'wallet' }, { key: 'docs', label: t('cab.docs'), icon: 'file' },
      { key: 'chat', label: t('cab.chat'), icon: 'send', badge: chat.unread || null }, { key: 'build', label: t('cab.build'), icon: 'hammer' },
    ];
    let ws = null;
    (async () => {
      page.appendChild(h('div', { class: 'wrap', style: { paddingTop: '40px' } }, S.skeleton(4, 60)));
      try { deals = await S.api.myDeals(); } catch (e) { page.replaceChildren(S.errorCard(e)); return; }
      if (stopSel) return;
      if (!deals.length) { page.replaceChildren(h('div', { class: 'wrap' }, h('div', { class: 'card login-card login cab-empty', style: { textAlign: 'center' } }, h('h2', null, t('cab.hello', { name: (S.api.user && S.api.user.name) || '' }) + '!'), S.empty(t('cab.noActive'), 'building'), h('p', { class: 'muted' }, t('cab.noDeals')), h('a', { class: 'btn', href: '/apartments' }, t('cab.seeApts'))))); return; }
      deal = deals[0];
      ws = S.workspace(items(), 'overview', (k, main) => show(k, main));
      page.replaceChildren(ws.el);
      show('overview', ws.main);
      refreshChat();
      // Back from the bank's card page (/cabinet?success=1, ?payment=failed or ?payment=cancelled).
      const back = new URLSearchParams(location.search);
      if (back.has('success') || back.has('payment')) payReturn(back);
    })();

    // The payment reaches the schedule when its status is asked: the server then sees that the bank confirmed it.
    async function payReturn(q) {
      history.replaceState(null, '', location.pathname);
      let p = null; try { p = JSON.parse(S.store.get('sitora.pay') || 'null'); } catch (e) { p = null; }
      S.store.del('sitora.pay');
      if (q.get('payment') === 'cancelled') { S.toast(t('cab.payCancelled')); return; }
      if (!p) { if (q.has('success')) S.toast(t('cab.payDone'), 'ok'); return; }
      try {
        const r = await S.api.paymentStatus(p.dealId, p.id);
        if (r.status === 'Paid') S.toast(t('cab.payDone'), 'ok'); else if (r.status === 'Pending') S.toast(t('cab.payPending'), 'warn'); else S.toast(t('cab.payFailed'), 'bad');
      } catch (e) { S.err(e); return; }
      deal = deals.find((d) => String(d.id) === String(p.dealId)) || deal; sched = null;
      ws.go('schedule');
    }

    // "Pay by card" for the next unpaid line of the schedule: the bank's page takes the card and brings the buyer back here.
    function payCta(s) {
      const next = s.items.find((i) => i.status !== 'Paid');
      if (!next || !(s.remaining > 0) || deal.stage === 'Cancelled') return null;
      const due = Math.min(next.amountDue - next.amountPaid, s.remaining);
      const pay = (e) => S.busy(e.currentTarget, async () => {
        try {
          const r = await S.api.initiatePayment(deal.id, { returnUrl: '/cabinet?success=1' });
          S.store.set('sitora.pay', JSON.stringify({ dealId: deal.id, id: r.gatewayPaymentId }));
          location.href = r.paymentUrl;
        } catch (er) { S.err(er); }
      });
      return h('div', { class: 'card pay-cta' }, h('span', null, t('cab.payNext', { sum: S.fmt.money(due, s.currency) })), h('button', { class: 'btn gold', onclick: pay }, t('cab.payCard')));
    }

    async function ensureSchedule() { if (sched && sched.dealId === deal.id) return sched; try { sched = await S.api.schedule(deal.id); sched.dealId = deal.id; } catch (e) { sched = { dealId: deal.id, none: true, error: e }; } return sched; }

    function dealPicker() {
      if (deals.length < 2) return null;
      // Real ids are long GUIDs (UUIDv7, the tail tells them apart): show 8 characters.
      const short = (id) => (typeof id === 'string' && id.length > 12 ? id.replace(/-/g, '').slice(-8).toUpperCase() : id);
      const sel = S.select('deal', deals.map((d) => [d.id, t('cab.dealOpt', { n: d.apartmentNumber, id: short(d.id) })]), deal.id); sel.addEventListener('change', () => { deal = deals.find((d) => String(d.id) === sel.value); sched = null; ws.go(ws.current); });
      return h('div', { style: { maxWidth: '320px', marginBottom: '14px' } }, S.field(t('cab.deal'), sel));
    }

    async function show(k, main) {
      // The overview greets in its own card; the other sections keep the short line under the title.
      main.replaceChildren(h('h1', null, items().find((i) => i.key === k).label), k === 'overview' ? null : h('p', { class: 'sub' }, t('cab.hello', { name: (S.api.user && S.api.user.name) || '' }) + ' · ' + t('cab.aptLine', { n: deal.apartmentNumber, f: deal.floor })), dealPicker());
      const body = h('div', { class: 'stack' }, S.skeleton(3, 80)); main.appendChild(body);
      try {
        if (k === 'overview') body.replaceChildren(...(await overview()));
        else if (k === 'schedule') { const s = await ensureSchedule(); body.replaceChildren(s.none ? S.empty(t('cab.noSchedule'), 'wallet') : h('div', null, payCta(s), S.panels.schedule(s), h('div', { class: 'row', style: { marginTop: '14px' } }, pdfBtn()))); }
        else if (k === 'docs') body.replaceChildren(S.panels.documents(deal.id, false));
        else if (k === 'chat') {
          // Who answers and whether they are here now (any action in the CRM during the last 5 minutes).
          const head = h('div', { class: 'chat-head' });
          const drawHead = (u) => head.replaceChildren(h('span', { class: 'avatar' }, S.fmt.initials(u.managerName || deal.managerName)),
            h('span', null, h('b', null, u.managerName || deal.managerName || t('deal.manager')), h('small', { class: 'chat-presence' + (u.managerOnline ? ' on' : '') }, h('i', { 'aria-hidden': 'true' }),
              u.managerOnline ? t('chat.online') : u.managerLastSeenAt ? t('chat.seen', { d: S.fmt.dt(u.managerLastSeenAt) }) : t('chat.offline'))));
          drawHead(chat);
          body.replaceChildren(h('div', { class: 'card' }, head, S.panels.chat(deal.id, 'customer')));
          // Opening the chat reads the messages: refresh the presence now and clear the badge once the history has loaded.
          refreshChat().then(drawHead); setTimeout(refreshChat, 1500);
        }
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
      // The deal itself carries only the manager's id; the name comes with the chat's presence info.
      const manager = h('span', null, deal.managerName || chat.managerName || '—');
      if (!deal.managerName && !chat.managerName) refreshChat().then((u) => { if (u && u.managerName) manager.textContent = u.managerName; });
      const info = h('div', { class: 'card' }, h('div', { class: 'row between' }, h('h3', null, t('apt.title', { n: deal.apartmentNumber })), S.stageChip(deal.stage)),
        h('dl', { class: 'kv' }, kv(t('floor.nLabel'), deal.floor), kv(t('f.rooms'), S.fmt.rooms(deal.rooms)), kv(t('f.area'), S.fmt.area(deal.area)), kv(t('deal.price'), S.fmt.money(deal.price, deal.currency)), deal.discount ? kv(t('deal.discount'), '− ' + S.fmt.money(deal.discount, deal.currency)) : null, kv(t('deal.manager'), manager), kv(t('deal.since'), S.fmt.date(deal.createdAt))));
      const pay = h('div', { class: 'card' }, h('h3', null, t('cab.payments')), h('div', { class: 'paybig' }, ring, h('div', { class: 'stack', style: { gap: '4px' } }, h('div', { class: 'muted' }, t('sch.paid')), h('div', { class: 'big', style: { fontSize: '24px' } }, S.fmt.money(paid, deal.currency)), h('div', { class: 'muted' }, t('sch.left') + ': ' + S.fmt.money(left, deal.currency)), s.eq ? h('div', { class: 'hint' }, '≈ ' + S.fmt.money(s.eq.paid, s.eq.currency) + ' · ' + t('price.estimate')) : null)),
        countBox(s),
        next ? h('div', { class: 'next' + (next.status === 'Overdue' ? ' overdue' : ''), style: { marginTop: '14px' } }, h('div', null, h('div', { class: 'muted', style: { fontSize: '12px', fontWeight: 800, textTransform: 'uppercase' } }, t('cab.next')), h('b', null, S.fmt.date(next.dueDate)), next.status === 'Overdue' ? h('span', { style: { marginLeft: '8px' } }, S.chip('Overdue', t('pay.Overdue'))) : null), h('b', { class: 'big', style: { fontSize: '20px' } }, S.fmt.money(next.amountDue - next.amountPaid, deal.currency))) : null);
      // The whole name: in the CRM it is often written surname first, so the first word is not a safe "first name".
      const firstName = (S.api.user && S.api.user.name) || '';
      const welcome = h('div', { class: 'card cab-welcome' }, h('div', null, h('h2', null, t('cab.hi', { name: firstName })),
        h('p', null, t('cab.yourApt', { b: deal.buildingName || t('cab.building'), n: deal.apartmentNumber, f: deal.floor }))),
        h('a', { class: 'btn ghost sm', href: '/apartments' }, t('cab.seeApts')));
      return [welcome, h('div', { class: 'deal-head' }, info, pay), buildCard(), s.none ? null : h('div', { class: 'row' }, pdfBtn()), s.none ? h('div', { class: 'card flat' }, t('cab.noSchedule')) : null];
    }

    // "Paid 4 of 12 payments" — by the schedule lines, next to the money ring.
    function countBox(s) {
      if (s.none || !s.items || !s.items.length) return null;
      const done = s.items.filter((i) => i.status === 'Paid').length, all = s.items.length;
      const pct = Math.round((done * 100) / all);
      return h('div', { class: 'pay-count' }, h('div', { class: 'row between' }, h('span', null, t('cab.paidOf', { x: done, y: all })), h('b', { class: 'num' }, pct + '%')), S.bar(pct, 'gold'));
    }

    // The building's progress: the stage being built now, and the whole history on request.
    function buildCard() {
      const card = h('div', { class: 'card stack cab-build' }, S.skeleton(2, 24));
      if (!deal.buildingId) return null;
      S.api.stages(deal.buildingId).then((st) => {
        if (!st.length) { card.remove(); return; }
        const current = st.slice().reverse().find((x) => x.percent > 0 && x.percent < 100) || st.slice().reverse().find((x) => x.percent > 0) || st[0];
        const all = h('div', { hidden: true });
        const more = h('button', { class: 'btn ghost sm', type: 'button', 'aria-expanded': 'false', onclick: () => {
          const open = all.hidden; all.hidden = !open; more.setAttribute('aria-expanded', String(open)); more.textContent = t(open ? 'cab.buildHide' : 'cab.buildHistory');
          if (open && !all.firstChild) all.appendChild(S.panels.stages(deal.buildingId));
        } }, t('cab.buildHistory'));
        // The big number and the bar are the whole building (as on the "Construction" page); the line below is the stage now.
        const total = Math.round(st.reduce((s, x) => s + x.percent, 0) / st.length);
        card.replaceChildren(h('div', { class: 'row between' }, h('h3', null, t('cab.build')), h('b', { class: 'num' }, total + '%')),
          h('div', { class: 'muted' }, t('cab.stageNow', { s: current.title, p: current.percent })), S.bar(total, 'gold'), h('div', null, more), all);
      }).catch(() => card.remove());
      return card;
    }
    const kv = (k, v) => [h('dt', null, k), h('dd', null, v === undefined || v === null || v === '' ? '—' : v)];
    return () => { stopSel = true; };
  };
})();
