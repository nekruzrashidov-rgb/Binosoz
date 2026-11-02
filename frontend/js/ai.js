/* AI assistant: a bubble above the settings button opens a chat panel on the right.
   The question goes to the API (/ai/chat), which picks the prompt by the user's role; the history lives only in this tab. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  const MAX_HISTORY = 20;
  let history = [];
  let panel = null, list = null, input = null, sendBtn = null, busy = false;

  const bubble = h('button', { class: 'ai-fab', type: 'button', title: t('ai.open'), 'aria-label': t('ai.open'), onclick: () => toggle(true) }, '✦');

  // Replies come as light Markdown: paragraphs, "1." / "-" lists, headings, **bold** and `code`. Built as DOM nodes, never as HTML.
  function inline(text) {
    return text.split(/(\*\*[^*]+\*\*|`[^`]+`)/).filter(Boolean).map((p) =>
      p.length > 4 && p.startsWith('**') && p.endsWith('**') ? h('b', null, p.slice(2, -2))
        : p.length > 2 && p.startsWith('`') && p.endsWith('`') ? h('code', null, p.slice(1, -1)) : p);
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

  function build() {
    list = h('div', { class: 'ai-list', 'aria-live': 'polite' }, h('div', { class: 'ai-msg bot' }, S.api.isCustomer() ? t('ai.helloBuyer') : t('ai.helloStaff')));
    input = h('textarea', { class: 'inp ai-input', rows: 2, maxlength: 2000, placeholder: t('ai.ph'), 'aria-label': t('ai.ph') });
    sendBtn = h('button', { class: 'btn', type: 'submit', 'aria-label': t('ai.send') }, S.icon('send'));
    const form = h('form', { class: 'ai-form' }, input, sendBtn);
    form.addEventListener('submit', (e) => { e.preventDefault(); send(); });
    // Enter sends, Shift+Enter makes a new line.
    input.addEventListener('keydown', (e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); send(); } });
    panel = h('aside', { class: 'ai-panel', role: 'dialog', 'aria-label': t('ai.title') },
      h('div', { class: 'ai-head' }, h('b', null, '✦ ', t('ai.title')), h('button', { class: 'icon-btn', type: 'button', title: t('common.close'), 'aria-label': t('common.close'), onclick: () => toggle(false) }, S.icon('x'))),
      list, form);
    document.body.appendChild(panel);
  }

  async function send() {
    const text = input.value.trim();
    if (!text || busy) return;
    busy = true; sendBtn.disabled = true;
    input.value = '';
    addMessage('user', text);
    const typing = addMessage('bot', t('ai.typing'));
    typing.classList.add('typing');
    try {
      const r = await S.api.aiChat(text, history);
      typing.remove();
      addMessage('bot', r.reply);
      history.push({ role: 'user', content: text }, { role: 'assistant', content: r.reply });
      if (history.length > MAX_HISTORY) history = history.slice(-MAX_HISTORY);
    } catch (e) {
      typing.remove();
      addMessage('error', e.message || t('err.unknown'));
    } finally {
      busy = false; sendBtn.disabled = false; input.focus();
    }
  }

  function toggle(open) {
    if (open && !panel) build();
    if (!panel) return;
    panel.classList.toggle('open', open);
    bubble.classList.toggle('hidden', open);
    if (open) setTimeout(() => input.focus(), 50);
  }

  // The assistant is for signed-in people only; signing out forgets the conversation.
  function sync() {
    if (!document.body.contains(bubble)) document.body.appendChild(bubble);
    bubble.style.display = S.api.isAuthed ? '' : 'none';
    if (!S.api.isAuthed && panel) { panel.remove(); panel = null; history = []; bubble.classList.remove('hidden'); }
  }

  S.bus.on('auth', sync);
  S.bus.on('lang', () => { bubble.title = t('ai.open'); bubble.setAttribute('aria-label', t('ai.open')); });
  if (document.readyState === 'loading') window.addEventListener('DOMContentLoaded', sync); else sync();
})();
