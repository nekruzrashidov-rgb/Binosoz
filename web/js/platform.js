/* Platform owner's section: developers (organizations), working "as owner" of one, authenticator app. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  S.views = S.views || {};

  S.views.platform = function (root) {
    if (!S.api.isAuthed) return S.views.login(root);
    if (!S.api.isPlatform()) { root.appendChild(h('div', { class: 'wrap page-in' }, h('div', { class: 'card login-card login' }, h('h2', null, t('plat.only')), h('div', { class: 'row', style: { marginTop: '14px' } }, h('button', { class: 'btn', onclick: () => S.go(S.homeFor(S.api.user)) }, t('cab.toCrm')))))); return () => {}; }
    const page = h('div', { class: 'page-in' }); root.appendChild(page);
    const items = [{ key: 'orgs', label: t('plat.orgs'), icon: 'building' }, { key: 'security', label: t('plat.security'), icon: 'shield' }];
    const ws = S.workspace(items, 'orgs', (k, main) => { S.clear(main); main.append(h('h1', null, items.find((i) => i.key === k).label), h('p', { class: 'sub' }, t('plat.sub.' + k))); const body = h('div', { class: 'stack' }); main.appendChild(body); views[k](body); });
    page.appendChild(ws.el);
    queueMicrotask(() => ws.go('orgs'));

    const views = {
      orgs(body) {
        const holder = h('div');
        const load = async () => {
          holder.replaceChildren(S.skeleton(4, 38));
          try {
            const list = await S.api.organizations();
            holder.replaceChildren(S.tbl([
              { label: t('plat.name'), render: (o) => h('b', null, o.name) },
              { label: t('plat.slug'), render: (o) => h('code', null, o.slug) },
              { label: t('plat.buildings'), r: true, num: true, render: (o) => o.buildings },
              { label: t('plat.users'), r: true, num: true, render: (o) => o.users },
              { label: t('deal.stage'), render: (o) => S.chip(o.active ? 'Active ok' : 'Blocked', t(o.active ? 'plat.active' : 'plat.off')) },
              { label: '', r: true, render: (o) => (o.active ? h('button', { class: 'btn soft sm', onclick: (e) => S.busy(e.currentTarget, async () => { try { await S.api.impersonate(o.id); S.toast(t('imp.started', { o: o.name }), 'ok'); S.go('crm'); } catch (er) { S.err(er); } }) }, t('plat.enter')) : '') },
            ], list, { empty: t('plat.none') }));
          } catch (e) { holder.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
        };
        body.replaceChildren(h('div', { class: 'toolbar' }, h('p', { class: 'hint' }, t('plat.enterHint')), h('span', { class: 'spacer' }), h('button', { class: 'btn', onclick: () => orgForm(load) }, t('plat.new'))), holder);
        load();
      },

      security(body) {
        const holder = h('div', { class: 'card stack', style: { maxWidth: '520px' } }, S.skeleton(3, 30));
        body.replaceChildren(holder);
        (async () => {
          try {
            const k = await S.api.totpSetup();
            const code = S.input('code', { inputmode: 'numeric', maxlength: 6, autocomplete: 'one-time-code' });
            const btn = h('button', { class: 'btn', type: 'submit' }, t('plat.totpOn'));
            const form = h('form', { class: 'stack', novalidate: true }, S.field(t('plat.totpCode'), code), btn);
            form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await S.api.totpEnable(code.value.trim()); S.toast(t('plat.totpDone'), 'ok'); code.value = ''; } catch (er) { S.showErrors(form, er); } }); });
            holder.replaceChildren(h('h3', null, t('plat.totp')), h('p', { class: 'muted' }, t('plat.totpText')),
              S.field(t('plat.totpKey'), h('code', { class: 'paper', style: { display: 'block', wordBreak: 'break-all', userSelect: 'all' } }, k.sharedKey)),
              h('a', { href: k.authenticatorUri, class: 'hint' }, t('plat.totpLink')), form);
          } catch (e) { holder.replaceChildren(h('div', { class: 'form-err' }, e.message)); }
        })();
      },
    };

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
