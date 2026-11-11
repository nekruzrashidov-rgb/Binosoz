/* Interface languages: Russian, Tajik, English. The texts themselves are in js/i18n/ru.js, tg.js and en.js.
   Russian is on every page (loaded by the page, before this file): it is the default and the fallback for a missing key.
   Tajik or English is loaded only when it is chosen — right away for the language saved in the browser, so it is usually
   ready by the time the page draws. S.initLang() and S.setLang() return a promise that resolves when the texts are there. */
(function () {
  const S = (window.S = window.S || {});
  S.I18N = S.I18N || {};
  // Errors go to the console only on a developer's machine or with ?debug in the address; visitors see the page's own messages.
  S.DEBUG = /^(localhost|127\.0\.0\.1|\[::1\])$|\.localhost$/.test(location.hostname) || /[?&]debug\b/.test(location.search);
  S.logError = (...a) => { if (S.DEBUG) console.error(...a); };
  S.bus = (function () { const h = {}; return { on(n, f) { (h[n] = h[n] || []).push(f); return () => { h[n] = (h[n] || []).filter((x) => x !== f); }; }, emit(n, d) { (h[n] || []).slice().forEach((f) => { try { f(d); } catch (e) { S.logError(e); } }); } }; })();
  const dict = S.I18N;
  S.lang = 'ru';
  S.t = function (key, params, fallback) {
    let s = (dict[S.lang] && dict[S.lang][key]);
    if (s === undefined && dict.ru) s = dict.ru[key];
    if (s === undefined) s = fallback !== undefined && fallback !== null ? fallback : key;
    if (params) s = String(s).replace(/\{(\w+)\}/g, (m, k) => (params[k] !== undefined ? params[k] : m));
    return s;
  };
  // The switcher's languages, in order: code and button label.
  S.LANGS = [['ru', 'RU'], ['tg', 'TJ'], ['en', 'EN']];
  const known = (l) => (S.LANGS.some(([k]) => k === l) ? l : 'ru');

  // The dictionary script of a language, once; the same address the page would use (works on Vercel and behind the API).
  const loading = {};
  function loadLang(l) {
    if (dict[l]) return Promise.resolve();
    if (!loading[l]) {
      loading[l] = new Promise((resolve, reject) => {
        const el = document.createElement('script');
        el.src = '/js/i18n/' + l + '.js';
        el.onload = () => (dict[l] ? resolve() : reject(new Error('no texts for ' + l)));
        el.onerror = () => { delete loading[l]; reject(new Error('could not load ' + l)); };
        document.head.appendChild(el);
      });
    }
    return loading[l];
  }
  function saved() {
    // ?lang=tg in the address (links from the sitemap, shared links) wins and is remembered.
    let q = null; try { q = new URLSearchParams(location.search).get('lang'); } catch (e) { q = null; }
    if (q && S.LANGS.some(([k]) => k === q)) { try { window.localStorage.setItem('sitora.lang', q); } catch (e) { /* storage may be blocked */ } return q; }
    let l = null; try { l = window.localStorage.getItem('sitora.lang'); } catch (e) { l = null; }
    if (!l) { const nav = (navigator.language || 'ru').toLowerCase(); l = nav.startsWith('tg') ? 'tg' : nav.startsWith('en') ? 'en' : 'ru'; }
    return known(l);
  }
  // Start loading the saved language now, while the other scripts of the page run.
  const first = saved();
  if (first !== 'ru') loadLang(first).catch(() => { /* Russian stays */ });

  S.initLang = function () {
    const l = saved();
    const apply = () => { S.lang = dict[l] ? l : 'ru'; document.documentElement.lang = S.lang; };
    apply();
    return loadLang(l).then(apply, () => { S.lang = 'ru'; document.documentElement.lang = 'ru'; });
  };
  S.setLang = function (l) {
    l = known(l);
    return loadLang(l).then(() => {
      S.lang = l; try { window.localStorage.setItem('sitora.lang', S.lang); } catch (e) { /* storage may be blocked */ }
      document.documentElement.lang = S.lang;
      if (S.bus) S.bus.emit('lang', S.lang);
    }, () => { if (S.toast) S.toast(S.t('err.network', null, 'Нет соединения'), 'bad'); });
  };
  S.dict = dict;
})();
