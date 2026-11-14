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
  const loginBrand = () => h('a', { class: 'login-brand', href: '/', 'aria-label': t('brand.name') }, S.logo(), h('span', null, t('brand.name')));

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

  // Two tabs: staff sign in with e-mail and password (then a 2FA code if it is on), buyers with a code by e-mail or SMS.
  // GET /auth/methods says which codes really reach people: the «По почте / По телефону» switch appears only when SMS
  // really work (sms: true); until the answer comes, and without an SMS provider, there is only e-mail — no flicker.
  // /login#reset=<token> from the "forgot password" letter opens the new-password form.
  S.views.login = function (root) {
    const card = h('div', { class: 'card login-card login' });
    // /login gets the split page (the building and the form); the cabinet's own fallback keeps the plain card.
    root.appendChild(document.body.dataset.page === 'login' ? loginShell(card) : h('div', { class: 'wrap page-in' }, loginBrand(), card));
    const tokenInHash = () => (location.hash.match(/reset=([A-Za-z0-9_-]+)/) || [])[1] || '';
    let resetToken = tokenInHash();
    // Buyers sign in far more often than staff: their tab is the default; the last choice is remembered.
    let who = resetToken || S.store.get('sitora.loginAs') === 'staff' ? 'staff' : 'buyer';
    let step = resetToken ? 'reset' : who, emailVal = '', timer = 0, tick = null, challenge = null, notice = '';
    const isEmail = (v) => /^\S+@\S+\.\S+$/.test(v);
    // How a buyer gets the code: 'email' or 'phone' (remembered); methods — { email, sms } from the server.
    let by = S.store.get('sitora.loginBy') === 'phone' ? 'phone' : 'email', phoneVal = '+992', methods = null;
    const smsOn = () => !!methods && !!methods.sms;
    const DEMO_BUYER = 'demo-buyer@binosoz.tj';
    S.api.authMethods().then((m) => {
      methods = m || null;
      if ((smsOn() || (methods && methods.demo)) && step === 'buyer') draw();
    }).catch(() => {});
    // Server codes for «the code cannot be delivered» → a clear text in the page language.
    const humanError = (er) => {
      if (er && er.code === 'email_not_configured') er.message = t('login.emailOff');
      if (er && er.code === 'sms_not_configured') er.message = t('login.smsOff');
      return er;
    };
    const badEmail = (form) => S.showErrors(form, { message: t('val.email'), errors: { email: [t('val.email')] } });
    const tabs = () => h('div', { class: 'login-tabs', role: 'tablist', 'aria-label': t('login.as') }, [['buyer', 'login.buyer'], ['staff', 'login.staff']].map(([k, key]) =>
      h('button', { type: 'button', role: 'tab', class: who === k ? 'on' : '', 'aria-selected': String(who === k), onclick: () => {
        if (who === k && step === k) return;
        who = k; S.store.set('sitora.loginAs', k); step = k; notice = ''; clearInterval(tick); draw();
      } }, t(key))));
    const link = (label, onclick) => h('button', { class: 'btn ghost sm', type: 'button', onclick }, label);
    // «По почте» / «По телефону» on the buyer's tab; the phone option is marked when SMS cannot be sent.
    const byToggle = () => !smsOn() ? null : h('div', { class: 'seg login-by', role: 'group', 'aria-label': t('login.howTo') }, [['email', 'login.byEmail'], ['phone', 'login.byPhone']].map(([k, key]) =>
      h('button', { type: 'button', class: by === k ? 'on' : '', 'aria-pressed': String(by === k), onclick: () => {
        if (by === k) return;
        by = k; S.store.set('sitora.loginBy', k); notice = ''; draw();
      } }, t(key))));

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
    const byPhoneNow = () => by === 'phone' && smsOn();
    const send = async () => { await (byPhoneNow() ? S.api.smsSend(phoneVal) : S.api.emailSend(emailVal)); startTick(); };

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
      } else if (step === 'demo') {
        // Demo buyer for the jury (only when GET /auth/methods says demo: true): the password is given by the presenter.
        const email = S.input('email', { type: 'email', value: DEMO_BUYER, readonly: true, autocomplete: 'username' });
        const pass = passwordField(t('login.password'), 'password', 'current-password');
        btn.textContent = t('login.enter');
        form.append(h('h2', null, t('demo.loginTitle')), h('p', { class: 'muted' }, t('demo.loginHint')), S.field(t('login.email'), email), pass.field, btn,
          link(t('login.forgotBack'), () => { step = 'buyer'; draw(); }));
        submit = async () => enter(await S.api.passwordLogin(DEMO_BUYER, pass.input.value));
        focus = pass.input;
      } else if (step === 'buyer' && byPhoneNow()) {
        const phone = S.input('phone', { type: 'tel', value: phoneVal, autocomplete: 'tel', inputmode: 'tel' });
        btn.textContent = t('login.sendCode');
        form.append(h('h2', null, t('login.buyerTitle')), byToggle(), h('p', { class: 'muted' }, t('login.hintPhone')), info, S.field(t('f.phone'), phone, t('f.phoneHint')), btn);
        submit = async () => {
          phoneVal = phone.value.replace(/[\s()-]/g, '');
          await send(); notice = ''; step = 'code'; draw();
        };
        focus = phone;
      } else if (step === 'buyer') {
        const email = S.input('email', { type: 'email', value: emailVal, placeholder: 'name@mail.tj', autocomplete: 'email', inputmode: 'email' });
        btn.textContent = t('login.sendCode');
        // The server answers the same for known and unknown addresses, so registration is offered as a link, not detected.
        form.append(h('h2', null, t('login.buyerTitle')), byToggle(), h('p', { class: 'muted' }, t('login.hintBuyer')), info, S.field(t('login.email'), email), btn,
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
          S.field(t('login.email'), email), S.field(t('f.phone'), phone, t('f.phoneHint')), btn, S.privacyNote(),
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
        const resend = h('button', { class: 'btn ghost sm', type: 'button', 'data-resend': '', disabled: timer > 0, onclick: async () => { try { await send(); S.toast(t('login.sent'), 'ok'); } catch (er) { S.err(humanError(er)); } } }, timer > 0 ? t('login.resendIn', { s: timer }) : t('login.resend'));
        const byPhone = byPhoneNow();
        form.append(h('h2', null, t('login.codeTitle')), h('p', { class: 'muted' }, byPhone ? t('login.codeSubPhone', { p: phoneVal }) : t('login.codeSub', { p: emailVal })), S.field(t('login.code'), code, t('login.codeHint')), btn,
          h('div', { class: 'row between' }, link(t(byPhone ? 'login.changePhone' : 'login.changeEmail'), () => { step = 'buyer'; clearInterval(tick); draw(); }), resend));
        submit = async () => {
          const value = code.value.replace(/\s/g, '');
          enter(await (byPhone ? S.api.smsVerify(phoneVal, value) : S.api.emailVerify(emailVal, value)));
        };
        focus = code;
      }
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await submit(); } catch (er) { S.showErrors(form, humanError(er)); } }); });
      // Tabs only on the first screens; the code, 2FA and password-reset steps keep their own "back" link.
      // «Войти как демо-покупатель» under the buyer's form — only when the server has the demo login on.
      if (step === 'buyer' && methods && methods.demo) form.append(h('button', { class: 'btn ghost block demo-login', type: 'button', onclick: () => { step = 'demo'; notice = ''; draw(); } }, t('demo.loginButton')));
      card.replaceChildren(step === 'staff' || step === 'buyer' || step === 'register' ? tabs() : null, form);
      setTimeout(() => focus && focus.focus(), 30);
    }
    // The building on the left (55%), the form on the right; on a phone the building shrinks to a strip above the form.
    function loginShell(c) {
      const cc = (window.SITORA_CONFIG || {}).CONTACT || {};
      const rnd = (a, b) => a + Math.random() * (b - a);
      const sky = h('div', { class: 'lg-sky', 'aria-hidden': 'true' }, Array.from({ length: 46 }, () => {
        const size = rnd(1.5, 4).toFixed(1) + 'px';
        return h('i', { style: { left: rnd(0, 100).toFixed(1) + '%', top: rnd(0, 100).toFixed(1) + '%', width: size, height: size, animationDelay: rnd(0, 8).toFixed(1) + 's', animationDuration: rnd(7, 14).toFixed(1) + 's' } });
      }));
      const lang = h('div', { class: 'seg lg-lang', role: 'group', 'aria-label': 'Language' }, S.LANGS.map(([k, l]) => h('button', { type: 'button', class: S.lang === k ? 'on' : '', 'aria-pressed': String(S.lang === k), onclick: () => { if (S.lang !== k) S.setLang(k); } }, l)));
      const facts = h('ul', { class: 'lg-facts' },
        cc.address ? h('li', null, S.icon('pin'), S.brandAddress(cc.address)) : null,
        cc.phone ? h('li', null, S.icon('phone'), h('a', { href: 'tel:' + cc.phone.replace(/[^\d+]/g, '') }, cc.phone)) : null);
      const hero = h('aside', { class: 'lg-hero' }, sky,
        h('div', { class: 'lg-hero-top' }, h('a', { class: 'lg-brand', href: '/', 'aria-label': t('login.toSite') }, S.logo(), h('small', { class: 'lg-platform' }, t('footer.platform'))), lang),
        h('div', { class: 'lg-hero-text' }, h('p', { class: 'lg-eyebrow' }, t('login.heroEyebrow')), h('h1', { class: 'lg-name' }, t('brand.name')), h('p', { class: 'lg-sub' }, t('login.heroSub')), facts));
      return h('div', { class: 'lg-split' }, hero, h('main', { class: 'lg-side' }, h('div', { class: 'lg-frame' }, c), demoChips()));
    }

    // Development only (this computer): the seeded accounts as chips that fill the form. No passwords here:
    // the buyer's code in development is 123456, the super admin's password is in user-secrets.
    function demoChips() {
      if (!/^(localhost|127.0.0.1|[::1])$|.localhost$/.test(location.hostname)) return null;
      const chip = (tab, email, key) => h('button', { type: 'button', class: 'lg-chip', onclick: () => {
        who = tab; S.store.set('sitora.loginAs', tab); step = tab; emailVal = email; notice = ''; clearInterval(tick); draw();
      } }, h('b', null, t(key)), h('span', null, email));
      return h('div', { class: 'lg-demo' }, h('p', null, t('login.demo')),
        h('div', { class: 'lg-chips' }, chip('buyer', 'client1@example.com', 'login.demoBuyer'), chip('staff', 'superadmin@binosoz.tj', 'login.demoAdmin')),
        h('small', null, t('login.demoHint')));
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
  // GET /cabinet/overview: no deals → a welcome card with the developer's contacts; otherwise the deal picker and six tabs.
  // Each tab loads its own data from the shared deal addresses (schedule, documents, stages, messages, reviews).
  const CAB_TABS = [['apt', '🏠', 'cab.t.apt'], ['pay', '💰', 'cab.t.pay'], ['docs', '📄', 'cab.t.docs'], ['build', '🏗️', 'cab.t.build'], ['chat', '💬', 'cab.t.chat'], ['review', '⭐', 'cab.t.review']];
  // Deal stages as the buyer sees them: booking → contract → ownership.
  const CAB_STEPS = [['booking', ['Lead', 'Showing', 'Booking']], ['deal', ['Contract', 'Payment']], ['own', ['Completed']]];
  const NEW_DAYS = 3;

  S.views.cabinet = function (root) {
    if (!S.api.isAuthed) return S.views.login(root);
    if (!S.api.isCustomer()) {
      const w = h('div', { class: 'wrap page-in' }, h('div', { class: 'card login-card login' }, h('h2', null, t('cab.staffTitle')), h('p', { class: 'muted', style: { margin: '8px 0 16px' } }, t('cab.staffText')), h('div', { class: 'row' }, h('button', { class: 'btn', onclick: () => S.go(S.homeFor(S.api.user)) }, t('cab.toCrm')), h('button', { class: 'btn ghost', onclick: async () => { await S.api.logout(); S.go('cabinet'); } }, t('nav.logout')))));
      root.appendChild(w); return () => {};
    }
    const page = h('div', { class: 'wrap cab page-in' }); root.appendChild(page);
    let ov = null, deal = null, sched = null, stopped = false, chat = { unread: 0 };
    let tab = CAB_TABS.some((x) => x[0] === S.store.get('sitora.cab.tab')) ? S.store.get('sitora.cab.tab') : 'apt';
    const tabsBar = h('div', { class: 'cab-tabs', role: 'tablist', 'aria-label': t('cab.tabs') });
    const panel = h('section', { class: 'cab-panel', role: 'tabpanel' });
    // Phones: a swipe left or right on the tab's content goes to the next or previous tab; dots under the tabs show where you are.
    const dots = h('div', { class: 'swipe-dots mob-only', 'aria-hidden': 'true' });
    const step = (d) => { const i = CAB_TABS.findIndex((x) => x[0] === tab); const n = CAB_TABS[i + d]; if (n) go(n[0]); };
    let sx = 0, sy = 0, skip = false;
    panel.addEventListener('touchstart', (e) => {
      sx = e.touches[0].clientX; sy = e.touches[0].clientY;
      // Not over a field, a table that scrolls sideways or the payment card (it turns over on a tap).
      skip = !!(e.target.closest && e.target.closest('input, textarea, select, .tbl-wrap, .ptrack-wrap, .chat-list'));
    }, { passive: true });
    panel.addEventListener('touchend', (e) => {
      if (skip) return;
      const dx = e.changedTouches[0].clientX - sx, dy = e.changedTouches[0].clientY - sy;
      if (Math.abs(dx) > 60 && Math.abs(dx) > Math.abs(dy) * 1.5) step(dx < 0 ? 1 : -1);
    }, { passive: true });
    const fab = h('button', { class: 'cab-fab', type: 'button', 'aria-label': t('cab.t.chat'), onclick: () => go('chat') }, '💬', h('span', { class: 'cab-fab-badge', hidden: true }));

    (async () => {
      page.appendChild(S.skeleton(4, 60));
      try { ov = await S.api.cabinetOverview(); } catch (e) { page.replaceChildren(S.errorCard(e)); return; }
      if (stopped) return;
      if (!ov.hasDeals) { page.replaceChildren(onboarding()); return; }
      // The remembered apartment, else the newest deal that is not cancelled.
      const saved = S.store.get('sitora.cab.deal');
      deal = ov.deals.find((d) => d.dealId === saved) || ov.deals.find((d) => d.stage !== 'Cancelled') || ov.deals[0];
      drawShell();
      const back = new URLSearchParams(location.search);
      if (back.has('success') || back.has('payment')) payReturn(back);
    })();

    function onboarding() {
      const c = ov.contacts || {}, cfg = window.SITORA_CONFIG || {}, cc = cfg.CONTACT || {};
      const phone = c.phone || cc.phone, email = c.email || cc.email;
      return h('div', { class: 'card cab-onboard' },
        h('div', { class: 'cab-wave', 'aria-hidden': 'true' }, '👋'),
        h('h1', null, t('cab.welcome', { name: firstName(ov.customerName) })),
        h('p', { class: 'muted' }, t('cab.noBooking')),
        h('div', { class: 'cab-contacts' }, h('b', null, '📞 ' + t('cab.contactUs')),
          phone ? h('a', { href: 'tel:' + phone.replace(/[^\d+]/g, '') }, phone) : null,
          email ? h('a', { href: 'mailto:' + email }, email) : null),
        h('a', { class: 'btn gold lg', href: '/apartments' }, t('cab.seeObjects')));
    }

    // Greeting card: the name, the apartment, three numbers (apartment, paid share, next payment) and the paid bar.
    function welcome() {
      const stat = (val, lbl) => h('div', { class: 'cab-stat' }, val, h('div', { class: 'cab-stat-lbl' }, lbl));
      const next = h('div', { class: 'cab-stat-val' }, '…');
      const card = h('section', { class: 'cab-welcome' },
        h('div', { class: 'cab-welcome-top' },
          h('div', null, h('h1', { class: 'cab-welcome-name' }, t('cab.hi', { name: firstName(ov.customerName) })), h('p', { class: 'cab-welcome-sub' }, aptTitle(deal))),
          picker()),
        h('div', { class: 'cab-welcome-stats' },
          stat(h('div', { class: 'cab-stat-val' }, '№ ' + deal.apartmentNumber), t('wow.cab.apt')),
          stat(h('div', { class: 'cab-stat-val' }, deal.paidPercent + '%'), t('wow.cab.paid')),
          stat(next, t('wow.cab.next'))),
        progress(deal.paidPercent, true));
      // The next payment comes from the schedule: the first item that is not paid yet.
      if (deal.stage === 'Cancelled') next.textContent = '—';
      else ensureSchedule().then((s) => {
        const it = !s.none && s.items.find((i) => i.status !== 'Paid');
        next.textContent = it && s.remaining > 0 ? longDate(it.dueDate) + ' · ' + S.fmt.money(Math.min(it.amountDue - it.amountPaid, s.remaining), s.currency) : '—';
      }).catch(() => { next.textContent = '—'; });
      return card;
    }

    function drawShell() {
      page.replaceChildren(welcome(), tabsBar, dots, panel);
      // On the body: the page's entrance animation leaves a transform, and position: fixed would follow the page.
      if (!fab.isConnected) document.body.appendChild(fab);
      drawTabs(); show();
      refreshChat();
    }

    // Several apartments: the choice is remembered in this browser.
    function picker() {
      if (ov.deals.length < 2) return null;
      const sel = S.select('deal', ov.deals.map((d) => [d.dealId, '🏠 ' + aptTitle(d) + (d.stage === 'Cancelled' ? ' · ' + t('stage.Cancelled') : '')]), deal.dealId, { 'aria-label': t('cab.deal'), class: 'inp cab-picker' });
      sel.addEventListener('change', () => { deal = ov.deals.find((d) => d.dealId === sel.value); S.store.set('sitora.cab.deal', deal.dealId); sched = null; chat = { unread: 0 }; drawShell(); });
      return sel;
    }

    function drawTabs() {
      tabsBar.replaceChildren(...CAB_TABS.map(([k, icon, key]) => h('button', { type: 'button', role: 'tab', id: 'cab-tab-' + k, class: 'cab-tab' + (tab === k ? ' on' : ''), 'aria-selected': String(tab === k), onclick: () => go(k) },
        h('span', { class: 'cab-tab-i', 'aria-hidden': 'true' }, icon), h('span', null, t(key)), k === 'chat' && chat.unread ? h('span', { class: 'cab-badge' }, chat.unread) : null)));
      panel.setAttribute('aria-labelledby', 'cab-tab-' + tab);
      dots.replaceChildren(...CAB_TABS.map(([k]) => h('i', { class: 'swipe-dot' + (tab === k ? ' on' : '') })));
      const badge = S.$('.cab-fab-badge', fab); badge.hidden = !chat.unread; badge.textContent = chat.unread || '';
      fab.hidden = tab === 'chat';
      const on = S.$('.cab-tab.on', tabsBar);
      if (on && tabsBar.scrollWidth > tabsBar.clientWidth) tabsBar.scrollLeft = on.offsetLeft - (tabsBar.clientWidth - on.offsetWidth) / 2;
    }

    function go(k) { tab = k; S.store.set('sitora.cab.tab', k); drawTabs(); show(); }

    async function show() {
      const k = tab;
      panel.classList.remove('cab-fade'); void panel.offsetWidth; panel.classList.add('cab-fade');
      panel.replaceChildren(S.skeleton(3, 70));
      try {
        const body = k === 'apt' ? aptTab() : k === 'pay' ? await payTab() : k === 'docs' ? await docsTab() : k === 'build' ? await buildTab() : k === 'chat' ? chatTab() : reviewTab();
        if (tab === k && !stopped) panel.replaceChildren(body);
      } catch (e) { if (tab === k) panel.replaceChildren(S.errorCard(e)); }
    }

    const refreshChat = () => S.api.chatUnread(deal.dealId).then((u) => { chat = u; if (!stopped) drawTabs(); return u; }).catch(() => chat);
    async function ensureSchedule() { if (sched && sched.dealId === deal.dealId) return sched; try { sched = await S.api.schedule(deal.dealId); sched.dealId = deal.dealId; } catch (e) { if (e.status !== 404) throw e; sched = { dealId: deal.dealId, none: true }; } return sched; }

    /* ---- 🏠 apartment ---- */
    function aptTab() {
      const d = deal;
      const step = CAB_STEPS.findIndex(([, st]) => st.includes(d.stage));
      const steps = d.stage === 'Cancelled'
        ? h('div', null, S.stageChip('Cancelled'))
        : h('ol', { class: 'cab-steps' }, CAB_STEPS.map(([k], i) => h('li', { class: i < step ? 'done' : i === step ? 'now' : '' }, h('i', { 'aria-hidden': 'true' }), t('cab.step.' + k))));
      const contract = h('button', { class: 'btn', type: 'button', onclick: (e) => S.busy(e.currentTarget, async () => {
        try {
          const docs = await S.api.documents(d.dealId);
          const c = docs.filter((x) => x.type === 'Contract').sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt))[0];
          if (!c) { S.toast(t('cab.noContract'), 'warn'); return; }
          await S.api.openSigned(c.id, c.name);
        } catch (er) { S.err(er); }
      }) }, '📋 ' + t('cab.contractPdf'));
      return h('div', { class: 'card cab-apt' },
        h('div', { class: 'row between' }, h('h2', null, '🏠 ' + d.buildingName + ', ' + t('cab.aptNo', { n: d.apartmentNumber })), S.stageChip(d.stage)),
        h('div', { class: 'cab-meta' }, h('span', null, t('cab.area') + ': ' + S.fmt.area(d.area)), h('span', null, S.fmt.rooms(d.rooms)), h('span', null, t('cab.floor') + ': ' + d.floor)),
        h('dl', { class: 'cab-money' },
          h('div', null, h('dt', null, t('cab.price')), h('dd', null, S.fmt.money(d.finalPrice, d.currency))),
          h('div', null, h('dt', null, t('cab.paidSum')), h('dd', { class: 'good' }, S.fmt.money(d.paid, d.currency) + ' (' + d.paidPercent + '%)')),
          h('div', null, h('dt', null, t('cab.left')), h('dd', null, S.fmt.money(d.remaining, d.currency)))),
        progress(d.paidPercent),
        h('div', { class: 'cab-status' }, h('span', { class: 'muted' }, t('cab.status') + ':'), steps),
        d.managerName ? h('p', { class: 'muted' }, t('deal.manager') + ': ' + d.managerName) : null,
        h('div', { class: 'row' }, contract, h('a', { class: 'btn ghost', href: '/apartments' }, '🗺️ ' + t('cab.plan'))));
    }

    /* ---- 💰 payments ---- */
    // «Моя карта оплаты»: a payment tracker shaped like a card (not a bank card). Front — paid share and the next date;
    // back — the next three payments. Turns over on a click, a tap, Enter or Space.
    function payCard(s, pct, open, next) {
      const dueDay = (i) => new Date(i.dueDate + 'T00:00:00').toLocaleDateString(S.lang === 'en' ? 'en-GB' : 'ru-RU', { day: 'numeric', month: 'short', year: 'numeric' });
      const bar = h('div', { class: 'ptrack-bar-fill', style: { width: '0%' } });
      requestAnimationFrame(() => requestAnimationFrame(() => { bar.style.width = pct + '%'; }));
      const pdf = h('button', { class: 'ptrack-pdf', type: 'button', onclick: (e) => { e.stopPropagation(); S.busy(e.currentTarget, () => S.api.openDoc('/deals/' + deal.dealId + '/statement.pdf', { date: S.today() }, t('cab.pdfAct')).catch(S.err)); } }, '⬇ ' + t('pcard.pdf'));
      const front = h('div', { class: 'ptrack-front' },
        h('span', { class: 'ptrack-ornament', 'aria-hidden': 'true' }),
        h('div', { class: 'ptrack-head' }, h('div', { class: 'ptrack-brand' }, '💎 ' + t('brand.name').toUpperCase()), pdf),
        h('div', { class: 'ptrack-name' }, ov.customerName || ''),
        h('div', { class: 'ptrack-apt' }, t('cab.aptNo', { n: deal.apartmentNumber }) + ' · ' + t('pcard.floor', { n: deal.floor })),
        h('div', { class: 'ptrack-bar-track' }, bar),
        h('div', { class: 'ptrack-pct' }, t('pcard.paidPct', { n: pct })),
        h('dl', { class: 'ptrack-sums' },
          h('div', null, h('dt', null, t('cab.paidSum')), h('dd', null, S.fmt.money(s.paid, s.currency))),
          h('div', null, h('dt', null, t('cab.left')), h('dd', null, S.fmt.money(s.remaining, s.currency))),
          h('div', null, h('dt', null, t('pcard.next')), h('dd', null, next && s.remaining > 0 ? dueDay(next) : '—'))));
      const rows = open.slice(0, 3);
      const back = h('div', { class: 'ptrack-back' },
        h('div', { class: 'ptrack-brand' }, t('pcard.schedule')),
        rows.length ? h('ul', { class: 'ptrack-list' }, rows.map((i) => h('li', null, h('span', null, dueDay(i)), h('b', null, S.fmt.money(i.amountDue - i.amountPaid, s.currency)), h('span', { 'aria-label': t('pay.' + i.status) }, i.status === 'Overdue' ? '⚠️' : '⏳'))))
          : h('p', { class: 'ptrack-empty' }, t('pcard.allPaid')),
        h('span', { class: 'ptrack-backlink' }, '← ' + t('pcard.back')));
      const card = h('div', { class: 'ptrack' }, front, back);
      const flip = () => { const on = card.classList.toggle('flipped'); wrap.setAttribute('aria-pressed', String(on)); };
      const wrap = h('div', { class: 'ptrack-wrap', role: 'button', tabindex: 0, 'aria-pressed': 'false', 'aria-label': t('pcard.title') + ': ' + t('pcard.flip'), onclick: flip,
        onkeydown: (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); flip(); } } }, card);
      return h('section', { class: 'ptrack-sec' }, h('h3', null, '💳 ' + t('pcard.title')), wrap, h('p', { class: 'muted cab-small' }, t('pcard.hint')));
    }

    async function payTab() {
      const s = await ensureSchedule();
      if (s.none) return h('div', { class: 'card' }, S.empty(t('cab.noSchedule'), 'wallet'));
      const pct = s.total ? Math.min(100, Math.floor((s.paid * 100) / s.total)) : 0;
      const open = s.items.filter((i) => i.status !== 'Paid');
      const next = deal.stage === 'Cancelled' ? null : open[0];
      const paidItems = s.items.filter((i) => i.status === 'Paid');
      const head = h('div', { class: 'card' }, h('div', { class: 'row between' }, h('b', null, t('cab.paidTotal')), h('b', { class: 'num' }, pct + '%')), progress(pct),
        h('div', { class: 'row between muted cab-small' }, h('span', null, t('cab.paidSum') + ': ' + S.fmt.money(s.paid, s.currency)), h('span', null, t('cab.left') + ': ' + S.fmt.money(s.remaining, s.currency))));
      const due = next ? Math.min(next.amountDue - next.amountPaid, s.remaining) : 0;
      const nextCard = next && s.remaining > 0 ? h('div', { class: 'card cab-next' + (next.status === 'Overdue' ? ' overdue' : '') },
        h('div', null, h('div', { class: 'cab-next-t' }, '🔔 ' + t('cab.nextDue', { d: longDate(next.dueDate) })), h('div', { class: 'cab-next-sum' }, t('cab.amount') + ': ', h('b', null, S.fmt.money(due, s.currency))),
          next.status === 'Overdue' ? S.chip('Overdue', t('pay.Overdue')) : null),
        h('button', { class: 'btn gold', type: 'button', onclick: (e) => startPayment(e.currentTarget) }, t('cab.payCard'))) : null;
      const history = s.payments.filter((p) => !p.reversal && !p.isReversed);
      const paidList = history.length ? h('div', { class: 'cab-paid' }, history.slice().reverse().map((p) => h('div', { class: 'cab-paid-i' }, h('span', { 'aria-hidden': 'true' }, '✅'),
        h('div', null, h('b', null, S.fmt.money(p.amountDeal || p.amount, s.currency)), h('small', { class: 'muted' }, S.fmt.date(p.date) + (p.method ? ' · ' + t('cab.m.' + p.method, null, '') : ''))),
        h('span', { class: 'cab-paid-ok' }, t('cab.paidOk'))))) : h('p', { class: 'muted' }, t('cab.noPayments'));
      const upcoming = open.slice(next ? 1 : 0);
      return h('div', { class: 'stack' }, payCard(s, pct, open, next), head, nextCard,
        h('div', { class: 'card' }, h('h3', null, t('cab.paidList', { x: paidItems.length, y: s.items.length })), paidList),
        upcoming.length ? h('div', { class: 'card' }, h('h3', null, t('cab.upcoming')), S.tbl([
          { label: t('cab.dueDate'), render: (i) => S.fmt.date(i.dueDate) },
          { label: t('cab.amount'), r: true, render: (i) => S.fmt.money(i.amountDue - i.amountPaid, s.currency) },
          { label: '', r: true, render: (i) => (i.status === 'Overdue' ? S.chip('Overdue', t('pay.Overdue')) : i.status === 'Partial' ? S.chip('warn', t('pay.Partial')) : null) },
        ], upcoming)) : null,
        pdfRow());
    }

    function startPayment(btn) {
      return S.busy(btn, async () => {
        try {
          const r = await S.api.initiatePayment(deal.dealId, { returnUrl: '/cabinet?success=1' });
          S.store.set('sitora.pay', JSON.stringify({ dealId: deal.dealId, id: r.gatewayPaymentId }));
          location.href = r.paymentUrl;
        } catch (er) { S.err(er); }
      });
    }

    function pdfRow() {
      const date = S.input('date', { type: 'date', value: S.today(), 'aria-label': t('cab.actDate') }); date.style.maxWidth = '170px';
      return h('div', { class: 'row' }, h('button', { class: 'btn ghost', onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/deals/' + deal.dealId + '/schedule.pdf', null, t('cab.pdfSchedule')).catch(S.err)) }, t('cab.pdfSchedule')),
        h('button', { class: 'btn ghost', onclick: (e) => S.busy(e.currentTarget, () => S.api.openDoc('/deals/' + deal.dealId + '/statement.pdf', { date: date.value }, t('cab.pdfAct')).catch(S.err)) }, t('cab.pdfAct')), date);
    }

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
      // The paid amount changed: take fresh numbers and open the payments.
      try { ov = await S.api.cabinetOverview(); } catch (e) { /* keep the old numbers */ }
      deal = ov.deals.find((d) => d.dealId === p.dealId) || deal; sched = null; tab = 'pay'; drawShell();
    }

    /* ---- 📄 documents ---- */
    async function docsTab() {
      const docs = (await S.api.documents(deal.dealId)).sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
      if (!docs.length) return h('div', { class: 'card' }, S.empty(t('cab.noDocs'), 'file'));
      const fresh = Date.now() - NEW_DAYS * 86400000;
      return h('div', { class: 'card cab-docs' }, docs.map((d) => h('div', { class: 'cab-doc' },
        h('span', { class: 'cab-doc-i', 'aria-hidden': 'true' }, '📄'),
        h('div', { class: 'cab-doc-txt' }, h('b', null, d.name, new Date(d.createdAt).getTime() > fresh ? h('span', { class: 'cab-new' }, t('cab.new')) : null),
          h('small', { class: 'muted' }, [t('cab.dt.' + d.type, null, ''), S.fmt.date(d.createdAt), S.fmt.size(d.size)].filter(Boolean).join(' · '))),
        h('button', { class: 'btn soft sm', type: 'button', onclick: (e) => S.busy(e.currentTarget, async () => { try { await S.api.openSigned(d.id, d.name); } catch (er) { S.err(er); } }) }, '⬇ ' + t('cab.download')))));
    }

    /* ---- 🏗️ construction ---- */
    async function buildTab() {
      const st = await S.api.stages(deal.buildingId);
      if (!st.length) return h('div', { class: 'card' }, S.empty(t('con.none'), 'hammer'));
      const total = Math.round(st.reduce((s, x) => s + x.percent, 0) / st.length);
      const last = st.map((x) => x.date).filter(Boolean).sort().pop();
      // The first stage not finished yet is the one being built now (the next ones wait).
      const nowIdx = st.findIndex((x) => x.percent < 100);
      const photos = st.slice().sort((a, b) => String(b.date).localeCompare(String(a.date))).flatMap((x) => x.photos.map((p) => Object.assign({ stage: x.title }, p))).slice(0, 6);
      return h('div', { class: 'stack' },
        h('div', { class: 'card' }, h('div', { class: 'row between' }, h('b', null, t('con.total')), h('b', { class: 'num cab-big' }, total + '%')), progress(total),
          last ? h('p', { class: 'muted cab-small' }, t('cab.lastUpdate', { d: longDate(last) })) : null),
        h('div', { class: 'card' }, h('ul', { class: 'cab-stages' }, st.map((x, i) => {
          const state = x.percent >= 100 ? 'done' : i === nowIdx ? 'now' : 'wait';
          return h('li', { class: state }, h('span', { 'aria-hidden': 'true' }, state === 'done' ? '✅' : state === 'now' ? '🔄' : '⏳'),
            h('div', null, h('b', null, x.title), h('small', { class: 'muted' }, t('cab.stage.' + state) + (state === 'now' ? ' · ' + x.percent + '%' : ''))));
        }))),
        photos.length ? h('div', { class: 'card' }, h('h3', null, t('cab.photos')), h('div', { class: 'cab-photos' }, photos.map((p) => h('a', { href: S.fileSrc(p.url), target: '_blank', rel: 'noopener' }, h('img', { src: S.fileSrc(p.url), alt: p.caption || p.stage, loading: 'lazy' }))))) : null);
    }

    /* ---- 💬 chat ---- */
    function chatTab() {
      const head = h('div', { class: 'chat-head' });
      const drawHead = (u) => head.replaceChildren(h('span', { class: 'avatar' }, S.fmt.initials(u.managerName || deal.managerName)),
        h('span', null, h('b', null, u.managerName || deal.managerName || t('deal.manager')), h('small', { class: 'chat-presence' + (u.managerOnline ? ' on' : '') }, h('i', { 'aria-hidden': 'true' }),
          u.managerOnline ? t('chat.online') : u.managerLastSeenAt ? t('chat.seen', { d: S.fmt.dt(u.managerLastSeenAt) }) : t('chat.offline'))));
      drawHead(chat);
      // Opening the chat reads the messages: refresh the presence now and clear the badge once the history has loaded.
      refreshChat().then(drawHead); setTimeout(refreshChat, 1500);
      return h('div', { class: 'card cab-chat' }, head, S.panels.chat(deal.dealId, 'customer'));
    }

    /* ---- ⭐ review ---- */
    function reviewTab() {
      const box = h('div', { class: 'card cab-review' });
      const r = deal.myReview;
      const draw = (editing) => {
        if (r && !editing) {
          box.replaceChildren(h('h2', null, t('cab.yourReview')),
            h('p', null, t('cab.youRated') + ' ', stars(r.rating), ' (' + r.rating + '/5)'),
            h('blockquote', { class: 'cab-quote' }, r.text),
            r.ownerReply ? h('div', { class: 'cab-reply' }, h('b', null, t('cab.managerReply') + ':'), h('p', null, r.ownerReply)) : null,
            h('p', { class: 'muted cab-small' }, r.isPublished ? t('cab.revPublished') : t('cab.revModeration')),
            r.isPublished ? null : h('button', { class: 'btn ghost', type: 'button', onclick: () => draw(true) }, '✏️ ' + t('cab.edit')));
          return;
        }
        if (deal.stage === 'Cancelled') { box.replaceChildren(S.empty(t('cab.revCancelled'), 'star')); return; }
        let rating = r ? r.rating : 0;
        const starBtns = [1, 2, 3, 4, 5].map((n) => h('button', { type: 'button', class: 'cab-star', role: 'radio', 'aria-label': t('rev.stars', { n }), onclick: () => { rating = n; paint(); } }, '★'));
        const paint = () => starBtns.forEach((b, i) => { b.classList.toggle('on', i < rating); b.setAttribute('aria-checked', String(i + 1 === rating)); });
        const text = h('textarea', { class: 'inp', name: 'text', id: 'cab-rev-text', rows: 5, maxlength: 1000, placeholder: t('cab.revPh') }); text.value = r ? r.text : '';
        const btn = h('button', { class: 'btn gold', type: 'submit' }, t(r ? 'common.save' : 'cab.send'));
        const form = h('form', { class: 'stack', novalidate: true }, h('h2', null, t(r ? 'cab.editReview' : 'cab.leaveReview')), h('p', { class: 'muted' }, t('cab.revHint', { b: deal.buildingName })),
          h('div', { class: 'cab-stars', role: 'radiogroup', 'aria-label': t('cab.rating') }, starBtns), S.field(t('cab.revText'), text), h('div', { class: 'row' }, btn, r ? h('button', { class: 'btn ghost', type: 'button', onclick: () => draw(false) }, t('common.cancel')) : null));
        form.addEventListener('submit', (e) => {
          e.preventDefault(); S.showErrors(form, null);
          if (!rating) { S.showErrors(form, { message: t('cab.revNeedRating') }); return; }
          if (!text.value.trim()) { S.showErrors(form, { message: t('cab.revNeedText'), errors: { text: [t('cab.revNeedText')] } }); return; }
          S.busy(btn, async () => {
            try {
              const saved = await S.api.saveReview(deal.buildingId, r && r.id, { rating, text: text.value.trim() });
              ov.deals.filter((d) => d.buildingId === deal.buildingId).forEach((d) => { d.myReview = saved; });
              S.toast(t('cab.revSaved'), 'ok'); show();
            } catch (er) { S.showErrors(form, er); }
          });
        });
        paint(); box.replaceChildren(form);
      };
      draw(false);
      return box;
    }

    /* ---- helpers ---- */
    // Paid bar: a gold shimmer that grows from 0 to the paid share (0.8 s pause, then 1 s); withPct shows the number above it.
    function progress(pct, withPct) {
      const v = Math.max(0, Math.min(100, pct || 0));
      const fill = h('div', { class: 'pay-progress-fill' });
      setTimeout(() => { fill.style.width = v + '%'; }, 800);
      return h('div', { class: 'pay-progress-wrap cab-progress', role: 'progressbar', 'aria-valuemin': '0', 'aria-valuemax': '100', 'aria-valuenow': String(v) },
        h('div', { class: 'pay-progress-track' }, fill), withPct ? h('span', { class: 'pay-progress-pct' }, v + '%') : null);
    }
    const stars = (n) => h('span', { class: 'cab-stars-ro', 'aria-label': t('rev.stars', { n }) }, '★'.repeat(n), h('span', null, '★'.repeat(5 - n)));
    // "15 декабря 2026" / "15 декабри 2026" — in the chosen language.
    function longDate(d) {
      const x = new Date(d); if (Number.isNaN(x.getTime())) return d || '';
      return x.getDate() + ' ' + t('cab.month.' + x.getMonth()) + ' ' + x.getFullYear();
    }
    function aptTitle(d) { return d.buildingName + ' — ' + t('cab.aptNo', { n: d.apartmentNumber }) + ', ' + S.fmt.rooms(d.rooms); }
    // In the CRM the name is often written surname first, so the whole name is safer than a guessed "first name".
    function firstName(n) { return n || (S.api.user && S.api.user.name) || ''; }
    return () => { stopped = true; fab.remove(); };
  };
})();
