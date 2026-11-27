/* Login (buyers: e-mail + own 6-digit code; staff: password), staff invitations and the buyer cabinet. */
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
  const loginBrand = () => h('a', { class: 'login-brand', href: '/', 'aria-label': S.brandName() }, S.logo(), h('span', null, S.brandName()));

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

  // The same rules as the server (PinRules): 6 digits, not all the same, not consecutive (123456, 654321, 012345…).
  // Returns the i18n key of the problem or ''.
  S.pinProblem = (v) => {
    if (!/^\d{6}$/.test(v || '')) return 'pin.need6';
    if (/^(\d)\1{5}$/.test(v)) return 'pin.weakSame';
    const step = (d) => [...v].every((c, i) => !i || c.charCodeAt(0) - v.charCodeAt(i - 1) === d);
    return step(1) || step(-1) ? 'pin.weakSeq' : '';
  };

  // Six cells for a 6-digit code: digits only, a typed digit moves to the next cell, Backspace in an empty cell goes back,
  // pasting (or the phone's autofill of) 6 digits fills them all. secret — the buyer's own code: hidden, with
  // a «Показать код» button; otherwise the code from the e-mail. Returns { field, value(), clear(), focus() }.
  S.pinCells = function (name, label, o) {
    o = o || {};
    const id = 'f-' + name + '-' + Math.random().toString(36).slice(2, 6);
    const cells = Array.from({ length: 6 }, (_, i) => h('input', {
      class: 'pin-cell', id: i ? id + '-' + i : id, type: o.secret ? 'password' : 'text', inputmode: 'numeric', pattern: '[0-9]*',
      autocomplete: i ? 'off' : o.autocomplete || 'off', 'aria-label': t('pin.digit', { n: i + 1 }), 'data-pin': name, enterkeyhint: i === 5 ? 'done' : 'next',
    }));
    const value = () => cells.map((c) => c.value).join('');
    const changed = () => {
      const v = value();
      if (o.onChange) o.onChange(v);
      if (v.length === 6 && o.onComplete) o.onComplete(v);
    };
    const fill = (from, digits) => {
      let i = from;
      for (const d of digits) { if (i > 5) break; cells[i].value = d; i++; }
      cells[Math.min(i, 5)].focus();
      changed();
    };
    cells.forEach((c, i) => {
      c.addEventListener('input', () => {
        const d = c.value.replace(/\D/g, '');
        if (d.length > 1) { c.value = ''; fill(d.length >= 6 ? 0 : i, d.slice(0, 6)); return; }
        c.value = d;
        if (d && i < 5) cells[i + 1].focus();
        changed();
      });
      c.addEventListener('keydown', (e) => {
        if (e.key === 'Backspace' && !c.value && i > 0) { e.preventDefault(); cells[i - 1].value = ''; cells[i - 1].focus(); changed(); }
        else if (e.key === 'ArrowLeft' && i > 0) { e.preventDefault(); cells[i - 1].focus(); }
        else if (e.key === 'ArrowRight' && i < 5) { e.preventDefault(); cells[i + 1].focus(); }
        else if (e.key.length === 1 && !/\d/.test(e.key) && !e.ctrlKey && !e.metaKey) e.preventDefault();
      });
      c.addEventListener('paste', (e) => {
        e.preventDefault();
        const d = ((e.clipboardData || window.clipboardData).getData('text') || '').replace(/\D/g, '');
        if (d) fill(d.length >= 6 ? 0 : i, d.slice(0, 6));
      });
      c.addEventListener('focus', () => c.select());
    });
    const row = h('div', { class: 'pin-cells', role: 'group', 'aria-label': label }, cells);
    let eye = null;
    if (o.secret) {
      eye = h('button', { type: 'button', class: 'pin-eye', 'aria-pressed': 'false' }, t('pin.show'));
      eye.addEventListener('click', () => {
        const show = cells[0].type === 'password';
        cells.forEach((c) => { c.type = show ? 'text' : 'password'; });
        eye.setAttribute('aria-pressed', String(show)); eye.textContent = t(show ? 'pin.hide' : 'pin.show');
      });
    }
    const hint = h('div', { class: 'hint pin-hint', 'aria-live': 'polite' }, o.hint || '');
    const field = h('div', { class: 'field pin-field', dataset: { name } },
      h('div', { class: 'pin-head' }, h('label', { for: id }, label), eye), row, hint, h('div', { class: 'ferr' }));
    return {
      field, cells, value,
      hint: (text, bad) => { hint.textContent = text || o.hint || ''; hint.classList.toggle('bad', !!bad); },
      clear: () => { cells.forEach((c) => { c.value = ''; }); changed(); },
      focus: () => (cells.find((c) => !c.value) || cells[5]).focus(),
    };
  };

  // Two tabs: staff sign in with e-mail and password (then a 2FA code if it is on), buyers with their e-mail and own
  // 6-digit code. A new buyer registers (name, e-mail, phone, the code twice) and confirms the e-mail with a code
  // from a letter; «Забыли код?» sends such a code too, then the buyer sets a new code. GET /auth/methods: the
  // «По почте / По телефону» switch appears only when SMS really work, «Войти как демо-покупатель» — with demo: true.
  // /login#reset=<token> from the staff "forgot password" letter opens the new-password form.
  S.views.login = function (root) {
    const card = h('div', { class: 'card login-card login' });
    // /login gets the split page (the building and the form); the cabinet's own fallback keeps the plain card.
    root.appendChild(document.body.dataset.page === 'login' ? loginShell(card) : h('div', { class: 'wrap page-in' }, loginBrand(), card));
    const tokenInHash = () => (location.hash.match(/reset=([A-Za-z0-9_-]+)/) || [])[1] || '';
    let resetToken = tokenInHash();
    // Buyers sign in far more often than staff: their tab is the default; the last choice is remembered.
    let who = resetToken || S.store.get('sitora.loginAs') === 'staff' ? 'staff' : 'buyer';
    let step = resetToken ? 'reset' : who, emailVal = '', timer = 0, tick = null, challenge = null, notice = '';
    // The buyer who signed in on this device before: «Войти как user@… · сменить» — only the 6-digit code is left to enter.
    let known = S.api.lastEmail;
    if (known && step === 'buyer') emailVal = known;
    // The buyer's registration (kept for «Изменить почту»), the code from the e-mail for «Забыли код?», the last sign-in error.
    let reg = { first: '', last: '', phone: '+992', pin: '' }, resetCode = '', pinError = null;
    const isEmail = (v) => /^\S+@\S+\.\S+$/.test(v);
    // How a buyer signs in: 'email' (own code) or 'phone' (SMS, only when the server has it on); methods — from the server.
    let by = S.store.get('sitora.loginBy') === 'phone' ? 'phone' : 'email', phoneVal = '+992', methods = null;
    // By phone: a code in Telegram (Telegram Gateway, methods.telegram) or an SMS (methods.sms).
    const smsOn = () => !!methods && (!!methods.sms || !!methods.telegram);
    const viaTelegram = () => !!methods && !!methods.telegram;
    const DEMO_BUYER = 'demo-buyer@binosoz.tj';
    S.api.authMethods().then((m) => {
      methods = m || null;
      if ((smsOn() || (methods && methods.demo)) && step === 'buyer') draw();
    }).catch(() => {});
    // Server codes → a clear text in the page language.
    const humanError = (er) => {
      if (er && er.code === 'email_not_configured') er.message = t('login.emailOff');
      if (er && er.code === 'sms_not_configured') er.message = t('login.smsOff');
      if (er && er.code === 'telegram_no_account') er.message = t('login.noTelegram');
      if (er && (er.code === 'telegram_gateway_limit' || er.code === 'telegram_gateway_failed')) er.message = t('login.tgCodesOff');
      return er;
    };
    const badEmail = (form) => S.showErrors(form, { message: t('val.email'), errors: { email: [t('val.email')] } });
    const tabs = () => h('div', { class: 'login-tabs', role: 'tablist', 'aria-label': t('login.as') }, [['buyer', 'login.buyer'], ['staff', 'login.staff']].map(([k, key]) =>
      h('button', { type: 'button', role: 'tab', class: who === k ? 'on' : '', 'aria-selected': String(who === k), onclick: () => {
        if (who === k && step === k) return;
        who = k; S.store.set('sitora.loginAs', k); step = k; notice = ''; pinError = null; clearInterval(tick); draw();
      } }, t(key))));
    const link = (label, onclick, cls) => h('button', { class: 'btn ghost sm' + (cls ? ' ' + cls : ''), type: 'button', onclick }, label);
    const go = (next, o) => { o = o || {}; notice = o.notice || ''; pinError = null; if (o.stopTick) clearInterval(tick); step = next; draw(); };
    // «По почте» / «По телефону» on the buyer's tab — only when SMS really work.
    const byToggle = () => !smsOn() ? null : h('div', { class: 'seg login-by', role: 'group', 'aria-label': t('login.howTo') }, [['email', 'login.byEmail'], ['phone', viaTelegram() ? 'login.byTelegram' : 'login.byPhone']].map(([k, key]) =>
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
    // «Отправить ещё раз» under a code: what it sends depends on the screen (SMS, registration, «Забыли код?»).
    const resendButton = (send) => h('button', { class: 'btn ghost sm', type: 'button', 'data-resend': '', disabled: timer > 0, onclick: async () => {
      try { await send(); startTick(); S.toast(t('login.sent'), 'ok'); } catch (er) { S.err(humanError(er)); }
    } }, timer > 0 ? t('login.resendIn', { s: timer }) : t('login.resend'));
    const codeInput = (name) => S.input(name, { inputmode: 'numeric', maxlength: 6, placeholder: '••••••', autocomplete: 'one-time-code', style: { letterSpacing: '.4em', fontSize: '20px', textAlign: 'center', fontWeight: 800 } });
    // A new code twice with the weakness shown before sending; returns { fields, check() → the code or null }.
    const newPinPair = (form) => {
      const second = S.pinCells('pinRepeat', t('pin.repeat'), { secret: true, autocomplete: 'new-password' });
      const first = S.pinCells('pin', t('pin.new'), { secret: true, autocomplete: 'new-password', hint: t('pin.rules'),
        onChange: (v) => { const p = v.length === 6 ? S.pinProblem(v) : ''; first.hint(p ? t(p) : '', !!p); },
        onComplete: (v) => { if (!S.pinProblem(v)) second.focus(); } });
      const check = () => {
        const a = first.value(), b = second.value(), p = S.pinProblem(a);
        if (p) { S.showErrors(form, { message: t(p), errors: { pin: [t(p)] } }); first.focus(); return null; }
        if (a !== b) { S.showErrors(form, { message: t('pin.mismatch'), errors: { pinRepeat: [t('pin.mismatch')] } }); second.clear(); second.focus(); return null; }
        return a;
      };
      return { first, second, check };
    };

    function draw() {
      const form = h('form', { class: 'stack', novalidate: true });
      const btn = h('button', { class: 'btn lg block', type: 'submit' }, t('login.enter'));
      const info = notice ? h('div', { class: 'form-ok', role: 'status' }, notice) : null;
      let submit, focus;
      if (step === 'staff') {
        const email = S.input('email', { type: 'email', value: emailVal, placeholder: 'name@mail.tj', autocomplete: 'username', inputmode: 'email' });
        const pass = passwordField(t('login.password'), 'password', 'current-password');
        form.append(h('h2', null, t('login.staffTitle')), h('p', { class: 'muted' }, t('login.staffHint')), info, S.field(t('login.email'), email), pass.field, btn,
          h('p', { style: { textAlign: 'center', margin: 0 } }, link(t('login.forgot'), () => { emailVal = email.value.trim(); go('forgot'); })));
        submit = async () => {
          emailVal = email.value.trim();
          if (!isEmail(emailVal)) { badEmail(form); return; }
          try {
            const r = await S.api.passwordLogin(emailVal, pass.input.value);
            if (r.challenge) { challenge = r.challenge; go('2fa'); return; }
            enter(r);
          } catch (er) {
            // A buyer's address: they sign in with their own code — open that tab with the address already filled in.
            if (er.status === 400 && who === 'staff') { who = 'buyer'; S.store.set('sitora.loginAs', 'buyer'); go('buyer', { notice: t('login.useCode') }); return; }
            throw er;
          }
        };
        focus = emailVal ? pass.input : email;
      } else if (step === '2fa') {
        const code = codeInput('code');
        form.append(h('h2', null, t('login.twoFaTitle')), h('p', { class: 'muted' }, t('login.twoFaSub')), S.field(t('login.twoFaLabel'), code), btn,
          link(t('login.again'), () => { challenge = null; go('staff'); }));
        submit = async () => enter(await S.api.twoFactorVerify(challenge.token, code.value.replace(/\s/g, '')));
        focus = code;
      } else if (step === 'forgot') {
        const email = S.input('email', { type: 'email', value: emailVal, placeholder: 'name@mail.tj', autocomplete: 'email', inputmode: 'email' });
        btn.textContent = t('login.forgotSend');
        form.append(h('h2', null, t('login.forgotTitle')), h('p', { class: 'muted' }, t('login.forgotSub')), info, S.field(t('login.email'), email), btn,
          link(t('login.forgotBack'), () => go('staff')));
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
          link(t('login.forgotBack'), () => { history.replaceState(null, '', location.pathname); go('staff'); }));
        submit = async () => {
          if (pass.input.value !== again.input.value) { S.showErrors(form, { message: t('login.mismatch'), errors: { repeat: [t('login.mismatch')] } }); return; }
          await S.api.passwordReset(resetToken, pass.input.value);
          history.replaceState(null, '', location.pathname);
          go('staff', { notice: t('login.resetDone') });
        };
        focus = pass.input;
      } else if (step === 'demo') {
        // Demo buyer for the jury (only when GET /auth/methods says demo: true): the password is given by the presenter.
        const email = S.input('email', { type: 'email', value: DEMO_BUYER, readonly: true, autocomplete: 'username' });
        const pass = passwordField(t('login.password'), 'password', 'current-password');
        form.append(h('h2', null, t('demo.loginTitle')), h('p', { class: 'muted' }, t('demo.loginHint')), S.field(t('login.email'), email), pass.field, btn,
          link(t('login.forgotBack'), () => go('buyer')));
        submit = async () => enter(await S.api.passwordLogin(DEMO_BUYER, pass.input.value));
        focus = pass.input;
      } else if (step === 'buyer' && byPhoneNow()) {
        const phone = S.input('phone', { type: 'tel', value: phoneVal, autocomplete: 'tel', inputmode: 'tel' });
        btn.textContent = t('login.sendCode');
        form.append(h('h2', null, t('login.buyerTitle')), byToggle(), h('p', { class: 'muted' }, t(viaTelegram() ? 'login.hintTelegram' : 'login.hintPhone')), info, S.field(t('f.phone'), phone, t('f.phoneHint')), btn);
        submit = async () => {
          phoneVal = phone.value.replace(/[\s()-]/g, '');
          await S.api.smsSend(phoneVal); startTick(); go('code');
        };
        focus = phone;
      } else if (step === 'buyer') {
        // E-mail and the buyer's own 6-digit code. A wrong code shows the attempts left and highlights «Забыли код?».
        const asKnown = !!known && emailVal === known;
        const email = S.input('email', { type: asKnown ? 'hidden' : 'email', value: emailVal, placeholder: 'name@mail.tj', autocomplete: 'username', inputmode: 'email' });
        const knownRow = asKnown ? h('div', { class: 'login-known', 'data-known': '' }, h('span', null, t('login.asKnown') + ' ', h('b', null, known)),
          link(t('login.changeEmail'), () => { known = ''; emailVal = ''; S.api.forgetEmail(); draw(); })) : null;
        const pin = S.pinCells('pin', t('pin.label'), { secret: true, autocomplete: 'current-password', onComplete: asKnown ? () => form.requestSubmit() : undefined });
        const forgot = link(t('pin.forgot'), () => { emailVal = email.value.trim(); go('pinForgot'); }, 'pin-forgot' + (pinError ? ' hot' : ''));
        const problem = pinError ? h('div', { class: 'pin-problem', role: 'alert' }, pinError.text, pinError.resend
          ? h('div', null, link(t('pin.resendConfirm'), async () => {
            try { await S.api.customerResend(emailVal); startTick(); go('confirm'); } catch (er) { S.err(humanError(er)); }
          })) : null) : null;
        form.append(h('h2', null, t('login.buyerTitle')), byToggle(), h('p', { class: 'muted' }, t('login.hintBuyer')), info, problem,
          asKnown ? h('div', null, knownRow, email) : S.field(t('login.email'), email), pin.field, btn,
          h('div', { class: 'row between pin-links' }, forgot, link(t('pin.register'), () => { emailVal = email.value.trim(); go('register'); })));
        submit = async () => {
          emailVal = email.value.trim();
          pinError = null;
          if (!isEmail(emailVal)) { badEmail(form); return; }
          if (!/^\d{6}$/.test(pin.value())) { S.showErrors(form, { message: t('pin.need6'), errors: { pin: [t('pin.need6')] } }); pin.focus(); return; }
          try {
            enter(await S.api.customerLogin(emailVal, pin.value()));
          } catch (er) {
            const d = er.data || {};
            if (er.code === 'invalid_pin') pinError = { text: t('pin.wrong', { n: d.attemptsLeft !== undefined ? d.attemptsLeft : '…' }) };
            else if (er.code === 'pin_locked') pinError = { text: t('pin.locked', { m: Math.max(1, Math.ceil((d.retryAfterSeconds || 900) / 60)) }) };
            else if (er.code === 'email_not_confirmed') pinError = { text: t('pin.notConfirmed'), resend: true };
            // Created by a manager: no code yet — «Забыли код?» is how the first code is set.
            else if (er.code === 'pin_not_set') { go('pinForgot', { notice: t('pin.notSet') }); return; }
            else throw er;
            draw();
          }
        };
        focus = emailVal || asKnown ? pin : email;
      } else if (step === 'register') {
        const first = S.input('firstName', { autocomplete: 'given-name', value: reg.first });
        const last = S.input('lastName', { autocomplete: 'family-name', value: reg.last });
        const email = S.input('email', { type: 'email', value: emailVal, placeholder: 'name@mail.tj', autocomplete: 'email', inputmode: 'email' });
        const phone = S.input('phone', { type: 'tel', value: reg.phone, autocomplete: 'tel' });
        const pins = newPinPair(form);
        btn.textContent = t('pin.continue');
        form.append(h('h2', null, t('reg.title')), h('p', { class: 'muted' }, t('reg.sub')), S.field(t('reg.firstName'), first), S.field(t('reg.lastName'), last),
          S.field(t('login.email'), email), S.field(t('f.phone'), phone, t('f.phoneHint')), pins.first.field, pins.second.field, btn, S.privacyNote(),
          link(t('reg.haveAccount'), () => go('buyer')));
        submit = async () => {
          emailVal = email.value.trim();
          reg = { first: first.value.trim(), last: last.value.trim(), phone: phone.value.replace(/[\s()-]/g, ''), pin: '' };
          if (!reg.first || !reg.last) { S.showErrors(form, { message: t('reg.nameRequired'), errors: Object.assign({}, reg.first ? {} : { firstName: [t('reg.nameRequired')] }, reg.last ? {} : { lastName: [t('reg.nameRequired')] }) }); return; }
          if (!isEmail(emailVal)) { badEmail(form); return; }
          const code = pins.check();
          if (!code) return;
          reg.pin = code;
          await S.api.customerRegister({ name: reg.first + ' ' + reg.last, email: emailVal, phone: reg.phone, pin: code });
          startTick(); go('confirm');
        };
        focus = reg.first ? email : first;
      } else if (step === 'confirm') {
        // «Мы отправили код на …»: 6 cells for the code from the letter; complete — sent at once.
        const code = S.pinCells('code', t('pin.otp'), { autocomplete: 'one-time-code', hint: t('login.codeHint'), onComplete: () => form.requestSubmit() });
        btn.textContent = t('pin.confirm');
        form.append(h('h2', null, t('pin.confirmTitle')), h('p', { class: 'muted' }, t('pin.confirmSub', { p: emailVal })), info, code.field, btn,
          h('div', { class: 'row between pin-links' }, link(t('pin.changeEmail'), () => go('register', { stopTick: true })), resendButton(() => S.api.customerResend(emailVal))));
        submit = async () => {
          if (!/^\d{6}$/.test(code.value())) { S.showErrors(form, { message: t('pin.need6'), errors: { code: [t('pin.need6')] } }); code.focus(); return; }
          try { enter(await S.api.customerConfirm(emailVal, code.value())); } catch (er) { code.clear(); code.focus(); throw er; }
        };
        focus = code;
      } else if (step === 'pinForgot') {
        const email = S.input('email', { type: 'email', value: emailVal, placeholder: 'name@mail.tj', autocomplete: 'email', inputmode: 'email' });
        btn.textContent = t('pin.forgotSend');
        form.append(h('h2', null, t('pin.forgotTitle')), h('p', { class: 'muted' }, t('pin.forgotSub')), info, S.field(t('login.email'), email), btn,
          link(t('login.forgotBack'), () => go('buyer')));
        submit = async () => {
          emailVal = email.value.trim();
          if (!isEmail(emailVal)) { badEmail(form); return; }
          await S.api.pinResetRequest(emailVal);
          resetCode = ''; startTick(); go('pinCode');
        };
        focus = email;
      } else if (step === 'pinCode') {
        const code = S.pinCells('code', t('pin.otp'), { autocomplete: 'one-time-code', hint: t('login.codeHint'), onComplete: () => form.requestSubmit() });
        btn.textContent = t('pin.next');
        form.append(h('h2', null, t('pin.codeTitle')), h('p', { class: 'muted' }, t('pin.codeSub', { p: emailVal })), info, code.field, btn,
          h('div', { class: 'row between pin-links' }, link(t('pin.changeEmail'), () => go('pinForgot', { stopTick: true })), resendButton(() => S.api.pinResetRequest(emailVal))));
        // The server checks the code together with the new sign-in code (next screen).
        submit = async () => {
          if (!/^\d{6}$/.test(code.value())) { S.showErrors(form, { message: t('pin.need6'), errors: { code: [t('pin.need6')] } }); code.focus(); return; }
          resetCode = code.value(); go('pinNew');
        };
        focus = code;
      } else if (step === 'pinNew') {
        const pins = newPinPair(form);
        btn.textContent = t('pin.save');
        form.append(h('h2', null, t('pin.newTitle')), h('p', { class: 'muted' }, t('pin.newSub')), pins.first.field, pins.second.field, btn,
          link(t('login.forgotBack'), () => go('buyer', { stopTick: true })));
        submit = async () => {
          const code = pins.check();
          if (!code) return;
          try {
            const r = await S.api.pinReset(emailVal, resetCode, code);
            S.toast(t('pin.saved'), 'ok'); enter(r);
          } catch (er) {
            // A wrong or expired code from the letter: back to it, with the server's text.
            if (er.code === 'invalid_code') { notice = ''; step = 'pinCode'; draw(); S.showErrors(S.$('form', card), humanError(er)); return; }
            throw er;
          }
        };
        focus = pins.first;
      } else {
        // The SMS code (only when the server has SMS sign-in on).
        const code = codeInput('code');
        form.append(h('h2', null, t('login.codeTitle')), h('p', { class: 'muted' }, t(viaTelegram() ? 'login.codeSubTelegram' : 'login.codeSubPhone', { p: phoneVal })), S.field(t('login.code'), code, t('login.codeHint')), btn,
          h('div', { class: 'row between' }, link(t('login.changePhone'), () => go('buyer', { stopTick: true })), resendButton(() => S.api.smsSend(phoneVal))));
        submit = async () => enter(await S.api.smsVerify(phoneVal, code.value.replace(/\s/g, '')));
        focus = code;
      }
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await submit(); } catch (er) { S.showErrors(form, humanError(er)); } }); });
      // Tabs only on the first screens; the code, 2FA and reset steps keep their own "back" link.
      // «Войти как демо-покупатель» under the buyer's form — only when the server has the demo login on.
      if (step === 'buyer' && methods && methods.demo) form.append(h('button', { class: 'btn ghost block demo-login', type: 'button', onclick: () => go('demo') }, t('demo.loginButton')));
      card.replaceChildren(step === 'staff' || step === 'buyer' || step === 'register' ? tabs() : null, form);
      setTimeout(() => focus && focus.focus(), 30);
    }
    // The building on the left (55%), the form on the right; on a phone the building shrinks to a strip above the form.
    function loginShell(c) {
      const cc = (window.SITORA_CONFIG || {}).CONTACT || {};
      // The picture is a render of the site's 3D model (frontend/tools/renders), signed as such.
      const src = (w) => '/img/renders/tower-night-tall-' + w + '.webp';
      const pic = h('figure', { class: 'lg-pic', 'aria-hidden': 'true' },
        h('img', { src: src(1600), srcset: [800, 1600, 2400].map((w) => src(w) + ' ' + w + 'w').join(', '), sizes: '(max-width: 768px) 100vw, 55vw', width: 2400, height: 3000, alt: '', decoding: 'async' }));
      const words = S.brandName().split(/\s+/);
      const lang = h('div', { class: 'seg lg-lang', role: 'group', 'aria-label': 'Language' }, S.LANGS.map(([k, l]) => h('button', { type: 'button', class: S.lang === k ? 'on' : '', 'aria-pressed': String(S.lang === k), onclick: () => { if (S.lang !== k) S.setLang(k); } }, l)));
      // The theme switch, as in the header of the other pages.
      const theme = h('button', { type: 'button', class: 'lg-theme' });
      const drawTheme = () => { const dark = S.isDark && S.isDark(); theme.replaceChildren(S.icon(dark ? 'sun' : 'moon')); const l = t(dark ? 'nav.themeLight' : 'nav.themeDark'); theme.setAttribute('aria-label', l); theme.title = l; };
      theme.addEventListener('click', () => { if (S.toggleTheme) S.toggleTheme(); drawTheme(); });
      drawTheme();
      const facts = h('ul', { class: 'lg-facts' },
        cc.address ? h('li', null, S.icon('pin'), S.brandAddress(cc.address)) : null,
        cc.phone ? h('li', null, S.icon('phone'), h('a', { href: 'tel:' + cc.phone.replace(/[^\d+]/g, '') }, cc.phone)) : null);
      const hero = h('aside', { class: 'lg-hero nr-dark' }, pic, h('div', { class: 'lg-shade', 'aria-hidden': 'true' }), h('span', { class: 'nr-render-note lg-note' }, t('nr.render')),
        h('div', { class: 'lg-hero-top' }, h('a', { class: 'lg-brand', href: '/', title: t('login.toSite') }, S.towerLogo(), h('span', { class: 'brand-word' }, h('b', null, words[0]), words.length > 1 ? ' ' : null, words.length > 1 ? h('small', null, words.slice(1).join(' ')) : null)), h('div', { class: 'lg-tools' }, lang, theme)),
        h('div', { class: 'lg-hero-text' }, h('p', { class: 'lg-eyebrow' }, t('login.heroEyebrow')), h('h1', { class: 'lg-name' }, t('login.heroTitle')), h('p', { class: 'lg-sub' }, t('login.heroSub')), facts));
      return h('div', { class: 'lg-split' }, hero, h('main', { class: 'lg-side' }, h('div', { class: 'lg-frame' }, c), demoChips()));
    }

    // Development only (this computer): the seeded accounts as chips that fill the form. No passwords here:
    // the code from a letter in development is 123456 («Забыли код?» sets the buyer's code), the super admin's password is in user-secrets.
    function demoChips() {
      if (!/^(localhost|127.0.0.1|[::1])$|.localhost$/.test(location.hostname)) return null;
      const chip = (tab, email, key) => h('button', { type: 'button', class: 'lg-chip', onclick: () => {
        who = tab; S.store.set('sitora.loginAs', tab); emailVal = email; go(tab, { stopTick: true });
      } }, h('b', null, t(key)), h('span', null, email));
      return h('div', { class: 'lg-demo' }, h('p', null, t('login.demo')),
        h('div', { class: 'lg-chips' }, chip('buyer', 'client1@example.com', 'login.demoBuyer'), chip('staff', 'superadmin@binosoz.tj', 'login.demoAdmin')),
        h('small', null, t('login.demoHint')));
    }

    draw();
    // The reset link opened while this page is already on screen changes only the hash.
    const onHash = () => { const token = tokenInHash(); if (token) { resetToken = token; who = 'staff'; go('reset'); } };
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
  const CAB_TABS = [['apt', '🏠', 'cab.t.apt'], ['pay', '💰', 'cab.t.pay'], ['docs', '📄', 'cab.t.docs'], ['build', '🏗️', 'cab.t.build'], ['chat', '💬', 'cab.t.chat'], ['review', '⭐', 'cab.t.review'], ['settings', '⚙️', 'cab.t.settings']];
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
      // No deal yet: the welcome card, and below it the same settings (notifications, Telegram, devices) — a buyer
      // without a deal still signs in, links Telegram and closes sessions on other devices.
      if (!ov.hasDeals) {
        page.replaceChildren(onboarding());
        try { const st = await settingsTab(); if (!stopped) page.appendChild(h('div', { class: 'cab-onboard-settings' }, h('h2', null, '⚙️ ' + t('cab.t.settings')), st)); }
        catch (e) { /* settings are optional here: the welcome card is enough */ }
        return;
      }
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
          stat(h('div', { class: 'cab-stat-val' }, S.aptShort(deal)), t('wow.cab.apt')),
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
        const body = k === 'apt' ? aptTab() : k === 'pay' ? await payTab() : k === 'docs' ? await docsTab() : k === 'build' ? await buildTab() : k === 'chat' ? chatTab() : k === 'settings' ? await settingsTab() : reviewTab();
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
        h('div', { class: 'row between' }, h('h2', null, '🏠 ' + (d.blockCode ? S.unitName(d) : d.buildingName + ', ' + t('cab.aptNo', { n: d.apartmentNumber }))), S.stageChip(d.stage)),
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
        h('div', { class: 'ptrack-head' }, h('div', { class: 'ptrack-brand' }, '💎 ' + S.brandName().toUpperCase()), pdf),
        h('div', { class: 'ptrack-name' }, ov.customerName || ''),
        h('div', { class: 'ptrack-apt' }, (deal.blockCode ? S.unitName(deal) : t('cab.aptNo', { n: deal.apartmentNumber })) + ' · ' + t('pcard.floor', { n: deal.floor })),
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

    /* ---- ⚙️ settings: devices (sign-ins of this account) ---- */
    async function settingsTab() {
      const parts = await Promise.all([notificationsCard(), devicesCard()]);
      return h('div', { class: 'stack cab-settings' }, parts);
    }

    // Notifications: Telegram (linked through the bot: a one-time link t.me/<bot>?start=…, 15 minutes) and e-mail — both by
    // default, what is connected; which events to send. Payment reminders always stay in the cabinet's bell.
    async function notificationsCard() {
      let st = await S.api.notificationSettings();
      const card = h('section', { class: 'card cab-notify' });
      const check = (name, label, on, off) => {
        const i = h('input', { type: 'checkbox', name, id: 'ntf-' + name });
        i.checked = !!on; i.disabled = !!off;
        return { i, el: h('label', { class: 'ntf-check', for: i.id }, i, h('span', null, label)) };
      };
      let link = null;
      const draw = () => {
        const tg = check('telegram', t('ntf.chTelegram'), st.telegram && st.telegramLinked, !st.telegramLinked);
        const em = check('email', t('ntf.chEmail'), st.email && st.emailAvailable, !st.emailAvailable);
        const events = ['payments', 'bookings', 'deal', 'documents', 'chat'].map((k) => [k, check('ev-' + k, t('ntf.ev.' + k), st.events && st.events[k])]);
        const save = async () => {
          try {
            st = await S.api.saveNotificationSettings({ telegram: tg.i.disabled ? st.telegram : tg.i.checked, email: em.i.disabled ? st.email : em.i.checked,
              events: Object.fromEntries(events.map(([k, c]) => [k, c.i.checked])) });
            S.toast(t('common.saved'), 'ok');
          } catch (er) { S.err(er); draw(); }
        };
        [tg, em].concat(events.map(([, c]) => c)).forEach((c) => c.i.addEventListener('change', save));

        let tgState;
        if (!st.telegramAvailable) tgState = h('p', { class: 'muted cab-small' }, t('ntf.tgOff'));
        else if (st.telegramLinked) {
          tgState = h('div', { class: 'tg-state', 'data-tg-linked': '' }, h('span', { class: 'dev-this' }, '✓ ' + t('ntf.tgLinked')),
            h('button', { class: 'btn ghost sm', type: 'button', onclick: (e) => S.busy(e.currentTarget, async () => {
              try { await S.api.telegramUnlink(); st.telegramLinked = false; S.toast(t('ntf.tgUnlinked'), 'ok'); draw(); } catch (er) { S.err(er); }
            }) }, t('ntf.tgUnlink')));
        } else if (link) {
          // The link is opened by the person's own click: a window opened after a request would be blocked as a pop-up.
          tgState = h('div', { class: 'stack' }, h('div', { class: 'tg-state' },
            h('a', { class: 'btn', href: link.url, target: '_blank', rel: 'noopener', 'data-tg-open': '' }, t('ntf.tgOpen')),
            h('button', { class: 'btn ghost sm', type: 'button', onclick: (e) => S.busy(e.currentTarget, async () => {
              st = await S.api.notificationSettings(); if (st.telegramLinked) { link = null; S.toast(t('ntf.tgDone'), 'ok'); } else S.toast(t('ntf.tgNotYet'), 'warn'); draw();
            }) }, t('ntf.tgCheck'))), h('p', { class: 'hint' }, t('ntf.tgSteps')));
        } else {
          tgState = h('div', { class: 'tg-state' }, h('button', { class: 'btn', type: 'button', 'data-tg-link': '', onclick: (e) => S.busy(e.currentTarget, async () => {
            try { link = await S.api.telegramLink(); draw(); } catch (er) { S.err(er.code === 'telegram_not_configured' ? Object.assign(er, { message: t('ntf.tgOff') }) : er); }
          }) }, '✈️ ' + t('ntf.tgGet')));
        }
        card.replaceChildren(h('h2', null, t('ntf.title')), h('p', { class: 'muted' }, t('ntf.sub')),
          h('h3', { class: 'cab-small' }, 'Telegram'), tgState,
          h('div', { class: 'ntf-group', role: 'group', 'aria-label': t('ntf.channels') }, tg.el, em.el,
            st.emailAvailable ? null : h('p', { class: 'hint' }, t('ntf.emailOff'))),
          h('div', { class: 'ntf-group', role: 'group', 'aria-label': t('ntf.events') }, h('b', null, t('ntf.events')), events.map(([, c]) => c.el)),
          h('p', { class: 'hint' }, t('ntf.note')));
      };
      draw();
      return card;
    }

    // Every device where the buyer is signed in: the browser, since when, the last visit; «это устройство» is marked.
    // A session lives Jwt:RefreshTokenDays after the last visit; «Выйти на всех устройствах» revokes them all.
    async function devicesCard() {
      const list = await S.api.sessions();
      const row = (s) => h('li', { class: 'dev-row' + (s.current ? ' current' : '') },
        h('span', { class: 'dev-ico', 'aria-hidden': 'true' }, /iPhone|Android|iPad/.test(s.device) ? '📱' : '💻'),
        h('div', { class: 'dev-info' },
          h('b', null, s.device || t('dev.unknown')), s.current ? h('span', { class: 'dev-this' }, t('dev.this')) : null,
          h('div', { class: 'muted cab-small' }, t('dev.since', { d: longDate(s.startedAt) }) + ' · ' + t('dev.last', { d: longDate(s.lastActiveAt) }))),
        s.current ? null : h('button', { class: 'btn ghost sm', type: 'button', onclick: (e) => S.busy(e.currentTarget, async () => {
          try { await S.api.revokeSession(s.id); S.toast(t('dev.closed'), 'ok'); show(); } catch (er) { S.err(er); }
        }) }, t('dev.close')));
      const all = h('button', { class: 'btn ghost', type: 'button', 'data-logout-all': '', onclick: async () => {
        if (!(await S.confirm(t('dev.allConfirm'), t('dev.allOut'), true))) return;
        await S.busy(all, async () => { try { await S.api.revokeAllSessions(); } catch (er) { /* signed out anyway */ } location.href = '/login'; });
      } }, t('dev.allOut'));
      return h('section', { class: 'card cab-devices' }, h('h2', null, t('dev.title')), h('p', { class: 'muted' }, t('dev.sub')),
        h('ul', { class: 'dev-list' }, list.map(row)), all);
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
    function aptTitle(d) { return (d.blockCode ? S.unitName(d) : d.buildingName + ' — ' + t('cab.aptNo', { n: d.apartmentNumber })) + ', ' + S.fmt.rooms(d.rooms); }
    // In the CRM the name is often written surname first, so the whole name is safer than a guessed "first name".
    function firstName(n) { return n || (S.api.user && S.api.user.name) || ''; }
    return () => { stopped = true; fab.remove(); };
  };
})();
