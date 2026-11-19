/* AI assistant: a gold bubble in the corner opens a chat panel (on the right; a bottom sheet on phones).
   Two modes:
   - signed in (cabinet, CRM, platform): /ai/chat, the server picks the prompt by the user's role;
   - public (the pages of the complex's site, for guests and signed-in buyers alike): /public/{ORG_SLUG}/ai/chat —
     only questions about buying an apartment here; the bubble shows only when the server says the assistant is on.
   The history lives only in this tab. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  const PUBLIC_PAGES = ['home', 'apartments', 'about', 'gallery', 'contact'];
  const isPublic = PUBLIC_PAGES.includes(document.body.dataset.page);
  // The public chat takes questions up to 500 characters and the last 10 turns (the server cuts the same way).
  const MAX_HISTORY = isPublic ? 10 : 20;
  const MAX_LENGTH = isPublic ? 500 : 2000;
  let history = [];
  let panel = null, list = null, input = null, sendBtn = null, busy = false;
  let publicEnabled = false;

  const bubble = h('button', { class: 'ai-fab', type: 'button', title: t('ai.open'), 'aria-label': t('ai.open'), 'aria-haspopup': 'dialog', onclick: () => toggle(true) }, '✦');

  // Paths of this site in a reply ("/apartments", "/contact#map") become links; nothing else is turned into a link.
  const SITE_LINK = /((?:^|[\s(«"'])\/(?:apartments|contact|about|gallery|privacy|cabinet|login|register)(?:#[\w-]+)?)(?=$|[\s.,;:!?)»"'])/;
  function links(text) {
    return text.split(SITE_LINK).filter(Boolean).map((p) => {
      const m = p.match(/^([\s(«"']?)(\/\S+)$/);
      return m ? [m[1], h('a', { href: m[2], class: 'ai-link' }, m[2])] : p;
    }).flat();
  }
  // Replies come as light Markdown: paragraphs, "1." / "-" lists, headings, **bold** and `code`. Built as DOM nodes, never as HTML.
  function inline(text) {
    return text.split(/(\*\*[^*]+\*\*|`[^`]+`)/).filter(Boolean).map((p) =>
      p.length > 4 && p.startsWith('**') && p.endsWith('**') ? h('b', null, p.slice(2, -2))
        : p.length > 2 && p.startsWith('`') && p.endsWith('`') ? h('code', null, p.slice(1, -1)) : links(p)).flat();
  }
  function rich(text) {
    const out = []; let listEl = null;
    for (const raw of String(text || '').split('\n')) {
      const line = raw.trim();
      if (!line) continue; // a blank line between list items keeps the list going
      const item = line.match(/^(\d+)[.)]\s+(.*)$/) || line.match(/^()[-*•]\s+(.*)$/);
      if (item) {
        const tag = item[1] ? 'ol' : 'ul';
        if (!listEl || listEl.tagName.toLowerCase() !== tag) { listEl = h(tag, item[1] && item[1] !== '1' ? { start: item[1] } : null); out.push(listEl); }
        listEl.appendChild(h('li', null, inline(item[2])));
        continue;
      }
      listEl = null;
      const head = line.match(/^#{1,4}\s+(.*)$/);
      out.push(h('p', null, head ? h('b', null, head[1].replace(/\*\*/g, '')) : inline(line)));
    }
    return out;
  }

  function addMessage(role, text) {
    const bot = role !== 'user' && role !== 'error';
    const el = h('div', { class: 'ai-msg ' + (role === 'user' ? 'me' : role === 'error' ? 'err' : 'bot') + (bot ? ' rich' : '') }, bot ? rich(text) : text);
    list.appendChild(el);
    list.scrollTop = list.scrollHeight;
    return el;
  }

  // "Busy" (429): the manager's contacts — from the server's answer, else the site's own contact settings.
  function contactsBlock(fromServer) {
    const cfg = (window.SITORA_CONFIG && window.SITORA_CONFIG.CONTACT) || {};
    const c = fromServer && Object.keys(fromServer).length ? fromServer : { phone: cfg.phone, telegram: cfg.telegram };
    const tg = c.telegram ? String(c.telegram).replace(/^@/, '').replace(/^https?:\/\/t\.me\//, '') : '';
    const items = [
      c.phone && h('a', { class: 'ai-contact', href: 'tel:' + String(c.phone).replace(/[^\d+]/g, '') }, S.icon('phone'), t('ai.call')),
      tg && h('a', { class: 'ai-contact', href: 'https://t.me/' + encodeURIComponent(tg), target: '_blank', rel: 'noopener' }, S.icon('send'), 'Telegram'),
      c.email && h('a', { class: 'ai-contact', href: 'mailto:' + c.email }, t('ai.email')),
      h('a', { class: 'ai-contact', href: '/contact' }, t('ai.contactPage')),
    ].filter(Boolean);
    return h('div', { class: 'ai-contacts', role: 'group', 'aria-label': t('ai.busyContacts') }, h('span', null, t('ai.busyContacts')), ...items);
  }

  // The greeting and quick questions follow the mode and the role: guest, buyer, platform owner (SuperAdmin) or staff.
  function getGreeting() {
    if (isPublic) return t('ai.helloGuest');
    if (S.api.isCustomer()) return t('ai.helloBuyer');
    if (S.api.isPlatform()) return t('ai.helloPlatform');
    return t('ai.helloStaff');
  }
  function getChips() {
    if (isPublic) return ['ai.chip.free', 'ai.chip.installment', 'ai.chip.book', 'ai.chip.price2'].map((k) => t(k));
    if (S.api.isCustomer()) return ['ai.chip.nextPay', 'ai.chip.layout', 'ai.chip.contacts'].map((k) => t(k));
    if (S.api.isPlatform()) return ['ai.chip.platformStats', 'ai.chip.tariffs', 'ai.chip.users'].map((k) => t(k));
    return ['ai.chip.newDeal', 'ai.chip.addPay', 'ai.chip.dashboard'].map((k) => t(k));
  }
  // A chip sends its text without the leading emoji.
  function renderChips() {
    return h('div', { class: 'ai-chips', role: 'group', 'aria-label': t('ai.chips') },
      ...getChips().map((c) => h('button', { class: 'ai-chip', type: 'button', onclick: () => { if (busy) return; input.value = c.replace(/^\S+\s/, ''); send(); } }, c)));
  }
  function intro() { return [h('div', { class: 'ai-msg bot' }, getGreeting()), renderChips()]; }

  // A new conversation: the history is forgotten and a reply still on its way is ignored.
  let generation = 0;
  function clearChat() {
    generation++;
    history = [];
    busy = false; sendBtn.disabled = false;
    list.replaceChildren(...intro());
    input.focus();
  }

  function note() {
    return h('p', { class: 'ai-note' }, t('ai.publicNote'), ' ', h('a', { href: '/privacy' }, t('ai.privacyLink')));
  }

  function build() {
    list = h('div', { class: 'ai-list', 'aria-live': 'polite' }, ...intro());
    input = h('textarea', { class: 'inp ai-input', rows: 2, maxlength: MAX_LENGTH, placeholder: t('ai.ph'), 'aria-label': t('ai.ph') });
    sendBtn = h('button', { class: 'btn', type: 'submit', 'aria-label': t('ai.send') }, S.icon('send'));
    const form = h('form', { class: 'ai-form' }, input, sendBtn);
    form.addEventListener('submit', (e) => { e.preventDefault(); send(); });
    // Enter sends, Shift+Enter makes a new line.
    input.addEventListener('keydown', (e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); send(); } });
    panel = h('aside', { class: 'ai-panel' + (isPublic ? ' ai-public' : ''), role: 'dialog', 'aria-label': t('ai.title') },
      h('div', { class: 'ai-head' }, h('b', null, '✦ ', t('ai.title')),
        h('div', { class: 'ai-head-btns' },
          h('button', { class: 'icon-btn', type: 'button', title: t('ai.clearChat'), 'aria-label': t('ai.clearChat'), onclick: clearChat }, '🗑️'),
          h('button', { class: 'icon-btn', type: 'button', title: t('common.close'), 'aria-label': t('common.close'), onclick: () => toggle(false) }, S.icon('x')))),
      list, form, isPublic ? note() : null);
    // Esc closes the panel and gives the focus back to the bubble.
    panel.addEventListener('keydown', (e) => { if (e.key === 'Escape') { e.stopPropagation(); toggle(false); } });
    document.body.appendChild(panel);
  }

  async function send() {
    const text = input.value.trim().slice(0, MAX_LENGTH);
    if (!text || busy) return;
    busy = true; sendBtn.disabled = true;
    input.value = '';
    addMessage('user', text);
    // Three gold dots while the answer is on its way; screen readers hear "typing…".
    const typing = h('div', { class: 'ai-msg bot typing-dots', role: 'status', 'aria-label': t('ai.typing') }, h('span'), h('span'), h('span'));
    list.appendChild(typing);
    list.scrollTop = list.scrollHeight;
    const gen = generation;
    try {
      const r = isPublic ? await S.api.aiPublicChat(text, history) : await S.api.aiChat(text, history);
      if (gen !== generation) return;
      typing.remove();
      addMessage('bot', r.reply);
      history.push({ role: 'user', content: text }, { role: 'assistant', content: r.reply });
      if (history.length > MAX_HISTORY) history = history.slice(-MAX_HISTORY);
    } catch (e) {
      if (gen !== generation) return;
      typing.remove();
      addMessage('error', e.message || t('err.unknown'));
      if (isPublic && e.status === 429) { list.appendChild(contactsBlock(e.data && e.data.contacts)); list.scrollTop = list.scrollHeight; }
    } finally {
      if (gen === generation) { busy = false; sendBtn.disabled = false; input.focus(); }
    }
  }

  function toggle(open) {
    if (open && !panel) build();
    if (!panel) return;
    // On the first open the panel needs one frame in the closed position, or it appears without sliding in.
    requestAnimationFrame(() => panel.classList.toggle('open', open));
    bubble.classList.toggle('hidden', open);
    bubble.setAttribute('aria-expanded', open ? 'true' : 'false');
    if (open) setTimeout(() => input.focus(), 320);
    else bubble.focus();
  }

  // Signed-in mode: the assistant is for signed-in people only; signing out forgets the conversation.
  // Public mode: shown for everyone once the server says the assistant is configured; signing in or out changes nothing.
  function sync() {
    if (!document.body.contains(bubble)) document.body.appendChild(bubble);
    if (isPublic) { bubble.style.display = publicEnabled ? '' : 'none'; return; }
    bubble.style.display = S.api.isAuthed ? '' : 'none';
    if (!S.api.isAuthed && panel) { panel.remove(); panel = null; history = []; bubble.classList.remove('hidden'); }
  }

  async function checkPublic() {
    sync();
    try { publicEnabled = !!(await S.api.aiPublicStatus()).enabled; } catch (e) { publicEnabled = false; }
    sync();
  }

  if (!isPublic) S.bus.on('auth', sync);
  S.bus.on('lang', () => {
    bubble.title = t('ai.open'); bubble.setAttribute('aria-label', t('ai.open'));
    // A conversation not yet started greets again in the new language.
    if (panel && !history.length && !busy) list.replaceChildren(...intro());
    if (panel && isPublic) { const n = panel.querySelector('.ai-note'); if (n) n.replaceWith(note()); }
  });
  const start = isPublic ? checkPublic : sync;
  if (document.readyState === 'loading') window.addEventListener('DOMContentLoaded', start); else start();
})();
